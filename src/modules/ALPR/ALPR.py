# Import our general libraries
import io
import math
import re
import time
import traceback
from typing import Tuple
import utils.tools as tool

from analysis.codeprojectai import CodeProjectAIRunner
from analysis.analysislogging import LogMethod
from common import JSON

from PIL import Image

from options import Options
opts = Options()

if opts.use_OpenCV:
    import cv2
    import numpy as np

from paddleocr import PaddleOCR
ocr = PaddleOCR(lang                = opts.language,
                use_gpu             = opts.use_GPU,
                show_log            = opts.showLog,
                det_db_unclip_ratio = opts.det_db_unclip_ratio,
                det_db_box_thresh   = opts.box_detect_threshold,
                drop_score          = opts.char_detect_threshold,
                rec_algorithm       = opts.algorithm,
                cls_model_dir       = opts.cls_model_dir,
                det_model_dir       = opts.det_model_dir,
                rec_model_dir       = opts.rec_model_dir)

no_plate_found = 'Characters Not Found'


async def detect_platenumber(module_runner: CodeProjectAIRunner, image: Image) -> JSON:

    """
    Performs the plate number detection
    Returns a tuple containing the Json description of what was found, along 
    """

    outputs      = []
    pillow_image = image

    inferenceMs: int = 0

    # Convert to format suitable for a POST
    with io.BytesIO() as image_buffer :
        pillow_image.save(image_buffer, format='JPEG') # 'PNG' - slow
        image_buffer.seek(0)
    
        # Look for plates
        try:
            # image_data = ('image.png', image_buffer, 'image/png')
            image_data = ('image.jpeg', image_buffer, 'image/jpeg')

            start_time = time.perf_counter()
            detect_plate_response = await module_runner.call_api("vision/custom/license-plate",
                                                                 files={ "image": image_data },
                                                                 data={"min_confidence": opts.plate_confidence})
            inferenceMs += int((time.perf_counter() - start_time) * 1000)

            if not detect_plate_response["success"]:
                return { "error": detect_plate_response["error"], "inferenceMs": inferenceMs }

            # Note: we will only get plates that have at least opts.plate_confidence 
            # confidence. 
            if not detect_plate_response["predictions"] or len(detect_plate_response["predictions"]) == 0:
                return { "predictions": [], "inferenceMs": inferenceMs }

        except Exception as ex:
            # err_trace = traceback.format_exc()
            message = "".join(traceback.TracebackException.from_exception(ex).format())
            module_runner.log(LogMethod.Error | LogMethod.Server,
                              { 
                                  "filename": "alpr.py",
                                  "method": "detect",
                                  "loglevel": "error",
                                  "message": message,
                                  "exception_type": "Exception"
                              })

            return { "error": message, "inferenceMs": inferenceMs }

    # We have a plate detected, so let's prep the incoming image for some work
    if opts.use_OpenCV:
        numpy_image = np.array(pillow_image)
        numpy_image = cv2.cvtColor(numpy_image, cv2.COLOR_RGB2BGR)

    # If a plate is found we'll pass this onto OCR
    for plate_detection in detect_plate_response["predictions"]:

        label  = plate_detection["label"]
        left   = int(plate_detection["x_min"])
        top    = int(plate_detection["y_min"])
        right  = int(plate_detection["x_max"])
        bottom = int(plate_detection["y_max"])

        # Pull out just the detected plate
        (numpy_plate, pillow_plate) = (None, None)
        if opts.use_OpenCV:
            numpy_plate = numpy_image[top:bottom, left:right]
        else:
            pillow_plate = pillow_image.crop((left, top, right, bottom))

        # Possibly run it through a super-resolution module to improve readability
        # plate = numpy_plate_image if pillow_plate_image == None else pillow_plate_image
        # *_plate_image = enhance_image(plate)

        # resize image
        scale_percent = 200 # percent of original size
        if opts.use_OpenCV:
            numpy_plate = tool.resize_image(numpy_plate, scale_percent)

            if opts.auto_deskew:
                numpy_plate = tool.deskew(numpy_plate)
        else:
            new_size = ( int(pillow_plate.size[0] * scale_percent / 100), \
                         int(pillow_plate.size[1] * scale_percent / 100) )
            pillow_plate = pillow_plate.resize(new_size)
            # implement deskew for PIL images

        """ Maybe try some preprocessing
        if opts.use_OpenCV:
            # perform otsu thresh (using binary inverse since opencv contours work better with white text)
            ret, numpy_plate_image = cv2.threshold(numpy_plate_image, 0, 255, cv2.THRESH_OTSU | cv2.THRESH_BINARY_INV)
            ret, numpy_plate_image = cv2.threshold(numpy_plate_image,0,255,cv2.THRESH_BINARY+cv2.THRESH_OTSU)
        """

        # Read plate
        plate = numpy_plate if pillow_plate == None else pillow_plate
        (label, confidence, plateInferenceMs) = await read_plate_chars_PaddleOCR(module_runner, plate)
        inferenceMs += plateInferenceMs

        # If we had no success reading the original plate, apply some image enhancement
        # and try again
        if label == no_plate_found:
            if opts.use_OpenCV:
                # If characters are not found try gamma correction and equalize
                # numpy_plate_image = cv2.fastNlMeansDenoisingColored(numpy_plate_image, None, 10, 10, 7, 21)
                numpy_plate = tool.gamma_correction(numpy_plate)
                # numpy_plate_image = tool.equalize(numpy_plate_image)
                # numpy_plate_image = cv2.cvtColor(numpy_plate_image, cv2.COLOR_BGR2GRAY)
                # cv2.imwrite("alpr-enhanced.jpg", numpy_plate_image)

            # Read plate, 2nd attempt
            plate = numpy_plate if pillow_plate == None else pillow_plate

            # We've only done image enhancement for OpenCV, so only retry if we're using OpenCV
            if opts.use_OpenCV:
                (label, confidence, plateInferenceMs) = await read_plate_chars_PaddleOCR(module_runner, plate)
                inferenceMs += plateInferenceMs

        if label and confidence:
            detection = {
                "confidence": confidence,
                "label": "Plate: " + label,
                "plate": label,  
                "characters": len(label),
                "x_min": left,
                "y_min": top,
                "x_max": right,
                "y_max": bottom,
            }
            outputs.append(detection)

    return { "predictions": outputs, "inferenceMs": inferenceMs }


