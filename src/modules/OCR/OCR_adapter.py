# Import our general libraries
import sys
import time

# Import the CodeProject.AI SDK. This will add to the PATH var for future imports
sys.path.append("../../SDK/Python")
from common import JSON
from request_data import RequestData
from module_runner import ModuleRunner

from options import Options

from PIL import Image

# Import the method of the module we're wrapping
from OCR import init_detect_ocr, read_text

class OCR_adapter(ModuleRunner):

    def __init__(self):
        super().__init__()
        self.opts = Options()

    def initialise(self) -> None:
        self.opts.use_gpu = self.support_GPU and self.hasPaddleGPU
        if self.opts.use_gpu:
            self.processor_type     = "GPU"
            self.execution_provider = "CUDA"   # PaddleOCR supports only CUDA enabled GPUs at this point

        init_detect_ocr(self.opts)

    def process(self, data: RequestData) -> JSON:
        try:
            image: Image = data.get_image(0)

            start_time = time.perf_counter()

            result = read_text(self, image)

            if "error" in result and result["error"]:
                return { "success": False, "error": result["error"] }

            predictions = result["predictions"]
            message = "1 text found" if len(predictions) == 1 else f"{len(predictions)} pieces of text found"

            return {
                "success":     True,
                "predictions": result["predictions"],
                "message":     message,
                "processMs":   int((time.perf_counter() - start_time) * 1000),
                "inferenceMs": result["inferenceMs"]
            }

        except Exception as ex:
            self.report_error(ex, __file__)
            return { "success": False, "error": "unable to process the image" }

    def shutdown(self) -> None:
        pass

if __name__ == "__main__":
    OCR_adapter().start_loop()
