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

# Import the CodeProject.AI SDK. This will add to the PATH var for
# future imports
sys.path.append("../../SDK/Python")
from request_data import RequestData
from module_runner import ModuleRunner
from common import JSON

# Import the method of the module we're wrapping
from PIL import Image

# Import the method of the module we're wrapping
from rembg.bg import remove

class rembg_adapter(ModuleRunner):

    def initialise(self) -> None:   
        """ Initialises the module """

        self.selftest_check_pkgs = False # Too messy, will fail

        if self.enable_GPU and self.system_info.hasONNXRuntimeGPU:
            self.inference_device  = "GPU"
            self.inference_library = "ONNX"


    def process(self, data: RequestData) -> JSON:
        """ Processes a request from the client and returns the results"""
        try:
            img: Image             = data.get_image(0)
            use_alphamatting: bool = data.get_value("use_alphamatting", "false") == "true"

            # Make the call to the AI code we're wrapping, and time it
            start_time = time.perf_counter()
            (processed_img, inferenceTime) = remove(img, use_alphamatting)
            processMs = int((time.perf_counter() - start_time) * 1000)

            response = { 
                "success":      True, 
                "imageBase64":  RequestData.encode_image(processed_img),
                "processMs" :   processMs,
                "inferenceMs" : inferenceTime
            }
        
        except Exception as ex:
            self.report_error(ex, __file__)
            response = { "success": False, "error": "unable to process the image" }

        return response 


    def selftest(self) -> JSON:
        
        import os
        os.environ["U2NET_HOME"] = os.path.join(self.module_path, "models")
        file_name = os.path.join("test", "chris-hemsworth-2.jpg")

        request_data = RequestData()
        request_data.queue   = self.queue_name
        request_data.command = "removebackground"
        request_data.add_file(file_name)
        request_data.add_value("use_alphamatting", "true")

        result = self.process(request_data)
        print(f"Info: Self-test for {self.module_id}. Success: {result['success']}")
        # print(f"Info: Self-test output for {self.module_id}: {result}")

        return { "success": result['success'], "message": "Remove background test successful" }
 

if __name__ == "__main__":
    rembg_adapter().start_loop()
