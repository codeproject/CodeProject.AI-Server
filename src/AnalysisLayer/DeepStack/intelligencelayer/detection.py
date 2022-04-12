import sys
import os
import json
import threading

from senseAI import SenseAIBackend # will also set the python packages path correctly
senseAI = SenseAIBackend()

from shared import SharedOptions

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

        queue = senseAI.getCommand(IMAGE_QUEUE);

        if len(queue) > 0:

            for req_data in queue:
                timer    = senseAI.startTimer("Object Detection")
                req_data = json.JSONDecoder().decode(req_data)

                #img_id    = req_data["imgid"]
                req_id    = req_data["reqid"]
                req_type  = req_data["reqtype"]
                threshold = float(req_data.get("min_confidence", "0.4"))
                #img_path  = os.path.join(TEMP_PATH, img_id)

                try:
                    img = senseAI.getImageFromRequest(req_data, 0)
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
                    senseAI.log(err_trace, is_error=True)

                    output = {
                        "success": False,
                        "error":   "invalid image file",
                        "code":    400,
                    }
                    senseAI.errLog("objectdetection", "detection.py", err_trace, "UnidentifiedImageError")

                except Exception:

                    err_trace = traceback.format_exc()
                    senseAI.log(err_trace, is_error=True)

                    output = {
                        "success": False,
                        "error":   "error occured on the server",
                        "code":    500,
                    }

                    senseAI.errLog("objectdetection", "detection.py", err_trace, "Exception")

                finally:
                    senseAI.endTimer(timer)
                    senseAI.sendResponse(req_id, json.dumps(output))

        # time.sleep(delay)

if __name__ == "__main__":
    senseAI.log("Object Detection module started.")
    objectdetection("", SharedOptions.SLEEP_TIME)
    # TODO: Send back a "I'm alive" message to the backend of the API server so it can report to the user

    # for x in range(1, 4):
    #     thread = threading.Thread(None, objectdetection, args = ("", SharedOptions.SLEEP_TIME))
    #     thread.start();
    # 
    # thread.join();
