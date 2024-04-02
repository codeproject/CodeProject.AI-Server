# Import our general libraries
import os
import sys
import time

# Import the CodeProject.AI SDK. This will add to the PATH var for future imports
sys.path.append("../../SDK/Python")
from common import JSON
from request_data import RequestData
from module_runner import ModuleRunner

# Import the method of the module we're wrapping
from options import Options
from PIL import UnidentifiedImageError, Image

# Make sure we can find the coral libraries
import platform
if platform.system() == "Darwin": # or platform.system() == "Linux"
    search_path = ''
    # if platform.system() == "Linux": # Linux installs in global sitepackages
    #     search_path = f"/usr/lib/python{version.major}.{version.minor}/site-packages/"
    # else:
    if platform.uname()[4] == 'x86_64' and platform.release()[:2] != '20':   # macOS 11 / Big Sur on Intel can install pycoral PIP
       search_path = f"./pycoral_simplified/"    # macOS will use the simplified library
    elif platform.uname()[4] == 'arm64' and platform.release()[:2] != '21':  # macOS 12 / Monterey on arm64 can install pycoral PIP
       search_path = f"./pycoral_simplified/"    # macOS will use the simplified library
    if search_path:
        import sys
        sys.path.insert(0, search_path)

# Import the method of the module we're wrapping
opts = Options()

class CoralObjectDetector_adapter(ModuleRunner):

    # async 
    def initialise(self) -> None:
        
        # if the module was launched outside of the server then the queue name 
        # wasn't set. This is normally fine, but here we want the queue to be
        # the same as the other object detection queues
        if not self.launched_by_server:
            self.queue_name = "objectdetection_queue"

        self.inference_library = "TF-Lite"

        if self.enable_GPU:
            self.enable_GPU = self.system_info.hasCoralTPU
            if self.enable_GPU:
                print("Info: TPU detected")

        # Multi-TPU depends on pycoral.bind._pywrap which we only have if the
        # Coral libs are installed and accessible. Test this first
        if opts.use_multi_tpu and self.enable_GPU:
            print("Info: Attempting multi-TPU initialisation")
    
            import objectdetection_coral_multitpu as odcm
            (device, error) = odcm.init_detect(opts)

            # Fallback if we need to
            if not device:
                print("Info: Failed to init multi-TPU. Falling back to single TPU.")
                opts.use_multi_tpu = False

                import objectdetection_coral_singletpu as odcs
                (device, error) = odcs.init_detect(opts)

        else:
            import objectdetection_coral_singletpu as odcs
            (device, error) = odcs.init_detect(opts)

        if not device or device.upper() == "CPU":
            self.inference_device = "CPU"
            print("Info: Using CPU")
        else:
            if opts.use_multi_tpu:
                print("Info: Supporting multiple Edge TPUs")
                self.inference_device = "Multi-TPU"
            else:
                print("Info: Using Edge TPU")
                self.inference_device = "TPU"

        self._num_items_found = 0
        self._histogram       = {}

        
    #async 
    def process(self, data: RequestData) -> JSON:

        # The route to here is /v1/vision/detection

        if data.command == "list-custom":               # list all models available
            return self._list_models()     

        if data.command == "detect" or data.command == "custom":
            threshold: float  = float(data.get_value("min_confidence", opts.min_confidence))
            img: Image        = data.get_image(0)

            model_name:str = "MobileNet SSD"
            if data.segments and data.segments[0]:
                model_name = data.segments[0]

            if model_name.lower() != opts.model_name.lower():
                opts.set_model(model_name)

            # response = await self._do_detection(img, threshold)
            response = self._do_detection(img, threshold)

        else:
            # await self.report_error_async(None, __file__, f"Unknown command {data.command}")
            self.report_error(None, __file__, f"Unknown command {data.command}")
            response = { "success": False, "error": "unsupported command" }

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


    def cleanup(self):
        if opts.use_multi_tpu:
            from objectdetection_coral_multitpu import cleanup
            cleanup()


    def _list_models(self):
        if opts.use_multi_tpu and self.enable_GPU:
            from objectdetection_coral_multitpu import list_models
        else:
            from objectdetection_coral_singletpu import list_models

        return list_models(opts)


    # async 
    def _do_detection(self, img: any, score_threshold: float):
        
        start_process_time = time.perf_counter()
    
        # Multi-TPU depends on pycoral.bind._pywrap which we only have if the
        # Coral libs are installed and accessible. Test this first
        if opts.use_multi_tpu and self.enable_GPU:
            from objectdetection_coral_multitpu import do_detect
        else:
            from objectdetection_coral_singletpu import do_detect

        try:
            result = do_detect(opts, img, score_threshold)   

            if not result['success']:
                return {
                    "success"     : False,
                    "error"       : result["error"] if "error" in result else "Unable to perform detection",
                    "inferenceMs" : result['inferenceMs'],
                    "processMs"   : int((time.perf_counter() - start_process_time) * 1000),
                    "predictions" : [],
                    "message"     : '',
                    "count"       : 0
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
                
            # Update the device on which inferencing occurred
            if "inferenceDevice" in result:
                self.inference_device = result["inferenceDevice"]

            return {
                "success"     : result['success'],
                "inferenceMs" : result['inferenceMs'],
                "processMs"   : int((time.perf_counter() - start_process_time) * 1000),
                "message"     : message,
                "count"       : result["count"],
                "predictions" : result['predictions']
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
