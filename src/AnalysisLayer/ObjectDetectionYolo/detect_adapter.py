# Import our general libraries
import os
import sys
import traceback
import time
from os.path import exists

# Hacky VS Code debug stuff
# if os.getenv("DEBUG_IN_VSCODE", "") == "True":
#    ...

# Import the CodeProject.AI SDK. This will add to the PATH vaar for future imports
sys.path.append("../SDK/Python")
from common import JSON
from codeprojectai import CodeProjectAIRunner
from requestdata import AIRequestData
from analysislogging import LogMethod

# Import the method of the module we're wrapping
from options import Options
from PIL import UnidentifiedImageError, Image
from process import YOLODetector
from threading import Lock

models_lock = Lock()

# Don't see that this is needed. Maybe for data dirs?
# sys.path.insert(0, os.path.join(os.path.dirname(os.path.realpath(__file__)), "."))


# Setup a global bucket of YOLO detectors. One for each model
models_last_checked = None
model_names    = []  # We'll use this to cache the available model names
detectors      = {}  # We'll use this to cache the detectors based on models

def main():

    # create a CodeProject.AI module object
    module_runner = CodeProjectAIRunner("detection_queue", object_detect_callback, 
                                        object_detect_init_callback)

    # Hack for debug mode
    if module_runner.module_id == "CodeProject.AI":
        module_runner.module_id   = "ObjectDetectionYolo"
        module_runner.module_name = "Object Detection (YOLO)"
        os.environ["PYTORCH_ENABLE_MPS_FALLBACK"] = "1"     # For PyTorch on Apple silicon

    if Options.use_CUDA and module_runner.support_GPU:
        module_runner.execution_provider = "CUDA"
    elif Options.use_MPS and module_runner.support_GPU:
        module_runner.execution_provider = "MPS"
    
    # Start the module
    module_runner.start_loop()


def object_detect_init_callback (module_runner: CodeProjectAIRunner):

    module_runner.log(LogMethod.Info | LogMethod.Server,
                        { 
                            "filename": "detect_adapter.py",
                            "loglevel": "information",
                            "method": "init_callback",
                            "message": f"Running init for {module_runner.module_name}"
                        })
    # do other initialization here


def object_detect_callback(module_runner: CodeProjectAIRunner, data: AIRequestData) -> JSON:
    
    if data.command == "list-custom":               # list all models available

        # The route to here is /v1/vision/custom/list

        response = list_models(module_runner, Options.custom_models_dir)

    elif data.command == "detect":                  # Perform 'standard' object detection

        # The route to here is /v1/vision/detection

        threshold: float  = float(data.get_value("min_confidence", "0.4"))
        img: Image        = data.get_image(0)

        response = do_detection(module_runner, Options.models_dir,
                                Options.std_model_name, Options.resolution_pixels,
                                Options.use_CUDA, Options.cuda_device_num,
                                Options.use_MPS, Options.half_precision,
                                img, threshold)

    elif data.command == "custom":                  # Perform custom object detection

        threshold: float  = float(data.get_value("min_confidence", "0.4"))
        img: Image        = data.get_image(0)

        # The route to here is /v1/vision/custom/<model-name>. if mode-name = general,
        # or no model provided, then a built-in general purpose mode will be used.
        model_dir  = Options.custom_models_dir
        if not data.segments or not data.segments[0]:
            model_name = "general"
        else:
            model_name = data.segments[0]

        # Map the "general" model to our current "general" model

        # if model_name == "general":              # use the standard YOLO model
        #    model_dir  = Options.models_dir
        #    model_name = Options.std_model_name

        if model_name == "general":                # Use the custom IP Cam general model
            model_dir  = Options.custom_models_dir
            model_name = "ipcam-general" 

        module_runner.log(LogMethod.Info | LogMethod.Server,
                         { 
                             "filename": "detect_adapter.py",
                             "loglevel": "information",
                             "method": "object_detect_callback",
                             "message": f"Detecting using {model_name}"
                         })

        use_mX_GPU = False # Options.use_MPS   - Custom models don't currently work with pyTorch on MPS
        response = do_detection(module_runner, model_dir, model_name, 
                                Options.resolution_pixels, Options.use_CUDA,
                                Options.cuda_device_num, use_mX_GPU,
                                Options.half_precision, img, threshold)

    else:
        module_runner.log(LogMethod.Info | LogMethod.Server,
                         { 
                             "filename": "detect_adapter.py",
                             "loglevel": "error",
                             "method": "object_detect_callback",
                             "message": f"Unknown command {data.command}"
                         })

    return response


