
import numpy as np
import torch
from models.experimental import attempt_load
from PIL import Image
from utils.datasets import letterbox
from utils.general import (
    non_max_suppression,
    scale_coords,
)

class YOLODetector(object):
    def __init__(self, model_path: str, reso: int = 640, cuda: bool = False):

        self.device = torch.device("cuda:0" if cuda else "cpu")
        self.half = self.device.type != "cpu"

        self.reso = (reso, reso)
        self.cuda = cuda
        self.model = attempt_load(model_path, map_location=self.device)
        self.names = (
            self.model.module.names
            if hasattr(self.model, "module")
            else self.model.names
        )
        if self.half:
            self.model.half()

    def predict(self, img_path: str, confidence: float = 0.4):

        confidence = max(0.1,confidence)

        img0 = Image.open(img_path).convert("RGB")

        return self.predictFromImage(img0, confidence)


    def predictFromImage(self, img0: Image, confidence: float = 0.4):
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

        pred = self.model(img, augment=False)[0]
        pred = non_max_suppression(
            pred, confidence, 0.45, classes=None, agnostic=False
        )[0]

        if pred is None:
            pred = []
        else:
            # Rescale boxes from img_size to im0 size
            pred[:, :4] = scale_coords(img.shape[2:], pred[:, :4], img0.shape).round()

        return pred
