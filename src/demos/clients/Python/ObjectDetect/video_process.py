
import io
import platform

import cv2
import imutils
from imutils.video import VideoStream, FileVideoStream
import numpy as np
from PIL import Image, ImageDraw, ImageFont
import requests


# Settings =====================================================================

# Server
server_port    = 32168
server_host    = "localhost"
server_url     = f"http://{server_host}:{server_port}/v1/"

# Video Source.
file_path      = "../../../TestData/Video/sample.mp4"

# Output
output_file    = "results.txt"  # leave blank to not dump result to file

# Display
font_size  = 25 if platform.system() == "Windows" else 15
padding    = 5  if platform.system() == "Windows" else 2
line_width = 5  if platform.system() == "Windows" else 2


# Main logic ===================================================================
 
def do_detection(image, log_file):
   
    # Convert to format suitable for a POST
    buf = io.BytesIO()
    image.save(buf, format='PNG')
    buf.seek(0)

    # Better to have a session object created once at the start and closed at
    # the end, but we keep the code simpler here for demo purposes    
    with requests.Session() as session:
        response = session.post(server_url + "vision/detection",
                                files={"image": ('image.png', buf, 'image/png') },
                                data={"min_confidence": 0.5}).json()

    # Get the predictions (but be careful of a null return)
    predictions = None
    if response is not None and "predictions" in response:
       predictions = response["predictions"]

    if predictions is not None:
        # Draw each bounding box that was returned by the AI engine
        # font = ImageFont.truetype("Arial.ttf", font_size)
        font = ImageFont.load_default(size=font_size)
        draw = ImageDraw.Draw(image)

        for object in predictions:
            label = object["label"]
            conf  = object["confidence"]
            y_max = int(object["y_max"])
            y_min = int(object["y_min"])
            x_max = int(object["x_max"])
            x_min = int(object["x_min"])

            if y_max < y_min:
                temp = y_max
                y_max = y_min
                y_min = temp

            if x_max < x_min:
                temp = x_max
                x_max = x_min
                x_min = temp

            draw.rectangle([(x_min, y_min), (x_max, y_max)], outline="red", width=line_width)
            # draw.rectangle([(x_min, y_min - 2*padding - font_size), (x_max, y_min)], fill="red", outline="red")

            object_info = f"{label} {round(conf*100.0,0)}%"
            draw.text((x_min + padding, y_min - padding - font_size), object_info, font=font)

            log_file.write(f"{object_info}: ({x_min}, {y_min}), ({x_max}, {y_max})\n")

    # ...and we're done
    return image


def main():

    detect_objects = True

    vs = FileVideoStream(file_path).start()
    # vs = VideoDecoder(file_path).start() # if you want to try offloading decoding to a diff thread

    with open(output_file, 'w') as log_file:
        while True:
        
            # Grab one frame at a time
            if not vs.more():
                break

            frame = vs.read()
            if frame is None:
                break

            image = None
            if detect_objects:
                # You may need to convert the colour space.
                # image = Image.fromarray(cv2.cvtColor(frame, cv2.COLOR_BGR2RGB))
                image = Image.fromarray(frame)
                image = do_detection(image, log_file)
                frame = np.asarray(image) if image else None

            # Resize and display the frame on the screen
            if frame is None:
                frame = image

            if frame is not None:
                frame = imutils.resize(frame, width = 640)
                cv2.imshow("Movie File", frame)
        
            # Wait for the user to hit 'q' for quit
            key = cv2.waitKey(1) & 0xFF
            if key == ord('q'):
                break

    # Clean up and we're outta here.
    vs.stop()
    cv2.destroyAllWindows()


"""
# The VideoDecoder class allows offloading the frame decoding to another thread.
# Decoding a frame and processing an inference on the same thread can cause 
# bottlenecks. This class can be used as a drop-in alternative to FileVideoStream
# In testing, though, it hasn't provided aby perf improvement

import concurrent.futures
class VideoDecoder:
    def __init__(self, filename):
        self.cap        = cv2.VideoCapture(filename)
        self.executor   = concurrent.futures.ThreadPoolExecutor(max_workers=1)
        self.frames     = []
        self.index      = 0
        self._cancelled = False

    def read_frame(self):
        while not self._cancelled:
            ret, frame = self.cap.read()
            if not ret:
                break
            self.frames.append(frame)

    def start(self):
        self.executor.submit(self.read_frame)
        return self

    def more(self):
        return self.frames == [] or self.index < len(self.frames)

    def read(self):
        if self.index < len(self.frames):
            self.index += 1
            return self.frames[self.index - 1]    # "++" operator would be nice
        return None

    def stop(self):
        self._cancelled = True
        
    def __del__(self):
        self.cap.release()
"""


if __name__ == "__main__":
    main()