import os

import requests
from PIL import Image, ImageDraw

import demoConfig as cfg

minConfidence = 0.4

cfg.cleanDetectedDir()

# process all the files in the input directory and store
# the results in the output directory
files = os.listdir(cfg.imageDir)
for f in files:
    if f.endswith(".jpg") or f.endswith(".png"):
        print(f)
        filepath = os.path.join(cfg.imageDir, f)
        image_data = open(filepath, "rb").read()
        image      = Image.open(filepath).convert("RGB")

        response = requests.post(
            cfg.serverUrl + "vision/detection",
            files={"image": image_data},
            data={"min_confidence": minConfidence}, 
            verify=cfg.verifySslCert
        ).json()

        print(response)
        predictions = response["predictions"]
        if (predictions is None):
            predictions = []

        draw = ImageDraw.Draw(image)
        for object in predictions:
            label = object["label"]
            conf  = object["confidence"]
            y_max = int(object["y_max"])
            y_min = int(object["y_min"])
            x_max = int(object["x_max"])
            x_min = int(object["x_min"])

            draw.rectangle([(x_min, y_min), (x_max, y_max)], outline="red", width=5)
            draw.text((x_min, y_min), "{}".format(label))
            draw.text((x_min, y_min - 10), "{}".format(conf))

    (root, ext) = os.path.splitext(f)
    savedName   = f"{root}.jpg"
    image.save(os.path.join(cfg.detectedDir, savedName))
