import cv2
import imutils
from imutils.video import VideoStream

import io
import requests
import numpy as np
from PIL import Image, ImageDraw, ImageFont

from options import Options
opts = Options() 


def do_detection(image):
   
    # Convert to format suitable for a POST
    buf = io.BytesIO()
    image.save(buf, format='PNG')
    buf.seek(0)

    # Better to have a session object created once at the start and closed at
    # the end, but we keep the code simpler here for demo purposes    
    with requests.Session() as session:
        response = session.post(opts.endpoint("vision/detection"),
                                files={"image": ('image.png', buf, 'image/png') },
                                data={"min_confidence": 0.5}).json()

    # Get the predictions (but be careful of a null return)
    predictions = response["predictions"]
    if predictions is None:
        predictions = []

    # Draw each bounding box that was returned by the AI engine
    # font = ImageFont.load_default()
    font_size = 25
    padding   = 5
    font = ImageFont.truetype("arial.ttf", font_size)
    draw = ImageDraw.Draw(image)

    for object in predictions:
        label = object["label"]
        conf  = object["confidence"]
        y_max = int(object["y_max"])
        y_min = int(object["y_min"])
        x_max = int(object["x_max"])
        x_min = int(object["x_min"])

        draw.rectangle([(x_min, y_min), (x_max, y_max)], outline="red", width=5)
        draw.rectangle([(x_min, y_min), (x_max, y_min - 2*padding - font_size)], fill="red", outline="red")
        draw.text((x_min + padding, y_min - padding - font_size), f"{label} {round(conf*100.0,0)}%", font=font)

    # ...and we're done
    return image


def main():

    detect_objects = True

    # Open the RTSP stream
    vs = VideoStream(opts.rtsp_url).start() 

    while True:

        # Grab a frame at a time
        frame = vs.read()
        if frame is None:
            continue

        if detect_objects:
            # You may need to convert the colour space.
            # image = Image.fromarray(cv2.cvtColor(frame, cv2.COLOR_BGR2RGB))
            image = Image.fromarray(frame)
            image = do_detection(image)
            frame = np.asarray(image)

        # Resize and display the frame on the screen
        frame = imutils.resize(frame, width = 1200)
        cv2.imshow('WyzeCam', frame)
    
        # Wait for the user to hit 'q' for quit
        key = cv2.waitKey(1) & 0xFF
        if key == ord('q'):
            break

    # Clean up and we're outta here.
    cv2.destroyAllWindows()
    vs.stop()


if __name__ == "__main__":
    main()