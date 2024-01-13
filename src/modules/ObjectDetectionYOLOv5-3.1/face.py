import _thread as thread
import ast
import io
import json
import os
import sqlite3
import sys
import time
import warnings
from multiprocessing import Process

sys.path.insert(0, os.path.join(os.path.dirname(os.path.realpath(__file__)), "."))
from shared import SharedOptions
if SharedOptions.PROFILE == "windows_native":
    sys.path.append(os.path.join(SharedOptions.APP_DIR,"windows_packages"))

import numpy as np
import torch

# import cv2
import torch.nn.functional as F
import torchvision.transforms as transforms
from PIL import Image, UnidentifiedImageError
from process import YOLODetector
from recognition import FaceRecognitionModel
from redis import RedisError, StrictRedis
import traceback


def load_faces():

    master_face_map = {"map": {}}

    SELECT_FACE = "SELECT * FROM {}".format(SharedOptions.TB_EMBEDDINGS)
    conn = sqlite3.connect(SharedOptions.DATA_DIR + "/faceembedding.db")
    cursor = conn.cursor()
    embeddings = cursor.execute(SELECT_FACE)
    embedding_arr = []

    i = 0
    for row in embeddings:

        embedding = row[1]
        user_id = row[0]
        embedding = ast.literal_eval(embedding)
        embedding_arr.append(embedding)
        master_face_map["map"][i] = user_id
        i += 1

    master_face_map["tensors"] = embedding_arr
    facemap = repr(master_face_map)
    SharedOptions.db.set("facemap", facemap)

    conn.close()


