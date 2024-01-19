# Import our general libraries
import sys
import time

# Import the CodeProject.AI SDK. This will add to the PATH  for future imports
sys.path.append("../../SDK/Python")
from request_data import RequestData
from module_runner import ModuleRunner
from common import JSON

from options import Options

from PIL import Image

# Import the method of the module we're wrapping
from ALPR import init_detect_platenumber, detect_platenumber


class ALPR_adapter(ModuleRunner):

    def __init__(self):
        super().__init__()
        self.opts = Options()

        self.inferenceCount = 0
        self.itemsDetected  = 0

    def initialise(self) -> None:
        self.can_use_GPU = self.system_info.hasPaddleGPU

        # HACK: We're seeing problems with GPU support on older cards. Allow
        # some checks to be done
        if self.system_info.hasPaddleGPU:
            import paddle

            if not paddle.device.cuda.device_count() or \
                paddle.device.cuda.get_device_capability()[0] < self.opts.min_compute_capability:
                self.can_use_GPU = False

            if paddle.device.get_cudnn_version() / 100.0 < self.opts.min_cuDNN_version: 
                self.can_use_GPU = False
        # end hack

        self.opts.use_gpu = self.enable_GPU and self.can_use_GPU

        if self.opts.use_gpu:
            self.processor_type     = "GPU"
            self.execution_provider = "CUDA"   # PaddleOCR supports only CUDA enabled GPUs at this point

        init_detect_platenumber(self.opts)


    async def process(self, data: RequestData) -> JSON:
        try:
            image: Image = data.get_image(0)

            start_time = time.perf_counter()

            result = await detect_platenumber(self, self.opts, image)

            if "error" in result and result["error"]:
                return { "success": False, "error": result["error"] }

            self.inferenceCount += 1 

            predictions = result["predictions"]
            if len(predictions) > 3:
                message = 'Found ' + (', '.join(det["label"] for det in predictions)) + "..."
            elif len(predictions) > 0:
                message = 'Found ' + (', '.join(det["label"] for det in predictions))
            else:
                message = "No plates found"

            self.itemsDetected += len(predictions) 

            return {
                "success": True, 
                "predictions": predictions, 
                "message": message,
                "processMs" : int((time.perf_counter() - start_time) * 1000),
                "inferenceMs" : result["inferenceMs"]
            }

        except Exception as ex:
            await self.report_error_async(ex, __file__)
            return { "success": False, "error": "unable to process the image" }


    def selftest(slf) -> JSON:
        try:
            import paddle
            paddle.utils.run_check()
            return { "success": True, "message": "PaddlePaddle self test successful" }
        except:
            return { "success": False, "message": "PaddlePaddle self test failed" }


    def cleanup(self) -> None:
        pass

if __name__ == "__main__":
    ALPR_adapter().start_loop()
