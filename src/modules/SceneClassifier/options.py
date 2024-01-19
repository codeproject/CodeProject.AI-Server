import os
from module_options import ModuleOptions

class Options:

    def __init__(self):

        self._show_env_variables = True

        self.app_dir            = os.path.normpath(ModuleOptions.getEnvVariable("APPDIR", os.getcwd()))
        self.models_dir         = os.path.normpath(ModuleOptions.getEnvVariable("MODELS_DIR", f"{self.app_dir}/assets"))

        self.sleep_time         = 0.01

        self.model_size         = ModuleOptions.getEnvVariable("MODEL_SIZE", "Medium")   # small, medium, large //, nano, x-large
        self.use_CUDA           = ModuleOptions.getEnvVariable("USE_CUDA",   "True")     # True / False
        self.use_MPS            = False   # Default is False, but we'll enable if possible

        # -------------------------------------------------------------------------
        # dump the important variables

        if self._show_env_variables:
            print(f"Debug: APPDIR:      {self.app_dir}")
            print(f"Debug: MODEL_SIZE:  {self.model_size}")
            print(f"Debug: MODELS_DIR:  {self.models_dir}")
