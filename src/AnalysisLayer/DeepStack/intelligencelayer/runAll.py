import os
import threading
from shared import SharedOptions
import scene
import detection
import face

if __name__ == "__main__":
    face_enabled      = os.getenv("VISION-FACE", False)
    detection_enabled = os.getenv("VISION-DETECTION", False)
    scene_enabled     = os.getenv("VISION-SCENE", False)

    thread_to_wait = None

    if face_enabled :
        face.init_db()
        faceupdate_thread = threading.Thread(None, face.update_faces,         args = ("", 1))
        face_thread       = threading.Thread(None, face.face,                 args = ("", SharedOptions.SLEEP_TIME))
        faceupdate_thread.start()
        face_thread.start()
        thread_to_wait = face_thread

    if detection_enabled :
        detect_thread     = threading.Thread(None, detection.objectdetection, args = ("", SharedOptions.SLEEP_TIME))
        detect_thread.start()
        thread_to_wait = detect_thread

    if scene_enabled :
        scene_thread      = threading.Thread(None, scene.scenerecognition,    args = ("", SharedOptions.SLEEP_TIME))
        scene_thread.start()
        thread_to_wait = scene_thread

    if thread_to_wait != None :
        thread_to_wait.join();