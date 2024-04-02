# Import our general libraries
import os
from os.path import exists
import sys
from threading import Lock
import time

# os.environ["OMP_NUM_THREADS"]             = "1"     # OpenBlas issue
os.environ["PYTORCH_ENABLE_MPS_FALLBACK"] = "1"     # For PyTorch on Apple silicon

# Import the CodeProject.AI SDK. This will add to the PATH var for future imports
sys.path.append("../../SDK/Python")
from common import JSON
from request_data import RequestData
from module_runner import ModuleRunner
from module_logging import LogMethod

# Import the method of the module we're wrapping
from options import Options

from PIL import UnidentifiedImageError, Image

from process import YOLODetector, init_detect


class YOLO31_adapter(ModuleRunner):

    def __init__(self):
        super().__init__()
        self.opts           = Options()
        self.models_updated = None
        self.model_names    = []  # We'll use this to cache the available model names
        self.detectors      = {}  # We'll use this to cache the detectors based on models
        self.models_lock    = Lock()

    def initialise(self):

        # if the module was launched outside of the server then the queue name 
        # wasn't set. This is normally fine, but here we want the queue to be
        # the same as the other object detection queues
        if not self.launched_by_server:
            self.queue_name = "objectdetection_queue"

        init_detect(self.opts)

        self.can_use_GPU = self.system_info.hasTorchCuda or \
                           self.system_info.hasTorchMPS

        if self.opts.use_CUDA:
            self.inference_device  = "GPU"
            self.inference_library = "CUDA"
        elif self.opts.use_MPS:
            self.inference_device  = "GPU"
            self.inference_library = "MPS"

        if self.opts.use_CUDA and self.half_precision == 'enable' and \
           not self.system_info.hasTorchHalfPrecision:
            self.half_precision = 'disable'

        self._num_items_found      = 0
        self._histogram            = {}


    def process(self, data: RequestData) -> JSON:
        
        response = None

        if data.command == "list-custom":               # list all models available

            # The route to here is /v1/vision/custom/list

            response = self._list_models(self.opts.custom_models_dir)

        elif data.command == "detect":                  # Perform 'standard' object detection

            # The route to here is /v1/vision/detection

            threshold: float = float(data.get_value("min_confidence", "0.4"))
            img: Image       = data.get_image(0)

            response = self._do_detection(self.opts.models_dir, self.opts.std_model_name,
                                          self.opts.resolution_pixels, self.opts.use_CUDA,
                                          self.accel_device_name, self.opts.use_MPS,
                                          self.half_precision, img, threshold)

        elif data.command == "custom":                  # Perform custom object detection

            threshold: float  = float(data.get_value("min_confidence", "0.4"))
            img: Image        = data.get_image(0)

            # The route to here is /v1/vision/custom/<model-name>. if mode-name = general,
            # or no model provided, then a built-in general purpose mode will be used.
            model_dir:str  = self.opts.custom_models_dir
            model_name:str = "general"
            if data.segments and data.segments[0]:
                model_name = data.segments[0]

            # Map the "general" model to our current "general" model

            # if model_name == "general":              # use the standard YOLO model
            #    model_dir  = opts.models_dir
            #    model_name = opts.std_model_name

            if model_name == "general":                # Use the custom IP Cam general model
                model_dir  = self.opts.custom_models_dir
                model_name = "ipcam-general" 

            self.log(LogMethod.Info | LogMethod.Server,
            { 
                "filename": __file__,
                "loglevel": "information",
                "method": sys._getframe().f_code.co_name,
                "message": f"Detecting using {model_name}"
            })

            use_mX_GPU = False # self.opts.use_MPS   - Custom models don't currently work with pyTorch on MPS
            response = self._do_detection(model_dir, model_name, self.opts.resolution_pixels,
                                          self.opts.use_CUDA, self.accel_device_name, use_mX_GPU,
                                          self.half_precision, img, threshold)

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


    def _list_models(self, models_path):

        """
        Lists the custom models we have in the assets folder. This ignores the 
        yolo* files.
        """

        # We'll only refresh the list of models at most once a minute
        if self.models_updated is None or (time.time() - self.models_updated) >= 60:
            self.model_names = [entry.name[:-3] for entry in os.scandir(models_path)
                                            if (entry.is_file()
                                            and entry.name.endswith(".pt")
                                            and not entry.name.startswith("yolov5"))]
            self.models_updated = time.time()

        return { "success": True, "models": self.model_names }

    def _get_detector(self, models_dir: str, model_name: str, resolution: int,
                      use_Cuda: bool, accel_device_name: str, use_MPS: bool,
                      half_precision: str) -> any:

        """
        We have a detector for each custom model. Lookup the detector, or if it's 
        not found, create a new one and add it to our lookup.
        """

        detector = self.detectors.get(model_name, None)
        if detector is None:
            with self.models_lock:
                detector = self.detectors.get(model_name, None)
                if detector is None:
                    model_path = os.path.join(models_dir, model_name + ".pt")

                    # YOLOv5 will check for the existence of files and attempt to 
                    # download missing files from the cloud. Let's not do that: it's
                    # unexpected, can fail if no internet, and slows things down if
                    # a system is constantly asking for a model that simply doesn't
                    # exist. Set things up correctly at install time.
                    if exists(model_path):
                        try:
                            detector = YOLODetector(model_path, resolution, 
                                                    use_Cuda, accel_device_name,
                                                    use_MPS, half_precision)
                            self.detectors[model_name] = detector

                            self.log(LogMethod.Server,
                            { 
                                "filename": __file__,
                                "method": sys._getframe().f_code.co_name,
                                "loglevel": "debug",
                                "message": f"Model Path is {model_path}"
                            })

                        except Exception as ex:
                            self.report_error(None, __file__, f"Unable to load model at {model_path} ({str(ex)})")
                            detector = None

                    else:
                        self.report_error(None, __file__, f"{model_path} does not exist")

        return detector

    def _do_detection(self, models_dir: str, model_name: str, resolution: int,
                      use_Cuda: bool, accel_device_name: str, use_MPS: bool,
                      half_precision: str, img: any, threshold: float):
        
        # We have a detector for each custom model. Lookup the detector, or if it's
        # not found, create a new one and add it to our lookup.

        create_err_msg = f"Unable to create YOLO detector for model {model_name}"

        start_time = time.perf_counter()

        try:
            detector = self._get_detector(models_dir, model_name, resolution, use_Cuda,
                                          accel_device_name, use_MPS, half_precision)
        except Exception as ex:
            create_err_msg = f"{create_err_msg} ({str(ex)})"

        if detector is None:
            self.report_error(None, __file__, create_err_msg)
            return { "success": False, "error": create_err_msg }
        
        # We have a detector for this model, so let's go ahead and detect
        try:
            (det, inferenceMs) = detector.predictFromImage(img, threshold)

            outputs = []

            for *xyxy, conf, cls in reversed(det):
                x_min = xyxy[0]
                y_min = xyxy[1]
                x_max = xyxy[2]
                y_max = xyxy[3]
                score = conf.item()

                label = detector.names[int(cls.item())]

                detection = {
                    "confidence": score,
                    "label": label,
                    "x_min": int(x_min),
                    "y_min": int(y_min),
                    "x_max": int(x_max),
                    "y_max": int(y_max),
                }

                outputs.append(detection)

            if len(outputs) > 3:
                message = 'Found ' + (', '.join(det["label"] for det in outputs[0:3])) + "..."
            elif len(outputs) > 0:
                message = 'Found ' + (', '.join(det["label"] for det in outputs))
            else:
                message = "No objects found"

            return {
                "message"     : message,
                "count"       : len(outputs),
                "predictions" : outputs,
                "success"     : True, 
                "processMs"   : int((time.perf_counter() - start_time) * 1000),
                "inferenceMs" : inferenceMs
            }

        except UnidentifiedImageError as img_ex:
            self.report_error(img_ex, __file__, "The image provided was of an unknown type")
            return { "success": False, "error": "invalid image file" }

        except Exception as ex:
            self.report_error(ex, __file__)
            return { "success": False, "error": "Error occurred on the server" }


if __name__ == "__main__":
    YOLO31_adapter().start_loop()