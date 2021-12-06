import os
import requests
import demoConfig as cfg

filepath = os.path.join(cfg.imageDir, "test-image5.jpg")
image_data = open(filepath, "rb").read()

response = requests.post(
    cfg.serverUrl + "vision/scene", 
    files={"image": image_data},
    verify=cfg.verifySslCert
).json()

print("Label:", response["label"])
print(response)
