import os
import requests
from options import Options

def main():

    opts = Options()

    filepath = os.path.join(opts.imageDir + "/Faces", "chris-hemsworth-1.jpg")
    test_image = open(filepath, "rb").read()

    res = requests.post(opts.endpoint("vision/face/recognize"),
                        files={"image": test_image},
                        data={"min_confidence": 0.1}).json()

    for user in res["predictions"]:
        print(f'Recognized as: {user["userid"]}')

            
if __name__ == "__main__":
    main()