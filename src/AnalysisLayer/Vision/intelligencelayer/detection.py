import sys
sys.path.append("../../SDK/Python")
from CodeProjectAI import ModuleWrapper, LogMethod # will also set the python packages path correctly
module = ModuleWrapper()

# Hack for debug mode
if module.moduleId == "CodeProject.AI":
    module.moduleId = "VisionObjectDetection";


import os
import json
import threading

from shared import SharedOptions

if SharedOptions.CUDA_MODE:
    module.hardwareId = "GPU"
    module.executionProvider = "CUDA"

sys.path.insert(0, os.path.join(os.path.dirname(os.path.realpath(__file__)), "."))
sys.path.append(os.path.join(SharedOptions.APPDIR, SharedOptions.SETTINGS.PLATFORM_PKGS))

import argparse
import traceback

from PIL import UnidentifiedImageError
from process import YOLODetector

parser = argparse.ArgumentParser()
parser.add_argument("--model", type=str, default=None)
parser.add_argument("--name", type=str, default=None)

opt = parser.parse_args()

MODE           = SharedOptions.MODE
SHARED_APP_DIR = SharedOptions.SHARED_APP_DIR
CUDA_MODE      = SharedOptions.CUDA_MODE
TEMP_PATH      = SharedOptions.TEMP_PATH

if opt.name == None:
    IMAGE_QUEUE = "detection_queue"
else:
    IMAGE_QUEUE = opt.name + "_queue"

if opt.model == None:
    model_path = os.path.join(
        SHARED_APP_DIR, SharedOptions.SETTINGS.DETECTION_MODEL
    )
else:
    model_path = opt.model
#print(f"Model Path is {model_path}")

reso = SharedOptions.SETTINGS.DETECTION_MEDIUM
if MODE == "High":
    reso = SharedOptions.SETTINGS.DETECTION_HIGH
elif MODE == "Medium":
    reso = SharedOptions.SETTINGS.DETECTION_MEDIUM
elif MODE == "Low":
    reso = SharedOptions.SETTINGS.DETECTION_LOW

detector = YOLODetector(model_path, reso, cuda=CUDA_MODE)

def objectdetection(thread_name: str, delay: float):

    while True:

        queue = module.get_command(IMAGE_QUEUE);

        if len(queue) > 0:

            for req_data in queue:
                timer    = module.start_timer("Object Detection")
                req_data = json.JSONDecoder().decode(req_data)

                #img_id    = req_data["imgid"]
                req_id    = req_data["reqid"]
                req_type  = req_data["reqtype"]
                threshold = float(module.get_request_value(req_data, "min_confidence", "0.4"))
                #img_path  = os.path.join(TEMP_PATH, img_id)

                try:
                    img = module.get_image_from_request(req_data, 0)
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
                               { "process": "objectdetection", 
                                 "file": "detection.py",
                                 "method": "objectdetection",
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
                               { "process": "objectdetection", 
                                 "file": "detection.py",
                                 "method": "objectdetection",
                                 "message": err_trace, 
                                 "exception_type": "Exception"})

                finally:
                    module.end_timer(timer)
                    module.send_response(req_id, json.dumps(output))

        # time.sleep(delay)

if __name__ == "__main__":
    module.log(LogMethod.Info | LogMethod.Server, {"message": "Object Detection module started."})
    objectdetection("", SharedOptions.SLEEP_TIME)

    # for x in range(1, 4):
    #     thread = threading.Thread(None, objectdetection, args = ("", SharedOptions.SLEEP_TIME))
    #     thread.start();
    # 
    # thread.join();
