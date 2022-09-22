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
import traceback

# Import the CodeProject.AI SDK. This will add to the PATH vaar for future imports
sys.path.append("../SDK/Python")
from codeprojectai import CodeProjectAIRunner
from analysislogging import LogMethod
from requestdata import AIRequestData
from common import JSON

from PIL import Image

# Import the method of the module we're wrapping
from rembg.bg import remove


def main():

    # create a CodeProject.AI module object
    module_runner = CodeProjectAIRunner("removebackground_queue", remove_background_callback)

    # Hack for debug mode
    if module_runner.module_id == "CodeProject.AI":
        module_runner.module_id   = "BackgroundRemover"
        module_runner.module_name = "Background Remover"

    # TODO: Set OMP_NUM_THREADS from Parallelism

    # Start the module
    module_runner.start_loop()


def remove_background_callback(module_runner: CodeProjectAIRunner, data: AIRequestData) -> JSON:

    try:
        img: Image             = data.get_image(0)
        use_alphamatting: bool = data.get_value("use_alphamatting", "false") == "true"

        processed_img: Image   = remove(img, use_alphamatting)

        return {"success": True, "imageBase64": data.encode_image(processed_img)}

    except Exception as ex:
        err_trace = traceback.format_exc()
        module_runner.log(LogMethod.Error | LogMethod.Cloud | LogMethod.Server,
                    {
                        "filename": "rembg_adapter.py",
                        "method": "remover_background",
                        "loglevel": "error",
                        "message": ex, # err_trace,
                        "exception_type": "Exception"
                    })

        return {"success": False, "error": "unable to process the image", "code": 500}


if __name__ == "__main__":
    main()
