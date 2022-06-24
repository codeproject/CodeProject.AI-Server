"""
Script to time how long it takes the DeepStack modules to process a set of images. 
The purpose is to allow comparison of speed on different hosts. We also count the
total number of predictions as a quality check.

Example run: python3 benchmark.py --images_folder /Users/my/images
"""
import argparse
import time
from pathlib import Path

import requests
import demoConfig as cfg

# deepstack credentials
DEFAULT_IP_ADDRESS = cfg.serverHost
DEFAULT_PORT       = cfg.serverPort
DEFAULT_API_KEY    = ""
DEFAULT_IMAGES_FOLDER = cfg.imageDir


def main():
    parser = argparse.ArgumentParser(description="Perform benchmarking of DeepStack modules in CodeProject.AI")
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
        "--api_key", default=DEFAULT_API_KEY, type=str, help="Deepstack API key"
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
            response = requests.post(
                cfg.serverUrl + "vision/detection",
                files={"image": image_bytes},
                data={"api_key": args.api_key},
            )

        predictions = response.json()["predictions"]
        if (predictions == None):
            predictions = []

        total_predictions = total_predictions + len(predictions)
        print(
            f"Processed image number {i} : {str(img_path)}, {len(predictions)} predictions"
        )

    end_time = time.time()
    duration = end_time - start_time
    print(
        f"Processing completes in {round(duration, 5)} seconds, total of {total_predictions} predictions"
    )


if __name__ == "__main__":
    main()
