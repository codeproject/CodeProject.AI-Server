# This file uses the YoloV5DatasetCreator and YoloV5ModelTrainer classes to create
# datasets and train YOLOv5 6.2 models.

# Import the CodeProject.AI SDK. This will add to the PATH for future imports
import sys
from time import sleep
sys.path.append("../../SDK/Python")

from common import JSON
import tqdm
import psutil


from request_data import RequestData
from module_runner import ModuleRunner
from module_options import ModuleOptions
from module_logging import LogMethod, LogVerbosity
 
# Import the method of the module we're wrapping
from utils import Actions, ActionStates
from fiftyone_dataset_creator import YoloV5DatasetCreator
from YOLOV5_Trainer import YoloV5ModelTrainer

# HACK: ========================================================================
# Monkey Patch tqdm so that all instances are disabled. This stops the training 
# from filling the log with tons of stuff written to the console. This must be
# after all the imports that directly or indirectly import tqdm.
# Note that we only do this for modules launched by the server. Modules launched
# from the debugger or otherwise separately won't have their stdout/stderr 
# captured and so should continue to use the console for output

if ModuleOptions.launched_by_server:
    original_tqdm_init = tqdm.tqdm.__init__
    def new_init(self, iterable=None, desc=None, total=None, leave=True, file=None,
                 ncols=None, mininterval=0.1, maxinterval=10.0, miniters=None,
                 ascii=None, disable=False, unit='it', unit_scale=False,
                 dynamic_ncols=False, smoothing=0.3, bar_format=None, initial=0,
                 position=None, postfix=None, unit_divisor=1000, write_bytes=False,
                 lock_args=None, nrows=None, colour=None, delay=0, gui=False,
                 **kwargs):
        original_tqdm_init(self, iterable=iterable, desc=desc, total=total,
                           leave=leave, file=file, ncols=ncols, mininterval=mininterval,
                           maxinterval=maxinterval, miniters=miniters, ascii=ascii,
                           disable=True, unit=unit, unit_scale=unit_scale,
                           dynamic_ncols=dynamic_ncols, smoothing=smoothing, 
                           bar_format=bar_format, initial=initial, position=position,
                           postfix=postfix, unit_divisor=unit_divisor, 
                           write_bytes=write_bytes, lock_args=lock_args, nrows=nrows,
                           colour=colour, delay=delay, gui=gui, **kwargs)

    tqdm.tqdm.__init__ = new_init
    

