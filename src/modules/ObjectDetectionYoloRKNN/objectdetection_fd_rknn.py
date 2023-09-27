"""
    Copyright (c) 2022 PaddlePaddle Authors. All Rights Reserved.

    Licensed under the Apache License, Version 2.0 (the "License");
    you may not use this file except in compliance with the License.
    You may obtain a copy of the License at

    http://www.apache.org/licenses/LICENSE-2.0

    Unless required by applicable law or agreed to in writing, software
    distributed under the License is distributed on an "AS IS" BASIS,
    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
    See the License for the specific language governing permissions and
    limitations under the License.
"""

import os
from os.path import exists
import sys
import time
from threading import Lock

from numpy import array
from PIL import UnidentifiedImageError

from module_logging import LogMethod
from options import Options

# import fastdeploy as fd # rknn
from fastdeploy import RuntimeOption, vision, ModelFormat
from utils.tools import resize_image, convert_bounding_boxes, count_labels, extract_label_from_file

# Setup a global bucket of YOLO detectors. One for each model
detectors   = {}  # We'll use this to cache the detectors based on models
models_lock = Lock()
max_size    = None


def init_detect(opts: Options) -> None:

    global max_size
    max_size = opts.resolution

def get_detector(module_runner, models_dir: str, model_name: str) -> any:

    """
    We have a detector for each custom model. Lookup the detector, or if it's 
    not found, create a new one and add it to our lookup.
    """

    detector = detectors.get(model_name, None)

    if detector is None:
        
        with models_lock:
            detector = detectors.get(model_name, None)

            if detector is None:
                
                model_path = os.path.join(models_dir, model_name + ".rknn")
                label_path = os.path.join(models_dir, model_name + ".txt")

                if exists(model_path):
                    
                    try:
                        runtime_option = RuntimeOption()
                        runtime_option.use_rknpu2()
                                                
                        detector = vision.detection.RKYOLOV5(model_path,
                                                             runtime_option=runtime_option,
                                                             model_format=ModelFormat.RKNN)
                        
                        detector.postprocessor.class_num = count_labels(label_path)

                        detectors[model_name] = detector

                        module_runner.log(LogMethod.Server,
                        { 
                            "filename": __file__,
                            "method": sys._getframe().f_code.co_name,
                            "loglevel": "debug",
                            "message": f"Model Path is {model_path}"
                        })

                    except Exception as ex:
                        module_runner.report_error(ex, __file__, f"Unable to load model at {model_path} ({str(ex)})")
                        detector = None

                else:
                    module_runner.report_error(None, __file__, f"{model_path} does not exist")

    return detector


def do_detect(module_runner, models_dir, model_name, img: any, score_threshold: float):# rknn

    create_err_msg = f"Unable to create YOLO detector for model {model_name}"

    start_process_time = time.perf_counter()

    try:
        detector = get_detector(module_runner, models_dir, model_name)

    except Exception as ex:
        create_err_msg = f"{create_err_msg} ({str(ex)})"

    if detector is None:
        module_runner.report_error(None, __file__, create_err_msg)
        return { "success": False, "error": create_err_msg }

    # We have a detector for this model, so let's go ahead and detect
    try:
    
        # Predicting Image Results
        im = array(img)
        
        # Resize the image to a maximum size of 640
        resized_image, x_scaling_factor, y_scaling_factor = resize_image(im, max_size)

        start_inference_time = time.perf_counter()
        result = detector.predict(resized_image, conf_threshold=score_threshold, nms_iou_threshold=0.45)
        inferenceMs = int((time.perf_counter() - start_inference_time) * 1000)

        result = str(result)
        lines = result.strip().split("\n")

        label_path = os.path.join(models_dir, model_name + ".txt")
        outputs = []

        for line in lines[1:]:
            # Split the line by comma to get a list of values
            values = line.split(",")
            values = [x.strip(' ') for x in values]

            box = values[0], values[1], values[2], values[3]
            
            box = convert_bounding_boxes(box, x_scaling_factor, y_scaling_factor)
            
            # Convert the values to appropriate data types
            xmin        = int(float(box[0]))
            ymin        = int(float(box[1]))
            xmax        = int(float(box[2]))
            ymax        = int(float(box[3]))
            score       = float(values[4])
            label_id    = int(values[5])
            label       = str(extract_label_from_file(label_id, label_path))

            detection = {
                "confidence": score,
                "label": label,
                "x_min": xmin,
                "y_min": ymin,
                "x_max": xmax,
                "y_max": ymax,
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
            "processMs"   : int((time.perf_counter() - start_process_time) * 1000),
            "inferenceMs" : inferenceMs
        }

    except UnidentifiedImageError as img_ex:
        module_runner.report_error(img_ex, __file__, "The image provided was of an unknown type")
        return { "success": False, "error": "invalid image file"}

    except Exception as ex:
        module_runner.report_error(ex, __file__)
        return { "success": False, "error": "Error occurred on the server" }
