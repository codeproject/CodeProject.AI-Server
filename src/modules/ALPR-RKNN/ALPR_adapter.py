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
            self.enable_GPU = self.hasFastDeployRockNPU

        if self.enable_GPU:
            print("Rockchip NPU detected")
            self.execution_provider = "RKNPU"
        
        init_detect_platenumber(self.opts)


    def process(self, data: RequestData) -> JSON:

        image: Image = data.get_image(0)

        start_time = time.perf_counter()

        result = detect_platenumber(self, self.opts, image)
        # result = detect_platenumber(self, self.opts, image)


        if "error" in result and result["error"]:
            return { "success": False, "error": result["error"] }

        predictions = result["predictions"]
        if len(predictions) > 3:
            message = 'Found ' + (', '.join(det["label"] for det in predictions)) + "..."
        elif len(predictions) > 0:
            message = 'Found ' + (', '.join(det["label"] for det in predictions))
        else:
            message = "No plates found"

        return {
            "success": True, 
            "predictions": predictions, 
            "message": message,
            "processMs" : int((time.perf_counter() - start_time) * 1000),
            "inferenceMs" : result["inferenceMs"]
        }

    def shutdown(self) -> None:
        pass

if __name__ == "__main__":
    ALPR_adapter().start_loop()
