import os
import requests
from options import Options

def main():

    opts = Options()

    filepath = os.path.join(opts.imageDir + "/Scenes", "pexels-pixabay-273935.jpg")
    image_data = open(filepath, "rb").read()

    response = requests.post(opts.endpoint("vision/scene"), 
                             files={"image": image_data}).json()

    print("Image scene is:", response["label"])
    
if __name__ == "__main__":
    main()