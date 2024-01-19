import os
from module_options import ModuleOptions

class Options:

    def __init__(self):

        # -------------------------------------------------------------------------
        # Setup constants

        self.NUM_THREADS    = 1
        self.MIN_CONFIDENCE = 0.1
        
        # -------------------------------------------------------------------------
        # Setup values

        self._show_env_variables = True

        self.module_path    = ModuleOptions.module_path
        self.models_dir     = os.path.normpath(ModuleOptions.getEnvVariable("MODELS_DIR", f"{self.module_path}/assets"))
        self.model_size     = ModuleOptions.getEnvVariable("MODEL_SIZE", "Small")   # small, medium, large

        # custom_models_dir = os.path.normpath(ModuleOptions.getEnvVariable("CUSTOM_MODELS_DIR", f"{module_path}/custom-models"))

        self.num_threads    = int(ModuleOptions.getEnvVariable("NUM_THREADS",      self.NUM_THREADS))
        self.min_confidence = float(ModuleOptions.getEnvVariable("MIN_CONFIDENCE", self.MIN_CONFIDENCE))

        self.sleep_time     = 0.01


        # Get settings
        # self.cpu_model_name = settings.cpu_model_name
        # self.labels_name    = settings.labels_name

        # pre-chew
        # self.model_tpu_file = os.path.normpath(os.path.join(self.models_dir, self.tpu_model_name))
        # self.label_file     = os.path.normpath(os.path.join(self.models_dir, self.labels_name))

        # -------------------------------------------------------------------------
        # dump the important variables

        if self._show_env_variables:
            print(f"Debug: MODULE_PATH:    {self.module_path}")
            print(f"Debug: MODELS_DIR:     {self.models_dir}")
