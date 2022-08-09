# Import our general libraries
import ast
import json
import os
import sqlite3
import sys
import time
import traceback
import threading

# Import the CodeProject.AI SDK. This will add to the PATH vaar for future imports
sys.path.append("../../SDK/Python")
from common import JSON
from codeprojectai import CodeProjectAIRunner
from requestdata import AIRequestData
from analysislogging import LogMethod

# Deepstack settings
from shared import SharedOptions

# Set the path based on Deepstack's settings so CPU / GPU packages can be correctly loaded
sys.path.insert(0, os.path.join(os.path.dirname(os.path.realpath(__file__)), "."))
sys.path.append(os.path.join(SharedOptions.APPDIR, SharedOptions.SETTINGS.PLATFORM_PKGS))

# Import libraries from the Python VENV using the correct packages dir
from PIL import UnidentifiedImageError, Image
import torch
# import cv2
import torch.nn.functional as F
import torchvision.transforms as transforms

# Deepstack inference wrappers
from process import YOLODetector
from recognition import FaceRecognitionModel


# Constants
ADD_FACE     = "INSERT INTO TB_EMBEDDINGS(userid,embedding) VALUES(?,?)"
UPDATE_FACE  = "UPDATE TB_EMBEDDINGS SET embedding = ? where userid = ?"
SELECT_FACE  = "SELECT * FROM TB_EMBEDDINGS where userid = ?"
SELECT_FACES = "SELECT * FROM TB_EMBEDDINGS"
LIST_FACES   = "SELECT userid FROM TB_EMBEDDINGS"
DELETE_FACE  = "DELETE FROM TB_EMBEDDINGS where userid = ?"

# Globals
databaseFilePath = os.path.join(SharedOptions.DATA_DIR, "faceembedding.db")
master_face_map = {"map": {}}
facemap         = {}
database_ok     = True

resolution = SharedOptions.SETTINGS.DETECTION_MEDIUM
if SharedOptions.MODE == "High":
    resolution = SharedOptions.SETTINGS.DETECTION_HIGH
elif SharedOptions.MODE == "Medium":
    resolution = SharedOptions.SETTINGS.DETECTION_MEDIUM
elif SharedOptions.MODE == "Low":
    resolution = SharedOptions.SETTINGS.DETECTION_LOW

model_path = os.path.join(SharedOptions.SHARED_APP_DIR, "facerec-high.model")
faceclassifier = FaceRecognitionModel(model_path, cuda=SharedOptions.CUDA_MODE)

model_path = os.path.join(SharedOptions.SHARED_APP_DIR, SharedOptions.SETTINGS.FACE_MODEL)
detector = YOLODetector(model_path, resolution, cuda=SharedOptions.CUDA_MODE)

trans = transforms.Compose(
    [
        transforms.Resize((112, 112)),
        transforms.ToTensor(),
        transforms.Normalize(mean=[0.5, 0.5, 0.5], std=[0.5, 0.5, 0.5]),
    ]
)


def main():

    # create a CodeProject.AI module object
    module_runner = CodeProjectAIRunner("face_queue")

    # Hack for debug mode
    if module_runner.module_id == "CodeProject.AI":
        module_runner.module_id   = "FaceProcessing"
        module_runner.module_name = "Face Processing"

    if SharedOptions.CUDA_MODE:
        module_runner.hardware_id        = "GPU"
        module_runner.execution_provider = "CUDA"

    init_db(module_runner)
    load_faces(module_runner)

    faceupdate_thread = threading.Thread(None, update_faces,    args = (1, module_runner))
    face_thread       = threading.Thread(None, start_face_loop, args = (module_runner,))
    faceupdate_thread.start()
    face_thread.start()

    face_thread.join();


def start_face_loop(module_runner: CodeProjectAIRunner) -> None:

    module_runner.start_loop(face_callback)
    

# make sure the sqlLite database exists
def init_db(module_runner: CodeProjectAIRunner) -> None:

    global database_ok

    try:
        conn          = sqlite3.connect(databaseFilePath)
        cursor        = conn.cursor()

        CREATE_TABLE  = "CREATE TABLE IF NOT EXISTS TB_EMBEDDINGS(userid TEXT PRIMARY KEY, embedding TEXT NOT NULL)"
        cursor.execute(CREATE_TABLE)
        
        # CREATE_TABLE  = "CREATE TABLE IF NOT EXISTS TB_FACEMAP(map TEXT NOT NULL)"
        # cursor.execute(CREATE_TABLE)
        
        conn.commit()
        conn.close()

    except Exception:
        database_ok = False

        # err_trace = traceback.format_exc()
        module_runner.log(LogMethod.Error | LogMethod.Cloud | LogMethod.Server,
        { 
            "filename": "face.py",
            "method": "init_db",
            "loglevel": "error",
            "message": f"Unable to open the face database at {databaseFilePath}", 
            "exception_type": "Exception"
        })