def list_models(module_runner, models_path):

    """
    Lists the custom models we have in the assets folder. This ignores the 
    yolo* files.
    """

    global model_names
    global models_last_checked

    # We'll only refresh the list of models at most once a minute
    if models_last_checked is None or (time.time() - models_last_checked) >= 60:
        model_names = [entry.name[:-3] for entry in os.scandir(models_path)
                                           if (entry.is_file()
                                           and entry.name.endswith(".pt")
                                           and not entry.name.startswith("yolov5"))]
        models_last_checked = time.time()

    return { "success": True, "models": model_names }


def get_detector(module_runner, models_dir: str, model_name: str, resolution: int,
                 use_Cuda: bool, cuda_device_num: int, use_MPS: bool,
                 half_precision: str) -> any:

    """
    We have a detector for each custom model. Lookup the detector, or if it's 
    not found, create a new one and add it to our lookup.
    """

    detector = detectors.get(model_name, None)
    if detector is None:
        with models_lock:
            detector = detectors.get(model_name, None)
            if detector is None:
                model_path = os.path.join(models_dir, model_name + ".pt")

                # YOLOv5 will check for the existence of files and attempt to 
                # download missing files from the cloud. Let's not do that: it's
                # unexpected, can fail if no internet, and slows things down if
                # a system is constantly asking for a model that simply doens't
                # exist. Set things up correctly at install time.
                if exists(model_path):
                    try:
                        detector = YOLODetector(model_path, resolution, 
                                                use_Cuda, cuda_device_num,
                                                use_MPS, half_precision)
                        detectors[model_name] = detector

                        module_runner.log(LogMethod.Server,
                                            { 
                                                "filename": "detect_adapter.py",
                                                "method": "do_detection",
                                                "loglevel": "debug",
                                                "message": f"Model Path is {model_path}"
                                            })
                    except Exception as ex:
                        module_runner.log(LogMethod.Server,
                                        { 
                                            "filename": "detect_adapter.py",
                                            "method": "do_detection",
                                            "loglevel": "error",
                                            "message": f"Unable to load model at {model_path} ({str(ex)})"
                                        })
                        detector = None

                else:
                    module_runner.log(LogMethod.Server,
                                    { 
                                        "filename": "detect_adapter.py",
                                        "method": "do_detection",
                                        "loglevel": "error",
                                        "message": f"{model_path} does not exist"
                                    })

    return detector

def do_detection(module_runner, models_dir: str, model_name: str, resolution: int,
                 use_Cuda: bool, cuda_device_num: int, use_MPS: bool,
                 half_precision: str, img: any, threshold: float):
    
    # We have a detector for each custom model. Lookup the detector, or if it's
    # not found, create a new one and add it to our lookup.

    create_err_msg = f"Unable to create YOLO detector for model {model_name}"

    try:
        detector = get_detector(module_runner, models_dir, model_name,
                                resolution, use_Cuda, cuda_device_num, use_MPS,
                                half_precision)
    except Exception as ex:
        create_err_msg = f"{create_err_msg} ({str(ex)})"

    if detector is None:
        module_runner.log(LogMethod.Error | LogMethod.Server,
                            { 
                                "filename": "detect_adapter.py",
                                "method":   "do_detection",
                                "loglevel": "error",
                                "message":   create_err_msg
                            })
        return { "success": False, "error": create_err_msg, "code": 500 }
    
    # We have a detector for this model, so let's go ahead and detect
    try:
        det = detector.predictFromImage(img, threshold)

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

        return {"success": True, "predictions": outputs}

    except UnidentifiedImageError:

        err_trace = traceback.format_exc()
        message = err_trace or "The image provided was of an unknown type"
        module_runner.log(LogMethod.Error | LogMethod.Server,
                          {
                             "filename": "detect_adapter.py",
                             "method": "do_detection",
                             "loglevel": "error",
                             "message": message,
                             "exception_type": "UnidentifiedImageError"
                          })

        return { "success": False, "error": "invalid image file", "code": 400 }

    except Exception as ex:

        # err_trace = traceback.format_exc()
        message = str(ex) or f"A {ex.__class__.__name__} error occurred"
        module_runner.log(LogMethod.Error | LogMethod.Server,
                          { 
                              "filename": "detect_adapter.py",
                              "method": "do_detection",
                              "loglevel": "error",
                              "message": message,
                              "exception_type": "Exception"
                          })

        return { "success": False, "error": "Error occured on the server", "code": 500 }


if __name__ == "__main__":
    main()