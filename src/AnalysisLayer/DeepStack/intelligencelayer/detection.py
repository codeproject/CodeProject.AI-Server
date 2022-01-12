import _thread as thread
import ast
import io
import json
import os
import sqlite3
import sys
import time
import warnings

sys.path.insert(0, os.path.join(os.path.dirname(os.path.realpath(__file__)), "."))

from shared import SharedOptions, FrontendClient

# TODO: Currently doesn't exist. The Python venv is setup at install time for a single platform in
# order to reduce downloads. Having the ability to switch profiles at runtime will be added, but
# will increase downloads. Lazy loading will help, somewhat, and the infrastructure is already in
# place, though it needs to be adjusted.
sys.path.append(os.path.join(SharedOptions.APPDIR, SharedOptions.SETTINGS.PLATFORM_PKGS))

import numpy as np
import torch
import torch.nn.functional as F
from PIL import Image, UnidentifiedImageError

import argparse
import traceback

import torchvision.transforms as transforms
from PIL import UnidentifiedImageError
from process import YOLODetector

parser = argparse.ArgumentParser()
parser.add_argument("--model", type=str, default=None)
parser.add_argument("--name", type=str, default=None)

opt = parser.parse_args()

frontendClient = FrontendClient()

def objectdetection(thread_name: str, delay: float):

    MODE           = SharedOptions.MODE
    SHARED_APP_DIR = SharedOptions.SHARED_APP_DIR
    CUDA_MODE      = SharedOptions.CUDA_MODE
    # db           = SharedOptions.db
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

    if MODE == "High":

        reso = SharedOptions.SETTINGS.DETECTION_HIGH

    elif MODE == "Medium":

        reso = SharedOptions.SETTINGS.DETECTION_MEDIUM

    elif MODE == "Low":

        reso = SharedOptions.SETTINGS.DETECTION_LOW

    detector = YOLODetector(model_path, reso, cuda=CUDA_MODE)
    while True:

        queue = frontendClient.getCommand(IMAGE_QUEUE);

        if len(queue) > 0:

            for req_data in queue:
                timer    = frontendClient.startTimer("Object Detection")
                req_data = json.JSONDecoder().decode(req_data)

                img_id    = req_data["imgid"]
                req_id    = req_data["reqid"]
                req_type  = req_data["reqtype"]
                threshold = float(req_data["minconfidence"])
                img_path  = os.path.join(TEMP_PATH, img_id)

                try:
                    det     = detector.predict(img_path, threshold)

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
                    frontendClient.log(err_trace, is_error=True)

                    output = {
                        "success": False,
                        "error":   "invalid image file",
                        "code":    400,
                    }

                except Exception:

                    err_trace = traceback.format_exc()
                    frontendClient.log(err_trace, is_error=True)

                    output = {
                        "success": False,
                        "error":   "error occured on the server",
                        "code":    500,
                    }

                finally:
                    frontendClient.endTimer(timer)
                    frontendClient.sendResponse(req_id, json.dumps(output))

                    # the image file deletion should, and is, being
                    # done at the front end.
                    if os.path.exists(img_path):
                        os.remove(img_path)

        # time.sleep(delay)

if __name__ == "__main__":
    frontendClient.log("Object Detection module started.")
    objectdetection("", SharedOptions.SLEEP_TIME)
    # TODO: Send back a "I'm alive" message to the backend of the API server so it can report to the user

