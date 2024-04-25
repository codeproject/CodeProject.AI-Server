
# We're going to utilise our shared python installation in /runtimes. We run this
# directly without activating the  virtual environment

import time
import io

import cv2
import imutils
from imutils.video import VideoStream
import numpy as np
from PIL import Image, ImageDraw, ImageFont
import requests


# Settings =====================================================================

# Server
server_port    = 32168
server_host    = "localhost"
server_url     = f"http://{server_host}:{server_port}/v1/"

# Video Source. If you want to use the webcam, keep file_path = None

#  - For RTSP feed from a webcam
rtsp_user = "Username"
rtsp_pass = "Password"
rtsp_IP   = "10.0.0.198"
source    = f"rtsp://{rtsp_user}:{rtsp_pass}@{rtsp_IP}/live"

# Or...use the local webcam
source=0

# Display
font_size  = 50
padding    = 15
line_width = 5


# Main logic ===================================================================
 
def do_detection(image):
   
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
    if not "predictions" in response:
        return None
    
    predictions = response["predictions"]
    if predictions is None:
        predictions = []

    # Draw each bounding box that was returned by the AI engine
    font = ImageFont.truetype("Arial.ttf", font_size)
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
        draw.text((x_min + padding, y_min - padding - font_size), f"{label} {round(conf*100.0,0)}%", font=font)

    # ...and we're done
    return image


def main():

    detect_objects = True

    vs = VideoStream(source).start() 
    if not source:
        time.sleep(2.0)  # Allow some time for the camera to warm up

    while True:
       
        # Grab one frame at a time
        frame = vs.read()
        if frame is None:
            continue

        image = None
        if detect_objects:
            # You may need to convert the colour space.
            # image = Image.fromarray(cv2.cvtColor(frame, cv2.COLOR_BGR2RGB))
            image = Image.fromarray(frame)
            image = do_detection(image)
            frame = np.asarray(image) if image else None

        # Resize and display the frame on the screen
        if frame is None:
            frame = image

        if frame is not None:
            frame = imutils.resize(frame, width = 640)
            cv2.imshow("Webcam" , frame)
    
        # Wait for the user to hit 'q' for quit
        key = cv2.waitKey(1) & 0xFF
        if key == ord('q'):
            break

    # Clean up and we're outta here.
    vs.stop()
    cv2.destroyAllWindows()


if __name__ == "__main__":
    main()