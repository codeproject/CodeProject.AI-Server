import os
import requests
from options import Options

def main():

    opts = Options()

    image_dir = opts.imageDir + "/Faces/"
    image_data2 = open(os.path.join(image_dir, "chris-hemsworth-1.jpg"), "rb").read()
    image_data1 = open(os.path.join(image_dir, "chris-hemsworth-3.jpg"), "rb").read()

    response = requests.post(opts.endpoint("vision/face/match"),
                             files={"image1": image_data1, "image2": image_data2}).json()

    print(response)
    
if __name__ == "__main__":
    main()