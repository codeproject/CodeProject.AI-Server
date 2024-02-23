# [Matthew] RENAME THIS FILE to Text2Image_adapter.py

# Import our general libraries
import sys
import time
import gc

# Import the CodeProject.AI SDK. This will add to the PATH var for future imports
sys.path.append("../../SDK/Python")
from common import JSON
from request_data import RequestData      # RequestData is passed to 'process'
from module_options import ModuleOptions  # Handle options passed to the module
from module_runner import ModuleRunner    # The ModuleRunner core
from module_logging import LogMethod      # For logging

# import libraries we've installed in the venv
from diffusers import AutoPipelineForText2Image, DEISMultistepScheduler, StableDiffusionXLPipeline
import torch

# Import the method of the module we're wrapping
# from Text2Image import create_pipeline, create_image


# Our adapter
class Text2Image_adapter(ModuleRunner):

    def initialise(self):
        # Can we use the GPU (via PyTorch / CUDA)?
        if self.system_info.hasTorchCuda:
            self.can_use_GPU       = True
            self.inference_device  = "GPU"
            self.inference_library = "CUDA"

        # TODO: get the other parameters from the request
        
        start_inference_time = time.perf_counter()
        
        self.models_dir  = ModuleOptions.getEnvVariable("CPAI_MODULE_TEXT2IMAGE_MODEL_DIR", "assets")
        self.model_name  = ModuleOptions.getEnvVariable("CPAI_MODULE_TEXT2IMAGE_MODEL_NAME", "runwayml/stable-diffusion-v1-5")
        self.device_name = "cuda" if self.can_use_GPU else "cpu"
      
        # Let's store some stats
        self._num_items_found = 0
        self._histogram       = {}
        self.pipeline         = None
        
    def process(self, data: RequestData) -> JSON:      
        # This is a long running operation so we return a reference to the method
        # that will actually do the work. This will result in the SDK spinning
        # this method up as a separate task and monitoring it until it's complete.
        
        # NOTE: we can't pass parameters to this method, so we add params to the
        # RequestData object as a means to pass params.
        # However, the method we're returning is actually a class method so has
        # the 'self' object we can query directly
        # data.add_value("models_dir",     "assets")
        # data.add_value("model_name",     "stable-diffusion-v1-5")
        # data.add_value("half_precision", self.half_precision)
        # data.add_value("device_name",    self.accel_device_name)

        return self.long_process
    
    def command_status(self):
    #    [Matthew] Is there some intermediate form of the image during creation?
    #    Midjourney shows you a blurry image that, every few seconds, becomes
    #    less blurry. If this module did that it would be an excellent demo of
    #    a long process status update
         return {}
    
    # def selftest(self) -> JSON:
    #     # (optional but encouraged) Perform a self-test
    

    def long_process(self, data:RequestData):
        """
        This method performs the long running process, which in our case is image
        generation.
        REVIEW:[Matthew] I would prefer we stick to the convention of using this
               name for the long process. It makes it easier to learn and maintain
               and document.
        TODO: Store intermediate results, if possible, and return those results
              in the get_command_status method so a client can poll the server
              for regular updates
        """
        
        prompt                = data.get_value("prompt", "A painting of a cat")
        negative_prompt       = data.get_value("negative_prompt", None)
        seed                  = data.get_int("seed", None)
        num_inference_steps   = data.get_int("num_inference_steps", 40)
        num_images_per_prompt = data.get_int("num_images_per_prompt", 1)
        width                 = data.get_int("width", 1024)
        height                = data.get_int("height", 768)
        guidance_scale        = data.get_float("guidance_scale", 7.0)

        # No need to get these from RequestData since this method is part of the
        # adapter and we can query these values directly
        # models_dir          = data.get_value("models_dir",   "assets")
        # model_name          = data.get_value("model_name",   "stable-diffusion-v1-5")
        # half_precision      = data.get_bool("half_precision", True)
        # device_name         = data.get_value("device_name",   "cpu")

        if seed == 'null':
            seed = None
       
        # [Matthew] CreatePipeline should be imported from the Text2Image.py file
        self.pipeline = self.CreatePipeline(self.model_name, self.device_name,
                                            self.half_precision)
        if not self.pipeline:
            return {
                "success": False,
                "error": "Unable to create pipeline"
            }
        
        # Run the image generation
        start_time  = time.perf_counter()
        # [Matthew] CreateImage should be imported from the Text2Image.py file
        images      = self.CreateImage(prompt, negative_prompt, seed,
                                       num_inference_steps, num_images_per_prompt,
                                       width, height, guidance_scale,
                                       self.device_name)
        inferenceMs = int((time.perf_counter() - start_time) * 1000)
        
        # This is a good place to clean up the pipeline
        del self.pipeline
        self.pipeline = None
        gc.collect()
        if self.can_use_GPU and self.system_info.hasTorchCuda:
            torch.cuda.empty_cache()

        output = {
            "success":               True,
            "inferenceMs":           inferenceMs,
            "prompt":                prompt,
            "negative_prompt":       negative_prompt,
            "seed":                  seed,
            "num_inference_steps":   num_inference_steps,
            "num_images_per_prompt": num_images_per_prompt,
            "width":                 width,
            "height":                height,
            "guidance_scale":        guidance_scale,
            "images": [ RequestData.encode_image(image) for image in images ]
        }
        return output
        

    
    # ==========================================================================    
    # [Matthew] Move these into a separate file so there's separation between 
    # adapter and the code being wrapped


    # Rename to create_pipeline?
    def CreatePipeline(self, model_path, device_name:str, half_precision: bool=True):

        if half_precision:
            dtype = torch.float16
            variant = "fp16"
        else:
            dtype = torch.float32
            variant = "fp32"

        try:
            if model_path.endswith(".safetensors"):
                pipeline = StableDiffusionXLPipeline.from_single_file(model_path,
                                                                    torch_dtype=dtype, 
                                                                    variant=variant,
                                                                    use_safetensors=True,
                                                                    #safety_checker=None,
                                                                    cache_dir = self.models_dir)
            else:
                pipeline = AutoPipelineForText2Image.from_pretrained(model_path, 
                                                                    torch_dtype=dtype, 
                                                                    variant=variant,
                                                                    use_safetensors=True,
                                                                    #safety_checker=None,
                                                                    cache_dir = self.models_dir)

            #pipeline.scheduler = DEISMultistepScheduler.from_config(pipeline.scheduler.config)
            pipeline = pipeline.to(device_name)
            return pipeline
        
        except Exception as ex:
            return None

    # Rename to create_image?
    def CreateImage(self, prompt:str, negative_prompt:str = None, seed = None,
                    num_inference_steps = 40, num_images_per_prompt = 1,
                    width = 1024, height = 768, guidance_scale = 7.0,
                    device_name:str = "cpu"):
        
        if negative_prompt is None:
            negative_prompt = "extra arms, extra legs, extra hands, extra fingers," \
                            + "extra eyes, extra faces, extra ears, extra noses, "  \
                            + "extra heads, extra body parts, deformed, bad anatomy"

        generator = torch.Generator(device_name)

        if seed is not None:
            generator.manual_seed(seed)
        else:
            generator.seed()

        result = self.pipeline(prompt, negative_prompt=negative_prompt,
                               num_inference_steps=num_inference_steps,
                               generator=generator,
                               num_images_per_prompt=num_images_per_prompt,
                               guidance_scale = guidance_scale,
                               width=width, height=height)
   
        return result.images

    # ==========================================================================    

if __name__ == "__main__":
    Text2Image_adapter().start_loop()
