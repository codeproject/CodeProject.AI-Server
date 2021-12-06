import time

import torch
import torchvision.transforms as transforms
from PIL import Image

from .networks import Backbone, MobileFaceNet


def load_model(model, path):
    checkpoint = torch.load(path, map_location=lambda storage, loc: storage)

    try:
        model.load_state_dict(checkpoint)

    except:
        copy = dict()
        for x, y in zip(model.state_dict(), checkpoint):
            new_name = y[y.index(x) :]
            copy[new_name] = checkpoint[y]


class FaceRecognitionModel(object):
    def __init__(self, model_path, emb_size=512, cuda=False):
        self.cuda_mode = False
        # self.model = MobileFaceNet(emb_size)
        self.model = Backbone(50, 0.6, "ir_se")
        load_model(self.model, model_path)

        if torch.cuda.is_available() and cuda:
            self.model = self.model.cuda()
            self.cuda_mode = True

        self.model.eval()

    def predict(self, image_tensors):

        if self.cuda_mode:
            image_tensors = image_tensors.cuda()

        outputs = self.model(image_tensors)

        return outputs
