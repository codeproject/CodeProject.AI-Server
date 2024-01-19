import base64
from datetime import datetime
import io
from io import BytesIO
from typing import List, Tuple
import requests

import imutils
from imutils.video import VideoStream
import cv2

import numpy as np
from PIL import Image, ImageDraw, ImageFont

import smtplib
from email.mime.text import MIMEText
from email.mime.multipart import MIMEMultipart

from options import Options
opts = Options() 

recipient           = "alerts@acme_security.com"         # Sucker who deals with the reports
model_name          = "critters"                         # Model we'll use
intruders           = [ "racoon", "squirrel", "skunk" ]  # Things we care about
secs_between_checks = 5   # Min secs between sending a frame to CodeProject.AI
secs_between_alerts = 300 # Min secs between sending alerts (don't spam!)

# Set to any time that's over an hour old
last_check_time = datetime(1999, 11, 15, 0, 0, 0)
last_alert_time = datetime(1999, 11, 15, 0, 0, 0)

def do_detection(image: Image, intruders: List[str]) -> "(Image, str)":

    """
    Performs object detection on an image and returns an image with the objects
    that were detected outlined, as well as a de-duped list of objects detected.
    If nothing detected, image and list of objects are both returned as None
    """

    # Convert to format suitable for a POST
    buf = io.BytesIO()
    image.save(buf, format='JPEG')
    buf.seek(0)

    # Better to have a session object created once at the start and closed at
    # the end, but we keep the code simpler here for demo purposes    
    with requests.Session() as session:
        response = session.post(opts.endpoint("vision/custom/" + model_name),
                                files={"image": ('image.png', buf, 'image/png') },
                                data={"min_confidence": 0.5}).json()

    # Get the predictions (but be careful of a null return)
    predictions = response["predictions"]

    detected_list = []

    if predictions:
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
            draw.rectangle([(x_min, y_min - 2*padding - font_size),
                            (x_max, y_min)], fill="red", outline="red")
            draw.text((x_min + padding, y_min - padding - font_size), 
                      f"{label} {round(conf*100.0,0)}%", font=font)

            # We're looking for specific objects. Build a deduped list 
            # containing only the objects we're interested in.
            if label in intruders and not label in detected_list:
                detected_list.append(label)

    # All done. Did we find any objects we were interested in?
    if detected_list:
        return image, ', '.join(detected_list)
    
    return None, None


def report_intruder(image: Image, objects_detected: str, recipient: str) -> None:
        
    # time since we last sent an alert
    global last_alert_time
    seconds_since_last_alert = (datetime.now() - last_alert_time).total_seconds()

    # Only send an alert if there's been sufficient time since the last alert
    if seconds_since_last_alert > secs_between_alerts:

        # Simple console output
        timestamp = datetime.now().strftime("%d %b %Y %I:%M:%S %p")
        print(f"{timestamp} Intruder or intruders detected: {objects_detected}")

        # Send an email alert as well
        with BytesIO() as buffered:
            image.save(buffered, format="JPEG")
            img_dataB64_bytes : bytes = base64.b64encode(buffered.getvalue())
            img_dataB64 : str = img_dataB64_bytes.decode("ascii");
        
        message_html = "<p>An intruder was detected. Please review this image</p>" \
                     + f"<img src='data:image/jpeg;base64,{img_dataB64}'>"
        message_text = "A intruder was detected. We're all doomed!"

        send_email(opts.email_acct, opts.email_pwd, recipient, "Intruder Alert!", 
                   message_text, message_html)
        
        # Could send an SMS or a tweet. Whatever takes your fancy...
        
        last_alert_time = datetime.now()


def send_email(sender, pwd, recipient, subject, message_text, message_html):
    
    msg = MIMEMultipart('alternative')
    msg['From']    = sender
    msg['To']      = recipient
    msg['Subject'] = subject
    
    text = MIMEText(message_text, 'plain')
    html = MIMEText(message_html, 'html')
    msg.attach(text)
    msg.attach(html)

    try:
        server = smtplib.SMTP(opts.email_server, opts.email_port)
        server.ehlo()
        server.starttls()
        server.ehlo()
        server.login(sender, pwd)
        server.send_message(msg, sender, [recipient])
    except Exception as ex:
        print(f"Error sending email: {ex}")
    finally:
        server.quit()


def main():

    # Open the RTSP stream
    vs = VideoStream(opts.rtsp_url).start() 

    while True:

        # Grab a frame at a time
        frame = vs.read()
        if frame is None:
            continue

        objects_detected = ""

        # Let's not send an alert *every* time we see an object, otherwise we'll
        # get an endless stream of emails, fractions of a second apart
        global last_check_time
        seconds_since_last_check = (datetime.now() - last_check_time).total_seconds()

        if seconds_since_last_check >= secs_between_checks:
            # You may need to convert the colour space.
            # image: Image = Image.fromarray(cv2.cvtColor(frame, cv2.COLOR_BGR2RGB))
            image: Image = Image.fromarray(frame)
            (image, objects_detected) = do_detection(image, intruders)

            # Replace the webcam feed's frame with our image that include object 
            # bounding boxes
            if image:
                frame = np.asarray(image)

            last_check_time = datetime.now()

        # Resize and display the frame on the screen
        if frame is not None:
            frame = imutils.resize(frame, width = 1200)
            cv2.imshow('WyzeCam', frame)

            if objects_detected:
                # Shrink the image to reduce email size
                frame = imutils.resize(frame, width = 600)
                image = Image.fromarray(cv2.cvtColor(frame, cv2.COLOR_BGR2RGB))
                report_intruder(image, objects_detected, recipient)

        # Wait for the user to hit 'q' for quit
        key = cv2.waitKey(1) & 0xFF
        if key == ord('q'):
            break

    # Clean up and we're outta here.
    cv2.destroyAllWindows()
    vs.stop()


if __name__ == "__main__":
    main()