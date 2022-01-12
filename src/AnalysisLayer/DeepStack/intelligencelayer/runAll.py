import threading
from shared import SharedOptions
import scene
import detection
import face

if __name__ == "__main__":
    detect_thread     = threading.Thread(None, detection.objectdetection, args = ("", SharedOptions.SLEEP_TIME))
    scene_thread      = threading.Thread(None, scene.scenerecognition,    args = ("", SharedOptions.SLEEP_TIME))

    face.init_db()
    faceupdate_thread = threading.Thread(None, face.update_faces,         args = ("", 1))
    face_thread       = threading.Thread(None, face.face,                 args = ("", SharedOptions.SLEEP_TIME))

    detect_thread.start()
    scene_thread.start()
    faceupdate_thread.start()
    face_thread.start()

    face_thread.join()