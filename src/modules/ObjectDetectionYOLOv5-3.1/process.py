
import time
from typing import Tuple
import cv2
import numpy as np

# https://github.com/pytorch/pytorch/issues/7082#issuecomment-483090607
try:
    import os
    del os.environ['MKL_NUM_THREADS']
except: pass
import torch

from models.experimental import attempt_load
from PIL import Image
from utils.datasets import LoadImages, LoadStreams, letterbox
from options import Options

from utils.general import (
    non_max_suppression,
    scale_coords,
)

def init_detect(opts: Options):

    if opts.use_CUDA:
        try:
            opts.use_CUDA = torch.cuda.is_available()
        except:
            print("Unable to test for CUDA support: " + str(ex))
            opts.use_CUDA = False

    try:
        import cpuinfo
        cpu_brand = cpuinfo.get_cpu_info().get('brand_raw')
        if cpu_brand and cpu_brand.startswith("Apple M"):
            opts.use_MPS = hasattr(torch.backends, "mps") and torch.backends.mps.is_available()
    except Exception as ex:
        print("Unable to import test for Apple Silicon: " + str(ex))


class YOLODetector(object):

    def __init__(self, model_path: str, reso: int = 640, cuda: bool = False, 
                 accel_device_name: str = 0, mps: bool = False,
                 half_precision: str = 'enable'):

        if cuda:
            self.device_type = "cuda"

            # accel_device_name = "cuda:1" - for testing

            if accel_device_name: # should be "cuda:N" where N < # devices
                self.device = torch.device(accel_device_name)
            else:
                self.device = torch.device("cuda") # = cuda:0

            device_name = torch.cuda.get_device_name(self.device)

            print(f"GPU compute capability is {torch.cuda.get_device_capability()[0]}.{torch.cuda.get_device_capability()[1]}")
            
            self.half = half_precision != 'disable'

            if self.half:
                print(f"Using half-precision for the device '{device_name}'")
            else:
                print(f"Not using half-precision for the device '{device_name}'")

        elif mps:
            self.device_type = "mps"
            self.device = torch.device(self.device_type)
            device_name = "Apple Silicon GPU"
            self.half   = False

        else:
            self.device_type = "cpu"
            device_name = "CPU"
            self.device = torch.device(self.device_type)
            self.half   = False

        print(f"Inference processing will occur on device '{device_name}'")

        self.reso = (reso, reso)
        self.cuda = cuda
        self.model = attempt_load(model_path, map_location=self.device)
        self.names = (
            self.model.module.names
            if hasattr(self.model, "module")
            else self.model.names
        )

        # Fix for issue "AttributeError: 'Upsample' object has no attribute 'recompute_scale_factor'"
        # https://github.com/ultralytics/yolov5/issues/6948#issuecomment-1519070669
        import torch.nn as nn
        for m in self.model.modules():
            if isinstance(m, nn.Upsample):
                m.recompute_scale_factor = None

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
