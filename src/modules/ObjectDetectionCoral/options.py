import os
from module_options import ModuleOptions

class Settings:
    def __init__(self, resolution, std_model_name, tpu_model_name, labels_name):
        self.resolution     = resolution
        self.cpu_model_name = std_model_name
        self.tpu_model_name = tpu_model_name
        self.labels_name    = labels_name

class Options:

    def __init__(self):

        # -------------------------------------------------------------------------
        # Setup constants

        # Models at https://coral.ai/models/object-detection/
        self.MODEL_SETTINGS = {
            # Large: SSD/FPN MobileNet V1 90 objects, COCO 640x640x3	TF-lite v2	229.4 ms	31.1% mAP
            "large":  Settings(640, 'tf2_ssd_mobilenet_v1_fpn_640x640_coco17_ptq.tflite',
                                    'tf2_ssd_mobilenet_v1_fpn_640x640_coco17_ptq_edgetpu.tflite',
                                    'tf2_ssd_mobilenet_v1_coco_labels.txt'),
            # Medium: EfficientDet-Lite3   90 objects, COCO 512x512x3	TF-lite v2	107.6 ms    39.4%  mAP
            "medium": Settings(512, 'efficientdet_lite3_512_ptq.tflite',
                                    'efficientdet_lite3_512_ptq_edgetpu.tflite',
                                    'efficientdet_lite3_512_ptq_labels.txt'),
            # Small: SSD/FPN MobileNet V2 90 objects, COCO 300x300x3	TF-lite v2	7.6 ms	22.4% mAP
            "small": Settings(300,  'tf2_ssd_mobilenet_v2_coco17_ptq.tflite',
                                    'tf2_ssd_mobilenet_v2_coco17_ptq_edgetpu.tflite',
                                    'tf2_ssd_mobilenet_v2_coco17_labels.txt'),

            # Tiny: MobileNet V2 90 objects, COCO 300x300x3	TF-lite v2 Quant
            "tiny": Settings(300,   'ssd_mobilenet_v2_coco_quant_postprocess.tflite',
                                    'ssd_mobilenet_v2_coco_quant_postprocess_edgetpu.tflite',
                                    'ssd_mobilenet_v2_coco_quant_postprocess_labels.txt'),
        }

        self.NUM_THREADS    = 1
        self.MIN_CONFIDENCE = 0.5
        
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

        # Normalise input
        self.model_size     = self.model_size.lower()
        if self.model_size == "tiny":
            self.model_size = "small"
        if self.model_size not in [ "tiny", "small", "medium", "large" ]:
            self.model_size = "small"

        # Get settings
        settings = self.MODEL_SETTINGS[self.model_size]   
        self.cpu_model_name = settings.cpu_model_name
        self.tpu_model_name = settings.tpu_model_name
        self.labels_name    = settings.labels_name

        # pre-chew
        self.model_cpu_file = os.path.normpath(os.path.join(self.models_dir, self.cpu_model_name))
        self.model_tpu_file = os.path.normpath(os.path.join(self.models_dir, self.tpu_model_name))
        self.label_file     = os.path.normpath(os.path.join(self.models_dir, self.labels_name))

        # -------------------------------------------------------------------------
        # dump the important variables

        if self._show_env_variables:
            print(f"Debug: MODULE_PATH:    {self.module_path}")
            print(f"Debug: MODELS_DIR:     {self.models_dir}")
            print(f"Debug: MODEL_SIZE:     {self.model_size}")
            print(f"Debug: CPU_MODEL_NAME: {self.cpu_model_name}")
            print(f"Debug: TPU_MODEL_NAME: {self.tpu_model_name}")
