import os
import requests
from PIL import Image, ImageDraw

from .. import utils
from options import Options

def main():

    minConfidence = 0.4

    opts = Options() 
    utils.cleanDir(opts.detectedDir)

    imagedir = opts.imageDir + "/Objects"

    # process all the files in the input directory and store the results in the 
    # output directory
    filelist = os.listdir(imagedir)

    for filename in filelist:
        if not filename.endswith(".jpg") and not filename.endswith(".png"):
            continue

        filepath = os.path.join(imagedir, filename)
        image_data = open(filepath, "rb").read()
        image      = Image.open(filepath).convert("RGB")

        response = requests.post(opts.endpoint("vision/detection"),
                                 files={"image": image_data},
                                 data={"min_confidence": minConfidence}).json()

        predictions = response["predictions"]
        if (predictions is None):
            predictions = []

        print(f"Processed {filename}: {len(predictions)} predictions")

        draw = ImageDraw.Draw(image)
        for object in predictions:
            label = object["label"]
            conf  = object["confidence"]
            y_max = int(object["y_max"])
            y_min = int(object["y_min"])
            x_max = int(object["x_max"])
            x_min = int(object["x_min"])

            draw.rectangle([(x_min, y_min), (x_max, y_max)], outline="red", width=5)
            draw.text((x_min, y_min), f"{label}")
            draw.text((x_min, y_min - 10), f"{round(conf*100.0,0)}")

        (root, _) = os.path.splitext(filename)
        savedName = f"{root}.jpg"
        image.save(os.path.join(opts.detectedDir, savedName))


if __name__ == "__main__":
    main()