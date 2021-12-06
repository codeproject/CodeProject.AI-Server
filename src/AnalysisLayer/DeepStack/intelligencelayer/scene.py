import _thread as thread
import ast
import io
import json
import os
import sqlite3
import sys
import time
import warnings

sys.path.insert(0, os.path.join(os.path.dirname(os.path.realpath(__file__)), "."))
from shared import SharedOptions, getCommand, sendResponse
if SharedOptions.PROFILE == "windows_native":
    sys.path.append(os.path.join(SharedOptions.APP_DIR,"python_packages"))

import numpy as np
import torch
import torch.nn.functional as F
from PIL import Image, UnidentifiedImageError

import traceback

import torchvision.transforms as transforms
from torchvision.models import resnet50

class SceneModel(object):
    def __init__(self, model_path, cuda=False):

        self.cuda = cuda

        self.model = resnet50(num_classes=365)
        checkpoint = torch.load(model_path, map_location=lambda storage, loc: storage)
        state_dict = {str.replace(k,'module.',''): v for k,v in checkpoint['state_dict'].items()}
        self.model.load_state_dict(state_dict)
        self.model.eval()
        if self.cuda:
            self.model = self.model.cuda()

    def predict(self, image_tensors):

        if self.cuda:
            image_tensors = image_tensors.cuda()

        logit = self.model.forward(image_tensors)
        out = torch.softmax(logit, 1)

        return out.argmax(), out.max().item()


def scenerecognition(thread_name, delay):

    classes = list()
    with open(
        os.path.join(SharedOptions.SHARED_APP_DIR, "categories_places365.txt")
    ) as class_file:
        for line in class_file:
            classes.append(line.strip().split(" ")[0][3:])

    placesnames = tuple(classes)

    IMAGE_QUEUE = "scene_queue"
    classifier = SceneModel(
        os.path.join(SharedOptions.SHARED_APP_DIR, "scene.pt"),
        SharedOptions.CUDA_MODE,
    )

    while True:
        queue = getCommand(IMAGE_QUEUE);

        if len(queue) > 0:

            for req_data in queue:
                req_data = json.JSONDecoder().decode(req_data)
                img_id = req_data["imgid"]
                req_id = req_data["reqid"]
                req_type = req_data["reqtype"]
                img_path = os.path.join(SharedOptions.TEMP_PATH,img_id)
                try:

                    img = Image.open(img_path).convert("RGB")

                    trans = transforms.Compose(
                        [
                            transforms.Resize((256, 256)),
                            transforms.CenterCrop(224),
                            transforms.ToTensor(),
                            transforms.Normalize(
                                [0.485, 0.456, 0.406], [0.229, 0.224, 0.225]
                            ),
                        ]
                    )
                    img = trans(img).unsqueeze(0)
                    
                    os.remove(img_path)

                    cl, conf = classifier.predict(img)

                    cl = placesnames[cl]

                    conf = float(conf)

                    output = {"success": True, "label": cl, "confidence": conf}

                except UnidentifiedImageError:
                    err_trace = traceback.format_exc()
                    print(err_trace, file=sys.stderr, flush=True)

                    output = {
                        "success": False,
                        "error": "error occured on the server",
                        "code": 400,
                    }

                except Exception:

                    err_trace = traceback.format_exc()
                    print(err_trace, file=sys.stderr, flush=True)

                    output = {"success": False, "error": "invalid image", "code": 500}

                finally:
                    sendResponse(req_id, json.dumps(output))
                    if os.path.exists(img_path):
                        os.remove(img_path)

        # time.sleep(delay)


if __name__ == "__main__":
    print("Starting scene.py")
    scenerecognition("", SharedOptions.SLEEP_TIME)
