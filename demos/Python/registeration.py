import os
import requests
import demoConfig as cfg


def register_face(img_path, user_id):
    filepath = os.path.join(cfg.imageDir, img_path)
    image_data = open(filepath, "rb").read()
    response = requests.post(
        cfg.serverUrl + "vision/face/register",
        files={"image": image_data},
        data={"userid": user_id},
        verify=cfg.verifySslCert
   ).json()
    print(response)


register_face("cruise.jpg", "Tom Cruise")
register_face("adele.jpg", "Adele")
register_face("elba.jpg", "Idris Elba")
register_face("perri.jpg", "Christina Perri")