class YoloV5Trainer_adaptor(ModuleRunner):

    def initialise(self) -> None:
        """ Initialises this module """

        # Process settings
        self.selftest_check_pkgs = False # Too messy, will fail
        self.parallelism         = 1     # There can be only one

        # determine the device to use during training
        self.default_device = "cpu"
        if ModuleOptions.enable_GPU:
            if self.system_info.hasTorchCuda:
                self.default_device    = "cuda" # or cuda:0, cuda:1 etc
                self.inference_device  = "GPU"
                self.inference_library = "CUDA"
            elif self.system_info.hasTorchMPS:
                self.default_device    = "mps"
                self.inference_device  = "GPU"
                self.inference_library = "MPS"

        # Global Settings
        self.datasets_dirname          = ModuleOptions.getEnvVariable("YOLO_DATASETS_DIRNAME",     "datasets")
        self.training_dirname          = ModuleOptions.getEnvVariable("YOLO_TRAINING_DIRNAME",     "train")
        self.models_dirname            = ModuleOptions.getEnvVariable("YOLO_MODELS_DIRNAME",       "assets")
        self.weights_dirname           = ModuleOptions.getEnvVariable("YOLO_WEIGHTS_DIRNAME",      "weights")
        self.zoo_dirname               = ModuleOptions.getEnvVariable("YOLO_DATASET_ZOO_DIRNAME",  "zoo")
        self.fiftyone_dirname          = ModuleOptions.getEnvVariable("FIFTYONE_DATABASE_DIRNAME", "fiftyone")

        self.current_action            = Actions.Idle
        self.action_state              = ActionStates.Idle
        self.action_message            = ""
        
        ## initializing the dataset creator can fail, so we retry a few times
        num_retries = 3
        while num_retries > 0:
            try:
                print (F"Initializing YoloV5DatasetCreator. Retries left: {num_retries}")
                self.dataset_creator = YoloV5DatasetCreator(self,
                                                            self.module_path,
                                                            self.fiftyone_dirname,
                                                            self.zoo_dirname,
                                                            self.datasets_dirname,
                                                            self.server_root_path)
                break
            
            except Exception as e:
                self.report_error(e, __file__)
                num_retries -= 1
                if num_retries == 0:
                    raise ## re-raise the exception
                
            # sleep(1)
        
        self.current_dataset_name = None
        
        self.model_trainer = YoloV5ModelTrainer(self,
                                                self.module_path, 
                                                self.datasets_dirname,
                                                self.training_dirname, 
                                                self.models_dirname,
                                                self.weights_dirname, 
                                                self.server_root_path,
                                                self.python_pkgs_dir)
        
        self.current_model_name = None
           
        self.cancel_requested = False

    def process(self, data: RequestData) -> JSON:
        """
        Processes a request from the server. Gets the command from the request
        and dispatches to the appropriate function.
        """

        if not data or not hasattr(data, "command"):
            return {"success": False, "error": "Request data has no command."}

        command = data.command;
        
        # the long-running commands
        if command == "create-dataset":
            return self.start_long_running_command(Actions.CreateDataset, self.create_dataset, data)
        elif command == "train-model":
            return self.start_long_running_command(Actions.TrainModel, self.train_model, data)
        elif command == "resume-training":
            return self.start_long_running_command(Actions.ResumeTrainModel, self.resume_training, data)
        
        # the short-running commands
        elif command == "list-classes":
            return self.list_classes(data)
        elif command == "model-info":
            return self.get_model_info(data)
        elif command == "dataset-info":
            return self.get_dataset_info(data)
        
        # what are you talking about Willis?
        else:
            return {"success": False, "error": "Invalid command."}
        
    def start_long_running_command(self, action: Actions, method, data: RequestData) -> JSON:
        """
        Starts a long running command.
        """
        # if self.current_action != Actions.Idle:
        #     return { "success": False, "error": "Another command is already running." }
            
            
        try:
            self.check_memory()
            self.method_to_execute = method
            return self.long_process
        

        except MemoryError as me:
            self.report_error(me, __file__, str(me))
            self.action_state   = ActionStates.Failed
            self.action_message = "Memory: " + str(me)
               
    def long_process(self, data: RequestData) -> JSON:
        self.action_state   = ActionStates.Initializing
        self.action_message = ""
        try:
            result = self.method_to_execute(data)
            
            if self.cancel_requested:
                self.action_state   = ActionStates.Cancelled
                self.action_message = "Operation was cancelled"
            else:
                self.action_state = ActionStates.Completed
                
        except Exception as e:
            self.report_error(e, __file__)
            self.action_state   = ActionStates.Failed
            self.action_message = str(e)
            result = {};
            
        finally:
            self.cancel_requested = False
    
        return result
      
############### Long Running Commands ####################

    def create_dataset(self, data: RequestData) -> JSON:
        """
        Creates a dataset for training a YOLOv5 model.
        """
        self.current_action = Actions.CreateDataset
        # Get parameters
        dataset_name = data.get_value("dataset_name")
        if not dataset_name:
            return { "success": False, "error": "Dataset name is required." }

        classes = data.get_value("classes")
        if not classes:
            return { "success": False, "error": "Classes are required." }
        
        classes = classes.split(",")
        for idx, item in enumerate(classes):
            classes[idx] = item.strip()

        num_images = data.get_int("num_images", 100)
        num_images = data.clamp(num_images, 10, 10000)
        
        self.current_dataset_name = dataset_name
        self.current_model_name = ""
        
        # call the create_dataset method from the YoloV5Dataset class
        result = self.dataset_creator.create_dataset(dataset_name, classes, num_images)

        # TODO: return the result of the method
        return self.command_status()
    
    def train_model(self, data: RequestData) -> JSON:
        """
        Trains a YOLOv5 model.
        """
        self.current_action = Actions.TrainModel
        # Get parameters
        model_name   = data.get_value("model_name")
        if not model_name:
            return { "success": False, "error": "Model name is required." }

        dataset_name = data.get_value("dataset_name")
        if not dataset_name:
            return { "success": False, "error": "Dataset name is required." }
        
        self.current_model_name   = model_name
        self.current_dataset_name = dataset_name

        model_size   = data.get_value("model_size", "small").lower()
        model_size   = data.restrict(model_size, [ "tiny", "small", "medium", "large" ], "small")

        # TODO: add min,max to data.get_* methods to have clamp done in same op
        num_epochs   = data.get_int("num_epochs", 100)
        num_epochs   = data.clamp(num_epochs, 10, 1000)

        # -1 = autosize
        batch_size   = data.get_int("batch", 8)
        batch_size   = data.clamp(batch_size, -1, 256)
          
        freeze       = data.get_int("freeze", 10)
        freeze       = data.clamp(freeze, 0, 24)

        hyp_type     = data.get_value("hyp", "fine")
        hyp_type     = data.restrict(hyp_type, [ "fine", "low", "medium", "high" ], "fine")

        patience     = data.get_int("patience", 100)
        patience     = data.clamp(patience, 0, 1000)

        workers      = data.get_int("workers", 8)
        workers      = data.clamp(workers, 1, 128)

        device = "cpu"
        if self.inference_device == "GPU":
            if self.inference_library == "MPS":
                device = "mps"
            elif self.inference_library == "CUDA":
                device = data.get_value("device", self.default_device)
                
        # call the train_model method from the YoloV5Trainer class
        
        result = self.model_trainer.train_model(model_name   = model_name, 
                                                dataset_name = dataset_name,
                                                model_size   = model_size,
                                                epochs       = num_epochs, 
                                                batch_size   = batch_size,
                                                device       = device, 
                                                freeze       = freeze,
                                                hyp_type     = hyp_type,
                                                patience     = patience,
                                                workers      = workers)
    
         # TODO: return the result of the method
        return self.command_status()

    def resume_training(self, data: RequestData) -> JSON:
        """
        Resumes training a YOLOv5 model.
        """
        self.current_action = Actions.ResumeTrainModel
         # Get parameters
        model_name = data.get_value("model_name")
        self.current_model_name = model_name
        self.current_dataset_name = "Unknown"
        if not model_name:
            return { "success": False, "error": "Model name is required." }

        self.current_model_name = model_name
        # call the resume_training method from the YoloV5Trainer class
        result =  self.model_trainer.resume_train_model(model_name = model_name)
        
        # TODO: return the result of the method
        return self.command_status()

