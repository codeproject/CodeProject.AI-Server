# Import our general libraries
import os
import sys
import time
from threading import Lock

# Import the CodeProject.AI SDK. This will add to the PATH var for future imports
sys.path.append("../../SDK/Python")
from common import JSON
from request_data import RequestData
from module_runner import ModuleRunner
from module_logging import LogMethod

# Import the method of the module we're wrapping
import torch
import torchvision.transforms as transforms
from torchvision.models import resnet50
from PIL import UnidentifiedImageError, Image

from options import Options

 
class SceneModel(object):

    def __init__(self, model_path, cuda=False):

        self.cuda = cuda

        self.model = resnet50(num_classes=365)
        checkpoint = torch.load(model_path, map_location=lambda storage, loc: storage)
        state_dict = { str.replace(key, 'module.', ''): value for key,value in checkpoint['state_dict'].items() }
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


class Scene_adapter(ModuleRunner):

    def __init__(self):
        super().__init__()
        self.opts        = Options()
        self.models_lock = Lock()

        self.classes     = list()
        self.scene_names = None
        self.classifier  = None # Lazy load later on


    def initialise(self) -> None:
    
        # TODO: Read this file async
        with open(os.path.join(self.opts.models_dir, "categories_places365.txt")) as class_file:
            for line in class_file:
                self.classes.append(line.strip().split(" ")[0][3:])

        self.scene_names = tuple(self.classes)

        self.can_use_GPU = self.system_info.hasTorchCuda or self.system_info.hasTorchMPS

        if self.opts.use_CUDA:
            self.opts.use_CUDA = self.system_info.hasTorchCuda

        if not self.opts.use_CUDA:
            self.opts.use_MPS = self.system_info.hasTorchMPS

        if self.opts.use_CUDA:
            self.inference_device  = "GPU"
            self.inference_library = "CUDA"
        elif self.opts.use_MPS:
            self.inference_device  = "GPU"
            self.inference_library = "MPS"

        self._histogram = {}


    def process(self: ModuleRunner, data: RequestData) -> JSON:

        img: Image = data.get_image(0)

        start_time = time.perf_counter()

        trans = transforms.Compose([
                    transforms.Resize((256, 256)),
                    transforms.CenterCrop(224),
                    transforms.ToTensor(),
                    transforms.Normalize([0.485, 0.456, 0.406], [0.229, 0.224, 0.225]),
                ])

        img = trans(img).unsqueeze(0)
                    
        try:
            self._init_models()

            start_inference_time = time.perf_counter()
            name, conf           = self.classifier.predict(img)
            inferenceMs          = int((time.perf_counter() - start_inference_time) * 1000)

            name = self.scene_names[name]
            conf = float(conf)

            response = {
                "success": True, 
                "label": name, 
                "confidence": conf,
                "message": f"Detected scene {name}",
                "processMs" : int((time.perf_counter() - start_time) * 1000),
                "inferenceMs" : inferenceMs
            }

        except UnidentifiedImageError as img_ex:
            self.report_error(img_ex, __file__, "The image provided was of an unknown type")
            response = { "success": False, "error": "Error occurred on the server" }
    
        except Exception as ex:
            self.report_error(ex, __file__)
            response = { "success": False, "error": "Error occurred on the server" }
    
        return response


    def status(self, data: RequestData = None) -> JSON:
        status = super().status()
        status["histogram"] = self._histogram
        return status


    def update_statistics(self, response):
        super().update_statistics(response)
        if "success" in response and response["success"] and "label" in response:
            label = response["label"]
            if label not in self._histogram:
                self._histogram[label] = 1
            else:
                self._histogram[label] += 1


    def selftest(self) -> None:
        
        file_name = os.path.join("test", "beach.jpg")

        request_data = RequestData()
        request_data.queue   = self.queue_name
        request_data.command = "classify"
        request_data.add_file(file_name)
        request_data.add_value("min_confidence", 0.4)

        result = self.process(request_data)
        print(f"Info: Self-test for {self.module_id}. Success: {result['success']}")
        # print(f"Info: Self-test output for {self.module_id}: {result}")

        return { "success": result['success'], "message": "Scene classification test successful" }


    def _init_models(self, re_entered: bool = False) -> None:

        """
        For lazy loading the models
        """
        if self.classifier is not None:
            return

        try:
            with self.models_lock:
                if self.classifier is None:
                    self.classifier = SceneModel(os.path.join(self.opts.models_dir, "scene.pt"), self.opts.use_CUDA)

        except Exception as ex:
            if not re_entered and self.opts.use_CUDA and str(ex).startswith('CUDA out of memory'):

                """ Force switch to CPU-only mode """
                self.classifier    = None
                self.opts.use_CUDA = False

                self.log(LogMethod.Info | LogMethod.Server,
                { 
                    "filename": __file__,
                    "method":   sys._getframe().f_code.co_name,
                    "message": "GPU out of memory. Switching to CPU mode",
                    "loglevel": "information",
                })

                self._init_models(re_entered = True)


if __name__ == "__main__":
    Scene_adapter().start_loop()