def load_faces(module_runner: CodeProjectAIRunner) -> None:

    global database_ok
    global facemap

    if not database_ok:
        return

    try:
        # master_face_map = {"map": {}}

        conn = sqlite3.connect(databaseFilePath)

        cursor = conn.cursor()
        embeddings = cursor.execute(SELECT_FACES)
        embedding_arr = []

        i = 0
        for row in embeddings:

            embedding = row[1]
            user_id   = row[0]
            embedding = ast.literal_eval(embedding)
            embedding_arr.append(embedding)
            master_face_map["map"][i] = user_id
            i += 1

        master_face_map["tensors"] = embedding_arr
        facemap = repr(master_face_map)

        conn.close()
        
    except Exception:
        database_ok = False

        # err_trace = traceback.format_exc()
        module_runner.log(LogMethod.Error | LogMethod.Cloud | LogMethod.Server,
        { 
            "filename": "face.py",
            "method": "load_faces",
            "loglevel": "error",
            "message": f"Unable to open the face database at {databaseFilePath}", 
            "exception_type": "Exception"
        })


def face_callback(module_runner: CodeProjectAIRunner, data: AIRequestData) -> JSON:

    command = data.command
                
    output = { "success": False, "error": "Unknown command" }

    if command == "detect":
        output = detect_face(module_runner, data)
    elif command == "register":
        output = register_face(module_runner, data)
    elif command == "list":
        output = list_faces(module_runner, data)
    elif command == "delete":
        output = delete_user_faces(module_runner, data)
    elif command == "recognize":
        output = recognise_face(module_runner, data)
    elif command == "match":
        output = match_faces(module_runner, data)

    if not output["success"]:
        message = output["error"]
        if output.get("err_trace", ""):
            message += ': ' + output["err_trace"]

        module_runner.log(LogMethod.Error | LogMethod.Cloud | LogMethod.Server,
                          { 
                              "filename": "face.py",
                              "method": command,
                              "message": message,
                              "loglevel": "error",
                          })

    return output


def update_faces(delay: int, module_runner: CodeProjectAIRunner) -> None:

    while True:
        load_faces(module_runner)
        time.sleep(delay)


def detect_face(_: CodeProjectAIRunner, data: AIRequestData) -> JSON:
                    
    try:
        threshold: float  = float(data.get_value("min_confidence", "0.4"))
        img: Image = data.get_image(0)

        det = detector.predictFromImage(img, threshold)

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

        output = {"success": True, "predictions": outputs}

    except UnidentifiedImageError:
        output = {
            "success": False,
            "error": "invalid image",
            "err_trace": traceback.format_exc(),
            "code": 400,
        }
                        
    except Exception:
        output = {
            "success": False,
            "error": "An Error occured during processing",
            "err_trace": traceback.format_exc(),
            "code": 500,
        }

    return output


def register_face(module_runner: CodeProjectAIRunner, data: AIRequestData) -> JSON:

    try:
        user_id = data.get_value("userid")

        batch = None
        numFiles = len(data.files) if data.files else 0

        for i in range(0, numFiles):

            pil_image = data.get_image(i)
            det = detector.predictFromImage(pil_image, 0.55)

            new_img = None

            for *xyxy, _, _ in reversed(det):
                x_min = xyxy[0]
                y_min = xyxy[1]
                x_max = xyxy[2]
                y_max = xyxy[3]

                new_img = pil_image.crop((int(x_min), int(y_min), int(x_max), int(y_max)))
                break

            if new_img is not None:

                img = trans(new_img).unsqueeze(0)

                if batch is None:
                    batch = img
                else:
                    batch = torch.cat([batch, img], 0)

        if batch is not None:
            img_embeddings = faceclassifier.predict(batch).cpu()
            img_embeddings = torch.mean(img_embeddings, 0)

            conn = sqlite3.connect(databaseFilePath)
            cursor = conn.cursor()

            emb = img_embeddings.tolist()
            emb = repr(emb)

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

            load_faces(module_runner);

            output = {"success": True, "message": message}

        else:
            output = { "success": False, "error": "Mo face detected", "code": 400 }

    except UnidentifiedImageError:
        output = {
            "success": False,
            "error": "invalid image",
            "err_trace": traceback.format_exc(),
            "code": 400,
        }
                        
    except Exception:
        output = {
            "success": False,
            "error": "An Error occured during processing",
            "err_trace": traceback.format_exc(),
            "code": 500,
        }

    return output


def list_faces(_: CodeProjectAIRunner, data: AIRequestData) -> JSON:

    try:                        
        conn = sqlite3.connect(databaseFilePath)

        cursor = conn.cursor()
        cursor.execute(LIST_FACES)

        rows = cursor.fetchall()

        faces = []
        for row in rows:
            faces.append(row[0])

        conn.close()

        output = {"success": True, "faces": faces}

    except Exception:
        output = {
            "success": False,
            "error": "An Error occured during processing",
            "err_trace": traceback.format_exc(),
            "code": 500,
        }

    return output


