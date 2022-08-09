# Import our general libraries
import os
import sys
import traceback

# Import the CodeProject.AI SDK. This will add to the PATH vaar for future imports
sys.path.append("../../SDK/Python")
from common import JSON
from codeprojectai import CodeProjectAIRunner
from requestdata import AIRequestData
from analysislogging import LogMethod

from shared import SharedOptions

# Set the path based on Deepstack's settings so CPU / GPU packages can be correctly loaded
sys.path.insert(0, os.path.join(os.path.dirname(os.path.realpath(__file__)), "."))
sys.path.append(os.path.join(SharedOptions.APPDIR, SharedOptions.SETTINGS.PLATFORM_PKGS))

# Import libraries from the Python VENV using the correct packages dir
from PIL import UnidentifiedImageError, Image
import torch
import torch.nn.functional as F
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


classes = list()
with open(os.path.join(SharedOptions.SHARED_APP_DIR, "categories_places365.txt")) as class_file:
    for line in class_file:
        classes.append(line.strip().split(" ")[0][3:])

placesnames = tuple(classes)

classifier = SceneModel(os.path.join(SharedOptions.SHARED_APP_DIR, "scene.pt"), 
                        SharedOptions.CUDA_MODE)


def main():

    # create a CodeProject.AI module object
    module_runner = CodeProjectAIRunner("scene_queue")

    # Hack for debug mode
    if module_runner.module_id == "CodeProject.AI":
        module_runner.module_id   = "SceneClassification"
        module_runner.module_name = "Scene Classification"

    if SharedOptions.CUDA_MODE:
        module_runner.hardware_id        = "GPU"
        module_runner.execution_provider = "CUDA"

    # Start the module
    module_runner.start_loop(sceneclassification_callback)


def sceneclassification_callback(module_runner: CodeProjectAIRunner, data: AIRequestData) -> JSON:

    img: Image = data.get_image(0)

    trans = transforms.Compose([
                transforms.Resize((256, 256)),
                transforms.CenterCrop(224),
                transforms.ToTensor(),
                transforms.Normalize([0.485, 0.456, 0.406], [0.229, 0.224, 0.225]),
            ])

    img = trans(img).unsqueeze(0)
                    
    try:
        cl, conf = classifier.predict(img)

        cl   = placesnames[cl]
        conf = float(conf)

        return {"success": True, "label": cl, "confidence": conf}

    except UnidentifiedImageError:

        err_trace = traceback.format_exc()
        module_runner.log(LogMethod.Error | LogMethod.Cloud | LogMethod.Server,
                          { 
                             "filename": "scene.py",
                             "method": "sceneclassification_callback",
                             "loglevel": "error",
                             "message": err_trace, 
                             "exception_type": "UnidentifiedImageError"
                          })
        
        return { "success": False, "error": "Error occured on the server", "code": 400 }
    
    except Exception:

        err_trace = traceback.format_exc()
        module_runner.log(LogMethod.Error | LogMethod.Cloud | LogMethod.Server,
                          { 
                              "filename": "scene.py",
                              "method": "sceneclassification_callback",
                              "loglevel": "error",
                              "message": err_trace, 
                              "exception_type": "Exception"
                          })

        return { "success": False, "error": "Error occured on the server", "code": 500 }
    

if __name__ == "__main__":
    main()
