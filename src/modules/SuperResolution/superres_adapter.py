#!/usr/bin/env python
# coding: utf-8

# Import our general libraries
import sys
import time
import traceback

# Import the CodeProject.AI SDK. This will add to the PATH vaar for future imports
sys.path.append("../../SDK/Python")
from analysis.codeprojectai import CodeProjectAIRunner
from analysis.analysislogging import LogMethod
from analysis.requestdata import AIRequestData
from common import JSON

# Import libraries needed
import os
from PIL import Image

# Import the method of the module we're wrapping
from superresolution import superresolution, load_pretrained_weights


def main():

    # create a CodeProject.AI module object
    module_runner = CodeProjectAIRunner("superresolution_queue", superresolution_callback)

    # Hack for debug mode
    if module_runner.module_id == "CodeProject.AI":
        module_runner.module_id   = "SuperResolution"
        module_runner.module_name = "Super Resolution"

    # Setup the module
    assets_path = os.path.normpath(os.path.join(os.path.dirname(__file__), "assets/"))
    load_pretrained_weights(assets_path)

    # Start the module
    module_runner.start_loop()


def superresolution_callback(module_runner: CodeProjectAIRunner, data: AIRequestData) -> JSON:

    try:
        img: Image = data.get_image(0)

        start_time = time.perf_counter()
        (out_img, inferenceMs) = superresolution(img)

        return {
            "success": True,
            "imageBase64": data.encode_image(out_img),
            "processMs" : int((time.perf_counter() - start_time) * 1000),
            "inferenceMs": inferenceMs,
            "code": 200
        }

    except Exception as ex:
        message = "".join(traceback.TracebackException.from_exception(ex).format())
        module_runner.log(LogMethod.Error | LogMethod.Server,
                    {
                        "filename": "superres_adapter.py",
                        "method": "superresolution",
                        "loglevel": "error",
                        "message": message,
                        "exception_type": "Exception"
                    })

        return {"success": False, "error": "unable to process the image", "code": 500}


if __name__ == "__main__":
    main()
