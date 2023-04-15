import os
import sqlite3
from sqlite3 import Cursor, Error

DATA_DIR = os.getenv("DATA_DIR", "/datastore")

CREATE_TABLE = "CREATE TABLE IF NOT EXISTS TB_EMBEDDINGS(userid TEXT PRIMARY KEY, embedding TEXT NOT NULL)"
# CREATE_TABLE2 = "CREATE TABLE IF NOT EXISTS TB_EMBEDDINGS2(userid TEXT PRIMARY KEY, embedding TEXT NOT NULL)"
conn = sqlite3.connect(os.path.join(DATA_DIR,"faceembedding.db"))
cursor = conn.cursor()
cursor.execute(CREATE_TABLE)
# cursor.execute(CREATE_TABLE2)
conn.commit()
conn.close()
