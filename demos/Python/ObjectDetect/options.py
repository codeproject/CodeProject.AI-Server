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
    rtsp_IP        = os.getenv("CPAI_RTSP_DEMO_IP",   "10.0.0.198")
    rtsp_url       = f"rtsp://{rtsp_user}:{rtsp_pass}@{rtsp_IP}/live"

    email_server   = os.getenv("CPAI_EMAIL_DEMO_SERVER",   "smtp.gmail.com")
    email_port     = int(os.getenv("CPAI_EMAIL_DEMO_PORT", 587))
    email_acct     = os.getenv("CPAI_EMAIL_DEMO_FROM",     "me@gmail.com")
    email_pwd      = os.getenv("CPAI_EMAIL_DEMO_PWD",      "password123")

    # names of directories of interest
    detectedDir = "detected"

    def endpoint(self, route) -> str:
        return self.server_url + route
