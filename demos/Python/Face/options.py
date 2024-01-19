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

