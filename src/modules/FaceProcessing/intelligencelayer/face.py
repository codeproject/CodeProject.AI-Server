# Import our general libraries
import ast
import os
import sys
import time
import traceback
import threading
from threading import Lock
from typing import Tuple

# TODO: We should move to async SQLite https://github.com/omnilib/aiosqlite and
#       make our init and process methods async.
import sqlite3

# Import the CodeProject.AI SDK. This will add to the PATH var for future imports
if os.getcwd().endswith("intelligencelayer"):
    sys.path.append("../../../SDK/Python")
else:
    sys.path.append("../../SDK/Python")
from common import JSON
from request_data import RequestData
from module_runner import ModuleRunner
from module_logging import LogMethod

# Deepstack settings
from shared import SharedOptions

# Set the path based on Deepstack's settings so CPU / GPU packages can be correctly loaded
sys.path.insert(0, os.path.join(os.path.dirname(os.path.realpath(__file__)), "."))
# Currently not using this
# sys.path.append(os.path.join(SharedOptions.APPDIR, SharedOptions.SETTINGS.PLATFORM_PKGS))

# Import libraries from the Python VENV using the correct packages dir
from PIL import UnidentifiedImageError, Image
import torch
# import cv2
import torch.nn.functional as F
import torchvision.transforms as transforms

# Inference wrappers
from process import YOLODetector
from recognition import FaceRecognitionModel

# Constants
CREATE_TABLE = "CREATE TABLE IF NOT EXISTS TB_EMBEDDINGS(userid TEXT PRIMARY KEY, embedding TEXT NOT NULL)"
ADD_FACE     = "INSERT INTO TB_EMBEDDINGS(userid,embedding) VALUES(?,?)"
UPDATE_FACE  = "UPDATE TB_EMBEDDINGS SET embedding = ? where userid = ?"
SELECT_FACE  = "SELECT * FROM TB_EMBEDDINGS where userid = ?"
SELECT_FACES = "SELECT * FROM TB_EMBEDDINGS"
LIST_FACES   = "SELECT userid FROM TB_EMBEDDINGS"
DELETE_FACE  = "DELETE FROM TB_EMBEDDINGS where userid = ?"


