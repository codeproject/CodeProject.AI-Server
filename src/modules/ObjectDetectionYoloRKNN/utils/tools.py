import cv2
import numpy as np
from threading import Lock

stored_lines = {}  # Dictionary to store lines for each file_path


def resize_image(img, max_size):
    
    resize_height, resize_width, _ = img.shape
    target_shape = (max_size, max_size)

    # Calculate the aspect ratio of the image
    img_aspect_ratio = resize_width / resize_height

    # Calculate the target width and height while maintaining aspect ratio
    if target_shape[1] / target_shape[0] > img_aspect_ratio:
        target_width = int(target_shape[0] * img_aspect_ratio)
        target_height = target_shape[0]
    else:
        target_width = target_shape[1]
        target_height = int(target_shape[1] / img_aspect_ratio)

    # Resize the image to the target dimensions
    resized_img = cv2.resize(img, (target_width, target_height), interpolation=cv2.INTER_AREA)

    # Create the output image with the target shape    
    output_img = np.zeros((*target_shape, 3), dtype=np.uint8)

    # Embed the resized image into the output image
    output_img[:target_height, :target_width] = resized_img
       
    # Calculate the empty dimensions
    empty_height = target_shape[0] - target_height
    empty_width  = target_shape[1] - target_width

    # Calculate the scaling factors
    y_scaling_factor = (max_size - empty_height) / resize_height
    x_scaling_factor = (max_size - empty_width) / resize_width
    
    # Convert color space
    output_img = cv2.cvtColor(output_img, cv2.COLOR_BGR2RGB)

    return output_img, x_scaling_factor, y_scaling_factor


def convert_bounding_boxes(bounding_boxes, x_scaling_factor, y_scaling_factor):
    converted_boxes = []

    xmin, ymin, xmax, ymax = bounding_boxes

    # Convert bounding box coordinates to the original image size
    xmin = int(float(xmin) / x_scaling_factor)
    ymin = int(float(ymin) / y_scaling_factor)
    xmax = int(float(xmax) / x_scaling_factor)
    ymax = int(float(ymax) / y_scaling_factor)
    converted_boxes = xmin, ymin, xmax, ymax
    return converted_boxes


def count_labels(file_path):
    with open(file_path, 'r') as file:
        label_count = sum(1 for line in file)
    return label_count


def read_file(file_path):
    global stored_lines
    if file_path not in stored_lines:
        with open(file_path, 'r') as file:
            lines = file.readlines()
            stored_lines[file_path] = lines


def extract_label_from_file(line_number, file_path):
    """
    Extracts the label from the stored lines at the given line number.
    
    Args:
        line_number (int): The line number to extract the label from.
        file_path (str): The path to the text file.
        
    Returns:
        str: The extracted label.
    """
    global stored_lines
    if file_path not in stored_lines:
        read_file(file_path)

    lines = stored_lines.get(file_path)
    if lines is not None:
        if line_number >= 0 and line_number < len(lines):
            label = lines[line_number].strip()
            return label
        else:
            # print(f"Line number {line_number} is out of range.")
            return None
