
import time
from typing import Tuple
import cv2
import numpy as np
import torch
from models.experimental import attempt_load
from PIL import Image
from utils.datasets import LoadImages, LoadStreams, letterbox

from utils.general import (
    non_max_suppression,
    scale_coords,
)

class YOLODetector(object):
    # def __init__(self, model_path: str, reso: int = 640, cuda: bool = False):
    def __init__(self, model_path: str, reso: int = 640, cuda: bool = False, 
                 cuda_device_num: int = 0, mps: bool = False,
                 half_precision: str = 'enable'):

        if cuda:
            self.device_type = "cuda"
        elif mps:
            self.device_type = "mps"
        else:
            self.device_type = "cpu"

        self.device = torch.device("cuda:0" if cuda else "cpu")

       # Use half-precision if possible. There's a bunch of Nvidia cards where
        # this won't work
        if cuda:
            if cuda_device_num >= 0 and cuda_device_num < torch.cuda.device_count():
                self.device = torch.device(f"cuda:{cuda_device_num}")
            else:
                self.device = torch.device("cuda")

            device_name = torch.cuda.get_device_name(self.device)

            no_half = ["TU102","TU104","TU106","TU116", "TU117",
                       "GeoForce GT 1030", "GeForce GTX 1050","GeForce GTX 1060",
                       "GeForce GTX 1060","GeForce GTX 1070","GeForce GTX 1080",
                       "GeForce RTX 2060", "GeForce RTX 2070", "GeForce RTX 2080",
                       "GeForce GTX 1650", "GeForce GTX 1660", "MX550", "MX450",
                       "Quadro RTX 8000", "Quadro RTX 6000", "Quadro RTX 5000", "Quadro RTX 4000"
                       # "Quadro P1000", - this works with half!
                       "Quadro P620", "Quadro P400",
                       "T1000", "T600", "T400","T1200","T500","T2000",
                       "Tesla T4"]

            if half_precision == 'disable':
                self.half = False
            else:
                self.half = half_precision == 'force' or \
                            not any(check_name in device_name for check_name in no_half)

            if self.half:
                print(f"Using half-precision for the device '{device_name}'")
            else:
                print(f"Not using half-precision for the device '{device_name}'")
        else:
            self.device = torch.device(self.device_type)
            device_name = "Apple Silicon GPU" if mps else "CPU"
            self.half = False

        print(f"Inference processing will occur on device '{device_name}'")

        self.reso = (reso, reso)
        self.cuda = cuda
        self.model = attempt_load(model_path, map_location=self.device)
        self.names = (
            self.model.module.names
            if hasattr(self.model, "module")
            else self.model.names
        )

        # Multi-GPU untested: use all GPUs for inference
        # self.model = torch.nn.DataParallel(model) #, device_ids=[0, 1, 2])        

        if self.half:
            self.model.half()

    def predict(self, img_path: str, confidence: float = 0.4) -> Tuple[any, int]:

        confidence = max(0.1,confidence)

        img0 = Image.open(img_path)

        return self.predictFromImage(img0, confidence)


    def predictFromImage(self, img0: Image, confidence: float = 0.4) -> Tuple[any, int]:

        if img0 is None:
            return [], 0

        if (isinstance(img0, Image.Image)):
            img0 = cv2.cvtColor(np.array(img0), cv2.COLOR_RGB2BGR)
        else:
            img0 = img0.convert("RGB")

        confidence = max(0.1,confidence)

        img = np.asarray(letterbox(img0, new_shape=self.reso)[0])
        img = img.transpose(2, 0, 1)
        img = np.ascontiguousarray(img)

        img = torch.from_numpy(img).to(self.device)
        img0 = np.asarray(img0)
        img = img.half() if self.half else img.float()  # uint8 to fp16/32
        img /= 255.0  # 0 - 255 to 0.0 - 1.0
        if img.ndimension() == 3:
            img = img.unsqueeze(0)

        start_inference_time = time.perf_counter()       
        pred = self.model(img, augment=False)[0]
        inferenceMs = int((time.perf_counter() - start_inference_time) * 1000)

        pred = non_max_suppression(
            pred, confidence, 0.45, classes=None, agnostic=False
        )[0]

        if pred is None:
            pred = []
        else:
            # Rescale boxes from img_size to im0 size
            pred[:, :4] = scale_coords(img.shape[2:], pred[:, :4], img0.shape).round()

        return pred, inferenceMs
