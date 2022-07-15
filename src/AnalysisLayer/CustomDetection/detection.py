import sys
import os
import json

from options import Options

sys.path.append("../SDK/Python")
from CodeProjectAI import ModuleWrapper, LogMethod # will also set the python packages path correctly

module = ModuleWrapper("customdetection_queue")

# Hack for debug mode
if module.moduleId == "CodeProject.AI":
    module.moduleId = "CustomObjectDetection";

if Options.use_CUDA:
    module.hardwareId        = "GPU"
    module.executionProvider = "CUDA"

sys.path.insert(0, os.path.join(os.path.dirname(os.path.realpath(__file__)), "."))
# TBD
# sys.path.append(os.path.join(Options.app_dir, Options.settings.site_package_dir))

import traceback

from PIL import UnidentifiedImageError
from process import YOLODetector

models_dir = Options.models_dir
   
model_path = os.path.join(models_dir, Options.model_name)
detectors = {
    'ipcam-general' : YOLODetector(model_path, reso=Options.resolution_pizels, cuda=Options.use_CUDA)
}

def customObjectDetection():

    module.log(LogMethod.Info|LogMethod.Server, {"message": f"Custom Models in {models_dir}"})

    while True:

        queue = module.get_command();

        if len(queue) > 0:

            for req_data in queue:
                req_data = json.JSONDecoder().decode(req_data)

                req_id    = req_data.get("requestId", None)
                if req_id is None or req_id == "":
                    req_id = req_data["reqid"]

                payload    = req_data["payload"]
                segments   = payload.get("urlSegments", None)
                command    = payload.get("command", None)

                if command == "list":           # list all models available
                    list_models(req_id, models_dir)

                else:                           # detect using the given model name
                    threshold  = float(module.get_request_value(req_data, "min_confidence", "0.4"))
                    img        = module.get_image_from_request(req_data, 0)

                    # The route to here is /v1/vision/custom/<model-name>. if mode-name = general,
                    # or no model provided, then a built-in general purpose mode will be used.
                    if segments is None or len(segments) == 0 or segments[0] is None or segments[0] == "":
                        model_name = "general"
                    else:
                        model_name = segments[0]

                    # Mhe "general" model to our current "general" model (currently a trimmed down
                    # YOLOv5 with a reduced model set specifically for webcam applications)
                    if model_name == "general":
                        model_name = "ipcam-general"

                    do_detection(req_id, model_name, img, threshold)


def list_models(req_id, models_path):
    model_names = [entry.name[:-3] for entry in os.scandir(models_path) if entry.is_file() and entry.name.endswith(".pt") ]
    output = { "success": True, "models": model_names }
    module.send_response(req_id, json.dumps(output))

def do_detection(req_id, model_name, img, threshold):
    
    module.log(LogMethod.Info | LogMethod.Cloud | LogMethod.Server,
                { "process": "customObjectDetection", 
                  "file": "detection.py",
                  "method": "do_detection",
                  "message": f"Detecting using {model_name}"})

    # We have a detector for each custom model. Lookup the detector, or if it's not
    # found, create a new one and add it to our lookup.
    detector = detectors.get(model_name, None)
    if detector is None:
        model_path = os.path.join(models_dir, model_name + ".pt")
        print(f"Model Path is {model_path}")

        try:
            detector = YOLODetector(model_path, reso=Options.resolution_pizels, cuda=Options.use_CUDA)
            detectors[model_name] = detector
        except Exception as ex:
            module.log(LogMethod.Error | LogMethod.Cloud | LogMethod.Server,
                { "process": "customObjectDetection", 
                  "file": "detection.py",
                  "method": "do_detection",
                  "message": ex})
            return
    
    timer = module.start_timer(f"Custom Object Detection:{model_name}")

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

        output = {"success": True, "predictions": outputs}

    except UnidentifiedImageError:
        err_trace = traceback.format_exc()

        output = {
            "success": False,
            "error":   "invalid image file",
            "code":    400,
        }

        module.log(LogMethod.Error | LogMethod.Cloud | LogMethod.Server,
                    { "process": "customObjectDetection", 
                        "file": "detection.py",
                        "method": "customObjectDetection",
                        "message": err_trace, 
                        "exception_type": "UnidentifiedImageError"})

    except Exception:

        err_trace = traceback.format_exc()

        output = {
            "success": False,
            "error":   "error occured on the server",
            "code":    500,
        }

        module.log(LogMethod.Error | LogMethod.Cloud | LogMethod.Server,
                    { "process": "customObjectDetection", 
                        "file": "detection.py",
                        "method": "customObjectDetection",
                        "message": err_trace, 
                        "exception_type": "Exception"})

    finally:
        module.end_timer(timer)
        module.send_response(req_id, json.dumps(output))


if __name__ == "__main__":
    module.log(LogMethod.Info | LogMethod.Server, {"message": "Custom Object Detection module started."})
    customObjectDetection()