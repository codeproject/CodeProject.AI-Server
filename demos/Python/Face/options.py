import os

class Options:

    serverPort    = 32168
    serverHost    = "localhost"
    serverUrl     = f"http://{serverHost}:{serverPort}/v1/"
    imageDir      = "../../TestData"

    # names of directories of interest
    detectedDir = "detected"

    def endpoint(self, route) -> str:
        return self.serverUrl + route

    def cleanDetectedDir(self) -> None:
        # make sure the detected directory exists
        if not os.path.exists(self.detectedDir):
            os.mkdir(self.detectedDir)

        # delete all the files in the output directory
        filelist = os.listdir(self.detectedDir)
        for filename in filelist:
            try:
                filepath = os.path.join(self.detectedDir, filename)
                os.remove(filepath)
            except:
                pass
