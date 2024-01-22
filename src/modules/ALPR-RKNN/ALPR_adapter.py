# Import our general libraries
import sys
import time

# Import the CodeProject.AI SDK. This will add to the PATH  for future imports
sys.path.append("../../SDK/Python")
from request_data import RequestData
from module_runner import ModuleRunner
from module_options import ModuleOptions
from common import JSON

from options import Options

from PIL import Image

# Import the method of the module we're wrapping
from ALPR import init_detect_platenumber, detect_platenumber


class ALPR_adapter(ModuleRunner):

    def __init__(self):
        super().__init__()
        self.opts = Options()

    def initialise(self) -> None:

        if self.enable_GPU:
            self.enable_GPU = self.system_info.hasFastDeployRockNPU

        if self.enable_GPU:
            print("Rockchip NPU detected")
            self.execution_provider = "RKNPU"
        
        init_detect_platenumber(self.opts)

        self.success_inferences   = 0
        self.total_success_inf_ms = 0
        self.failed_inferences    = 0
        self.num_items_found      = 0


    def process(self, data: RequestData) -> JSON:

        image: Image = data.get_image(0)

        start_time = time.perf_counter()

        result = detect_platenumber(self, self.opts, image)
        # result = detect_platenumber(self, self.opts, image)

        if "error" in result and result["error"]:
            response = { "success": False, "error": result["error"] }
            self._update_statistics(response)
            return response 

        predictions = result["predictions"]
        if len(predictions) > 3:
            message = 'Found ' + (', '.join(det["label"] for det in predictions)) + "..."
        elif len(predictions) > 0:
            message = 'Found ' + (', '.join(det["label"] for det in predictions))
        else:
            message = "No plates found"

        response = {
            "success": True, 
            "predictions": predictions, 
            "message": message,
            "processMs" : int((time.perf_counter() - start_time) * 1000),
            "inferenceMs" : result["inferenceMs"]
        }

        self._update_statistics(response)
        return response 


    def status(self, data: RequestData = None) -> JSON:
        return { 
            "successfulInferences" : self.success_inferences,
            "failedInferences"     : self.failed_inferences,
            "numInferences"        : self.success_inferences + self.failed_inferences,
            "numItemsFound"        : self.num_items_found,
            "averageInferenceMs"   : 0 if not self.success_inferences 
                                     else self.total_success_inf_ms / self.success_inferences,
        }


    def cleanup(self) -> None:
        pass


    def _update_statistics(self, response):

        if "success" in response and response["success"]:
            if "predictions" in response:
                if "inferenceMs" in response:
                    self.total_success_inf_ms += response["inferenceMs"]
                    self.success_inferences += 1
                predictions = response["predictions"]
                self.num_items_found += len(predictions) 
        else:
            self.failed_inferences += 1



if __name__ == "__main__":
    ALPR_adapter().start_loop()
