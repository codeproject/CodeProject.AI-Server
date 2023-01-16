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

from options import Options
opts = Options()

from PIL import Image

# Import the method of the module we're wrapping
from OCR import read_text


def main():

    # create a CodeProject.AI module object
    module_runner = CodeProjectAIRunner("OCR_queue", ocr_detect_callback, ocr_init_callback)

    # Hack for debug mode
    if module_runner.module_id == "CodeProject.AI":
        module_runner.module_id   = "OCR"
        module_runner.module_name = "OCR"

    if opts.use_GPU:
        module_runner.hardware_type      = "GPU"
        module_runner.execution_provider = "CUDA"   # PaddleOCR supports only CUDA enabled GPUs at this point

    # Start the module
    module_runner.start_loop()


def ocr_init_callback(module_runner: CodeProjectAIRunner) -> None:

    module_runner.log(LogMethod.Info | LogMethod.Server,
                        { 
                            "filename": "ocr_adapter.py",
                            "loglevel": "information",
                            "method": "ocr_init_callback",
                            "message": f"Running init for {module_runner.module_name}"
                        })
    # do other initialization here


async def ocr_detect_callback(module_runner: CodeProjectAIRunner, data: AIRequestData) -> JSON:

    try:
        image: Image = data.get_image(0)

        start_time = time.perf_counter()

        result = await read_text(module_runner, image)

        if not result:
            return {"success": False, "error": "No OCR result returned", "code": 500 }

        if "error" in result and result["error"]:
            return {"success": False, "error": result["error"], "code": 500 }

        return {
            "success": True,
            "predictions": result["predictions"],
            "processMs" : int((time.perf_counter() - start_time) * 1000),
            "inferenceMs" : result["inferenceMs"],
            "code": 200
        }

    except Exception as ex:
        message = "".join(traceback.TracebackException.from_exception(ex).format())
        module_runner.log(LogMethod.Error | LogMethod.Server,
                    {
                        "filename": "ocr_adapter.py",
                        "method": "ocr_detect_callback",
                        "loglevel": "error",
                        "message": message,
                        "exception_type": "Exception"
                    })

        return {"success": False, "error": "unable to process the image", "code": 500}


if __name__ == "__main__":
    main()
