# Import our general libraries
import sys
import time
from typing import Dict

# Import the CodeProject.AI SDK. This will add to the PATH var for future imports
sys.path.append("../../SDK/Python")
from common import JSON
from request_data import RequestData      # RequestData is passed to 'process'
from module_options import ModuleOptions  # Handle options passed to the module
from module_runner import ModuleRunner    # The ModuleRunner core

# import libraries we've installed in the venv
import torch

# Import the method of the module we're wrapping
import stable_diffusion


# Our adapter
class Text2Image_adapter(ModuleRunner):

    def initialise(self):
        # Can we use the GPU (via PyTorch / CUDA)?
        if self.system_info.hasTorchCuda:
            self.can_use_GPU       = True
            self.inference_device  = "GPU"
            self.inference_library = "CUDA"
       
        self.models_dir  = ModuleOptions.getEnvVariable("CPAI_MODULE_TEXT2IMAGE_MODELS_DIR", "./assets")
        if self.can_use_GPU:
            self.device_name = "cuda" 
            self.model_name  = ModuleOptions.getEnvVariable("CPAI_MODULE_TEXT2IMAGE_MODEL_NAME", "runwayml/stable-diffusion-v1-5")
        else:
             # See https://github.com/huggingface/blog/blob/main/stable-diffusion-inference-intel.md
             # for info on massive improvements on Intel hardware
             self.model_name  = ModuleOptions.getEnvVariable("CPAI_MODULE_TEXT2IMAGE_MODEL_NAME", "helenai/stabilityai-stable-diffusion-2-1-ov")
             self.device_name = "cpu"
      
        self.pipeline            = None
        self.skip_image_updates  = 5  # Display intermediate images each 5 steps

        # Let's store some stats
        self.steps               = 0
        self.status              = "Idle"
        self.status_msg          = ""
        self.error_msg           = ""
        self.intermediate_images = None
        self.width               = None
        self.height              = None


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

        try:
            message = self.status_msg
            if self.status == "Started":
                message += f" (Step {self.current_step}/{self.steps})"

            images = self.images if self.images is not None else []

            output = {
                "status":       self.status,        # Essentially an enum
                "message":      message,            # Human readable message
                "current_step": self.current_step,
                "steps":        self.steps,
                "error":        self.error_msg,
                "width":        self.width,
                "height":       self.height,
                "images":      [ RequestData.encode_image(image) for image in images ]
            }
            return output
        
        except Exception as ex:
            print(ex)

    def cleanup(self) -> None:
        stable_diffusion.cleanup()


    # def selftest(self) -> JSON:
    #     # (optional but encouraged) Perform a self-test
    

    def long_process(self, data:RequestData):
        """
        This method performs the long running process, which in our case is image
        generation.
        """
        self.current_step     = 0
        self.images           = None
        self.error_msg        = ""

        prompt                = data.get_value("prompt", None)
        negative_prompt       = data.get_value("negative_prompt", None)
        seed                  = data.get_int("seed", None)
        self.steps            = data.get_int("num_inference_steps", 40)
        num_images_per_prompt = data.get_int("num_images_per_prompt", 1)
        self.width            = data.get_int("width", 1024)
        self.height           = data.get_int("height", 768)
        guidance_scale        = data.get_float("guidance_scale", 7.0)

        if not prompt or self.width < 10 or self.height < 10 or self.steps < 1:
            self.status    = "Failed"
            self.error_msg = "Image generation failed: Bad input parameters"
            return {
                "success": False,
                "status":  "Failed",
                "error": "Bad input parameters"
            }

        if seed == 'null':
            seed = None
        if num_images_per_prompt < 1:
            num_images_per_prompt = 1

        # Create the pipeline
        self.status     = "Started"
        self.status_msg = "Initialising pipeline"

        try:
            self.pipeline = stable_diffusion.create_pipeline(self.models_dir, 
                                                             self.model_name, 
                                                             self.device_name,
                                                             num_images_per_prompt,
                                                             self.width, self.height,
                                                             self.half_precision)
            if not self.pipeline:
                self.status    = "Failed"
                self.error_msg = "Image generation failed: Unable to create pipeline"
                return {
                    "success": False,
                    "status":  "Failed",
                    "error": "Unable to create pipeline"
                }
                        
        except Exception as ex:
            error = f"Image generation failed: Unable to create pipeline from {self.model_name} ({ex})"
            print(error)
            return {
                "success": False,
                "status":  "Failed",
                "error":   error
            }

        # Run the image generation
        self.status     = "Started"
        self.status_msg = "Image generation in progress"

        start_time = time.perf_counter()
        try:
            callback = self.image_create_callback_cpu if self.device_name == 'cpu' \
                       else self.image_create_callback_cuda

            image_result = stable_diffusion.create_image(self.pipeline, prompt,
                                                         negative_prompt, seed,
                                                         self.steps,
                                                         num_images_per_prompt,
                                                         self.width, self.height,
                                                         guidance_scale,
                                                         self.device_name,
                                                         callback)

            inferenceMs = int((time.perf_counter() - start_time) * 1000)            
            output = {
                "status":               "Completed",
                "inferenceMs":           inferenceMs,
                "prompt":                prompt,
                "negative_prompt":       negative_prompt,
                "num_inference_steps":   self.steps,
                "num_images_per_prompt": num_images_per_prompt,
                "width":                 self.width,
                "height":                self.height,
                "guidance_scale":        guidance_scale,
                "seed":                  image_result["seed"],
                "images": [ RequestData.encode_image(image) for image in image_result["images"]]
            }

            self.status_msg = "Image generation completed"
            self.error_msg  = None

        except Exception as ex:
            error = f"Image creation failed: {ex}"
            print(error)
            output = {
                "success": False,
                "status": "Failed",
                "error":  error
            }
           
        # Cleanup to free memory
        if self.device_name != 'cpu':
            if self.system_info.hasTorchCuda:
                torch.cuda.empty_cache()
                stable_diffusion.cleanup()  # This means a new pipeline every image gen operation

        # Set status right at the end because this is reported by get_process_status
        # which in turn tells the explorer whether to keep sending update requests.
        # Setting completed or failed before clear caches (or GC) means we may
        # miss out on sending the results back because the status returns 
        # 'complete' before this output is returned
        self.status_msg = "Done"
        if output["status"] == "Completed":
            self.status    = "Completed"
        else:            
            self.status    = "Failed"
            self.error_msg = output["error"]

        return output
 

    def image_create_callback_cpu(self, step, tensor, latents):
        """ Called each step in image generation when device is 'cpu'. """

        self.current_step = step + 1

        # only display the intermediate step images every few steps
        if not (self.current_step % self.skip_image_updates):
            """
            try:
                # Note even ChatGPT can work out how to convert latents to images
                # for a OVStableDiffusionPipeline object
                from PIL import Image
                images = self.pipeline.generate_images(latents)
                pil_images = []
                for image_tensor in images:
                    image_array = tensor.permute(1, 2, 0).cpu().numpy()
                    pil_image = Image.fromarray((image_array * 255).astype('uint8'))
                    pil_images.append(pil_image)
                self.images = pil_images
            except Exception as ex:
                self.images = None
            """
            # TODO: Return an image that indicates progress continues
            self.images     = []

    def image_create_callback_cuda(self, pipeline, step: int, timestep: int, callback_kwargs: Dict):
        """ Called each step in image generation when device is 'cuda'. """
        
        self.current_step = step + 1

        # only display the intermediate step images every few steps
        if not (self.current_step % self.skip_image_updates):
            try:
                # get the vae from the pipeline
                vae             = pipeline.vae
                image_processor = pipeline.image_processor
                latents         = callback_kwargs["latents"]

                step_images     = vae.decode(latents / vae.config.scaling_factor, \
                                             return_dict=True)
                do_denormalize  = [True] * step_images.sample.shape[0]  # create tensor of True
                step_images     = image_processor.postprocess(step_images.sample,
                                                              do_denormalize = do_denormalize)
                self.images     = step_images

            except Exception as e:
                step_images = None

        return callback_kwargs


if __name__ == "__main__":
    Text2Image_adapter().start_loop()
