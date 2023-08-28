 # Import our general libraries
import sys
import time

# Import the CodeProject.AI SDK. This will add to the PATH var for future imports
sys.path.append("../../SDK/Python")
from request_data import RequestData
from module_runner import ModuleRunner
from common import JSON
from threading import Lock

# Import packages we've installed into our VENV
from PIL import Image

from options import Options

# Import the method of the module we're wrapping
from cartooniser import inference

class cartooniser_adapter(ModuleRunner):

    def __init__(self):
        super().__init__()
        self.opts = Options()
    async def initialise(self) -> None:
        # GPU support not fully working in Linux
        # if self.opts.use_gpu and not self.hasTorchCuda:
        #     self.opts.use_gpu = False
        self.opts.use_gpu = False

        if self.opts.use_gpu:
            self.execution_provider = "CUDA"

    async def process(self, data: RequestData) -> JSON:
        try:
            img: Image = data.get_image(0)
            model_name: str = data.get_value("model_name", self.opts.model_name)
            print("model name = " + model_name)

            device_type = "cuda" if self.opts.use_gpu else "cpu"

            start_time = time.perf_counter()
            (cartoon, inferenceMs) = inference(img, self.opts.weights_dir, 
                                                   model_name, device_type)

            processMs = int((time.perf_counter() - start_time) * 1000)

            return { 
                "success":     True, 
                "imageBase64": RequestData.encode_image(cartoon),
                "processMs":   processMs,
                "inferenceMs": inferenceMs
            }

        except Exception as ex:
            await self.report_error_async(ex, __file__)
            return {"success": False, "error": "unable to process the image"}

    def shutdown(self) -> None:
        pass

if __name__ == "__main__":
    cartooniser_adapter().start_loop()