class Face_adapter(ModuleRunner):

    def __init__(self):
        super().__init__()
        self.master_face_map = {"map": {}}
        self.face_map_rep    = {}

        self.models_lock     = Lock()
        self.face_lock       = Lock()

        # Will be lazy initialised
        self.faceclassifier  = None
        self.detector        = None

        self.trans = transforms.Compose([
            transforms.Resize((112, 112)),
            transforms.ToTensor(),
            transforms.Normalize(mean=[0.5, 0.5, 0.5], std=[0.5, 0.5, 0.5]),
        ])

        # self.resolution = SharedOptions.SETTINGS.DETECTION_MEDIUM
        self.resolution = SharedOptions.SETTINGS.FACE_MEDIUM
        if SharedOptions.MODE == "High":
        #    self.resolution = SharedOptions.SETTINGS.DETECTION_HIGH
             self.resolution = SharedOptions.SETTINGS.FACE_HIGH
        elif SharedOptions.MODE == "Medium":
        #    self.resolution = SharedOptions.SETTINGS.DETECTION_MEDIUM
             self.resolution = SharedOptions.SETTINGS.FACE_MEDIUM
        elif SharedOptions.MODE == "Low":
        #    self.resolution = SharedOptions.SETTINGS.DETECTION_LOW
             self.resolution = SharedOptions.SETTINGS.FACE_LOW


    def initialise(self) -> None:
        # Note that SharedOptions already tests for CUDA and only sets 
        # USE_CUDA = True if PyTorch reports CUDA being available, because
        # settings like resolution, package folders, model names etc are
        # based on whether CUDA is used. This can (and should) be refactored,
        # but not today.
        
        self.can_use_GPU = self.system_info.hasTorchCuda or self.system_info.hasTorchMPS

        if SharedOptions.USE_CUDA:
            SharedOptions.USE_CUDA = self.system_info.hasTorchCuda
        if SharedOptions.USE_MPS:
            SharedOptions.USE_MPS = self.system_info.hasTorchMPS

        # We'll assume that USE_CUDA / USE_MPS are correct to avoid slow code
        if SharedOptions.USE_CUDA:
            self.inference_device  = "GPU"
            self.inference_library = "CUDA"
        elif SharedOptions.USE_MPS:
            self.inference_device  = "GPU"
            self.inference_library = "MPS"

        if SharedOptions.USE_CUDA and self.half_precision == 'enable' and \
           not self.system_info.hasTorchHalfPrecision:
            self.half_precision = 'disable'

        self._init_db()
        self._load_faces()

        # refresh the copy of face embeddings every 5 seconds.
        self._update_faces_active = True
        faceUpdate_thread = threading.Thread(None, self._update_faces, args = (5,))
        faceUpdate_thread.start()

        self._num_items_found = 0


    def process(self, data: RequestData) -> JSON:

        command = data.command
                    
        response = { "success": False, "error": "Unknown command" }

        start_time = time.perf_counter()

        if command == "detect":
            response = self._detect_face(data)
        elif command == "register":
            response = self._register_face(data)
        elif command == "list":
            response = self._list_faces(data)
        elif command == "delete":
            response = self._delete_user_faces(data)
        elif command == "recognize":
            response = self._recognise_face(data)
        elif command == "match":
            response = self._match_faces(data)

        if "success" in response and response["success"]:
            response["processMs"] = int((time.perf_counter() - start_time) * 1000)
        else:
            message = response["error"] if "error" in response else "Error occurred"
            if response.get("err_trace", ""):
                message += ': ' + response["err_trace"]

            self.log(LogMethod.Error | LogMethod.Server,
            { 
                "filename": __file__,
                "method": command,
                "message": message,
                "loglevel": "error",
            })

        return response 


    def status(self) -> JSON:
        statusData = super().status()
        statusData["numItemsFound"] = self._num_items_found
        return statusData


    def update_statistics(self, response):
        super().update_statistics(response)
        if "success" in response and response["success"] and "predictions" in response:
            predictions = response["predictions"]
            self._num_items_found += len(predictions) 


    def selftest(self) -> JSON:
        
        file_name = os.path.join("test", "person.jpg")

        request_data = RequestData()
        request_data.queue   = self.queue_name
        request_data.command = "detect"
        request_data.add_file(file_name)
        request_data.add_value("min_confidence", 0.4)

        result = self.process(request_data)
        print(f"Info: Self-test for {self.module_id}. Success: {result['success']}")
        # print(f"Info: Self-test output for {self.module_id}: {result}")

        return { "success": result['success'], "message": "Face detection test successful" }


    def cleanup(self) -> None:
        """
        Called when this module has been asked to shutdown.
        """
        # Request for the face update thread to stop
        self._update_faces_active = False
    

    def _init_models(self, re_entered: bool = False) -> None:

        if self.faceclassifier is not None and self.detector is not None:
            return True

        try:
            with self.models_lock:
                if self.faceclassifier is None:
                    model_path = os.path.join(SharedOptions.SHARED_APP_DIR,
                                              SharedOptions.SETTINGS.FACE_RECOG_MODEL)
                    self.faceclassifier = FaceRecognitionModel(model_path,
                                                               cuda=SharedOptions.USE_CUDA)

                if self.detector is None:
                    model_path = os.path.join(SharedOptions.SHARED_APP_DIR,
                                              SharedOptions.SETTINGS.FACE_MODEL)
                    self.detector = YOLODetector(model_path, self.resolution,
                                                 cuda=SharedOptions.USE_CUDA, 
                                                 mps=SharedOptions.USE_MPS,
                                                 half_precision=self.half_precision)
    
            if self.faceclassifier is not None and self.detector is not None:
                return True

        except Exception as ex:
            if not re_entered and SharedOptions.USE_CUDA and str(ex).startswith('CUDA out of memory'):

                """ Force switch to CPU-only mode """
                self.faceclassifier    = None
                self.detector          = None
                SharedOptions.USE_CUDA = False

                self.log(LogMethod.Info | LogMethod.Server,
                { 
                    "filename": __file__,
                    "method": "switchToCPU",
                    "message": "GPU out of memory. Switching to CPU mode",
                    "loglevel": "information",
                })

                return self._init_models(re_entered = True)
            else:
                self.report_error(ex, __file__)
                return False


    # make sure the sqlLite database exists
    def _init_db(self) -> None:

        try:
            self.databaseFilePath = os.path.join(SharedOptions.DATA_DIR, "faceembedding.db")
            self.database_ok      = True
            conn                  = sqlite3.connect(self.databaseFilePath)
            cursor                = conn.cursor()

            cursor.execute(CREATE_TABLE)
            
            conn.commit()
            conn.close()

        except Exception as ex:
            self.database_ok = False

            message = f"Unable to open the face database at {self.databaseFilePath}"
            print(message)

            # We don't have a logger object yet
            # self.report_error(ex, __file__)


    def _load_faces(self) -> None:

        if not self.database_ok:
            return

        try:
            face_map      = {"map": {}}
            face_map_rep  = {}
            conn          = sqlite3.connect(self.databaseFilePath)

            cursor        = conn.cursor()
            embeddings    = cursor.execute(SELECT_FACES)
            embedding_arr = []

            i = 0
            for row in embeddings:

                embedding = row[1]
                user_id   = row[0]
                embedding = ast.literal_eval(embedding)
                embedding_arr.append(embedding)
                face_map["map"][i] = user_id
                i += 1

            face_map["tensors"] = embedding_arr
            face_map_rep        = repr(face_map)

            with self.face_lock:
                self.master_face_map = face_map
                self.face_map_rep    = face_map_rep;

            conn.close()
            
        except Exception as ex:
            self.database_ok = False

            # message = "".join(traceback.TracebackException.from_exception(ex).format())
            message = f"Unable to open the face database at {self.databaseFilePath}"
            print(message)

            # We don't have a logger object yet
            # self.report_error(ex, __file__)


    def _update_faces(self, delay: int) -> None:

        while True:
            if not self._update_faces_active:
                return

            self._load_faces()
            time.sleep(delay)


    def _detect_face(self, data: RequestData) -> JSON:

        if not self._init_models():
            return {
                "success": False,
                "predictions": [],
                "count": 0,
                "message": "Unable to load the face detector",
                "error": "Unable to load the face detector",
                "inferenceMs": 0
            }

        try:
            threshold: float  = float(data.get_value("min_confidence", "0.67"))
            img: Image        = data.get_image(0)

            start_time        = time.perf_counter()
            det               = self.detector.predictFromImage(img, threshold)
            inferenceMs       = int((time.perf_counter() - start_time) * 1000)

            outputs = []

            for *xyxy, conf, _ in reversed(det):
                x_min = xyxy[0]
                y_min = xyxy[1]
                x_max = xyxy[2]
                y_max = xyxy[3]
                score = conf.item()

                detection = {
                    "confidence": score,
                    "x_min": int(x_min),
                    "y_min": int(y_min),
                    "x_max": int(x_max),
                    "y_max": int(y_max),
                }

                outputs.append(detection)

            message = 'Found 1 face' if len(outputs) == 1 else f'Found {len(outputs)} faces'
            output  = {
                "success": True,
                "predictions": outputs,
                "count": len(outputs),
                "message": message,
                "inferenceMs": inferenceMs
            }

        except UnidentifiedImageError:
            message = "The image provided was of an unknown type"
            output  = {
                "success": False,
                "error": message
            }
                            
        except Exception as ex:
            trace  = "".join(traceback.TracebackException.from_exception(ex).format())
            output = {
                "success": False,
                "error": "An Error occurred during processing",
                "err_trace": trace
            }

        return output


    def _register_face(self, data: RequestData) -> Tuple[JSON, int]:

        if not self._init_models():
            return {
                "success": False,
                "message": "Unable to load the face detector",
                "error": "Unable to load the face detector",
                "inferenceMs": 0
            }

        try:
            user_id = data.get_value("userid")

            batch = None
            numFiles = len(data.files) if data.files else 0

            inferenceMs: int = 0

            for i in range(0, numFiles):

                pil_image    = data.get_image(i)

                start_time   = time.perf_counter()

                det = self.detector.predictFromImage(pil_image, 0.55)

                inferenceMs += int((time.perf_counter() - start_time) * 1000)

                new_img = None

                for *xyxy, _, _ in reversed(det):
                    x_min = xyxy[0]
                    y_min = xyxy[1]
                    x_max = xyxy[2]
                    y_max = xyxy[3]

                    new_img = pil_image.crop((int(x_min), int(y_min), int(x_max), int(y_max)))
                    break

                if new_img is not None:

                    img = self.trans(new_img).unsqueeze(0)

                    if batch is None:
                        batch = img
                    else:
                        batch = torch.cat([batch, img], 0)

            if batch is not None:

                start_time     = time.perf_counter()
                
                img_embeddings = self.faceclassifier.predict(batch).cpu()

                inferenceMs   += int((time.perf_counter() - start_time) * 1000)

                img_embeddings = torch.mean(img_embeddings, 0)

                conn   = sqlite3.connect(self.databaseFilePath)
                cursor = conn.cursor()

                emb    = img_embeddings.tolist()
                emb    = repr(emb)

                exist_emb = cursor.execute(SELECT_FACE, (user_id,))

                user_exist = False

                for _ in exist_emb:
                    user_exist = True
                    break

                if user_exist:
                    cursor.execute(UPDATE_FACE, (emb, user_id))
                    message = "face updated"
                else:
                    cursor.execute(ADD_FACE, (user_id, emb))
                    message = "face added"

                conn.commit()
                conn.close()

                self._load_faces();

                output = {
                    "success": True,
                    "message": message,
                    "inferenceMs": inferenceMs
                }

            else:
                output = {
                    "success": False,
                    "error": "No face detected" 
                }

        except UnidentifiedImageError:
            message = "The image provided was of an unknown type"
            output  = {
                "success": False,
                "error": message
            }
                            
        except Exception as ex:
            trace  = "".join(traceback.TracebackException.from_exception(ex).format())
            output = {
                "success": False,
                "error": "An Error occurred during processing",
                "err_trace": trace
            }

        return output


    def _list_faces(self, data: RequestData) -> JSON:

        try:                        
            conn = sqlite3.connect(self.databaseFilePath)

            cursor = conn.cursor()
            cursor.execute(LIST_FACES)

            rows = cursor.fetchall()

            faces = []
            for row in rows:
                faces.append(row[0])

            conn.close()

            output = {"success": True, "faces": faces}

        except Exception as ex:
            trace  = "".join(traceback.TracebackException.from_exception(ex).format())
            output = {
                "success": False,
                "error": "An Error occurred during processing",
                "err_trace": trace
            }

        return output


    def _delete_user_faces(self, data: RequestData) -> JSON:

        try:
            user_id = data.get_value("userid")
                            
            conn    = sqlite3.connect(self.databaseFilePath)

            cursor  = conn.cursor()
            cursor.execute(DELETE_FACE, (user_id,))

            conn.commit()
            conn.close()

            self._load_faces();

            output = {"success": True}

        except Exception as ex:
            trace  = "".join(traceback.TracebackException.from_exception(ex).format())
            output = {
                "success": False,
                "error": "An Error occurred during processing",
                "err_trace": trace
            }

        return output


    def _recognise_face(self, data: RequestData) -> JSON:

        if not self._init_models():
            return {
                "success": False,
                "predictions": [],
                "count": 0,
                "message": "Unable to load the face detector",
                "error": "Unable to load the face detector",
                "inferenceMs": 0
            }

        try:
            threshold  = float(data.get_value("min_confidence", "0.67"))
            pil_image  = data.get_image(0)

            with self.face_lock:
                face_map_rep = self.master_face_map["map"].copy()
                face_array   = self.master_face_map["tensors"].copy()

            if len(face_array) > 0:
                face_array_tensors = [ torch.tensor(emb).unsqueeze(0) for emb in face_array ]
                face_tensors = torch.cat(face_array_tensors)

            if SharedOptions.USE_CUDA and len(face_array) > 0:
                face_tensors = face_tensors.cuda()

            start_time  = time.perf_counter()

            det = self.detector.predictFromImage(pil_image, threshold)

            inferenceMs = int((time.perf_counter() - start_time) * 1000)

            faces = [[]]
            detections = []

            found_face = False

            for *xyxy, _, _ in reversed(det):

                found_face = True
                x_min = int(xyxy[0])
                y_min = int(xyxy[1])
                x_max = int(xyxy[2])
                y_max = int(xyxy[3])

                new_img    = pil_image.crop((x_min, y_min, x_max, y_max))
                img_tensor = self.trans(new_img).unsqueeze(0)

                if len(faces[-1]) % 10 == 0 and len(faces[-1]) > 0:
                    faces.append([img_tensor])
                else:
                    faces[-1].append(img_tensor)

                detections.append((x_min, y_min, x_max, y_max))

            if not found_face:

                output = {
                    "success": False,
                    "error": "No face found in image",
                    "inferenceMs": inferenceMs
                }

            elif len(face_map_rep) == 0:

                predictions = []

                for face in detections:

                    x_min = int(face[0])
                    if x_min < 0:
                        x_min = 0
                    y_min = int(face[1])
                    if y_min < 0:
                        y_min = 0
                    x_max = int(face[2])
                    if x_max < 0:
                        x_max = 0
                    y_max = int(face[3])
                    if y_max < 0:
                        y_max = 0

                    user_data = {
                        "confidence": 0,
                        "userid": "unknown",
                        "x_min": x_min,
                        "y_min": y_min,
                        "x_max": x_max,
                        "y_max": y_max,
                    }

                    predictions.append(user_data)

                output = {
                    "message": "No known faces",
                    "count": len(predictions),
                    "predictions": predictions,
                    "success": True,
                    "inferenceMs": inferenceMs
                }

            else:

                embeddings = []
                for face_list in faces:

                    start_time   = time.perf_counter()


                    embedding    = self.faceclassifier.predict(torch.cat(face_list))

                    inferenceMs += int((time.perf_counter() - start_time) * 1000)
                    
                    embeddings.append(embedding)

                embeddings = torch.cat(embeddings)

                predictions = []
                found_known = False

                for embedding, face in zip(embeddings, detections):

                    embedding = embedding.unsqueeze(0)

                    embedding_proj = torch.cat( [embedding for _ in range(face_tensors.size(0))] )
                    similarity     = F.cosine_similarity(embedding_proj, face_tensors)
                    user_index     = similarity.argmax().item()
                    max_similarity = (similarity.max().item() + 1) / 2

                    if max_similarity < threshold:
                        confidence = 0
                        user_id    = "unknown"
                    else:
                        confidence  = max_similarity
                        user_id     = face_map_rep[user_index]
                        found_known = True

                    x_min = int(face[0])
                    if x_min < 0:
                        x_min = 0
                    y_min = int(face[1])
                    if y_min < 0:
                        y_min = 0
                    x_max = int(face[2])
                    if x_max < 0:
                        x_max = 0
                    y_max = int(face[3])
                    if y_max < 0:
                        y_max = 0

                    user_data = {
                        "confidence": confidence,
                        "userid": user_id,
                        "x_min": x_min,
                        "y_min": y_min,
                        "x_max": x_max,
                        "y_max": y_max,
                    }

                    predictions.append(user_data)

                if found_known:
                    output = {
                        "message": "A face was recognised",
                        "count": len(predictions),
                        "predictions": predictions,
                        "success": True,
                        "inferenceMs": inferenceMs
                    }
                else:
                    output = {
                        "message": "No known faces",
                        "count": len(predictions),
                        "predictions": predictions,
                        "success": True,
                        "inferenceMs": inferenceMs
                    }

        except UnidentifiedImageError:
            message = "The image provided was of an unknown type"
            output = {
                "success": False,
                "error": message
            }

        except Exception as ex:
            trace = "".join(traceback.TracebackException.from_exception(ex).format())
            output = {
                "success": False,
                "error": "An Error occurred during processing",
                "err_trace": trace
            }

        return output


    def _match_faces(self, data: RequestData) -> JSON:

        if not self._init_models():
            return {
                "success": False,
                "message": "Unable to load the face detector",
                "error": "Unable to load the face detector",
                "inferenceMs": 0
            }

        try:
            image1 = data.get_image(0)
            image2 = data.get_image(1)

            start_time  = time.perf_counter()

            det1 = self.detector.predictFromImage(image1, 0.8)
            det2 = self.detector.predictFromImage(image2, 0.8)

            inferenceMs = int((time.perf_counter() - start_time) * 1000)

            if len(det1) > 0 and len(det2) > 0:

                for *xyxy, _, _ in reversed(det1):
                    x_min = xyxy[0]
                    y_min = xyxy[1]
                    x_max = xyxy[2]
                    y_max = xyxy[3]
                    face1 = self.trans(
                        image1.crop(
                            (int(x_min), int(y_min), int(x_max), int(y_max))
                        )
                    ).unsqueeze(0)

                    break

                for *xyxy, _, _ in reversed(det2):
                    x_min = xyxy[0]
                    y_min = xyxy[1]
                    x_max = xyxy[2]
                    y_max = xyxy[3]
                    face2 = self.trans(
                        image2.crop(
                            (int(x_min), int(y_min), int(x_max), int(y_max))
                        )
                    ).unsqueeze(0)

                    break

                faces = torch.cat([face1, face2], dim=0)

                start_time = time.perf_counter()
                
                embeddings = self.faceclassifier.predict(faces)

                inferenceMs += int((time.perf_counter() - start_time) * 1000)

                embed1 = embeddings[0, :].unsqueeze(0)
                embed2 = embeddings[1, :].unsqueeze(0)

                similarity = (
                    F.cosine_similarity(embed1, embed2).item() + 1
                ) / 2

                output = {
                    "success": True,
                    "similarity": similarity,
                    "inferenceMs": inferenceMs
                }

            else:
                output = {
                    "success": False,
                    "error": "No face found in one or both images",
                    "inferenceMs": inferenceMs
                }

        except Exception as ex:
            trace = "".join(traceback.TracebackException.from_exception(ex).format())
            output = {
                "success": False,
                "error": "An Error occurred during processing.",
                "err_trace": trace
            }

        return output


if __name__ == "__main__":
    Face_adapter().start_loop()
