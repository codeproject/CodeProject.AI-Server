import os
import requests
from PIL import Image
import demoConfig as cfg

filepath   = os.path.join(cfg.imageDir, "family.jpg")
image_data = open( filepath, "rb").read()
image      = Image.open(filepath).convert("RGB")

cfg.cleanDetectedDir()

response = requests.post(
    cfg.serverUrl + "vision/face", 
    files ={"image": image_data},
    verify=cfg.verifySslCert
).json()

print(response)
i = 0
for face in response["predictions"]:

    y_max = int(face["y_max"])
    y_min = int(face["y_min"])
    x_max = int(face["x_max"])
    x_min = int(face["x_min"])
    cropped = image.crop((x_min, y_min, x_max, y_max))

    filepath = os.path.join(cfg.detectedDir, "image{}.jpg".format(i))
    cropped.save(filepath)

    i += 1
