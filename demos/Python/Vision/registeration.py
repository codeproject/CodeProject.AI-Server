import os
import requests
from options import Options

def register_face(img_path, user_id):

    opts = Options()

    filepath = os.path.join(opts.imageDir + "/Faces", img_path)
    image_data = open(filepath, "rb").read()

    response = requests.post(opts.endpoint("vision/face/register"),
                             files={"image": image_data},
                             data={"userid": user_id}).json()

    print(f"Registration response: {response}")

def main():
    register_face("chris-hemsworth-1.jpg", "Chris Hemsworth")
    register_face("Robert-Downey-Jr-1.jpg", "Robert Downey Jr.")
    register_face("scarlett-johanson-1.jpg", "Scarlett Johanson")

    
if __name__ == "__main__":
    main()