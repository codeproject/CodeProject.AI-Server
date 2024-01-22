from module_options import ModuleOptions

class Options:

    def __init__(self):
        
        # Cartooniser settings
        self.use_gpu       = ModuleOptions.enable_GPU  # We'll disable this if we can't find GPU libraries
        self.weights_dir   = ModuleOptions.getEnvVariable("WEIGHTS_FOLDER", "weights")
        self.model_name    = ModuleOptions.getEnvVariable("MODEL_NAME",     "celeba_distill")

        # model names:  'face_paint_512_v1', 'face_paint_512_v2', 'celeba_distill', 'paprika' 