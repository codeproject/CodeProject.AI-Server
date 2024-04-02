# import libraries we've installed in the venv
import gc
import torch

pipeline        = None
old_device_name = None
old_models_dir  = None
old_models_id   = None
old_width       = None
old_height      = None
old_image_count = None
old_batch_size  = None
old_halfsize    = None


def cleanup():
    global pipeline
    if pipeline:
        del pipeline
    pipeline = None
    gc.collect()
    

def create_pipeline(models_dir, model_id, device_name:str, 
                    num_images_per_prompt = 1, width = 1024, height = 768,
                    half_precision: bool=True):
    
    global pipeline
    global old_device_name
    global old_models_dir
    global old_models_id
    global old_width
    global old_height
    global old_image_count
    global old_halfsize

    # Check if (in general) we need to recreate the pipeline
    recreate_pipeline = pipeline is None or old_device_name != device_name or \
                        models_dir != old_models_dir or model_id != old_models_id

    # Check if (for specific device type) we need to recreate the pipeline
    if not recreate_pipeline:
        if device_name == 'cpu':
            recreate_pipeline = old_width       != width      or \
                                old_height      != height     or \
                                old_image_count != num_images_per_prompt
        else:
            recreate_pipeline = old_halfsize != half_precision

    # Exit early if we don't need to recreate
    if not recreate_pipeline:
        return pipeline
    
    print(f"Creating pipeline for device {device_name}")
    
    old_device_name = device_name
    old_models_dir  = models_dir
    old_models_id   = model_id
    old_width       = width
    old_height      = height
    old_image_count = num_images_per_prompt
    old_halfsize    = half_precision
    
    # Clean up old if we already had one
    cleanup()

    if device_name == 'cpu':
        
        from optimum.intel.openvino.modeling_diffusion import OVStableDiffusionPipeline
        pipeline = OVStableDiffusionPipeline.from_pretrained(model_id, 
                                                             compile=False,
                                                             cache_dir = models_dir)
        
        if pipeline and device_name == 'cpu':
            pipeline.reshape(batch_size=1, height=height, width=width,
                             num_images_per_prompt=num_images_per_prompt)
            pipeline.compile()

    else:   # CUDA GPU
        
        if half_precision:
            dtype = torch.float16
            variant = "fp16"
        else:
            dtype = torch.float32
            variant = "fp32"

        if model_id.endswith(".safetensors"):
            from diffusers import StableDiffusionXLPipeline
            pipeline = StableDiffusionXLPipeline.from_single_file(model_id,
                                                                  torch_dtype=dtype, 
                                                                  variant=variant,
                                                                  use_safetensors=True,
                                                                  cache_dir = models_dir)
        else:
            from diffusers import AutoPipelineForText2Image
            pipeline = AutoPipelineForText2Image.from_pretrained(model_id, 
                                                                 torch_dtype=dtype, 
                                                                 variant=variant,
                                                                 use_safetensors=True,
                                                                 cache_dir = models_dir)

        # See https://huggingface.co/stabilityai/stable-diffusion-xl-base-0.9
        # Speedup of 20-30%. Only works for torch >= 2.0, so need to check
        # pipeline.unet = torch.compile(pipeline.unet, mode="reduce-overhead", fullgraph=True)
        pipeline = pipeline.to(device_name)

    return pipeline


def create_image(pipeline, prompt:str, negative_prompt:str = None, seed = None,
                 num_inference_steps = 40, num_images_per_prompt = 1,
                 width = 1024, height = 768, guidance_scale = 7.0,
                 device_name:str = "cpu", callback = None):
    
    if width % 8 != 0 or height % 8 != 0:
        print("width and height must be multiples of 8. Adjusting")
    
    width  = width  // 8 * 8
    height = height // 8 * 8
        
    if negative_prompt is None:
        negative_prompt = "extra arms, extra legs, extra hands, extra fingers," \
                        + "extra eyes, extra faces, extra ears, extra noses, "  \
                        + "extra heads, extra body parts, deformed, bad anatomy"

    if device_name == 'cpu':
        result = pipeline(prompt, negative_prompt=negative_prompt,
                          num_inference_steps=num_inference_steps,
                          # output_type="pil",
                          num_images_per_prompt=num_images_per_prompt,
                          guidance_scale = guidance_scale,
                          width=width, height=height,
                          callback=callback)
    else:
        generator = torch.Generator(device_name)
        if seed is not None:
            generator.manual_seed(seed)
        else:
            generator.seed()
        
        seed = generator.initial_seed()
        result = pipeline(prompt, negative_prompt=negative_prompt,
                          num_inference_steps=num_inference_steps,
                          generator=generator,
                          num_images_per_prompt=num_images_per_prompt,
                          guidance_scale = guidance_scale,
                          width=width, height=height,
                          callback_on_step_end=callback)
   
    return {
        "success": True,
        "seed": seed,
        "images": result.images
    }
