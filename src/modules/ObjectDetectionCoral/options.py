import os
from module_options import ModuleOptions

class Settings:
    def __init__(self, resolution, std_model_name, tpu_model_name, labels_name, tpu_segment_names):
        self.resolution        = resolution
        self.cpu_model_name    = std_model_name
        self.tpu_model_name    = tpu_model_name
        self.labels_name       = labels_name
        self.tpu_segment_names = tpu_segment_names

class Options:

    def __init__(self):

        # ----------------------------------------------------------------------
        # Setup constants

        # Models at:
        # https://coral.ai/models/object-detection/
        # https://github.com/MikeLud/CodeProject.AI-Custom-IPcam-Models/
        self.MODEL_SETTINGS = {
            "yolov5l":  Settings(448, 'yolov5l-int8.tflite',                    # 49Mb  # CPU model
                                      'yolov5l-int8_edgetpu.tflite',                    # TPU model
                                      'coco80_labels.txt',                              # labels
                                      ['yolov5l-int8_segment_0_of_7_edgetpu.tflite',    # TPU model segments
                                       'yolov5l-int8_segment_1_of_7_edgetpu.tflite',
                                       'yolov5l-int8_segment_2_of_7_edgetpu.tflite',
                                       'yolov5l-int8_segment_3_of_7_edgetpu.tflite',
                                       'yolov5l-int8_segment_4_of_7_edgetpu.tflite',
                                       'yolov5l-int8_segment_5_of_7_edgetpu.tflite',
                                       'yolov5l-int8_segment_6_of_7_edgetpu.tflite']),

            "yolov5m":  Settings(448, 'yolov5m-int8.tflite',                    # 22.7Mb
                                      'yolov5m-int8_edgetpu.tflite',
                                      'coco80_labels.txt',
                                      ['yolov5m-int8_segment_0_of_4_edgetpu.tflite',
                                       'yolov5m-int8_segment_1_of_4_edgetpu.tflite',
                                       'yolov5m-int8_segment_2_of_4_edgetpu.tflite',
                                       'yolov5m-int8_segment_3_of_4_edgetpu.tflite']),

            "yolov5s":  Settings(448, 'yolov5s-int8.tflite',                    # 7.9Mb
                                      'yolov5s-int8_edgetpu.tflite',
                                      'coco80_labels.txt',
                                      []),

            "yolov5n":  Settings(448, 'yolov5n-int8.tflite',                    # 2.3Mb
                                      'yolov5n-int8_edgetpu.tflite',
                                      'coco80_labels.txt',
                                      []),

            # Large: EfficientDet-Lite3x  90 objects, COCO 640x640x3    TF-lite v2    197.0 ms    43.9% mAP  20.6Mb
            "large":    Settings(640, 'efficientdet_lite3x_640_ptq.tflite',
                                      'efficientdet_lite3x_640_ptq_edgetpu.tflite',
                                      'coco_labels.txt',
                                      ['efficientdet_lite3x_640_ptq_segment_0_of_3_edgetpu.tflite',
                                       'efficientdet_lite3x_640_ptq_segment_1_of_3_edgetpu.tflite',
                                       'efficientdet_lite3x_640_ptq_segment_2_of_3_edgetpu.tflite']),
                                    
            # Medium: EfficientDet-Lite2  90 objects, COCO 448x448x3    TF-lite v2    104.6 ms    36.0% mAP  10.2Mb
            # Note: The compiler had trouble with EfficientDet-Lite3 and a large chunk didn't fit on
            # the TPU anyway, so we're using Lite2 since it fits well on 2 TPUs.
            "medium":   Settings(448, 'efficientdet_lite2_448_ptq.tflite',
                                      'efficientdet_lite2_448_ptq_edgetpu.tflite',
                                      'coco_labels.txt',
                                      ['efficientdet_lite2_448_ptq_segment_0_of_2_edgetpu.tflite',
                                       'efficientdet_lite2_448_ptq_segment_1_of_2_edgetpu.tflite']),

            # Small: EfficientDet-Lite1   90 objects, COCO 384x384x3    TF-lite v2    56.3 ms     34.3% mAP  7.6Mb
            "small":    Settings(384, 'efficientdet_lite1_384_ptq.tflite',
                                      'efficientdet_lite1_384_ptq_edgetpu.tflite',
                                      'coco_labels.txt',
                                      []),

            # Small: SSD MobileDet        90 objects, COCO 320x320x3    TF-lite v1    9.1 ms      32.9% mAP  5.1Mb
            # Faster than 'Small' with the tradeoff of slightly lower precision
            "small_lo": Settings(320, 'ssdlite_mobiledet_coco_qat_postprocess.tflite',
                                      'ssdlite_mobiledet_coco_qat_postprocess_edgetpu.tflite',
                                      'coco_labels.txt',
                                      []),

            # Tiny: MobileNet V2          90 objects, COCO 300x300x3    TF-lite v1    7.3 ms      25.6% mAP  6.6Mb
            "tiny":     Settings(300, 'ssd_mobilenet_v2_coco_quant_postprocess.tflite',
                                      'ssd_mobilenet_v2_coco_quant_postprocess_edgetpu.tflite',
                                      'coco_labels.txt',
                                      []),

            # Tiny: SSD MobileNet V2      90 objects, COCO 300x300x3    TF-lite v2    7.6 ms      22.4% mAP  6.7Mb
            # Similar to 'Tiny' with the tradeoff of slightly lower precision
            "tiny_lo":  Settings(300, 'tf2_ssd_mobilenet_v2_coco17_ptq.tflite',
                                      'tf2_ssd_mobilenet_v2_coco17_ptq_edgetpu.tflite',
                                      'coco_labels.txt',
                                      []),
        }

        self.ENABLE_MULTI_TPU                   = True
        
        self.MIN_CONFIDENCE                     = 0.5
        self.INTERPRETER_LIFESPAN_SECONDS       = 3600.0
        self.WATCHDOG_IDLE_SECS                 = 5.0       # To be added to non-multi code
        self.MAX_IDLE_SECS_BEFORE_RECYCLE       = 60.0      # To be added to non-multi code
        self.WARN_TEMPERATURE_THRESHOLD_CELSIUS = 80        # PCI only

        self.MAX_PIPELINE_QUEUE_LEN             = 1000      # Multi-only
        self.TILE_OVERLAP                       = 15        # Multi-only. Smaller number results in more tiles generated
        self.DOWNSAMPLE_BY                      = 5.8       # Multi-only
        self.IOU_THRESHOLD                      = 0.1       # Multi-only

        # ----------------------------------------------------------------------
        # Setup values

        self._show_env_variables = True

        self.module_path    = ModuleOptions.module_path
        self.models_dir     = os.path.normpath(ModuleOptions.getEnvVariable("MODELS_DIR", f"{self.module_path}/assets"))
        self.model_size     = ModuleOptions.getEnvVariable("MODEL_SIZE", "Small")   # small, medium, large

        # custom_models_dir = os.path.normpath(ModuleOptions.getEnvVariable("CUSTOM_MODELS_DIR", f"{module_path}/custom-models"))

        self.use_multi_tpu  = ModuleOptions.getEnvVariable("CPAI_CORAL_MULTI_TPU", str(self.ENABLE_MULTI_TPU)).lower() == "true"
        self.min_confidence = float(ModuleOptions.getEnvVariable("MIN_CONFIDENCE", self.MIN_CONFIDENCE))

        self.sleep_time     = 0.01

        # For multi-TPU tiling. Smaller number results in more tiles generated
        self.downsample_by  = float(ModuleOptions.getEnvVariable("CPAI_CORAL_DOWNSAMPLE_BY", self.DOWNSAMPLE_BY))
        self.tile_overlap   = int(ModuleOptions.getEnvVariable("CPAI_CORAL_TILE_OVERLAP",    self.TILE_OVERLAP))
        self.iou_threshold  = float(ModuleOptions.getEnvVariable("CPAI_CORAL_IOU_THRESHOLD", self.IOU_THRESHOLD))

        # Maybe - perhaps! - we need shorter var names
        self.watchdog_idle_secs           = float(ModuleOptions.getEnvVariable("CPAI_CORAL_WATCHDOG_IDLE_SECS",           self.WATCHDOG_IDLE_SECS))
        self.interpreter_lifespan_secs    = float(ModuleOptions.getEnvVariable("CPAI_CORAL_INTERPRETER_LIFESPAN_SECONDS", self.INTERPRETER_LIFESPAN_SECONDS))
        self.max_idle_secs_before_recycle = float(ModuleOptions.getEnvVariable("CPAI_CORAL_MAX_IDLE_SECS_BEFORE_RECYCLE", self.MAX_IDLE_SECS_BEFORE_RECYCLE))
        self.max_pipeline_queue_length    = int(ModuleOptions.getEnvVariable("CPAI_CORAL_MAX_PIPELINE_QUEUE_LEN",         self.MAX_PIPELINE_QUEUE_LEN))
        self.warn_temperature_thresh_C    = int(ModuleOptions.getEnvVariable("CPAI_CORAL_WARN_TEMPERATURE_THRESHOLD_CELSIUS", self.WARN_TEMPERATURE_THRESHOLD_CELSIUS))

        # Check input
        self.model_size = self.model_size.lower()
        if self.use_multi_tpu:
            model_valid = self.model_size in [ "yolov5l", "yolov5m", "yolov5s", "yolov5n", \
                                               "tiny", "small", "medium", "large",         \
                                               "small_lo", "tiny_lo" ]
        else:           
            model_valid = self.model_size in [ "tiny", "small", "medium", "large" ]
            
        if not model_valid:
            self.model_size = "small"

        # Get settings
        settings = self.MODEL_SETTINGS[self.model_size]   
        self.cpu_model_name = settings.cpu_model_name
        self.tpu_model_name = settings.tpu_model_name
        self.labels_name    = settings.labels_name
        if any(settings.tpu_segment_names):
            self.tpu_segment_names = settings.tpu_segment_names
        else:
            self.tpu_segment_names = []

        # pre-chew
        self.model_cpu_file    = os.path.normpath(os.path.join(self.models_dir, self.cpu_model_name))
        self.model_tpu_file    = os.path.normpath(os.path.join(self.models_dir, self.tpu_model_name))
        self.label_file        = os.path.normpath(os.path.join(self.models_dir, self.labels_name))
        self.tpu_segment_files = [os.path.normpath(os.path.join(self.models_dir, n)) for n in self.tpu_segment_names]

        # ----------------------------------------------------------------------
        # dump the important variables

        if self._show_env_variables:
            print(f"Debug: MODULE_PATH:    {self.module_path}")
            print(f"Debug: MODELS_DIR:     {self.models_dir}")
            print(f"Debug: MODEL_SIZE:     {self.model_size}")
            print(f"Debug: CPU_MODEL_NAME: {self.cpu_model_name}")
            print(f"Debug: TPU_MODEL_NAME: {self.tpu_model_name}")
