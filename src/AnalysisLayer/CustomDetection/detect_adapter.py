# Import our general libraries
import os
import sys
import traceback
import time

# Import the CodeProject.AI SDK. This will add to the PATH vaar for future imports
sys.path.append("../SDK/Python")
from common import JSON, shorten
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
model_names = []  # We'll use this to cache the available model names
models_dir  = Options.models_dir
model_path  = os.path.join(models_dir, Options.model_name)

detectors = {
    'ipcam-general' : YOLODetector(model_path, reso=Options.resolution_pizels, cuda=Options.use_CUDA)
}

def main():

    # create a CodeProject.AI module object
    module_runner = CodeProjectAIRunner("customdetection_queue")
    module_runner.log(LogMethod.Info | LogMethod.Server, {
      "message": f"Custom Models in {shorten(models_dir,50)}",
      "loglevel": "debug"
    })

    # Hack for debug mode
    if module_runner.module_id == "CodeProject.AI":
        module_runner.module_id   = "CustomObjectDetection"
        module_runner.module_name = "Custom Object Detection"

    if Options.use_CUDA:
        module_runner.execution_provider = "CUDA"

    # Start the module
    module_runner.start_loop(custom_detect_callback)


def custom_detect_callback(module_runner: CodeProjectAIRunner, data: AIRequestData) -> JSON:
    
    if data.command == "list":                      # list all models available
        response = list_models(module_runner, models_dir)

    elif data.command == "detect":                  # list all models available

        threshold: float  = float(data.get_value("min_confidence", "0.4"))
        img: Image        = data.get_image(0)

        # The route to here is /v1/vision/custom/<model-name>. if mode-name = general,
        # or no model provided, then a built-in general purpose mode will be used.
        if not data.segments or not data.segments[0]:
            model_name = "general"
        else:
            model_name = data.segments[0]

        # Map the "general" model to our current "general" model (currently a 
        # trimmed down YOLOv5 with a reduced model set specifically for webcam 
        # applications)
        if model_name == "general":
            model_name = "ipcam-general"

        module_runner.log(LogMethod.Info | LogMethod.Cloud | LogMethod.Server,
                         { 
                             "filename": "detect_adapter.py",
                             "loglevel": "information",
                             "method": "custom_detect_callback",
                             "message": f"Detecting using {model_name}"
                         })

        response = do_detection(module_runner, model_name, img, threshold)

    else:
        module_runner.log(LogMethod.Info | LogMethod.Cloud | LogMethod.Server,
                         { 
                             "filename": "detect_adapter.py",
                             "loglevel": "error",
                             "method": "custom_detect_callback",
                             "message": f"Unknown command {data.command}"
                         })

    return response


def list_models(module_runner, models_path):

    global model_names
    global models_last_checked

    # We'll only refresh the list of models at most once a minute
    if models_last_checked is None or (time.time() - models_last_checked) >= 60:
        model_names = [entry.name[:-3] for entry in os.scandir(models_path) if (entry.is_file() and 
                                                                            entry.name.endswith(".pt")) ]
        models_last_checked = time.time()

    return { "success": True, "models": model_names }

def get_detector(module_runner, model_name):
    # We have a detector for each custom model. Lookup the detector, or if it's not
    # found, create a new one and add it to our lookup.
    detector = detectors.get(model_name, None)
    if detector is None:
        with models_lock:
            detector = detectors.get(model_name, None)
            if detector is None:
                model_path = os.path.join(models_dir, model_name + ".pt")
                module_runner.log(LogMethod.Server,
                                    { 
                                        "filename": "detect_adapter.py",
                                        "method": "do_detection",
                                        "loglevel": "debug",
                                        "message": f"Model Path is {model_path}"
                                    })

                detector = YOLODetector(model_path, reso=Options.resolution_pizels, cuda=Options.use_CUDA)
                detectors[model_name] = detector
    return detector

def do_detection(module_runner, model_name, img, threshold):
    
    # We have a detector for each custom model. Lookup the detector, or if it's not
    # found, create a new one and add it to our lookup.
    try:
        detector = get_detector(module_runner, model_name)

    except Exception as ex:
        module_runner.log(LogMethod.Error | LogMethod.Cloud | LogMethod.Server,
                            { 
                                "filename": "detect_adapter.py",
                                "method": "do_detection",
                                "loglevel": "error",
                                "message": ex
                            })

        output = {
            "success": False,
            "error":   f"Unable to create YOLO detector for model {model_name}",
            "code":    500,
        }

        return output
    
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
        module_runner.log(LogMethod.Error | LogMethod.Cloud | LogMethod.Server,
                          {
                             "filename": "detect_adapter.py",
                             "method": "do_detection",
                             "loglevel": "error",
                             "message": err_trace, 
                             "exception_type": "UnidentifiedImageError"
                          })

        return { "success": False, "error": "invalid image file", "code": 400 }

    except Exception:

        err_trace = traceback.format_exc()
        module_runner.log(LogMethod.Error | LogMethod.Cloud | LogMethod.Server,
                          { 
                              "filename": "detect_adapter.py",
                              "method": "do_detection",
                              "loglevel": "error",
                              "message": err_trace, 
                              "exception_type": "Exception"
                          })

        return { "success": False, "error": "Error occured on the server", "code": 500 }


if __name__ == "__main__":
    #main()

    from threading import Thread
    nThreads = os.cpu_count() -1
    theThreads = []
    for i in range(nThreads):
        t = Thread(target=main)
        theThreads.append(t)
        t.start()

    for x in theThreads:
        x.join()

