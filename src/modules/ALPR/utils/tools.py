
import cv2
import math
import re
import numpy as np
from PIL import Image

from utils.cartesian import *

debug_log = False   # Output log info
debug_img = False   # Save intermediate images to view the progress

# Could switch to numpy, so keep this abstracted for now
ImageType = Image


def resize_image(image: ImageType, scale_percent: int) -> ImageType:
    """
    Resize an image by the given percent
    image: ImageType
    scale_percent: the percentage the image should be scaled to.
    """

    dimension = Size(image.shape[1], image.shape[0])  
    dimension.scale(scale_percent / 100.0)
    dimension.integerize()

    new_image = cv2.resize(image, dimension.as_tuple(), interpolation = cv2.INTER_CUBIC)

    if debug_img:
        cv2.imwrite("resize_image_pre.jpg", image)
        cv2.imwrite("resize_image_post.jpg", new_image)

    return new_image


def rotate_image(image: ImageType,  angle: float, largest_size: Size = None) -> ImageType:
    """
    Rotates an image by the given angle
    image: ImageType
    angle: degrees or radians? +ve is cw or ccw?
    largest_size: if provided, the rotated image will be cropped to this size
    """

    image_center    = tuple(np.array(image.shape[1::-1]) / 2)
    rotation_matrix = cv2.getRotationMatrix2D(image_center, angle, 1.0)
    image_rotated   = cv2.warpAffine(image, rotation_matrix, image.shape[1::-1], flags=cv2.INTER_LINEAR)

    if debug_img:
        cv2.imwrite("rotate_image_pre.jpg", image)

    if largest_size:
        image_rotated_cropped = crop_around_center(image_rotated, largest_size)
        if debug_img:
            cv2.imwrite("rotate_image_cropped.jpg", image_rotated_cropped)
        return image_rotated_cropped

    # image_height, image_width = image.shape[0:2]
    # image_rotated_cropped = crop_around_center(image_rotated,
    #    *largest_rotated_rect(image_width, image_height, math.radians(angle)))

    if debug_img:
        cv2.imwrite("rotate_image_post.jpg", image_rotated_cropped)

    return image_rotated


def crop_around_center(image: ImageType, size: Size) -> ImageType:
    """
    Given a NumPy / OpenCV 2 image, crops it to the given size (width,  height),
    around it's centre point
    """

    # Note that 'shape' is (height, width, depth). Because Python.
    image_size = Size(image.shape[1], image.shape[0])

    # Anything to do? If not, return
    if image_size.width <= size.width and image_size.height <= size.height:
        return image

    if size.width > image_size.width:
        size.width = image_size.width

    if size.height > image_size.height:
        size.height = image_size.height

    image_center = Point(image_size.width * 0.5, image_size.height * 0.5)
    x1 = int(image_center.x - size.width * 0.5)
    x2 = int(image_center.x + size.width * 0.5)
    y1 = int(image_center.y - size.height * 0.5)
    y2 = int(image_center.y + size.height * 0.5)

    cropped_image = image[y1:y2, x1:x2]

    if debug_img:
        cv2.imwrite("crop_around_center_pre.jpg", image)
        cv2.imwrite("crop_around_center_post.jpg", cropped_image)

    return cropped_image


def largest_rotated_rect(size: Size, angle: int) -> Size:
    """
    Given a rectangle of size w * h that has been rotated by 'angle' (in
    radians), computes the width and height of the largest possible
    axis-aligned rectangle within the rotated rectangle.

    Original JS code by 'Andri' and Magnus Hoff from Stack Overflow

    Converted to Python by Aaron Snoswell
    """

    quadrant   = int(math.floor(angle / (math.pi / 2))) & 3
    sign_alpha = angle if ((quadrant & 1) == 0) else math.pi - angle
    alpha      = (sign_alpha % math.pi + math.pi) % math.pi

    bb_w = size.width * math.cos(alpha) + size.height * math.sin(alpha)
    bb_h = size.width * math.sin(alpha) + size.height * math.cos(alpha)

    gamma = math.atan2(bb_w, bb_w) if (size.width < size.height) else math.atan2(bb_w, bb_w)

    delta = math.pi - alpha - gamma

    length = size.height if (size.width < size.height) else size.width

    d = length * math.cos(alpha)
    a = d * math.sin(alpha) / math.sin(delta)

    y = a * math.cos(gamma)
    x = y * math.tan(gamma)

    return Size(bb_w - 2 * x, bb_h - 2 * y)


def calculate_angle(x1, y1, x2, y2) -> float:
    delta_y = y2 - y1
    delta_x = x2 - x1

    # Calculate the angle in radians using atan2
    angle_radians = math.atan2(delta_y, delta_x)

    # Convert radians to degrees
    angle_degrees = math.degrees(angle_radians)

    return angle_degrees


