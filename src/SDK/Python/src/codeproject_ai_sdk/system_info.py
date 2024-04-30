# Import standard libs
import os
import platform
from platform import uname
import sys

# These are annoying and often unavoidable
import warnings
warnings.simplefilter("ignore", DeprecationWarning)

class SystemInfo:
    """
    A collection of methods to get information on the system hardware, identification, OS and
    libraries
    """

    def __init__(self) -> None:
        """ 
        Constructor. 
        """
        # Private fields
        self._osVersion              = None

        self._hasTorchCuda           = None
        self._hasTorchROCm           = None
        self._hasTorchDirectML       = None
        self._hasTorchHalfPrecision  = None
        self._hasTorchMPS            = None

        self._hasTensorflowGPU       = None

        self._hasONNXRuntime         = None
        self._hasONNXRuntimeGPU      = None
        self._hasOpenVINO            = None

        self._hasPaddleGPU           = None

        self._hasCoralTPU            = None
        self._hasFastDeployRockNPU   = None

        # Public fields -------------------------------------------------------

        # Hardware / accelerator info
        self.cpu_brand           = ""
        self.cpu_vendor          = ""
        self.cpu_arch            = ""

        # What OS, architecture and system are we running on?
        self.os     = { 'Linux': 'Linux', 'Darwin': 'macOS', 'Windows': 'Windows'}[platform.system()]
        self.system = self.os
        self.in_WSL = self.os == 'Linux' and 'microsoft-standard-WSL' in uname().release

        # Further tests for Micro devices
        if self.os == 'Linux': 
            try:
                import io
                with io.open('/sys/firmware/devicetree/base/model', 'r') as m:
                    model_info = m.read().lower()
                    if 'raspberry pi' in model_info:
                        self.system = 'Raspberry Pi'
                    elif 'orange pi' in model_info:
                        self.system = 'Orange Pi'
                    elif 'radxa rock' in model_info:
                        self.system = 'Radxa ROCK'
            except Exception: 
                pass

        # ...and for Jetson
        if self.os == 'Linux': 
            try:
                import io
                with io.open('/proc/device-tree/model', 'r') as m:
                    if 'nvidia jetson' in m.read().lower(): 
                        self.system = 'Jetson'
            except Exception:
                pass 

        # HACK: For the QEngineering Ubuntu 20.04 image for Jetson Nano we point
        # the python package importer to the pre-installed packages (if exists)
        if self.system == 'Jetson' and \
           os.path.exists("/usr/local/lib/python3.8/dist-packages/torch/"):
            sys.path.insert(0, "/usr/local/lib/python3.8/dist-packages/")

        # Get some (very!) basic CPU info
        try:
            import cpuinfo
            info = cpuinfo.get_cpu_info()
            self.cpu_brand = info.get('brand_raw')
            self.cpu_arch  = info.get('arch_string_raw')
        except:
            self.cpu_brand = ""
            self.cpu_arch  = ""

        self.cpu_vendor = self.cpu_brand
        if self.cpu_brand:
            if self.cpu_brand.startswith("Apple M"):
                self.cpu_vendor = 'Apple'
                self.cpu_arch   = 'arm64'
            elif self.cpu_brand.find("Intel(R)") != -1:
                self.cpu_vendor = 'Intel'
                

    @property
    def osVersion(self) -> str:

        if self._osVersion != None:
            return self._osVersion
        
        self._osVersion = ""

        if self.os == 'Windows':
            self._osVersion = platform.version()
        elif self.os == 'macOS':
            self._osVersion = platform.mac_ver()[0]
        elif self.os == 'Linux':
            try:
                with open('/etc/os-release') as file:
                    for line in file:
                        if line.startswith('VERSION='):
                            # Extract the version string and return it
                            self._osVersion = line.split('=')[1].strip().strip('"')
                            break
            except FileNotFoundError:
                pass    
        
        return self._osVersion


    @property
    def getCudaVersion(self) -> "tuple[int, int]":
        try:
            import subprocess
            output = subprocess.check_output(["nvcc", "--version"]).decode('utf-8')
        
            version_line = [line for line in output.split('\n') if 'release' in line][0]
            version = version_line.split(' ')[-1]
            major, minor = version.split('.')[:2]
            return int(major), int(minor)
        
        except Exception:
            try:
                output = subprocess.check_output(["nvidia-smi"]).decode('utf-8')
                version_line = [line for line in output.split('\n') if 'CUDA Version' in line][0]
                version = version_line.split(' ')[-1]
                major, minor = version.split('.')[:2]
                return int(major), int(minor)
            
            except Exception:
                return (None, None)

    @property
    def hasNvidiaGPU(self) -> bool:
        """ Returns True if a CUDA enabled GPU is present and available """
        return self.getCudaVersion is not None

    @property
    def hasTorchCuda(self) -> bool:
        """ Is CUDA support via PyTorch available? """

        if self._hasTorchCuda == None:
            self._hasTorchCuda = False
            try:
                import torch
                self._hasTorchCuda = torch.cuda.is_available()

                # TODO: Should also run torch.utils.collect_env to ensure a 
                # PyTorch version with a CUDA runtime is installed.
                
            except: pass
        return self._hasTorchCuda

    @property
    def hasTorchDirectML(self) -> bool:
        """ Is DirectML support via PyTorch available? """

        if self._hasTorchDirectML == None:
            self._hasTorchDirectML = False
            if self.in_WSL or self.system == "Windows":
                try:
                    import torch
                    import torch_directml
                    self._hasTorchDirectML = True
                except: pass
        return self._hasTorchDirectML

    @property
    def hasTorchROCm(self) -> bool:
        """ Is ROCm (AMD GPU) support via PyTorch available? """
        
        if self._hasTorchROCm == None:
            self._hasTorchROCm = False

            if self.os == 'Linux' or self.os == 'Windows':
                try:
                    import subprocess

                    devices = []
                    process_result = subprocess.run(['rocminfo'], stdout=subprocess.PIPE)
                    cmd_str = process_result.stdout.decode('utf-8')
                    cmd_split = cmd_str.split('Agent ')
                    for part in cmd_split:
                        item_single = part[0:1]
                        item_double = part[0:2]
                        if item_single.isnumeric() or item_double.isnumeric():
                            new_split = cmd_str.split('Agent '+item_double)
                            output = new_split[1].split('Marketing Name:')[0]
                            output = output.replace('  Name:                    ', '').replace('\n','')
                            output = output.replace('                  ','')
                            device = output.split('Uuid:')[0].split('*******')[1]
                            devices.append(device)
                    self._hasTorchROCm = len(devices) > 0
                except: pass
            
        return self._hasTorchROCm

    @property
    def hasTorchHalfPrecision(self) -> bool:
        """ Can this (assumed) NVIDIA GPU support half-precision operations? """

        if self._hasTorchHalfPrecision == None:
            self._hasTorchHalfPrecision = False
            try:
                # Half precision supported on Pascal architecture, which means compute
                # capability 6.0 and above
                import torch
                self._hasTorchHalfPrecision = torch.cuda.get_device_capability()[0] >= 6

                # Except...that's not the case in practice. Below are the cards that
                # also seem to have issues
                if self._hasTorchHalfPrecision:
                    problem_childs = [
                        
                        # FAILED:
                        # GeForce GTX 1650, GeForce GTX 1660
                        # T400, T600, T1000

                        # WORKING:
                        # Quadro P400, P600
                        # GeForce GT 1030, GeForce GTX 1050 Ti, 1060, 1070, and 1080
                        # GeForce RTX 2060 and 2070 (and we assume GeForce RTX 2080)
                        # Quadro RTX 4000 (and we assume Quadro RTX 5, 6, and 8000)
                        # Tesla T4

                        # Pascal - Compute Capability 6.1
                        "MX450", "MX550",                                   # unknown

                        # Turing - Compute Capability 7.5
                        "GeForce GTX 1650", "GeForce GTX 1660",             # known failures
                        "T400", "T500", "T600", "T1000", "T1200", "T2000",  # T400, T600, T1000 known failures
                        "TU102", "TU104", "TU106", "TU116", "TU117"         # unknown
                    ]
                    card_name = torch.cuda.get_device_name()
        
                    self._hasTorchHalfPrecision = not any(check_name in card_name for check_name in problem_childs)                

            except: pass
        return self._hasTorchHalfPrecision

    @property
    def hasTensorflowGPU(self) -> bool:
        """ Is GPU support via Tensorflow available? """

        if self._hasTensorflowGPU == None:
            self._hasTensorflowGPU = False
            try:
                import tensorflow as tf
                self._hasTensorflowGPU = len(tf.config.list_physical_devices('GPU')) > 0                
            except: pass
        return self._hasTensorflowGPU

    @property
    def hasONNXRuntime(self) -> bool:
        """ Is the ONNX runtime available? """
        
        if self._hasONNXRuntime == None:
            self._hasONNXRuntime = False
            try:
                import onnxruntime as ort
                providers = ort.get_available_providers()
                self._hasONNXRuntime = len(providers) > 0
            except: pass
        return self._hasONNXRuntime

    @property
    def hasONNXRuntimeGPU(self) -> bool:
        """ Is the ONNX runtime available and is there a GPU that will support it? """

        if self._hasONNXRuntimeGPU == None:
            self._hasONNXRuntimeGPU = False
            try:
                import onnxruntime as ort
                self._hasONNXRuntimeGPU = ort.get_device() == "GPU"
            except: pass
        return self._hasONNXRuntimeGPU

    @property
    def hasOpenVINO(self) -> bool:
        """ Is OpenVINO available? """

        if self._hasOpenVINO == None:
            self._hasOpenVINO = False
            try:
                import openvino.utils as utils
                utils.add_openvino_libs_to_path()
                self._hasOpenVINO = True
            except: pass
        return self._hasOpenVINO

    @property
    def hasTorchMPS(self) -> bool:
        """ Are we running on Apple Silicon and is MPS support in PyTorch available? """

        if self._hasTorchMPS == None:
            self._hasTorchMPS = False
            if self.cpu_vendor == 'Apple' and self.cpu_arch == 'arm64':
                try:
                    import torch
                    self._hasTorchMPS = hasattr(torch.backends, "mps") and torch.backends.mps.is_available()
                except: pass
        return self._hasTorchMPS

    @property
    def hasPaddleGPU(self) -> bool:
        """ Is PaddlePaddle available and is there a GPU that supports it? """

        if self._hasPaddleGPU == None:
            self._hasPaddleGPU = False
            try:
                import paddle
                self._hasPaddleGPU = paddle.device.get_device().startswith("gpu")
            except: pass
        return self._hasPaddleGPU

    @property
    def hasCoralTPU(self) -> bool:
        """ Is there a Coral.AI TPU connected and are the libraries in place to support it? """

        if self._hasCoralTPU == None:
            self._hasCoralTPU = False

            # First see if the incredibly difficult to install python-pycoral pkg
            # can help us.
            try:
                from pycoral.utils.edgetpu import list_edge_tpus
                self._hasCoralTPU = len(list_edge_tpus()) > 0
                return self._hasCoralTPU
            except: pass

            # Second, determine if we have TensorFlow-Lite runtime installed, or 
            # the whole Tensorflow. In either case we're looking to load TFLite models
            try:
                try:
                    from tflite_runtime.interpreter import load_delegate
                except ImportError:
                    import tensorflow as tf
                    load_delegate = tf.lite.experimental.load_delegate

                # On Windows, the interpreter.__init__ method accepts experimental
                # delegates. These are used in self._interpreter.ModifyGraphWithDelegate, 
                # which fails on Windows
                delegate = {
                    'Linux': 'libedgetpu.so.1',
                    'Darwin': 'libedgetpu.1.dylib',
                    'Windows': 'edgetpu.dll'}[platform.system()]
                delegates = [load_delegate(delegate)]
                self._hasCoralTPU = len(delegates) > 0

                return self._hasCoralTPU
            except Exception as ex:
                pass

        return self._hasCoralTPU

    @property
    def hasFastDeployRockNPU(self) -> bool:
        """ Is the Rockchip NPU present (ie. on a Orange Pi) and supported by
            the fastdeploy library? """

        if self._hasFastDeployRockNPU == None:           
            self._hasFastDeployRockNPU = False
            try:
                from fastdeploy import RuntimeOption
                RuntimeOption().use_rknpu2()
                self._hasFastDeployRockNPU = True
            except: pass

        return self._hasFastDeployRockNPU

    # macOS kernel major version map to macOS name / version
    """
	[23, ['Sonoma', '14']],
	[22, ['Ventura', '13']],
	[21, ['Monterey', '12']],
	[20, ['Big Sur', '11']],
	[19, ['Catalina', '10.15']],
	[18, ['Mojave', '10.14']],
	[17, ['High Sierra', '10.13']],
	[16, ['Sierra', '10.12']],
	[15, ['El Capitan', '10.11']],
	[14, ['Yosemite', '10.10']],
	[13, ['Mavericks', '10.9']],
	[12, ['Mountain Lion', '10.8']],
	[11, ['Lion', '10.7']],
	[10, ['Snow Leopard', '10.6']],
	[9, ['Leopard', '10.5']],
	[8, ['Tiger', '10.4']],
	[7, ['Panther', '10.3']],
	[6, ['Jaguar', '10.2']],
	[5, ['Puma', '10.1']],
    """