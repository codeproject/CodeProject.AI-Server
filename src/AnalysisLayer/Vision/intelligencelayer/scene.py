##import _thread as thread

import sys
sys.path.append("../../SDK/Python")
from CodeProjectAI import ModuleWrapper, LogMethod # will also set the python packages path correctly
module = ModuleWrapper()

# Hack for debug mode
if module.moduleId == "CodeProject.AI":
    module.moduleId = "SceneClassification";

import ast
import io
import json
import os
import sqlite3
import time
import warnings

sys.path.insert(0, os.path.join(os.path.dirname(os.path.realpath(__file__)), "."))
from shared import SharedOptions

if SharedOptions.CUDA_MODE:
    module.hardwareId = "GPU"
    module.executionProvider = "CUDA"

# TODO: Currently doesn't exist. The Python venv is setup at install time for a single platform in
# order to reduce downloads. Having the ability to switch profiles at runtime will be added, but
# will increase downloads. Lazy loading will help, somewhat, and the infrastructure is already in
# place, though it needs to be adjusted.
sys.path.append(os.path.join(SharedOptions.APPDIR, SharedOptions.SETTINGS.PLATFORM_PKGS))

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
        queue = module.get_command(IMAGE_QUEUE);

        if len(queue) > 0:           
            timer = module.start_timer("Scene Classification")

            for req_data in queue:
                req_data = json.JSONDecoder().decode(req_data)
                req_id   = req_data["reqid"]
                req_type = req_data["reqtype"]
                #img_id   = req_data["imgid"]
                #img_path = os.path.join(SharedOptions.TEMP_PATH,img_id)

                payload     = req_data["payload"]
                files       = payload["files"]
                img_file    = files[0]

                try:
                    img = module.get_image_from_request(req_data, 0)

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
                    
                    #os.remove(img_path)

                    cl, conf = classifier.predict(img)

                    cl = placesnames[cl]

                    conf = float(conf)

                    output = {"success": True, "label": cl, "confidence": conf}

                except UnidentifiedImageError:
                    err_trace = traceback.format_exc()

                    output = {
                        "success": False,
                        "error": "error occured on the server",
                        "code": 400,
                    }
                    module.log(LogMethod.Error | LogMethod.Cloud | LogMethod.Server,
                    { 
                        "process": "scene recognize", 
                        "file": "scene.py",
                        "method": "scenerecognition",
                        "message": err_trace, 
                        "exception_type": "UnidentifiedImageError"
                    })

                except Exception:
                    err_trace = traceback.format_exc()

                    output = {"success": False, "error": "invalid image", "code": 500}

                    module.log(LogMethod.Error | LogMethod.Cloud | LogMethod.Server,
                    { 
                        "process": "scene recognize", 
                        "file": "scene.py",
                        "method": "scenerecognition",
                        "message": err_trace,
                        "exception_type": "Exception"
                    })

                finally:
                    module.end_timer(timer)
                    module.send_response(req_id, json.dumps(output))

                    #if os.path.exists(img_path):
                    #    os.remove(img_path)

        # time.sleep(delay)


if __name__ == "__main__":
    module.log(LogMethod.Info | LogMethod.Server, {"message": "Scene Detection module started."})
    scenerecognition("", SharedOptions.SLEEP_TIME)
