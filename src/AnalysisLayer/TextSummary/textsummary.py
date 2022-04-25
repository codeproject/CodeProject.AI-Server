#!/usr/bin/env python
# coding: utf-8

from senseAI import SenseAIBackend, LogMethod
from summarize import Summarize

import json
import traceback


senseAI = SenseAIBackend()

def textsummary(thread_name):

    TEXT_QUEUE = "summary_queue"
    summary = Summarize()

    while True:
        queue_entries: list[str] = senseAI.getCommand(TEXT_QUEUE);

        if len(queue_entries) > 0:           
            timer: tuple = senseAI.startTimer("Text Summary")

            for queue_entry in queue_entries:

                req_data: dict = json.JSONDecoder().decode(queue_entry)

                # If we're passing a file by Id (path)
                # file_id     = req_data.get("fileid", "")
                # file_path   = os.path.join(TEMP_PATH, file_id)

                # If we're passing a file itself
                # payload     = req_data.get("payload", None)
                # files       = payload.get("files", None)
                # text_file   = files[0]

                req_id: str        = req_data.get("reqid", "")
                req_type: str      = req_data.get("reqtype", "")
                req_text: str      = senseAI.getRequestValue(req_data, "text")
                num_sentences: int = int(senseAI.getRequestValue(req_data, "num_sentences"))

                output: any = {}

                try:
                    # If we're passing a file by Id (path)
                    # summaryText = summary.generate_summary_from_file(file_path, num_sentences)

                    # If we're passing a file itself (generate_summary_from_textfile to be added)
                    # summaryText = summary.generate_summary_from_textfile(text_file, num_sentences)                   

                    #print("Will summarize the text: ", req_text);
                    summaryText: str = summary.generate_summary_from_text(req_text, num_sentences)                   

                    output = {"success": True, "summary": summaryText}

                except Exception:
                    err_trace = traceback.format_exc()

                    output = {"success": False, "error": "unable to summarize", "code": 500}

                    senseAI.log(LogMethod.Error | LogMethod.Cloud | LogMethod.Server,
                               { "process": "textsummary", 
                                 "file": "textsummary.py",
                                 "method": "textsummary",
                                 "message": err_trace, 
                                 "exception_type": "Exception"})

                finally:
                    senseAI.endTimer(timer)

                    try:
                        senseAI.sendResponse(req_id, json.dumps(output))
                    except Exception:
                        print("An exception occured")
    
                    # if os.path.exists(file_path):
                    #    os.remove(file_path)


if __name__ == "__main__":
    senseAI.log(LogMethod.Info | LogMethod.Server, {"message":"TextSummary module started."})
    textsummary("main_textsummary")