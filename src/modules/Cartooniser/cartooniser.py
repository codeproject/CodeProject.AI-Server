import os
import time
from typing import Tuple

from PIL import Image
import torch
from torchvision.transforms.functional import to_tensor, to_pil_image

# from .hubconf import face2paint
from model import Generator

# global

# face paint 1 => high style, low robustness
# face paint 2 => high robustness, low style
known_models = [ 'face_paint_512_v1', 'face_paint_512_v2', 'celeba_distill', 'paprika' ]
models = { name: ( None, None ) for name in known_models } # (path , model)


def get_model(weights_dir, model_name, device_type="cpu"):

    (path, loaded_model) = models.get(model_name)

    if loaded_model is None:
        print(f"Loading model {model_name}")

        # Create model object
        device = torch.device(device_type)
        loaded_model = Generator().to(device)

        # Load model weights
        model_path = os.path.join(weights_dir, model_name + ".pt")
        # state_dict = torch.hub.load_state_dict_from_url(model_path, map_location=device, progress=True, check_hash=True,)
        state_dict = torch.load(model_path, map_location=device)
        loaded_model.load_state_dict(state_dict)

        # Store path and loaded model for later
        models[model_name] = (model_path, loaded_model)
    else:
        print(f"Debug: Using cached model {model_name}")

    return loaded_model


def face2paint(model: torch.nn.Module, img: Image.Image, size: int = 512,
               side_by_side: bool = False, device_type: str = "cpu") -> Tuple[Image.Image, int]:

        w, h = img.size
        s = min(w, h)

        img = img.crop(((w - s) // 2, (h - s) // 2, (w + s) // 2, (h + s) // 2))
        img = img.resize((size, size), Image.LANCZOS)

        with torch.no_grad():
            input = to_tensor(img).unsqueeze(0) * 2 - 1

            start_time = time.perf_counter()
            output = model(input.to(device_type)).cpu()[0]
            inference_time: int = int((time.perf_counter() - start_time) * 1000)

            if side_by_side:
                output = torch.cat([input[0], output], dim=2)

            output = (output * 0.5 + 0.5).clip(0, 1)

        return to_pil_image(output), inference_time

def inference(img: Image, weights_dir: str = "weights", model_name: str ="face_paint_512_v2",
              device_type: str = "cpu") -> Tuple[Image.Image, int]:

    if device_type == "cuda" and not torch.cuda.is_available():
        device_type = "cpu"

    loaded_model = get_model(weights_dir, model_name, device_type)
    (output, inference_time) = face2paint(loaded_model, img, device_type = device_type)
    return (output, inference_time)

if __name__ == "__main__":
    pass
