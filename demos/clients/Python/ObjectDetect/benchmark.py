"""
Script to time how long it takes the Object Detection modules to process a set 
of images. The purpose is to allow comparison of speed on different hosts. We 
also count the total number of predictions as a quality check.

Example run: python3 benchmark.py --images_folder /Users/my/images
"""
import argparse
import time
from pathlib import Path
import requests

from options import Options

opts = Options()

def main():

    DEFAULT_IP_ADDRESS    = opts.serverHost
    DEFAULT_PORT          = opts.serverPort
    DEFAULT_IMAGES_FOLDER = opts.imageDir + "/Objects"

    parser = argparse.ArgumentParser(description="Perform benchmarking of CodeProject.AI Server")
    parser.add_argument(
        "--ip",
        default=DEFAULT_IP_ADDRESS,
        type=str,
        help="CodeProject.AI Server IP address",
    )
    parser.add_argument(
        "--port",
        default=DEFAULT_PORT,
        type=int,
        help="CodeProject.AI Server Port",
    )

    parser.add_argument(
        "--images_folder",
        default=DEFAULT_IMAGES_FOLDER,
        type=str,
        help="The folder of images to test (only jpg will be tested)",
    )
    args = parser.parse_args()

    images = list(Path(args.images_folder).rglob("*.jpg"))
    print(f"Processing {len(images)} images in folder {args.images_folder}")

    total_predictions = 0  # keep predictions
    start_time = time.time()

    for i, img_path in enumerate(images):
        with open(img_path, "rb") as image_bytes:
            response = requests.post(opts.endpoint("vision/detection"),
                                     files={"image": image_bytes}).json()

            predictions = response["predictions"]
            if (predictions == None):
                predictions = []

        total_predictions = total_predictions + len(predictions)
        print(f"Processed image {i} : {img_path}, {len(predictions)} predictions")

    end_time = time.time()
    duration = end_time - start_time

    print(f"Completes in {round(duration, 2)} seconds, {total_predictions} predictions, {round(total_predictions/duration,1)} ops/sec")


if __name__ == "__main__":
    main()