async def read_plate_chars_PaddleOCR(module_runner: CodeProjectAIRunner, image: Image) -> Tuple[str, float, float]:

    """
    This uses PaddleOCR for reading the plates. Note that the image being passed
    in should be a tightly cropped licence plate, so we're looking for the largest
    text box and will assume that's the plate number.
    Returns (plate label, confidence, inference time (ms))
    """

    pattern  = re.compile('[\W_]+')
    inferenceTimeMs: int = 0

    try:
        if opts.use_OpenCV:
            start_time = time.perf_counter()
            ocr_response = ocr.ocr(image, cls=True)
            inferenceTimeMs = int((time.perf_counter() - start_time) * 1000)
        else:
            # Convert the image to a bytes array
            with io.BytesIO() as image_buffer:
                image.save(image_buffer, format='JPEG') # 'PNG' - slow
                img_byte_arr = image_buffer.getvalue()

                start_time = time.perf_counter()
                ocr_response = ocr.ocr(img_byte_arr, cls=True)
                inferenceTimeMs = int((time.perf_counter() - start_time) * 1000)

        # Note that ocr_response[0][0][0][0] could be a float with value 0 ('false'), or in some
        # other universe maybe it's a string. To be really careful we would have a test like
        # if hasattr(ocr_response[0][0][0][0], '__len__') and (not isinstance(ocr_response[0][0][0][0], str))
        if not ocr_response or not ocr_response[0] or not ocr_response[0][0] or not ocr_response[0][0][0]:
            return no_plate_found, 0, inferenceTimeMs

        # Seems that different versions of paddle return different structures, OR
        # paddle returns different structures depending on its mood. We're expecting
        # ocr_response = array of single set of detections
        #  -> detections = array of detection
        #    -> detection = array of bounding box, classification
        #       -> bounding box = array of [x,y], classification = array of label, confidence.
        # so ocr_response[0][0][0][0][0] = first 'x' of the bounding boxes, which is a float
        # However, the first "array of single set of detections" isn't always there, so first
        # check to see if ocr_response[0][0][0][0] is a float

        detections = ocr_response if isinstance(ocr_response[0][0][0][0], float) else ocr_response[0]

        # Find the biggest textbox and assume that's the plate number
        (plate_label, plate_confidence, max_area) = (None, 0.0, 0)

        for detection in detections:
            bounding_box   = detection[0]   # [ topleft, topright, bottom right, bottom left ], each is [x,y]
            classification = detection[1]

            label      = classification[0]
            confidence = classification[1]

            # We won't assume the points are in a particular order (though we know they are)
            x_min = int(min(point[0] for point in bounding_box))   # = int(bounding_box[0][0]),
            y_min = int(min(point[1] for point in bounding_box))   # = int(bounding_box[0][1]),
            x_max = int(max(point[0] for point in bounding_box))   # = int(bounding_box[3][0]),
            y_max = int(max(point[1] for point in bounding_box))   # = int(bounding_box[3][1]),

            area = math.fabs((y_max - y_min) * (x_max - x_min))

            if area > max_area:
                max_area         = area
                plate_label      = pattern.sub('', label)
                plate_confidence = confidence

        if not plate_label:
            return no_plate_found, 0, inferenceTimeMs

        return plate_label, plate_confidence, inferenceTimeMs

    except Exception as ex:
        message = "".join(traceback.TracebackException.from_exception(ex).format())
        module_runner.log(LogMethod.Error | LogMethod.Server,
                            { 
                                "filename": "alpr.py",
                                "method": "read_plate_chars_PaddleOCR",
                                "loglevel": "error",
                                "message": message,
                                "exception_type": "Exception"
                            })
        return None, 0, inferenceTimeMs