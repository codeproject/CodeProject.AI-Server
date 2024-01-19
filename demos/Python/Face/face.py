import os
import requests
from PIL import Image
from options import Options
from .. import utils

def main():

    opts = Options()

    filepath   = os.path.join(opts.imageDir + "/People and actions/",
                              "pexels-polina-tankilevitch-5848781.jpg")
    image_data = open(filepath, "rb").read()
    image      = Image.open(filepath).convert("RGB")

    utils.cleanDir(opts.detectedDir)

    response = requests.post(opts.endpoint("vision/face"),
                             files = {"image": image_data}).json()

    predictions = response["predictions"]
    if (predictions is None):
        predictions = []

    print(f"Processed {filepath}: {len(predictions)} predictions")

    i = 0
    for face in predictions:

        y_max = int(face["y_max"])
        y_min = int(face["y_min"])
        x_max = int(face["x_max"])
        x_min = int(face["x_min"])
        cropped = image.crop((x_min, y_min, x_max, y_max))

        filepath = os.path.join(opts.detectedDir, "image{}.jpg".format(i))
        cropped.save(filepath)

        i += 1

if __name__ == "__main__":
    main()