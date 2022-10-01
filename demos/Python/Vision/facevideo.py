# from https://github.com/DeepQuestAI/DeepStackExamples

import cv2
import requests
from options import Options

def main():

    opts = Options()
    cap = cv2.VideoCapture(0)

    frame_index = 0
    predictions = {}
    skip_frame  = 5

    while cap.isOpened():

        if cv2.waitKey(1) & 0xFF == ord('q'):
            break

        valid, frame = cap.read()            
        if not valid:
            break;

        frame_index += 1
        
        if skip_frame > 1:
            if frame_index % skip_frame != 0:
               continue;

        retval, new_frame = cv2.imencode('.jpg', frame)

        response = requests.post(opts.endpoint("vision/face"),
                                 files={"image":new_frame}).json()

        predictions = response['predictions']

        print(f"Frame {frame_index}: {len(predictions)} predictions")

        num_prediction_json = len(predictions)

        for i in range(num_prediction_json):
            red, green, blue = 200, 100, 200
            frame = cv2.rectangle(frame, 
                                  (predictions[i]['x_min'], predictions[i]['y_min']),
                                  (predictions[i]['x_max'], predictions[i]['y_max']), 
                                  (red, green, blue), 1)
        
        cv2.imshow('Image Viewer', frame)
        
    cap.release()
    cv2.destroyAllWindows()

if __name__ == "__main__":
    main()