############### Short Running Commands ###################

    def list_classes(self, data: RequestData) -> JSON:
        """
        Lists the classes in the dataset.
        """
        # call the list_classes method from the YoloV5Dataset class
        return self.dataset_creator.list_classes()
        
    def get_model_info(self, data: RequestData) -> JSON:
        """
        Gets information about the model.
        """
        # Get parameters
        model_name = data.get_value("model_name")
        if not model_name:
            return { "success": False, "error": "Model name is required." }

        # call the get_model_info method from the YoloV5Trainer class
        return self.model_trainer.get_model_info(model_name)
        
    def get_dataset_info(self, data: RequestData) -> JSON:
        """
        Gets information about the dataset.
        """
        dataset_name = data.get_value("dataset_name")
        if not dataset_name:
            return { "success": False, "error": "Dataset name is required." }

        return self.dataset_creator.get_dataset_info(dataset_name)
        
############### Status Commands ###################

    def command_status(self) -> JSON:
        """
        Returns the status of the long process. The response should contain
        information about the current command and will be different depending
        on the command.
        """
        running_state = {}
        if self.current_action == Actions.CreateDataset:
            dataset_info  = self.dataset_creator.get_dataset_info(self.current_dataset_name)
            dataset_info["success"] = True
                
            running_state = {
                "action":   self.current_action.name,
                "progress": self.dataset_creator.progress.percent_done,
                "state":    self.dataset_creator.action_state.name,
                "message":  self.dataset_creator.action_message,
                "dataset_name": self.current_dataset_name
            }
            running_state.update(dataset_info)
            
        elif self.current_action == Actions.TrainModel or \
            self.current_action == Actions.ResumeTrainModel:
            model_info = self.model_trainer.get_model_info(self.current_model_name)
            model_info["success"] = True
           
            running_state = {
                "action":   self.current_action.name,
                "progress": self.model_trainer.progress.percent_done,
                "state":    self.model_trainer.action_state.name,
                "message":  self.model_trainer.action_message
            }
            
            running_state.update(model_info)
        return running_state
        
        return {
            "success": False, "error": "Invalid Module State"
        }
    
    def cancel_command_task(self):
        if (not self.cancel_requested):
            self.cancel_requested = True   # We will cancel this long process ourselves
            self.action_state     = ActionStates.Cancelling
            self.dataset_creator.cancel()
            self.model_trainer.cancel()
            
        self.force_shutdown   = False  # Tell ModuleRunner not to go ballistic

    ##### Other functions
    def check_memory(self) -> bool:
        """ Check if we have enough memory, raises an error if not enough """

        if self.required_MB: 
            available_MB = psutil.virtual_memory().available / (1024 * 1000)
            if available_MB < self.required_MB:
                raise MemoryError(f"Need {self.required_MB}Mb, only {round(available_MB,0)}Mb available")
    
        
# This is the entry point for the module
if __name__ == "__main__":
    YoloV5Trainer_adaptor().start_loop()
