# Import our general libraries
import io
import math
import re
import time
from typing import Tuple

import utils.tools as tool
from utils.cartesian import *

from module_runner import ModuleRunner
from module_logging import LogVerbosity
from common import JSON

from PIL import Image
import cv2
import numpy as np

from options import Options
from paddleocr import PaddleOCR

ocr = None
no_plate_found = 'Characters Not Found'

def init_detect_platenumber(opts: Options) -> None:

    global ocr
    ocr = PaddleOCR(lang                = opts.language,
                    use_gpu             = opts.use_gpu,
                    show_log            = opts.log_verbosity == LogVerbosity.Loud,
                    det_db_unclip_ratio = opts.det_db_unclip_ratio,
                    det_db_box_thresh   = opts.box_detect_threshold,
                    drop_score          = opts.char_detect_threshold,
                    rec_algorithm       = opts.algorithm,
                    cls_model_dir       = opts.cls_model_dir,
                    det_model_dir       = opts.det_model_dir,
                    rec_model_dir       = opts.rec_model_dir)


async def detect_platenumber(module_runner: ModuleRunner, opts: Options, image: Image) -> JSON:

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
            if not detect_plate_response["predictions"]:
                return { "predictions": [], "inferenceMs": inferenceMs }

        except Exception as ex:
            await module_runner.report_error_async(ex, __file__)
            return { "error": str(ex), "inferenceMs": inferenceMs }

    # We have a plate (or plates) detected, so let's prep the original image for some work
    numpy_image = np.array(pillow_image)

    # Remember: numpy is left handed when it comes to indexes
    orig_image_size: Size = Size(numpy_image.shape[1], numpy_image.shape[0])

    # Correct the colour space
    numpy_image = cv2.cvtColor(numpy_image, cv2.COLOR_RGB2BGR)

    # If a plate is found we'll pass this onto OCR
    for plate_detection in detect_plate_response["predictions"]:

        # Pull out just the detected plate.
        # The coordinates... (relative to the original image)
        plate_rect  = Rect(plate_detection["x_min"], plate_detection["y_min"],
                           plate_detection["x_max"], plate_detection["y_max"])
        # The image itself... (Its coordinates are now relativen to itself)
        numpy_plate = numpy_image[plate_rect.top:plate_rect.bottom, plate_rect.left:plate_rect.right]

        # Pre-processing of the extracted plate to give the OCR a better chance of success
        
        # Run it through a super-resolution module to improve readability (TBD, but could be slow)
        # numpy_plate = super_resolution(numpy_plate)

        # resize image if required
        if opts.OCR_rescale_factor != 1:
            numpy_plate = tool.resize_image(numpy_plate, opts.OCR_rescale_factor * 100)

        # Store the size of the scaled plate before we start rotating it
        scaled_plate_size = Size(numpy_plate.shape[1], numpy_plate.shape[0])

        # Work out the angle we need to rotate the image to de-skew it (or use the manual override).
        if opts.auto_plate_rotate:
            plate_rotate_deg = tool.compute_skew(numpy_plate)
        elif opts.plate_rotate_deg:
            plate_rotate_deg = opts.plate_rotate_deg

        # If we need to rotate, then check to ensure that rotating the image won't chop off
        # important parts of the plate. Note that this will only happen to plates that are at,
        # or very close to, the edge of the image.
        if plate_rotate_deg:
        
            # We start with the assumption that we have a plate that is not displayed level. Once
            # the plate is deskewed, the bounding box of the rotated plate will be *smaller* than
            # the skewed plate.
            
            # Calculate the corrected (smaller) bounding box if we were to rotate the image
            rotated_size: Size = tool.largest_rotated_rect(scaled_plate_size, math.radians(plate_rotate_deg))

            # Calculate the space between the corresponding edges of the (smaller) rotated plate
            # and the original plate.
            # buffer: Size = (scaled_plate_size - rotated_size) / 2.0
            buffer: Size = (scaled_plate_size - rotated_size).__div__(2.0)

            # Scale this back to the original plate dimensions (remember we scaled the plate)
            if opts.OCR_rescale_factor != 1:
                # buffer = buffer / opts.OCR_rescale_factor
                buffer = buffer.__div__(opts.OCR_rescale_factor)

            # 'buffer' represents the width/height of an area along the edges of the original image.
            #
            #         =============================
            #         |       buffer              |   
            #         |  ----------------------   |
            #         |  |                    |   |
            #         |  |    (safe area)     |   |
            #         |  |                    |   |
            #         |  ----------------------   |
            #         |       (unsafe area)       |
            #         =============================
            #
            # If a plate is detected in the original image, and part of that plate extends into 
            # (or over) the buffer area then it means that, on rotatiom, part of the image could 
            # be cut off. We should avoid that since it could cut off characters.

            # Calculate the region inside the extracted plate that the rotated plate image must fit
            # inside for us to go ahead and do the rotation (left, top, right, bottom)
            safe_rotation_area: Rect = Rect(buffer.width,  buffer.height,
                                            orig_image_size.width  - buffer.width,
                                            orig_image_size.height - buffer.height)

            # If the plate image isn't completely contained within the 'safe' area then do not
            # rotate
            if not safe_rotation_area.contains(plate_rect):
                plate_rotate_deg = 0

        # Rotate if we've determined we have a valid rotation angle to apply
        if plate_rotate_deg:
            numpy_plate = tool.rotate_image(numpy_plate, plate_rotate_deg, rotated_size)

        # numpy_plate = cv2.GaussianBlur(numpy_plate, (5,5), 0)
        # numpy_plate = cv2.medianBlur(numpy_plate, 3)

        # perform otsu thresh (best to use binary inverse since opencv contours work better with white text)
        # ret, numpy_plate = cv2.threshold(numpy_plate, 0, 255, cv2.THRESH_OTSU   | cv2.THRESH_BINARY_INV)
        # ret, numpy_plate = cv2.threshold(numpy_plate, 0, 255, cv2.THRESH_BINARY + cv2.THRESH_OTSU)


        # Read the plate. This may require multiple attempts
 
        # Read plate
        (label, confidence, plateInferenceMs) = await read_plate_chars_PaddleOCR(module_runner, numpy_plate)
        inferenceMs += plateInferenceMs

        # If we had no success reading the original plate, apply some image enhancement
        # and try again
        if label == no_plate_found:
            # If characters are not found try gamma correction and equalize
            # numpy_plate = cv2.fastNlMeansDenoisingColored(numpy_plate, None, 10, 10, 7, 21)
            numpy_plate = tool.gamma_correction(numpy_plate)
            # numpy_plate = tool.equalize(numpy_plate)
            # numpy_plate = cv2.cvtColor(numpy_plate, cv2.COLOR_BGR2GRAY)
            # cv2.imwrite("alpr-enhanced.jpg", numpy_plate)

            # Read plate, 2nd attempt
            (label, confidence, plateInferenceMs) = await read_plate_chars_PaddleOCR(module_runner, numpy_plate)
            inferenceMs += plateInferenceMs

        if label and confidence:
            detection = {
                "confidence": confidence,
                "label": "Plate: " + label,
                "plate": label,
                "x_min": plate_rect.left,
                "y_min": plate_rect.top,
                "x_max": plate_rect.right,
                "y_max": plate_rect.bottom,
            }
            outputs.append(detection)

    return { "predictions": outputs, "inferenceMs": inferenceMs }


async def read_plate_chars_PaddleOCR(module_runner: ModuleRunner, image: Image) -> Tuple[str, float, float]:

    """
    This uses PaddleOCR for reading the plates. Note that the image being passed
    in should be a tightly cropped licence plate, so we're looking for the largest
    text box and will assume that's the plate number.
    Returns (plate label, confidence, inference time (ms))
    """

    pattern  = re.compile('[^a-zA-Z0-9]+')
    inferenceTimeMs: int = 0

    try:
        start_time = time.perf_counter()
        ocr_response = ocr.ocr(image, cls=True)
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

            if label and confidence:
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
        module_runner.report_error_aync(ex, __file__)    
        return None, 0, inferenceTimeMs