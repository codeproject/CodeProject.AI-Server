import os
import requests
import demoConfig as cfg

filepath = os.path.join(cfg.imageDir, "test-image.jpg")
test_image = open(filepath, "rb").read()

res = requests.post(
    cfg.serverUrl + "vision/face/recognize",
    files={"image": test_image},
    data={"min_confidence": 0.1},
    verify=cfg.verifySslCert
).json()
print(res)

for user in res["predictions"]:
    print(user["userid"])
