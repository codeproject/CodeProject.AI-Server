# Import our general libraries
import os
import sys

# Import the CodeProject.AI SDK. This will add to the PATH var for future imports
sys.path.append("../../SDK/Python")

# HACK: For a module in the demos/modules folder we need to add this search path
sys.path.append("../../../SDK/Python")

from common import JSON
from request_data import RequestData
from module_options import ModuleOptions
from module_runner import ModuleRunner
from module_logging import LogMethod

# Import necessary modules we've installed 
from PIL import Image

# Import the method of the module we're wrapping
from detect import do_detection

# Our adapter
class YOLOv8_adapter(ModuleRunner):

    def initialise(self):
        # Can we use the GPU (via PyTorch / CUDA)?
        if self.system_info.hasTorchCuda:
            self.can_use_GPU       = True
            self.inference_device  = "GPU"
            self.inference_library = "CUDA"

        self.models_dir        = ModuleOptions.getEnvVariable("CPAI_MODULE_YOLODEMO_MODEL_DIR",  "assets")
        self.std_model_name    = ModuleOptions.getEnvVariable("CPAI_MODULE_YOLODEMO_MODEL_NAME", "yolov8m")
        self.resolution_pixels = int(ModuleOptions.getEnvVariable("CPAI_MODULE_YOLODEMO_RESOLUTION", 640))
        self.accel_device_name = "cuda" if self.can_use_GPU else "cpu"
       
        # Let's store some stats
        self._num_items_found = 0
        self._histogram       = {}


    def process(self, data: RequestData) -> JSON:
        
        response = None

        if data.command == "detect": # Detection using standard models (API: /v1/vision/detection)

            threshold: float = float(data.get_value("min_confidence", "0.4"))
            img: Image       = data.get_image(0)
            model_name: str  = "yolov8m"

            response = do_detection(img, threshold, self.models_dir, model_name, 
                                    self.resolution_pixels, self.can_use_GPU, self.accel_device_name,
                                    False, False, self.half_precision)

        elif data.command == "custom": # Detection using custom model (API: /v1/vision/custom/<model>)

            threshold: float  = float(data.get_value("min_confidence", "0.4"))
            img: Image        = data.get_image(0)
            model_name: str   = None

            # 'segments' is everything after /v1/<route>/, so for
            # /v1/vision/custom/<model> segments will be ["<model>"]. 
            # eg /v1/vision/custom/animals => segment[0] = "animals"
            if data.segments and data.segments[0]:
                model_name = data.segments[0]

            if not model_name:
                return { "success": False, "error": "No custom model specified" }
            
            if not os.path.exists(os.path.join(self.models_dir, model_name + ".pt")):
                return { "success": False, "error": f"Could not find custom model {model_name}" }

            self.log(LogMethod.Info | LogMethod.Server,
            { 
                "filename": __file__,
                "loglevel": "information",
                "method": sys._getframe().f_code.co_name,
                "message": f"Detecting using {model_name}"
            })

            response = do_detection(img, threshold, self.models_dir, model_name, 
                                    self.resolution_pixels, self.can_use_GPU, self.accel_device_name,
                                    False, False, self.half_precision)
            
        else:
            response = { "success" : False }
            self.report_error(None, __file__, f"Unknown command {data.command}")

        return response


    def status(self) -> JSON:
        statusData = super().status()
        statusData["numItemsFound"] = self._num_items_found
        statusData["histogram"]     = self._histogram
        return statusData


    def update_statistics(self, response):
        super().update_statistics(response)
        if "success" in response and response["success"] and "predictions" in response:
            predictions = response["predictions"]
            self._num_items_found += len(predictions) 
            for prediction in predictions:
                label = prediction["label"]
                if label not in self._histogram:
                    self._histogram[label] = 1
                else:
                    self._histogram[label] += 1
    

    def selftest(self) -> JSON:
        
        file_name = os.path.join("test", "home-office.jpg")

        request_data = RequestData()
        request_data.queue   = self.queue_name
        request_data.command = "detect"
        request_data.add_file(file_name)
        request_data.add_value("min_confidence", 0.4)

        result = self.process(request_data)
        print(f"Info: Self-test for {self.module_id}. Success: {result['success']}")
        # print(f"Info: Self-test output for {self.module_id}: {result}")

        return { "success": result['success'], "message": "Object detection test successful" }


if __name__ == "__main__":   
    YOLOv8_adapter().start_loop()