"""
def compute_skew(src_img: ImageType) -> float:

    # Computes the degrees by which an image containing a licence plate (or anything
    # vaguely rectangular that's longer than it is high) is skewed
    
    try:
        dimensions: Size = Size(src_img.shape[1], src_img.shape[0])
        dimensions.integerize()

        # Exaggerate the width to make line detection more prominent
        dimensions.width  *= 8
        dimensions.height *= 2

        gray_img   = cv2.resize(src_img, dimensions.as_tuple(), interpolation = cv2.INTER_CUBIC)
        # gray_img = cv2.cvtColor(src_img,cv2.COLOR_BGR2GRAY)
        median_img = cv2.medianBlur(gray_img, 5)
        edges      = cv2.Canny(median_img,  threshold1 = 60,  threshold2 = 90, apertureSize = 3, L2gradient = True)

        lines = cv2.HoughLinesP(edges, rho = 1, theta = np.pi/180, threshold = int(dimensions.height * 0.50),
                                minLineLength = dimensions.height * 0.75, maxLineGap = dimensions.width * 0.04)

        for line in lines:
            x1,y1,x2,y2 = line[0]
            cv2.line(median_img, (x1,y1), (x2,y2), (255,255,255), 2)

        angle: float = 0.0
        count: int   = 0
        index: int   = 0
        
        for index in range(len(lines)):
            line = lines[index]
            for x1, y1, x2, y2 in line:
                line_angle = np.arctan2(y2 - y1, x2 - x1) * 4

            if math.fabs(line_angle) <= 0.524 and math.fabs(line_angle): # excluding extreme rotations
                angle += line_angle
                count += 1

    except Exception as ex:
        return 0.0

    # return the average line angle in degrees if lines were found
    if count:
        return (angle / count)*180/math.pi    

    return 0.0


# def deskew(src_img: ImageType) -> ImageType:
#    return rotate_image(src_img, compute_skew(src_img))


def gamma_correction(image: ImageType) -> ImageType:
    # HSV (or other color spaces)

    # convert img to HSV
    image_hsv = cv2.cvtColor(image, cv2.COLOR_BGR2HSV)
    hue, sat, val = cv2.split(image_hsv)

    # compute gamma = log(mid*255)/log(mean)
    mid = 0.5
    mean = np.mean(val)
    gamma = math.log(mid*255)/math.log(mean)
    gamma = 1 / gamma

    if debug_log == True:
        with open("log.txt", "a") as text_file:
            text_file.write("Gamma " + str(gamma))
            text_file.write("\n")

    lookUpTable = np.empty((1,256), np.uint8)
    for i in range(256):
        lookUpTable[0,i] = np.clip(pow(i / 255.0, gamma) * 255.0, 0, 255)
    corrected_image = cv2.LUT(image, lookUpTable)

    if debug_img:
        cv2.imwrite("gamma_correction_pre.jpg", image)
        cv2.imwrite("gamma_correction_post.jpg", corrected_image)

    return corrected_image


def equalize(image: ImageType) -> ImageType:

    b_image, g_image, r_image = cv2.split(image)
    b_image_eq = cv2.equalizeHist(b_image)
    g_image_eq = cv2.equalizeHist(g_image)
    r_image_eq = cv2.equalizeHist(r_image)

    image_eq = cv2.merge((b_image_eq, g_image_eq, r_image_eq))

    return image_eq
"""

def merge_text_detections(bounding_boxes, remove_spaces) -> Tuple[str, float, int, int]:
    
    pattern            = re.compile('[^a-zA-Z0-9]+')
    tallest_box        = None
    tallest_box_height = 0
    large_boxes        = []
    confidence_sum     = 0
    count              = 0
    large_boxes_count  = 0
    avg_char_width     = 0
    avg_char_height    = 0

    # Find the tallest bounding box
    for box, (text, confidence) in bounding_boxes:
        box_height = int(box[3][1] - box[0][1])
        
        if text and (tallest_box is None or box_height > tallest_box_height):
            tallest_box        = box
            tallest_box_text   = text
            tallest_box_height = box_height

            box_width          = int(tallest_box[2][0] - tallest_box[3][0])
            avg_char_width     = int(box_width / len(text))
            avg_char_height    = box_height

    # Find large boxes and calculate the average confidence. A large box is anything
    # that is at least 80% the height of the tallest box
    for box, (_, confidence) in bounding_boxes:
        box_height = int(box[3][1] - box[0][1])
        
        if box_height >= tallest_box_height * 0.8:
            large_boxes.append(box)        
            confidence_sum += confidence
            count += 1

    average_confidence = confidence_sum / count if count > 0 else 0
    
    # Merge all text from large boxes
    merged_text = ''
    for box, (text, _) in bounding_boxes:
        if box in large_boxes:
            large_boxes_count += 1
            if count > 1 and large_boxes_count < count:
                text = text + ' '
            merged_text += text

    if remove_spaces:
        merged_text  = pattern.sub('', merged_text)
    else:
        merged_text  = pattern.sub(' ', merged_text)

    if debug_log == True:
        with open("logbox.txt", "a") as text_file:
            text_file.write(f"Avg char (w,h): {avg_char_height} x {avg_char_width} - {tallest_box_text}\n\n")

    return merged_text, average_confidence, avg_char_height, avg_char_width