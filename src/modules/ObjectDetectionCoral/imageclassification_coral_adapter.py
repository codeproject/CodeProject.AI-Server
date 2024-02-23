# Import our general libraries
import sys
import time
import threading

# Import the CodeProject.AI SDK. This will add to the PATH var for future imports
sys.path.append("../../SDK/Python")
from common import JSON
from request_data import RequestData
from module_runner import ModuleRunner

# Import the method of the module we're wrapping
from options import Options
from PIL import UnidentifiedImageError, Image

# Import the method of the module we're wrapping
from imageclassification_coral import init_classify, do_classify

opts = Options()
sem = threading.Semaphore()

class CoralClassifier_adapter(ModuleRunner):

    # async 
    def initialise(self) -> None:

        if self.enable_GPU:
            self.enable_GPU = self.system_info.hasCoralTPU

        if self.enable_GPU:
            print("Edge TPU detected")
            self.inference_device  = "TPU"
            self.inference_library = "TF-Lite"
        # else:
        #    opts.model_tpu_file = None # disable TPU

        init_classify(opts)

    #async 
    def process(self, data: RequestData) -> JSON:

        # The route to here is /v1/vision/classify

        if data.command == "list-custom":               # list all models available
            return { "success": True, "models": [ 'MobileNet SSD'] }

        if data.command == "classify" or data.command == "custom":
            threshold: float  = float(data.get_value("min_confidence", opts.min_confidence))
            img: Image        = data.get_image(0)

            # response = await self.do_classification(img, threshold)
            response = self.do_classification(img, threshold)
        else:
            # await self.report_error_async(None, __file__, f"Unknown command {data.command}")
            self.report_error(None, __file__, f"Unknown command {data.command}")
            response = { "success": False, "error": "unsupported command" }

        return response


    # async 
    def do_classification(self, img: any, score_threshold: float):
        
        start_process_time = time.perf_counter()
    
        try:
        
            # An attempt to fix "RuntimeError: There is at least 1 reference to 
            # internal data in the interpreter in the form of a numpy array or 
            # slice. Be sure to only hold the function returned from tensor() if
            # you are using raw data access.
            if not sem.acquire(timeout=1):
                return {
                    "success"     : False,
                    "predictions" : [],
                    "message"     : "The interpreter is in use. Please try again later",
                    "count"       : 0,
                    "processMs"   : int((time.perf_counter() - start_process_time) * 1000),
                    "inferenceMs" : 0
                }

            result = do_classify(opts, img, score_threshold)
            sem.release()

            if not result['success']:
                return {
                    "success"     : False,
                    "predictions" : [],
                    "message"     : '',
                    "error"       : result["error"] if "error" in result else "Unable to perform classification",
                    "count"       : 0,
                    "processMs"   : int((time.perf_counter() - start_process_time) * 1000),
                    "inferenceMs" : result['inferenceMs']
                }
            
            predictions = result["predictions"]
            if len(predictions) > 3:
                message = 'Found ' + (', '.join(det["label"] for det in predictions[0:3])) + "..."
            elif len(predictions) > 0:
                message = 'Found ' + (', '.join(det["label"] for det in predictions))
            elif "error" in result:
                message = result["error"]
            else:
                message = "No objects found"
            
            # print(message)

            return {
                "success"     : result['success'],
                "predictions" : result['predictions'],
                "message"     : message,
                "count"       : result["count"],
                "processMs"   : int((time.perf_counter() - start_process_time) * 1000),
                "inferenceMs" : result['inferenceMs']
            }

        except UnidentifiedImageError as img_ex:
            # await self.report_error_async(img_ex, __file__, "The image provided was of an unknown type")
            self.report_error(img_ex, __file__, "The image provided was of an unknown type")
            return { "success": False, "error": "invalid image file" }

        except Exception as ex:
            # await self.report_error_async(ex, __file__)
            self.report_error(ex, __file__)
            return { "success": False, "error": "Error occurred on the server"}


if __name__ == "__main__":
    CoralClassifier_adapter().start_loop()
