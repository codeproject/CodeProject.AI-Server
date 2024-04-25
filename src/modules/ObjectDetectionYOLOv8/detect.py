import os
from os.path import exists
import sys
import time
from threading import Lock

import torch
import numpy as np
from PIL import UnidentifiedImageError

from ultralytics import YOLO

from module_logging import LogMethod


# Setup a global bucket of YOLO detectors. One for each model
detectors   = {}  # We'll use this to cache the detectors based on models
models_lock  = Lock()
predict_lock = Lock()

def get_detector(module_runner, models_dir: str, model_name: str, resolution: int,
                 use_Cuda: bool, accel_device_name: int, use_MPS: bool,
                 use_DirectML: bool, half_precision: str) -> any:

    """
    We have a detector for each custom model. Lookup the detector, or if it's 
    not found, create a new one and add it to our lookup.
    """

    detector = detectors.get(model_name, None)
    if detector is None:
        with models_lock:
            detector = detectors.get(model_name, None)
            half     = False

            if detector is None:
                model_path = os.path.join(models_dir, model_name + ".pt")

                if use_Cuda:
                    device_type = "cuda"
                    if accel_device_name:
                        device = torch.device(accel_device_name)
                    else:
                        device = torch.device("cuda")
                    device_name = torch.cuda.get_device_name(device)

                    print(f"GPU compute capability is {torch.cuda.get_device_capability()[0]}.{torch.cuda.get_device_capability()[1]}")

                    # Use half-precision if possible. There's a bunch of NVIDIA cards where
                    # this won't work
                    half = half_precision != 'disable'
                    if half:
                        print(f"Using half-precision for the device '{device_name}'")
                    else:
                        print(f"Not using half-precision for the device '{device_name}'")
                
                elif use_MPS:
                    device_type = "mps"
                    device_name = "Apple Silicon GPU"
                    device      = torch.device(device_type)

                elif use_DirectML:
                    device_type = "cpu"
                    device_name = "DirectML"                    
                    # Torch-DirectlML throws "Cannot set version_counter for inference tensor"
                    import torch_directml
                    device = torch_directml.device()

                else:
                    device_type = "cpu"
                    device_name = "CPU"
                    device = torch.device(device_type)

                print(f"Inference processing will occur on device '{device_name}'")
      
                # YOLOv5 will check for the existence of files and attempt to download
                # missing files from the cloud. Let's not do that: it's unexpected, 
                # can fail if no internet, and slows things down if a system is constantly
                # asking for a model that simply doesn't exist. Ensure we set things up
                # correctly at install time.
                if exists(model_path):
                    try:
                        # From then old YOLOv5 6.2 code.
                        #
                        # this will throw an exception when an old YoloV5 model
                        # is loaded and it does not have 80 classes. This exception
                        # is handled in the YoloV5 code: Ignore the exception.
                        #
                        # We're not using the hub.load as it will attempt to load 
                        # packages and weights from the Internet. We can't create the
                        # DetectionModel directly easily so leveraging the 
                        # DetectMultiBackend class. The magic sauce is to wrap that
                        # in AutoShape as that does the pre and post processing. 
                        # detector = DetectMultiBackend(model_path, device=device, fp16=half)
                        # detector = AutoShape(detector)

                        # New YOLOv8 method
                        detector = YOLO(model_path)

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

def do_detection(module_runner, models_dir: str, model_name: str, resolution: int,
                 use_Cuda: bool, accel_device_name: int, use_MPS: bool,
                 use_DirectML: bool, half_precision: str, img: any, threshold: float,
                 do_segmentation: bool = False):
    
    # We have a detector for each custom model. Lookup the detector, or if it's
    # not found, create a new one and add it to our lookup.

    create_err_msg = f"Unable to create YOLO detector for model {model_name}"

    start_process_time = time.perf_counter()

    detector = None
    try:
        detector = get_detector(module_runner, models_dir, model_name,
                                resolution, use_Cuda, accel_device_name, use_MPS,
                                use_DirectML, half_precision)
    except Exception as ex:
        create_err_msg = f"{create_err_msg} ({str(ex)})"

    if detector is None:
        module_runner.report_error(None, __file__, create_err_msg)
        return { "success": False, "error": create_err_msg }
    
    # We have a detector for this model, so let's go ahead and detect
    try:
        start_inference_time = time.perf_counter()

        use_half_precision = half_precision == "true"
        results = None
        with predict_lock:
            results = detector.predict(img, imgsz=resolution, half=use_half_precision,
                                                  device=accel_device_name)
        inferenceMs = int((time.perf_counter() - start_inference_time) * 1000)

        outputs = []

        # Process results list
        for result in results:
            
            boxes     = result.boxes        # Boxes object for bbox outputs
            if do_segmentation:
                masks   = result.masks        # Masks object for segmentation masks outputs
            # keypoints = result.keypoints    # Keypoints object for pose outputs
            # probs     = result.probs        # Probs object for classification outputs

            # for *xyxy, conf, cls in reversed(boxes):
            # for xyxy, conf, cls in boxes:
            for i in range(len(boxes.conf)):
                score = boxes.conf[i].item()

                if do_segmentation:
                    mask   = masks[i]
                    points = np.int32([mask.xy])

                if score >= threshold:
                    x_min = boxes.xyxy[i][0].item()
                    y_min = boxes.xyxy[i][1].item()
                    x_max = boxes.xyxy[i][2].item()
                    y_max = boxes.xyxy[i][3].item()

                    label = detector.names[int(boxes.cls[i].item())]

                    detection = {
                        "confidence": score,
                        "label": label,
                        "x_min": int(x_min),
                        "y_min": int(y_min),
                        "x_max": int(x_max),
                        "y_max": int(y_max),
                    }

                    if do_segmentation:
                        detection["mask"] = points.tolist()[0][0]
                        
                    outputs.append(detection)

        if len(outputs) > 3:
            message = 'Found ' + (', '.join(prediction["label"] for prediction in outputs[0:3])) + "..."
        elif len(outputs) > 0:
            message = 'Found ' + (', '.join(prediction["label"] for prediction in outputs))
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

