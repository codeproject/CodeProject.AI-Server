# Import our general libraries
import os
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
from objectdetection_coral import init_detect, do_detect

opts = Options()
sem = threading.Semaphore()

class CoralObjectDetector_adapter(ModuleRunner):

    # async 
    def initialise(self) -> None:
        # if the module was launched outside of the server then the queue name 
        # wasn't set. This is normally fine, but here we want the queue to be
        # the same as the other object detection queues
        if not self.launched_by_server:
            self.queue_name = "objectdetection_queue"

        if self.enable_GPU:
            self.enable_GPU = self.hasCoralTPU

        if self.enable_GPU:
            print("Edge TPU detected")
            self.execution_provider = "TPU"

        device = init_detect(opts)
        if device.upper() == "TPU":
            self.execution_provider = "TPU"
        else:
            self.execution_provider = "CPU"
        
    #async 
    def process(self, data: RequestData) -> JSON:

        # The route to here is /v1/vision/detection

        if data.command == "list-custom":               # list all models available
            return { "success": True, "models": [ 'MobileNet SSD'] }

        if data.command == "detect" or data.command == "custom":
            threshold: float  = float(data.get_value("min_confidence", opts.min_confidence))
            img: Image        = data.get_image(0)

            # response = await self.do_detection(img, threshold)
            response = self.do_detection(img, threshold)
        else:
            # await self.report_error_async(None, __file__, f"Unknown command {data.command}")
            self.report_error(None, __file__, f"Unknown command {data.command}")
            response = { "success": False, "error": "unsupported command" }

        return response


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

    # async 
    def do_detection(self, img: any, score_threshold: float):
        
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

            result = do_detect(opts, img, score_threshold)
            sem.release()

            if not result['success']:
                return {
                    "success"     : False,
                    "predictions" : [],
                    "message"     : '',
                    "error"       : result["error"] if "error" in result else "Unable to perform detection",
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
                "message"     : message,
                "count"       : result["count"],
                "predictions" : result['predictions'],
                "success"     : result['success'],
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
    CoralObjectDetector_adapter().start_loop()
