#!/usr/bin/env python
# coding: utf-8

from senseAI import SenseAIBackend
from summarize import Summarize

import json
import traceback


senseAI = SenseAIBackend()

def textsummary(thread_name):

    TEXT_QUEUE = "summary_queue"
    summary = Summarize()

    while True:
        queue = senseAI.getCommand(TEXT_QUEUE);

        if len(queue) > 0:           
            timer = senseAI.startTimer("Text Summary")

            for req_data in queue:

                req_data = json.JSONDecoder().decode(req_data)
                # file_id     = req_data["fileid"]
                req_text      = req_data["text"]
                num_sentences = int(req_data["numsentences"])
                req_id        = req_data["reqid"]
                req_type      = req_data["reqtype"]

                # file_path = os.path.join(TEMP_PATH, file_id)

                output = {}

                try:
                    # summaryText = summary.generate_summary_from_file(file_path, num_sentences)
                    # os.remove(file_path)

                    #print("Will summarize: ", req_text);
                    summaryText = summary.generate_summary_from_text(req_text, num_sentences)                   

                    output = {"success": True, "summary": summaryText}

                except Exception:
                    err_trace = traceback.format_exc()
                    senseAI.log(err_trace, is_error=True)

                    output = {"success": False, "error": "unable to summarize", "code": 500}

                    senseAI.errLog("textsummary", "summarize.py", err_trace, "Exception")

                finally:
                    senseAI.endTimer(timer)

                    try:
                        senseAI.sendResponse(req_id, json.dumps(output))
                    except Exception:
                        print("An exception occured")
    
                    # if os.path.exists(file_path):
                    #    os.remove(file_path)


if __name__ == "__main__":
    senseAI.log("Text Summary module started.")
    textsummary("main_textsummary")