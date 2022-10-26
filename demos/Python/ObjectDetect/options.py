import os

class Options:

    server_port    = 32168
    server_host    = "localhost"
    server_url     = f"http://{server_host}:{server_port}/v1/"
    image_dir      = "../../TestData"

    # Either set environment variables, or change the default values to what 
    # works for you
    rtsp_user      = os.getenv("CPAI_RTSP_DEMO_USER", "User")
    rtsp_pass      = os.getenv("CPAI_RTSP_DEMO_PASS", "Pass")
    rtsp_IP        = os.getenv("CPAI_RTSP_DEMO_IP",   "10.0.0.204")
    rtsp_url       = f"rtsp://{rtsp_user}:{rtsp_pass}@{rtsp_IP}/live"

    # names of directories of interest
    detectedDir = "detected"

    def endpoint(self, route) -> str:
        return self.server_url + route

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