def delete_user_faces(module_runner: CodeProjectAIRunner, data: AIRequestData) -> JSON:

    try:
        user_id = data.get_value("userid")
                        
        conn = sqlite3.connect(databaseFilePath)

        cursor = conn.cursor()
        cursor.execute(DELETE_FACE, (user_id,))

        conn.commit()
        conn.close()

        load_faces(module_runner);

        output = {"success": True}

    except Exception:
        output = {
            "success": False,
            "error": "An Error occured during processing",
            "err_trace": traceback.format_exc(),
            "code": 500,
        }

    return output


def recognise_face(_: CodeProjectAIRunner, data: AIRequestData) -> JSON:

    try:

        threshold = float(data.get_value("min_confidence", "0.4"))
        pil_image = data.get_image(0)

        facemap    = master_face_map ["map"]
        face_array = master_face_map ["tensors"]

        if len(face_array) > 0:

            face_array_tensors = [
                torch.tensor(emb).unsqueeze(0) for emb in face_array
            ]
            face_tensors = torch.cat(face_array_tensors)

        if SharedOptions.CUDA_MODE and len(face_array) > 0:
            face_tensors = face_tensors.cuda()

        det = detector.predictFromImage(pil_image, threshold)

        faces = [[]]
        detections = []

        found_face = False

        for *xyxy, _, _ in reversed(det):

            found_face = True
            x_min = int(xyxy[0])
            y_min = int(xyxy[1])
            x_max = int(xyxy[2])
            y_max = int(xyxy[3])

            new_img = pil_image.crop((x_min, y_min, x_max, y_max))
            img_tensor = trans(new_img).unsqueeze(0)

            if len(faces[-1]) % 10 == 0 and len(faces[-1]) > 0:
                faces.append([img_tensor])
            else:
                faces[-1].append(img_tensor)

            detections.append((x_min, y_min, x_max, y_max))

        if found_face == False:

            output = {"success": False, "error": "No face found in image"}

        elif len(facemap) == 0:

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

            output = {"success": True, "predictions": predictions, "message": "No known faces"}

        else:

            embeddings = []
            for face_list in faces:
                embedding = faceclassifier.predict(torch.cat(face_list))
                embeddings.append(embedding)

            embeddings = torch.cat(embeddings)

            predictions = []
            found_known = False

            for embedding, face in zip(embeddings, detections):

                embedding = embedding.unsqueeze(0)

                embedding_proj = torch.cat(
                    [embedding for _ in range(face_tensors.size(0))]
                )

                similarity = F.cosine_similarity(
                    embedding_proj, face_tensors
                )

                user_index     = similarity.argmax().item()
                max_similarity = (similarity.max().item() + 1) / 2

                if max_similarity < threshold:
                    confidence = 0
                    user_id    = "unknown"
                else:
                    confidence  = max_similarity
                    user_id     = facemap[user_index]
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
                output = {"success": True, "predictions": predictions, "message": "A face was recognised"}
            else:
                output = {"success": True, "predictions": predictions, "message": "No known faces"}

    except UnidentifiedImageError:
        output = {
            "success": False,
            "error": "Invalid image",
            "err_trace": traceback.format_exc(),
            "code": 400,
        }

    except Exception:
        output = {
            "success": False,
            "error": "An Error occured during processing",
            "err_trace": traceback.format_exc(),
            "code": 500,
        }

    return output


def match_faces(_: CodeProjectAIRunner, data: AIRequestData) -> JSON:

    try:
        image1 = data.get_image(0)
        image2 = data.get_image(1)

        det1 = detector.predictFromImage(image1, 0.8)
        det2 = detector.predictFromImage(image2, 0.8)

        if len(det1) > 0 and len(det2) > 0:

            for *xyxy, _, _ in reversed(det1):
                x_min = xyxy[0]
                y_min = xyxy[1]
                x_max = xyxy[2]
                y_max = xyxy[3]
                face1 = trans(
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
                face2 = trans(
                    image2.crop(
                        (int(x_min), int(y_min), int(x_max), int(y_max))
                    )
                ).unsqueeze(0)

                break

            faces = torch.cat([face1, face2], dim=0)

            embeddings = faceclassifier.predict(faces)

            embed1 = embeddings[0, :].unsqueeze(0)
            embed2 = embeddings[1, :].unsqueeze(0)

            similarity = (
                F.cosine_similarity(embed1, embed2).item() + 1
            ) / 2

            output = {"success": True, "similarity": similarity}                        

        else:
            output = {"success": False, "error": "No face found in one or both images"}

    except Exception:
        output = {
            "success": False,
            "error": "An Error occured during processing",
            "err_trace": traceback.format_exc(),
            "code": 500,
        }

    return output



if __name__ == "__main__":
    main()
