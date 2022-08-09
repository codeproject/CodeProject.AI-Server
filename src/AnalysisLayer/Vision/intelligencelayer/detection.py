# Import our general libraries
import os
import sys
import traceback
import argparse

# Import the CodeProject.AI SDK. This will add to the PATH vaar for future imports
sys.path.append("../../SDK/Python")
from common import JSON
from codeprojectai import CodeProjectAIRunner
from requestdata import AIRequestData
from analysislogging import LogMethod

# Deepstack settings
from shared import SharedOptions

# Set the path based on Deepstack's settings so CPU / GPU packages can be correctly loaded
sys.path.insert(0, os.path.join(os.path.dirname(os.path.realpath(__file__)), "."))
sys.path.append(os.path.join(SharedOptions.APPDIR, SharedOptions.SETTINGS.PLATFORM_PKGS))

# Import libraries from the Python VENV using the correct packages dir
from PIL import UnidentifiedImageError, Image

# Deepstack detector wrapper
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
    model_path = os.path.join(SHARED_APP_DIR, SharedOptions.SETTINGS.DETECTION_MODEL)
else:
    model_path = opt.model

reso = SharedOptions.SETTINGS.DETECTION_MEDIUM
if MODE == "High":
    reso = SharedOptions.SETTINGS.DETECTION_HIGH
elif MODE == "Medium":
    reso = SharedOptions.SETTINGS.DETECTION_MEDIUM
elif MODE == "Low":
    reso = SharedOptions.SETTINGS.DETECTION_LOW

detector = YOLODetector(model_path, reso, cuda=CUDA_MODE)


def main():

    # create a CodeProject.AI module object
    module_runner = CodeProjectAIRunner(IMAGE_QUEUE)

    # Hack for debug mode
    if module_runner.module_id == "CodeProject.AI":
        module_runner.module_id   = "VisionObjectDetection"
        module_runner.module_name = "Vision Object Detection"

    if SharedOptions.CUDA_MODE:
        module_runner.hardware_id        = "GPU"
        module_runner.execution_provider = "CUDA"

    # Start the module
    module_runner.start_loop(objectdetection_callback)


def objectdetection_callback(module_runner: CodeProjectAIRunner, data: AIRequestData) -> JSON:
    
    threshold: float  = float(data.get_value("min_confidence", "0.4"))
    img: Image        = data.get_image(0)

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

