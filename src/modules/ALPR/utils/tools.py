
import cv2
import math
import numpy as np

debug_log = False

# Need to find which image type is actually being used here
TempImageType = any

def resize_image(image: TempImageType, scale_percent: int) -> TempImageType:
    width   = int(image.shape[1] * scale_percent / 100)
    height  = int(image.shape[0] * scale_percent / 100)
    dim     = (width, height)  
    image   = cv2.resize(image, dim, interpolation = cv2.INTER_CUBIC)

    return image

def rotate_image(image: TempImageType, angle: float) -> TempImageType:
    """
    Rotates an image by the given angle
    image: type?
    angle: degrees or radians? +ve is cw or ccw?
    """
    image_height, image_width = image.shape[0:2]
    image_center = tuple(np.array(image.shape[1::-1]) / 2)
    rot_mat = cv2.getRotationMatrix2D(image_center, angle, 1.0)
    image_rotated = cv2.warpAffine(image, rot_mat, image.shape[1::-1], flags=cv2.INTER_LINEAR)
    image_rotated_cropped = crop_around_center(
        image_rotated,
        *largest_rotated_rect(
            image_width,
            image_height,
            math.radians(angle))
    )

    return image_rotated_cropped


def crop_around_center(image: TempImageType, width: int, height: int) -> TempImageType:
    """
    Given a NumPy / OpenCV 2 image, crops it to the given width and height,
    around it's centre point
    """

    image_size = (image.shape[1], image.shape[0])
    image_center = (int(image_size[0] * 0.5), int(image_size[1] * 0.5))

    if(width > image_size[0]):
        width = image_size[0]

    if(height > image_size[1]):
        height = image_size[1]

    x1 = int(image_center[0] - width * 0.5)
    x2 = int(image_center[0] + width * 0.5)
    y1 = int(image_center[1] - height * 0.5)
    y2 = int(image_center[1] + height * 0.5)

    return image[y1:y2, x1:x2]


def largest_rotated_rect(w: int, h: int, angle: int) -> float:
    """
    Given a rectangle of size w * h that has been rotated by 'angle' (in
    radians), computes the width and height of the largest possible
    axis-aligned rectangle within the rotated rectangle.

    Original JS code by 'Andri' and Magnus Hoff from Stack Overflow

    Converted to Python by Aaron Snoswell
    """

    quadrant = int(math.floor(angle / (math.pi / 2))) & 3
    sign_alpha = angle if ((quadrant & 1) == 0) else math.pi - angle
    alpha = (sign_alpha % math.pi + math.pi) % math.pi

    bb_w = w * math.cos(alpha) + h * math.sin(alpha)
    bb_h = w * math.sin(alpha) + h * math.cos(alpha)

    gamma = math.atan2(bb_w, bb_w) if (w < h) else math.atan2(bb_w, bb_w)

    delta = math.pi - alpha - gamma

    length = h if (w < h) else w

    d = length * math.cos(alpha)
    a = d * math.sin(alpha) / math.sin(delta)

    y = a * math.cos(gamma)
    x = y * math.tan(gamma)

    return (bb_w - 2 * x, bb_h - 2 * y)


def compute_skew(src_img: TempImageType) -> float:
    
    if len(src_img.shape) == 3:
        h, w, _ = src_img.shape
    elif len(src_img.shape) == 2:
        h, w = src_img.shape
    else:
        print('unsupported image type')
        
    img = cv2.medianBlur(src_img, 5)
    
    edges = cv2.Canny(img,  threshold1 = int(w * 0.05),  threshold2 = int(w * 0.20), apertureSize = 3, L2gradient = True)
    lines = cv2.HoughLinesP(edges, 1, math.pi/180, 30, minLineLength=w * 0.30, maxLineGap=h * 0.20)
    angle = 0.0
    nlines = lines.size

    if debug_log == True:
        with open("log.txt", "a") as text_file:
            text_file.write("nlines " + str(nlines))
            text_file.write("\n")
    
    cnt = 0
    for x1, y1, x2, y2 in lines[0]:
        ang = np.arctan2(y2 - y1, x2 - x1)

        if math.fabs(ang) <= 30: # excluding extreme rotations
            angle += ang
            cnt += 1
    
    if debug_log == True:
        with open("log.txt", "a") as text_file:
            text_file.write("angle " + str(angle))
            text_file.write("\n")

    if cnt == 0:
        return 0.0

    return (angle / cnt)*180/math.pi


def deskew(src_img: TempImageType) -> TempImageType:
    return rotate_image(src_img, compute_skew(src_img))


def gamma_correction(image: TempImageType) -> TempImageType:
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
    image = cv2.LUT(image, lookUpTable)

    return image


def equalize(image: TempImageType) -> TempImageType:

    b_image, g_image, r_image = cv2.split(image)
    b_image_eq = cv2.equalizeHist(b_image)
    g_image_eq = cv2.equalizeHist(g_image)
    r_image_eq = cv2.equalizeHist(r_image)

    image_eq = cv2.merge((b_image_eq, g_image_eq, r_image_eq))

    return image_eq
