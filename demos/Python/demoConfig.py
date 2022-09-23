import os

# some thing we might want to change 
serverPort    = 32168
serverHost    = "localhost"
serverUrl     = f"http://{serverHost}:{serverPort}/v1/"
verifySslCert = False # my development cert is self-signed so don't verify
imageDir      = "../TestData"

# names of directories of interest
detectedDir = "detected"

def cleanDetectedDir():
    # make sure the detected directory exists
    if not os.path.exists(detectedDir):
        os.mkdir(detectedDir)

    # delete all the files in the output directory
    files = os.listdir(detectedDir)
    for f in files:
        try:
            filepath = os.path.join(detectedDir, f)
            os.remove(filepath)
        except:
            pass
