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

    def initialise(self) -> None:

        # GPU support not fully working in Linux
        # if self.enable_GPU and self.system_info.hasTorchCuda:
        #    self.inference_device  = "GPU"
        #    self.inference_library = "CUDA"
        # else
        #    self.enable_GPU = False
        self.enable_GPU = False


    def process(self, data: RequestData) -> JSON:
        try:
            img: Image = data.get_image(0)
            model_name: str = data.get_value("model_name", self.opts.model_name)
            # print("model name = " + model_name)

            device_type = "cuda" if self.enable_GPU else "cpu"

            start_time = time.perf_counter()
            (cartoon, inferenceMs) = inference(img, self.opts.weights_dir, 
                                               model_name, device_type)

            processMs = int((time.perf_counter() - start_time) * 1000)

            response = { 
                "success":     True, 
                "imageBase64": RequestData.encode_image(cartoon),
                "processMs":   processMs,
                "inferenceMs": inferenceMs
            }

        except Exception as ex:
            self.report_error(ex, __file__)
            response = { "success": False, "error": "unable to process the image" }

        return response 


    def selftest(self) -> JSON:
        
        import os
        os.environ["WEIGHTS_FOLDER"] = os.path.join(self.module_path, "weights")
        file_name = os.path.join("test", "chris-hemsworth-2.jpg")

        request_data = RequestData()
        request_data.queue   = self.queue_name
        request_data.command = "cartoonise"
        request_data.add_file(file_name)
        request_data.add_value("model_name", "celeba_distill")

        result = self.process(request_data)
        print(f"Info: Self-test for {self.module_id}. Success: {result['success']}")
        # print(f"Info: Self-test output for {self.module_id}: {result}")

        return { "success": result['success'], "message": "cartoonise test successful" }


if __name__ == "__main__":
    cartooniser_adapter().start_loop()
