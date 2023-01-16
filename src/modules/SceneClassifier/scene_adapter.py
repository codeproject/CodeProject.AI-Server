# Import our general libraries
import os
import sys
import time
import traceback

# Import the CodeProject.AI SDK. This will add to the PATH vaar for future imports
sys.path.append("../../SDK/Python")
from common import JSON
from analysis.codeprojectai import CodeProjectAIRunner
from analysis.requestdata import AIRequestData
from analysis.analysislogging import LogMethod

# Import the method of the module we're wrapping
from options import Options
from PIL import UnidentifiedImageError, Image
from threading import Lock

import torch
import torchvision.transforms as transforms
from torchvision.models import resnet50

# Thread locking
models_lock = Lock()

   
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


# These will be populated in the init callback
classes     = list()
placesnames = None
classifier  = None # Lazy load later on


def main():

    # create a CodeProject.AI module object
    module_runner = CodeProjectAIRunner("scene_queue", scene_classification_callback,
                                        scene_classification_init_callback)

    # Hack for debug mode
    if module_runner.module_id == "CodeProject.AI":
        module_runner.module_id   = "SceneClassification"
        module_runner.module_name = "Scene Classification"
        os.environ["PYTORCH_ENABLE_MPS_FALLBACK"] = "1"     # For PyTorch on Apple silicon

    if Options.use_CUDA and module_runner.support_GPU:
        module_runner.execution_provider = "CUDA"
    elif Options.use_MPS and module_runner.support_GPU:
        module_runner.execution_provider = "MPS"

    # Start the module
    module_runner.start_loop()


def scene_classification_init_callback(module_runner: CodeProjectAIRunner) -> None:
    
    global classes
    global placesnames

    with open(os.path.join(Options.models_dir, "categories_places365.txt")) as class_file:
        for line in class_file:
            classes.append(line.strip().split(" ")[0][3:])

    placesnames = tuple(classes)


def init_models() -> None:

    """
    For lazy loading the models
    """
    global classifier
    if classifier is not None:
        return

    with models_lock:
        if classifier is None:
            classifier = SceneModel(os.path.join(Options.models_dir, "scene.pt"), 
                                    Options.use_CUDA)

def scene_classification_callback(module_runner: CodeProjectAIRunner, data: AIRequestData) -> JSON:

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

        init_models()

        start_inference_time = time.perf_counter()
        cl, conf = classifier.predict(img)
        inferenceMs = int((time.perf_counter() - start_inference_time) * 1000)

        cl   = placesnames[cl]
        conf = float(conf)

        return {
            "success": True, 
            "label": cl, 
            "confidence": conf,
            "processMs" : int((time.perf_counter() - start_time) * 1000),
            "inferenceMs" : inferenceMs,
            "code": 200
        }

    except UnidentifiedImageError:
        # message = "".join(traceback.TracebackException.from_exception(ex).format())
        message = "The image provided was of an unknown type"
        module_runner.log(LogMethod.Error | LogMethod.Server,
                          { 
                             "filename": "scene_adapter.py",
                             "method": "sceneclassification_callback",
                             "loglevel": "error",
                             "message": message,
                             "exception_type": "UnidentifiedImageError"
                          })
        
        return { "success": False, "error": "Error occured on the server", "code": 400 }
    
    except Exception as ex:
        # message = str(ex) or f"A {ex.__class__.__name__} error occurred"
        message = "".join(traceback.TracebackException.from_exception(ex).format())
        module_runner.log(LogMethod.Error | LogMethod.Server,
                          { 
                              "filename": "scene_adapter.py",
                              "method": "sceneclassification_callback",
                              "loglevel": "error",
                              "message": message,
                              "exception_type": "Exception"
                          })

        return { "success": False, "error": "Error occured on the server", "code": 500 }
    

if __name__ == "__main__":
    main()