def face(thread_name, delay):

    if SharedOptions.MODE == "High":

        reso = SharedOptions.SETTINGS.FACE_HIGH

    elif SharedOptions.MODE == "Low":

        reso = SharedOptions.SETTINGS.FACE_LOW

    else:

        reso = SharedOptions.SETTINGS.FACE_MEDIUM

    faceclassifier = FaceRecognitionModel(
        os.path.join(SharedOptions.SHARED_APP_DIR, "facerec-high.model"),
        cuda=SharedOptions.CUDA_MODE,
    )

    detector = YOLODetector(
        os.path.join(SharedOptions.SHARED_APP_DIR, SharedOptions.SETTINGS.FACE_MODEL),
        reso,
        cuda=SharedOptions.CUDA_MODE,
    )

    load_faces()

    ADD_FACE = "INSERT INTO TB_EMBEDDINGS(userid,embedding) VALUES(?,?)"
    UPDATE_FACE = "UPDATE TB_EMBEDDINGS SET embedding = ? where userid = ?"
    SELECT_FACE = "SELECT * FROM TB_EMBEDDINGS where userid = ? "

    trans = transforms.Compose(
        [
            transforms.Resize((112, 112)),
            transforms.ToTensor(),
            transforms.Normalize(mean=[0.5, 0.5, 0.5], std=[0.5, 0.5, 0.5]),
        ]
    )

    IMAGE_QUEUE = "face_queue"

    while True:

        queue = SharedOptions.db.lrange(IMAGE_QUEUE, 0, 0)

        SharedOptions.db.ltrim(IMAGE_QUEUE, len(queue), -1)

        if len(queue) > 0:

            for req_data in queue:

                req_data = json.JSONDecoder().decode(req_data)

                task_type = req_data["reqtype"]
                req_id = req_data["reqid"]

                if task_type == "detect":
                    img_id = req_data["imgid"]
                    img_path = os.path.join(SharedOptions.TEMP_PATH, img_id)
                    try:
                        
                        threshold = float(req_data["minconfidence"])

                        det = detector.predict(img_path, threshold)
                        os.remove(img_path)

                        outputs = []

                        for *xyxy, conf, cls in reversed(det):
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
                        err_trace = traceback.format_exc()
                        print(err_trace, file=sys.stderr, flush=True)
                        output = {
                            "success": False,
                            "error": "invalid image"
                        }

                    except Exception:
                        err_trace = traceback.format_exc()
                        print(err_trace, file=sys.stderr, flush=True)
                        output = {
                            "success": False,
                            "error": "error occurred on the server"
                        }

                    finally:
                        SharedOptions.db.set(req_id, json.dumps(output))
                        if os.path.exists(img_path):
                            os.remove(img_path)

                elif task_type == "register":

                    try:

                        user_id = req_data["userid"]

                        user_images = req_data["images"]

                        conn = sqlite3.connect(
                            SharedOptions.DATA_DIR + "/faceembedding.db"
                        )

                        batch = None

                        for img_id in user_images:

                            img_path = os.path.join(SharedOptions.TEMP_PATH , img_id)
                            pil_image = Image.open(img_path).convert("RGB")

                            det = detector.predict(img_path, 0.55)
                            os.remove(img_path)

                            outputs = []
                            new_img = None

                            for *xyxy, conf, cls in reversed(det):
                                x_min = xyxy[0]
                                y_min = xyxy[1]
                                x_max = xyxy[2]
                                y_max = xyxy[3]

                                new_img = pil_image.crop(
                                    (int(x_min), int(y_min), int(x_max), int(y_max))
                                )
                                break

                            if new_img is not None:

                                img = trans(new_img).unsqueeze(0)

                                if batch is None:
                                    batch = img
                                else:
                                    batch = torch.cat([batch, img], 0)

                        if batch is None:

                            output = {
                                "success": False,
                                "error": "no face detected"
                            }
                            SharedOptions.db.set(req_id, json.dumps(output))
                            continue

                        img_embeddings = faceclassifier.predict(batch).cpu()

                        img_embeddings = torch.mean(img_embeddings, 0)

                        cursor = conn.cursor()

                        emb = img_embeddings.tolist()
                        emb = repr(emb)

                        exist_emb = cursor.execute(SELECT_FACE, (user_id,))

                        user_exist = False

                        for row in exist_emb:
                            user_exist = True
                            break

                        if user_exist:

                            cursor.execute(UPDATE_FACE, (emb, user_id))
                            message = "face updated"
                        else:
                            cursor.execute(ADD_FACE, (user_id, emb))
                            message = "face added"

                        conn.commit()

                        output = {"success": True, "message": message}

                        conn.close()

                    except UnidentifiedImageError:
                        err_trace = traceback.format_exc()
                        print(err_trace, file=sys.stderr, flush=True)
                        output = {
                            "success": False,
                            "error": "invalid image"
                        }

                    except Exception:

                        err_trace = traceback.format_exc()
                        print(err_trace, file=sys.stderr, flush=True)

                        output = {
                            "success": False,
                            "error": "error occurred on the server"
                        }

                    finally:
                        SharedOptions.db.set(req_id, json.dumps(output))
                        for img_id in user_images:
                            if os.path.exists(os.path.join(SharedOptions.TEMP_PATH , img_id)):
                                os.remove(os.path.join(SharedOptions.TEMP_PATH , img_id))

                elif task_type == "recognize":

                    try:

                        master_face_map = SharedOptions.db.get("facemap")
                        master_face_map = ast.literal_eval(master_face_map)

                        facemap = master_face_map["map"]

                        face_array = master_face_map["tensors"]

                        if len(face_array) > 0:

                            face_array_tensors = [
                                torch.tensor(emb).unsqueeze(0) for emb in face_array
                            ]
                            face_tensors = torch.cat(face_array_tensors)

                        if SharedOptions.CUDA_MODE and len(face_array) > 0:
                            face_tensors = face_tensors.cuda()

                        img_id = req_data["imgid"]
                        threshold = float(req_data["minconfidence"])

                        img = os.path.join(SharedOptions.TEMP_PATH , img_id)

                        pil_image = Image.open(img).convert("RGB")

                        det = detector.predict(img, 0.55)

                        os.remove(img)

                        faces = [[]]
                        detections = []

                        found_face = False

                        for *xyxy, conf, cls in reversed(det):
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

                            output = {"success": True, "predictions": []}
                            SharedOptions.db.set(req_id, json.dumps(output))

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

                            output = {"success": True, "predictions": predictions}
                            SharedOptions.db.set(req_id, json.dumps(output))

                        else:

                            embeddings = []
                            for face_list in faces:

                                embedding = faceclassifier.predict(torch.cat(face_list))
                                embeddings.append(embedding)

                            embeddings = torch.cat(embeddings)

                            predictions = []

                            for embedding, face in zip(embeddings, detections):

                                embedding = embedding.unsqueeze(0)

                                embedding_proj = torch.cat(
                                    [embedding for i in range(face_tensors.size(0))]
                                )

                                similarity = F.cosine_similarity(
                                    embedding_proj, face_tensors
                                )

                                user_index = similarity.argmax().item()
                                max_similarity = (similarity.max().item() + 1) / 2

                                if max_similarity < threshold:
                                    confidence = 0
                                    user_id = "unknown"
                                else:
                                    confidence = max_similarity
                                    user_id = facemap[user_index]

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

                            output = {"success": True, "predictions": predictions}

                    except UnidentifiedImageError:
                        err_trace = traceback.format_exc()
                        print(err_trace, file=sys.stderr, flush=True)

                        output = {
                            "success": False,
                            "error": "invalid image"
                        }

                    except Exception:

                        err_trace = traceback.format_exc()
                        print(err_trace, file=sys.stderr, flush=True)

                        output = {
                            "success": False,
                            "error": "error occurred on the server"
                        }

                    finally:
                        SharedOptions.db.set(req_id, json.dumps(output))

                        if os.path.exists(os.path.join(SharedOptions.TEMP_PATH , img_id)):
                            os.remove(os.path.join(SharedOptions.TEMP_PATH , img_id))

                elif task_type == "match":

                    try:

                        user_images = req_data["images"]

                        img1 = os.path.join(SharedOptions.TEMP_PATH , user_images[0])
                        img2 = os.path.join(SharedOptions.TEMP_PATH , user_images[1])

                        image1 = Image.open(img1).convert("RGB")
                        image2 = Image.open(img2).convert("RGB")

                        det1 = detector.predict(img1, 0.8)
                        det2 = detector.predict(img2, 0.8)

                        os.remove(img1)
                        os.remove(img2)

                        if len(det1) == 0 or len(det2) == 0:

                            output = {"success": False, "error": "no face found"}
                            SharedOptions.db.set(req_id, json.dumps(output))
                            continue

                        for *xyxy, conf, cls in reversed(det1):
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

                        for *xyxy, conf, cls in reversed(det2):
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

                    except UnidentifiedImageError:
                        err_trace = traceback.format_exc()
                        print(err_trace, file=sys.stderr, flush=True)

                        output = {
                            "success": False,
                            "error": "invalid image"
                        }

                    except Exception:

                        err_trace = traceback.format_exc()
                        print(err_trace, file=sys.stderr, flush=True)

                        output = {
                            "success": False,
                            "error": "error occurred on the server"
                        }

                    finally:

                        SharedOptions.db.set(req_id, json.dumps(output))
                        if os.path.exists(os.path.join(SharedOptions.TEMP_PATH , user_images[0])):
                            os.remove(os.path.join(SharedOptions.TEMP_PATH , user_images[0]))

                        if os.path.exists(os.path.join(SharedOptions.TEMP_PATH , user_images[1])):
                            os.remove(os.path.join(SharedOptions.TEMP_PATH , user_images[1]))

        time.sleep(delay)


def update_faces(thread_name, delay):

    while True:

        load_faces()

        time.sleep(delay)

if __name__ == "__main__":
    p1 = Process(target=update_faces, args=("", 1))
    p1.start()

    p = Process(target=face, args=("", SharedOptions.SLEEP_TIME))
    p.start()
