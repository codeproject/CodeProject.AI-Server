#!/usr/bin/env python
# coding: utf-8

# Import our general libraries
import sys
import traceback

# Import the CodeProject.AI SDK. This will add to the PATH vaar for future imports
sys.path.append("../SDK/Python")
from codeprojectai import CodeProjectAIRunner
from analysislogging import LogMethod
from requestdata import AIRequestData
from common import JSON

# Import libraries needed
import os
from PIL import Image

# Import the method of the module we're wrapping
from superresolution import superresolution, load_pretrained_weights


def main():

    # create a CodeProject.AI module object
    module_runner = CodeProjectAIRunner("superresolution_queue")

    # Hack for debug mode
    if module_runner.module_id == "CodeProject.AI":
        module_runner.module_id   = "SuperResolution"
        module_runner.module_name = "Super Resolution"

    # Setup the module
    assets_path = os.path.normpath(os.path.join(os.path.dirname(__file__), "assets/"))
    load_pretrained_weights(assets_path)

    # Start the module
    module_runner.start_loop(superresolution_callback)


def superresolution_callback(module_runner: CodeProjectAIRunner, data: AIRequestData) -> JSON:

    try:
        img: Image     = data.get_image(0)
        out_img: Image = superresolution(img)

        return {"success": True, "imageBase64": data.encode_image(out_img)}

    except Exception:
        err_trace = traceback.format_exc()
        module_runner.log(LogMethod.Error | LogMethod.Cloud | LogMethod.Server,
                    {
                        "filename": "superres_adapter.py",
                        "method": "superresolution",
                        "loglevel": "error",
                        "message": err_trace,
                        "exception_type": "Exception"
                    })

        return {"success": False, "error": "unable to process the image", "code": 500}


if __name__ == "__main__":
    main()
