import os
import threading
from shared import SharedOptions
import scene
import detection
import face

if __name__ == "__main__":
    face_enabled      = os.getenv("VISION-FACE", True)
    detection_enabled = False # os.getenv("VISION-DETECTION", True)
    scene_enabled     = os.getenv("VISION-SCENE", True)

    thread_to_wait = None

    if face_enabled :
        face.main()
        # face.init_db()
        # faceupdate_thread = threading.Thread(None, face.update_faces, args = (1,))
        # face_thread       = threading.Thread(None, face.face,         args = None)
        # faceupdate_thread.start()
        # face_thread.start()
        # thread_to_wait = face_thread

    if detection_enabled :
        detection.main()
        # detect_thread     = threading.Thread(None, detection.main, args = None)
        # detect_thread.start()
        # thread_to_wait = detect_thread

    if scene_enabled :
        scene.main()
        # scene_thread      = threading.Thread(None, scene.main,    args = None)
        # scene_thread.start()
        # thread_to_wait = scene_thread

    # if thread_to_wait != None :
    #    thread_to_wait.join();