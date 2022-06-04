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

import sys

sys.path.append("../SDK/Python")
from senseAI import SenseAIBackend, LogMethod

from rembg.bg import remove

import base64
from io import BytesIO
import json
import traceback


senseAI = SenseAIBackend()

def remove_background(thread_name):

    QUEUE_NAME = "removebackground_queue"

    while True:
        queue_entries: list = senseAI.getCommand(QUEUE_NAME)

        if len(queue_entries) > 0:
            timer: tuple = senseAI.startTimer("Remove Background")

            for queue_entry in queue_entries:

                req_data: dict = json.JSONDecoder().decode(queue_entry)

                req_id: str            = req_data.get("reqid", "")
                req_type: str          = req_data.get("reqtype", "")
                use_alphamatting: bool = senseAI.getRequestValue(req_data, "use_alphamatting", "false") == "true"

                output: any = {}

                try:
                    img = senseAI.getImageFromRequest(req_data, 0)

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

                    senseAI.log(LogMethod.Error | LogMethod.Cloud | LogMethod.Server,
                               { "process": "removebackground",
                                 "file": "sense_rembg_adapter.py",
                                 "method": "remove_background",
                                 "message": err_trace,
                                 "exception_type": "Exception"})

                finally:
                    senseAI.endTimer(timer)

                    try:
                        senseAI.sendResponse(req_id, json.dumps(output))
                    except Exception:
                        print("An exception occured")

if __name__ == "__main__":
    senseAI.log(LogMethod.Info | LogMethod.Server, {"message":"RemoveBackground module started."})
    remove_background("main_removebackground")
