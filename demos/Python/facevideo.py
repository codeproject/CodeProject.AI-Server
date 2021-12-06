# from https://github.com/DeepQuestAI/DeepStackExamples

import numpy as np
import cv2
import requests
import demoConfig as cfg

cap = cv2.VideoCapture(0)

progress_tracker = 0
prediction_json = {}
skip_frame = 20
while(cap.isOpened()):
    valid, frame = cap.read()
    
        
    if valid == True:
        progress_tracker += 1

        if(progress_tracker % skip_frame == 0):
            retval, new_frame = cv2.imencode('.jpg', frame)
            response = requests.post(cfg.serverUrl + "vision/face",
                                     files={"image":new_frame},
                                     verify=cfg.verifySslCert).json()

            prediction_json = response['predictions']
            print(prediction_json)

        num_prediction_json = len(prediction_json)
        for i in range(num_prediction_json):
            red, green, blue = 200, 100, 200
            frame = cv2.rectangle(frame, (prediction_json[i]['x_min'], prediction_json[i]['y_min']),
                                  (prediction_json[i]['x_max'], prediction_json[i]['y_max']), (red, green, blue), 1)

        
        cv2.imshow('Image Viewer', frame)
        
        if cv2.waitKey(1) & 0xFF == ord('q'):
            break

    else:
        break

cap.release()
cv2.destroyAllWindows()
