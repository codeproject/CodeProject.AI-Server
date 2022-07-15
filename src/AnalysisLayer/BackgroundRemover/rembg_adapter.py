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

#!/usr/bin/env python
# coding: utf-8

# Import the CodeProject.AI helper
import sys
sys.path.append("../SDK/Python")
from CodeProjectAI import ModuleWrapper, LogMethod

# Import the rembg method we need to call
from rembg.bg import remove

import base64
from io import BytesIO
import json
import traceback
import onnxruntime as ort

ai_module = ModuleWrapper("removebackground_queue")

useOpenVino = False

if useOpenVino:
    ## For OpenVino support
    import openvino.utils as utils
    utils.add_openvino_libs_to_path()

    ## get the first Execution Provider Name to determine GPU/CPU type
    providers = ort.get_available_providers()
    if len(providers) > 0 :
        ai_module.executionProvider = str(providers[0]).removesuffix("ExecutionProvider")
        ai_module.hardwareId        = "GPU"

def remove_background():

    # Hack for debug mode
    if ai_module.moduleId == "CodeProject.AI":
        ai_module.moduleId = "BackgroundRemoval";

    while True:
        queue_entries: list = ai_module.get_command()

        if len(queue_entries) > 0:
            timer: tuple = ai_module.start_timer("Remove Background")

            for queue_entry in queue_entries:

                req_data: dict = json.JSONDecoder().decode(queue_entry)

                req_id: str            = req_data.get("reqid", "")
                req_type: str          = req_data.get("reqtype", "")
                use_alphamatting: bool = ai_module.get_request_value(req_data, "use_alphamatting", "false") == "true"

                output: any = {}

                try:
                    img = ai_module.get_image_from_request(req_data, 0)

                    processed = remove(img, use_alphamatting)

                    buffered = BytesIO()
                    processed.save(buffered, format="PNG")
                    img_dataB64_bytes = base64.b64encode(buffered.getvalue())
                    img_dataB64 = img_dataB64_bytes.decode("ascii");

                    # img_dataB64 = base64.b64encode(processed)

                    output = {"success": True, "imageBase64": img_dataB64}

                except Exception:
                    err_trace = traceback.format_exc()

                    output = {"success": False, "error": "unable to process the image", "code": 500}

                    ai_module.log(LogMethod.Error | LogMethod.Cloud | LogMethod.Server,
                               { "process": "removebackground",
                                 "file": "rembg_adapter.py",
                                 "method": "remove_background",
                                 "message": err_trace,
                                 "exception_type": "Exception"})

                finally:
                    ai_module.end_timer(timer)

                    try:
                        ai_module.send_response(req_id, json.dumps(output))
                    except Exception:
                        print("An exception occured")

if __name__ == "__main__":
    ai_module.log(LogMethod.Info | LogMethod.Server, {"message":"RemoveBackground module started."})
    remove_background()
