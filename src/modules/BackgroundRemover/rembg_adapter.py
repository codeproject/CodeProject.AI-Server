#!/usr/bin/env python
# coding: utf-8

"""
To call:

    def remove(data: Union[PILImage                                     # the image
               alpha_matting: bool = False,                             # handy for fuzzy boundaries
               alpha_matting_foreground_threshold: int = 240,
               alpha_matting_background_threshold: int = 10,
               alpha_matting_erode_size: int = 10,
               session: Optional[BaseSession] = None| new_session(model)
               only_mask: bool = False,                                 # return only the mask
    ) -> Union[bytes, PILImage, np.ndarray]:

    model can be: 'u2net', 'u2netp', 'u2net_human_seg', 'u2net_cloth_seg'

""" 

# Import our general libraries
import sys
import time

# Import the CodeProject.AI SDK. This will add to the PATH var for future imports
sys.path.append("../../SDK/Python")
from request_data import RequestData
from module_runner import ModuleRunner
from common import JSON

from PIL import Image

# Import the method of the module we're wrapping
from rembg.bg import remove

class rembg_adapter(ModuleRunner):

    def initialise(self) -> None:   
        if self.support_GPU:
            if self.hasONNXRuntimeGPU:
                self.execution_provider = "ONNX"

    def process(self, data: RequestData) -> JSON:
        try:
            img: Image             = data.get_image(0)
            use_alphamatting: bool = data.get_value("use_alphamatting", "false") == "true"

            start_time = time.perf_counter()
            (processed_img, inferenceTime) = remove(img, use_alphamatting)

            return { 
                "success": True, 
                "imageBase64": data.encode_image(processed_img),
                "processMs" : int((time.perf_counter() - start_time) * 1000),
                "inferenceMs" : inferenceTime
            }

        except Exception as ex:
            self.report_error(ex, __file__)
            return {"success": False, "error": "unable to process the image"}

    def shutdown(self) -> None:
        pass

if __name__ == "__main__":
    rembg_adapter().start_loop()
