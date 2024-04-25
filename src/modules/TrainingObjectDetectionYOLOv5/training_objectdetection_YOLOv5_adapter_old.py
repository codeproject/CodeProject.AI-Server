# This file should contain only the code for the adapter. The adapter derives
# from ModuleRunner, overrides the methods, and makes calls to the core training
# code in training_objectdetection_YOLOv5.py

# Import our general libraries
from operator import truediv
import os
import functools
import json

import asyncio
from datetime import datetime, timedelta
from enum import Enum
import platform
import random
import psutil
import shutil
import sys
from typing import List

from scipy.special import k0

# Import the CodeProject.AI SDK. This will add to the PATH var for future imports
sys.path.append("../../SDK/Python")
from common import JSON, timedelta_format, get_folder_size
from request_data import RequestData
from module_runner import ModuleRunner
from module_logging import LogMethod
from module_options import ModuleOptions

# Import libraries specific to training
import tqdm
from pyexpat import model
import yaml
from urllib.request import Request

from yolov5.train import parse_opt
from yolov5.train import main as train_main
from yolov5.utils.callbacks import Callbacks
from yolov5.utils.plots import plot_results


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


# Enums ------------------------------------------------------------------------

# Actions are the actions that can be executed for the long running background
# tasks.
class Actions(Enum):
    Idle             = 0 # The module has restarted and nothing is happening.
    InvalidCommand   = 1 # an invalid Action was requested.
    TrainModel       = 2 # Training a model
    ResumeTrainModel = 3 # Resuming training a model
    CreateDataset    = 4 # Create a dataset

# ActionStates are the states that the background tasks can be in.
class ActionStates(Enum):
    Idle         = 0     # Nothing is happening
    Initializing = 1     # the Action is Initializing
    Running      = 2     # the Action is Running
    Completed    = 3     # the Action successfully completed
    Cancelling   = 4     # a request to cancel the Action was received
    Cancelled    = 5     # the Action was Cancelled
    Failed       = 6     # the Action Failed due to an Error

# A simple progress handler ----------------------------------------------------

class ProgressHandler:
    def __init__(self):
        self.progress_max   = 100
        self.progress_value = 0

    @property
    def max(self):
        return self.progress_max

    @max.setter
    def max(self, max_value:int) -> None:
        self.progress_max = max(1, max_value)

    @property
    def value(self) -> int:
        return self.progress_value

    @value.setter
    def value(self, val: int) -> None:
        self.progress_value = max(0, min(val, self.progress_max))

    @property
    def percent_done(self) -> float:
        return self.progress_value * 100 / self.progress_max # progress_max is always >= 1


# the ModuleRunner ------------------------------------------------------------

class YoloV5Trainer_adaptor(ModuleRunner):

    def initialise(self):
        """ Initialises this module """

        # Process settings
        self.selftest_check_pkgs = False # Too messy, will fail
        self.parallelism         = 2     # One for background task
                                         # One to process other requests

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
        self.datasets_dirname          = ModuleOptions.getEnvVariable("YOLO_DATASETS_DIRNAME",    "datasets")
        self.training_dirname          = ModuleOptions.getEnvVariable("YOLO_TRAINING_DIRNAME",    "train")
        self.models_dirname            = ModuleOptions.getEnvVariable("YOLO_MODELS_DIRNAME",      "assets")
        self.weights_dirname           = ModuleOptions.getEnvVariable("YOLO_WEIGHTS_DIRNAME",     "weights")
        self.zoo_dirname               = ModuleOptions.getEnvVariable("YOLO_DATASET_ZOO_DIRNAME", "zoo")

        # Training Settings
        self.model_name                = None
        self.dataset_name              = None
        self.num_epochs                = 0

        self.current_action            = Actions.Idle
        self.action_state              = ActionStates.Idle
        self.worker_thread             = None
        self.worker_thread_aborted     = False   
        self.action_message            = ""
        self.cancel_requested          = False

        self.progress                  = ProgressHandler()

        # We don't have a self test yet, and this is expensive
        if not self._performing_self_test:
            self.init_fiftyone()
            self.init_custom_callbacks()


    @property
    def is_busy(self) -> bool:
        """ Returns True if we're currently running any major process """

        # Since only one background action is allowed at a time, we are busy
        # if the worker_thread exists and is not done.
        if not self.worker_thread:
            return False
        
        return not self.worker_thread.done()
    

    async def process(self, data: RequestData) -> JSON:
        """
        Processes a request from the server. Gets the command from the request
        and dispatches to the appropriate function.
        """

        if not data or not hasattr(data, "command"):
            return {"success": False, "error": "Request data has no command."}

        # Map of the available commands
        available_actions = {
            "create_dataset":  self.start_create_dataset_action,
            "train_model":     self.start_train_model_action,
            "resume_training": self.start_resume_train_model_action,
            "list-classes":    self.list_classes,
            "get_status":      self.get_status,
            "model_info":      self.get_model_info,
            "dataset_info":    self.get_dataset_info,
            "cancel":          self.cancel_current_action
        }

        # Get the command
        requested_action = available_actions.get(data.command, self.handle_invalid_action)

        # Execute the command
        return requested_action(data)


    def status(self, data: RequestData = None) -> JSON:
        """
        Called when this module has been asked to provide its current status.
        """
        # print("Getting status for training")
        return self.get_status(data)


    def selftest(self, data: RequestData = None) -> JSON:
        """
        Called to run general tests against this module to ensure it's in good
        working order
        """
        print("Running self test for training module")
        return { "success": True, "message": "[Null test: No test was actually performed]" }


    # COMMAND SWITCHBOARD ------------------------------------------------------

    def start_action(self, action, **kwargs):

        """ Sets things up and calls the model training routine """

        # Initialize the settings
        self.model_name                     = kwargs.get('model_name')
        self.dataset_name                   = kwargs.get('dataset_name')

        # Initialise the state        
        self.current_action                 = action
        self.action_state                   = ActionStates.Initializing
        self.training_start_time            = datetime.now()
        self.progress.value                 = 0
        self.cancel_requested               = False
        self.custom_callbacks.stop_training = False

        # NOTE: We've observed, possibly hallucinated, thread/task abort issues
        # where methods just fail and return without throwing exceptions. We need
        # to ensure we only set 'success' if the methods actually return True.
        # if this is not cleared by the finally code then the thread was aborted.
        self.worker_thread_aborted          = True

        try:
            if action == Actions.CreateDataset:
                self.check_memory()
                success = self.create_dataset(**kwargs)

            elif action == Actions.TrainModel:
                self.check_memory()
                success = self.train_model(**kwargs)

            elif action == Actions.ResumeTrainModel:
                self.check_memory()
                # set the progress value to non-zero so that the graphs will display
                self.progress.value = 1
                success = self.resume_train_model(**kwargs)

            else:
                self.action_state   = ActionStates.Failed
                self.action_message = f"I don't know how to do {action}"
                return
            
            # NOTE: on a task/thread abort, we won't get here and 
            # self.action_completed will not be set to False in the finally clause.

            if self.cancel_requested:
                self.action_state   = ActionStates.Cancelled
                self.action_message = "Operation was cancelled"
            else:
                self.action_state = ActionStates.Completed

        except MemoryError as me:
            self.report_error(me, __file__, str(me))
            self.action_state   = ActionStates.Failed
            self.action_message = "Memory: " + str(me)

        except Exception as e:
            self.report_error(e, __file__)
            self.action_state   = ActionStates.Failed
            self.action_message = str(e)

        finally:
            self.worker_thread_aborted = False


    def cancel_current_action(self, data: RequestData) -> any:
        if self.is_busy:
            self.action_state     = ActionStates.Cancelling
            self.cancel_requested = True
            return { "success": True }

        return {"success": False, "error": "No Action in running to cancel."}

    def handle_invalid_action(self, data: Request) -> any:
        self.current_action = Actions.InvalidCommand
        self.action_state   = ActionStates.Failed
        self.report_error(None, __file__, f"Unknown command {data.command}")
        return {"success": False, "error": f"Unknown command {data.command}"}


    # DATASET CREATION METHODS -------------------------------------------------

    def start_create_dataset_action(self, data: RequestData) -> any:

        # there can only be one background Action running at a time.
        if self.is_busy:
            return { "success": False, "error": "Action in Progress" }

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
            
        loop = asyncio.get_running_loop()

        self.worker_thread = loop.run_in_executor(None, functools.partial(
                                                    self.start_action,
                                                    Actions.CreateDataset,
                                                    dataset_name = dataset_name, 
                                                    classes      = classes,
                                                    num_images   = num_images)
                                                 )
                
        return { "success": True, "message": f"Starting to create dataset {dataset_name}." }

    def create_dataset(self, **kwargs) -> bool:
        """ Downloads a dataset """
        dataset_name = kwargs.get('dataset_name')
        classes      = kwargs.get('classes')
        num_images   = kwargs.get('num_images')
        # Already imported, so these won't do any database setup (hopefully),
        # but we need to 'import' again to get access to the namespace
        import fiftyone as fo
        import fiftyone.zoo as foz
        import fiftyone.utils.openimages as fouo

        self.action_state   = ActionStates.Running


        download_splits = ['train', 'validation', 'test']
        export_splits = ['train', 'val', 'test']
        # Export the Dataset
        export_dir = f'{self.datasets_dirname}/{dataset_name}'

        if os.path.exists(export_dir):
            shutil.rmtree(export_dir)

        label_types = ["detections"]

        # This will throw on invalid class name.
        normalized_classes = self.normalize_classlist(classes)
        num_classes = len(normalized_classes)
        #  1 init, 5 for each class/split (4 loading, 1 exporting). 'units' are arbitrary here
        self.progress.max   = 1 + num_classes * 5 * len(export_splits) 
        self.action_message = "Acquiring training data"

        if fo.dataset_exists(dataset_name):
            fo.delete_dataset(dataset_name)

        self.progress.value += 1 # basic init done

        if self.cancel_requested:
            return False

        if fo.dataset_exists(dataset_name):
            fo.delete_dataset(dataset_name)

        class_index = 1
        for current_class in normalized_classes:
            for split in download_splits:
                self.action_message = f"{class_index}/{num_classes}: Loading {split} split for '{current_class}' from Open Images"
 
                # this results in a 60, 20, 20 split for train, validation, test
                num_samples = num_images if split == 'train' else num_images // 3
                
                dataset = foz.load_zoo_dataset('open-images-v7',
                                           splits=split,
                                           label_types=label_types,
                                           classes = current_class,
                                           #only_matching = True,
                                           max_samples=num_samples,
                                           #seed=42,
                                           shuffle=True,
                                           dataset_name=dataset_name)

                self.progress.value += 4      # This is a really long step, so boost it

                if self.cancel_requested:
                    return False

                self.action_message = f"Export {split} split for '{current_class}' to '{export_dir}'"
 
                dataset.export(export_dir  = export_dir,
                               dataset_type= fo.types.YOLOv5Dataset,
                               label_field = 'ground_truth',
                               split       = 'val' if split == 'validation' else split,
                               classes     = normalized_classes)

                fo.delete_dataset(dataset_name);
        
                self.progress.value += 1    # +1 for each export, 3 in total

                if self.cancel_requested:
                    return False

            class_index += 1

        self.action_state    = ActionStates.Completed
        self.action_message  = "Dataset successfully created"

        # Here would be the place to write a marker or info file that would 
        # indicate that the dataset is complete
        dataset_info = {
            "name" : dataset_name,
            "classes" : normalized_classes,
            "num_images" : num_images,
            "created" : datetime.now().isoformat()
        }
        info_filename = self.get_dataset_info_filename(dataset_name)
        with open(info_filename, 'w') as f:
            f.write(json.dumps(dataset_info))
            
        return True
    
    def get_dataset_info_filename(self, dataset_name: str) -> str:
        return os.path.join(self.datasets_dirname, dataset_name, "info.json")
    
    # TRAINING METHODS ---------------------------------------------------------

    def start_train_model_action(self, data: RequestData) -> any:

        # there can only be one background Action running at a time.
        if self.is_busy:
            return { "success": False, "error": "Action in Progress" }

        # Get parameters
        model_name   = data.get_value("model_name")
        if not model_name:
            return { "success": False, "error": "Model name is required." }

        dataset_name = data.get_value("dataset_name")
        if not dataset_name:
            return { "success": False, "error": "Dataset name is required." }

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

        loop = asyncio.get_running_loop()

        self.worker_thread = loop.run_in_executor(None, functools.partial(
                                                    self.start_action,
                                                    Actions.TrainModel,
                                                    model_name   = model_name, 
                                                    dataset_name = dataset_name,
                                                    model_size   = model_size,
                                                    epochs       = num_epochs, 
                                                    batch_size   = batch_size,
                                                    device       = device, 
                                                    freeze       = freeze,
                                                    hyp_type     = hyp_type,
                                                    patience     = patience,
                                                    workers      = workers)
                                                 )
                
        # NOTE: The process we started is still running. From here on updates
        #       to progress are made via the status APIs

        return { "success": True, "message": F"Starting to train model {model_name}" }


    def start_resume_train_model_action(self, data: RequestData) -> any:

        # there can only be one background Action running at a time.
        if self.is_busy:
            return { "success": False, "error": "Action in Progress" }

        # Get parameters
        model_name = data.get_value("model_name")
        if not model_name:
            return { "success": False, "error": "Model name is required." }

        loop = asyncio.get_running_loop()

        self.worker_thread = loop.run_in_executor(None, functools.partial(
                                                    self.start_action,
                                                    Actions.ResumeTrainModel,
                                                    model_name = model_name)
                                                 )
                
        # We won't wait for the task to end. We'll return now and let the
        # (probably very long) task continue in the background.
        # await self.task_executor
        return { "success": True, "message": F"Resuming training for model '{model_name}'" }


    # Callbacks for monitoring progress ----------------------------------------

    def on_train_start(self):
        self.action_message = f"Starting to train model '{self.model_name}'"
        pass

    def on_train_epoch_start(self):
        
        self.epoch_start_time = datetime.now()
        training_project_dir = f'{self.training_dirname}/{self.model_name}'
        results_csv_path     = os.path.join(training_project_dir, "results.csv")

        if os.path.exists(results_csv_path):
            plot_results(results_csv_path)  # plot 'results.csv' as 'results.png'

        self.check_for_cancel_requested()

    def on_fit_epoch_end(self, logvals, epoch, best_fitness, fi):
        
        epochs_processed = epoch + 1

        self.progress.value = epochs_processed

        total_training_seconds = (datetime.now() - self.training_start_time).total_seconds()
        current_epoch_seconds  = (datetime.now() - self.epoch_start_time).total_seconds()
        
        # The time taken for each epoch changes. For best results we'll base time
        # left on the latest epoch rather than the first, or the average of all
        # epochs. We'll converge to a more accurate value faster.
        seconds_left           = (self.num_epochs - epochs_processed) * current_epoch_seconds
        
        time_spent             = timedelta_format(timedelta(seconds=total_training_seconds))
        time_remaining         = timedelta_format(timedelta(seconds=seconds_left))
        self.action_message    = f"Epoch {epoch+1}/{self.num_epochs}. Duration: {time_spent} Remaining: {time_remaining}"

    def on_train_end(self, last, best, epoch, results):
        self.progress.value   = self.num_epochs

    def check_for_cancel_requested(self):
        """ Checks to see if a request to cancel training has been received """
        if self.cancel_requested:
            self.custom_callbacks.stop_training = True

    def init_custom_callbacks(self):
        """ Sets up the callbacks for each training event """

        self.custom_callbacks = Callbacks()
        self.custom_callbacks.register_action("on_train_start",       callback=self.check_for_cancel_requested)
        self.custom_callbacks.register_action("on_train_epoch_start", callback=self.on_train_epoch_start)
        self.custom_callbacks.register_action("on_train_batch_start", callback=self.check_for_cancel_requested)
        self.custom_callbacks.register_action("on_val_start",         callback=self.check_for_cancel_requested)
        self.custom_callbacks.register_action("on_val_batch_start",   callback=self.check_for_cancel_requested)
        self.custom_callbacks.register_action("on_fit_epoch_end",     callback=self.on_fit_epoch_end)
        self.custom_callbacks.register_action("on_train_end",         callback=self.on_train_end)


    # The actual training ------------------------------------------------------

    def train_model(self, **kwargs) -> bool:
        """ Does the actual model training """
        model_name          = kwargs.get('model_name')
        num_epochs          = kwargs.get('epochs')
        model_size          = kwargs.get('model_size')
        dataset_name        = kwargs.get('dataset_name')
        hyp_type            = kwargs.get('hyp_type')
        
        self.num_epochs     = num_epochs
        self.action_state   = ActionStates.Initializing
        self.action_message = f"Preparing to train model '{model_name}'"

        self.progress.max = num_epochs

        self.log(LogMethod.Info|LogMethod.Server, {
            "message": f"Training the {model_name} model",
            "loglevel": "information"
        })

        training_project_dir = f'{self.training_dirname}/{model_name}'
        if os.path.exists(training_project_dir):
            shutil.rmtree(training_project_dir)

        # NOTE: We're going to force model size and hyperparameter file type to
        #       be valid values even if the user inputs garbage. Our goal here
        #       is to teach and spread the love, and that sometimes means
        #       politely moving on rather than pointing out the user messed up.

        weights_filename = 'yolov5s.pt'
        model_size = model_size.lower()
        if model_size == "tiny":
            weights_filename = 'yolov5n.pt'
        elif model_size == "small":
            weights_filename = 'yolov5s.pt'
        elif model_size == "medium":
            weights_filename = 'yolov5m.pt'
        elif model_size == "large":
            weights_filename = 'yolov5l.pt'

        self.action_message = f"Using {model_size} model {weights_filename} for training";

        hyp_name = "hyp.VOC.yaml"
        hyp_type = hyp_type.lower()
        if hyp_type == "fine":
            hyp_name = "hyp.VOC.yaml"           # fine-tuned on the VOC dataset
        elif hyp_type == "low":
            hyp_name = "hyp.scratch-low.yaml" 
        elif hyp_type == "medium":
            hyp_name = "hyp.scratch-med.yaml" 
        elif hyp_type == "high":
            hyp_name = "hyp.scratch-high.yaml" 
                
        # The hyp file is under <site-packages>/yolov5/data/hyps/, where venv is
        # the current virtual environments's site-packages folder
        hyp_file_path = os.path.join(self.python_pkgs_dir, "yolov5", "data", "hyps", hyp_name)
        if not os.path.exists(hyp_file_path):
            raise FileNotFoundError(f"The hyper-parameter file {hyp_file_path} does not exist.")
        
        # try to use the dataset name as a full path to the dataset directory.
        dataset_yaml_path = os.path.join(dataset_name, 'dataset.yaml')
        if not os.path.exists(dataset_yaml_path):
            dataset_yaml_path = os.path.join(self.datasets_dirname, dataset_name,'dataset.yaml')
            
        if not os.path.exists(dataset_yaml_path):
            raise FileNotFoundError(f"The Dataset {dataset_name} does not exist.")

        self.action_state   = ActionStates.Running
        kwargs['name']      = model_name
        kwargs['weights']   = f"{self.models_dirname}/{weights_filename}"
        kwargs['data']      = dataset_yaml_path
        kwargs['project']   = self.training_dirname
        kwargs['hyp']       = hyp_file_path
        
        return self.train(**kwargs)

    def resume_train_model(self, **kwargs) -> bool:
        """ Does the actual model training """
        model_name          = kwargs.get('model_name')
        self.action_state   = ActionStates.Initializing
        self.action_message = f"Preparing to resume training model '{model_name}'"
        self.log(LogMethod.Info|LogMethod.Server, {
            "message": f"Resume Training model '{model_name}'",
            "loglevel": "information"
        })

        last_checkpoint = os.path.join(self.training_dirname, model_name, "weights", "last.pt")
        if not os.path.exists(last_checkpoint):
            raise FileNotFoundError(f"A checkpoint does not exist for {model_name}")

        # read the num_epoch for the opt.yaml file
        opt_yaml_path = os.path.join(self.training_dirname, model_name, "opt.yaml")
        if not os.path.exists(opt_yaml_path):
            raise FileNotFoundError(f"A opt.yaml file not exist for {model_name}")
        
        with open(opt_yaml_path, errors='ignore') as f:
            d = yaml.safe_load(f)
        
        # Get the number of epochs for which the model is being trained.
        num_epochs = d['epochs']
        self.num_epochs = num_epochs
        self.progress.max = num_epochs

        # Get the name of the dataset on which the model is being trained.
        dataset_name = d['data']
        parts = dataset_name.split('/')
        if len(parts) > 1:
            dataset_name = parts[len(parts) - 2]
            self.dataset_name = dataset_name

        self.action_state   = ActionStates.Running

        # pass the resume parameter to the train method with the checkpoint
        return self.train(resume = last_checkpoint)


    def train(self, **kwargs) -> bool:
        """ Does the call to train the model """

        opt = parse_opt(True)
        for k, v in kwargs.items():
            setattr(opt, k, v)

        if not self.cancel_requested:
            try:
                self.training_start_time = datetime.now()

                train_main(opt, callbacks=self.custom_callbacks)

                duration   = (datetime.now() - self.training_start_time).total_seconds()
                time_spent = timedelta_format(timedelta(seconds=duration))

                if self.cancel_requested:
                    return False
                
                self.action_state   = ActionStates.Completed
                self.action_message = f"Model '{self.model_name}' training completed in {time_spent}"
                return True
            
            except Exception as e:
                self.report_error(e, __file__, str(e))
                return False


    # STATUS METHODS -----------------------------------------------------------

    def get_status(self, data: RequestData) -> any:
        """
        Returns the current status of the last started Action.
        """

        is_training         = (self.current_action == Actions.TrainModel or \
                               self.current_action == Actions.ResumeTrainModel) \
                               and self.action_state == ActionStates.Running
        is_creating_dataset = (self.current_action == Actions.CreateDataset) \
                               and self.action_state == ActionStates.Running

        progress            = self.progress.percent_done
        training_progress   = progress if is_training else 0
        dataset_progress    = progress if is_creating_dataset else 0

        if self.current_action == Actions.Idle:
            self.action_message = "Ready"
        ## The following lines are commented out because they overwrite the 
        ## action_messages set by the training and dataset creation callbacks.
        # elif is_creating_dataset:
        #     self.action_message = "Creating dataset"
        # elif is_training:
        #     self.action_message = "Training model"
        elif self.current_action == Actions.InvalidCommand:
            self.action_message = "Looking confused"

        elif not self.is_busy and self.worker_thread_aborted:
            # the background_worker was aborted with prejudice.
            self.action_state   = ActionStates.Failed
            self.action_message = f"{self.current_action.name} was Aborted."

        return { 
            "success":             True, 
            "model_name":          self.model_name,
            "dataset_name":        self.dataset_name,
            "action":              self.current_action.name,
            "state":               self.action_state.name,
            "message":             self.action_message,
            "is_busy":             self.is_busy,
            "progress":            progress,
        }

    def get_model_info(self, data: RequestData) -> any:
        """ Returns an object representing the current state of the model """        

        model_name = data.get_value("model_name")
        if not model_name:
            return { "success": False, "error": "Model Name not specified." }

        training_project_dir = os.path.join(self.module_path, self.training_dirname,
                                            model_name)
        if not os.path.exists(training_project_dir):
            return { "success": False, "error": "Training was not started on this model." }

        model_path         = os.path.join(training_project_dir, self.weights_dirname, "best.pt")
        results_graph_path = os.path.join(training_project_dir, "results.png")
        results_csv_path   = os.path.join(training_project_dir, "results.csv")
        pr_curve_path      = os.path.join(training_project_dir, "PR_curve.png")

        model_size = 0
        if os.path.exists(model_path):
            stats = os.stat(model_path)           
            model_size = round(stats.st_size / (1024 * 1000), 1)

        # Trim the root from this path. This may cause gnashing of teeth to those
        # who want the full path, but we're going to have people posting screen
        # shots of their window and so we have to remove the sensitive info
        rootPrefix = "" # "&lt;app&gt;";
        display_model_path = model_path or ""
        if display_model_path.startswith(self.server_root_path):
            display_model_path = rootPrefix + display_model_path[len(self.server_root_path):]

        display_graph_path = results_graph_path or ""
        if display_graph_path.startswith(self.server_root_path):
            display_graph_path = rootPrefix + display_graph_path[len(self.server_root_path):]
        
        display_csv_path = results_csv_path or ""
        if display_csv_path.startswith(self.server_root_path):
            display_csv_path = rootPrefix + display_csv_path[len(self.server_root_path):]
        
        display_curve_path = pr_curve_path or ""
        if display_curve_path.startswith(self.server_root_path):
            display_curve_path = rootPrefix + display_curve_path[len(self.server_root_path):]

        # Don't return graph image data if action==[TrainingModel, ResumeTrainingModel]
        # and worker_thread is running and progress.value == 0 as the information 
        # is not yet valid.
        # Reason: there is a gap between when training starts and the system has
        # information about the current model. Until then, there may be information
        # from a previous training of the Model. If this is called when not 
        # training, then we want to attempt to get the information as it currently
        # exists
        model_info_valid = not ( \
            (self.current_action in [Actions.TrainModel, Actions.ResumeTrainModel]) \
            and self.is_busy and self.progress.value == 0)

        model_created        = model_info_valid and os.path.exists(pr_curve_path)

        results_csv_exists   = os.path.exists(results_csv_path)   and model_info_valid
        return_pr_curve      = os.path.exists(pr_curve_path)      and model_info_valid
        return_results_graph = os.path.exists(results_graph_path) and model_info_valid

        return { 
            "success":             True, 
            "training_dir":        training_project_dir,
            "model_created":       model_created,

            "results_graph_path":  display_graph_path,
            "results_graph_image": RequestData.encode_file_contents(results_graph_path) if return_results_graph else "",

            "pr_curve_path":       display_curve_path,
            "pr_curve_image":      RequestData.encode_file_contents(pr_curve_path)      if return_pr_curve      else "",

            "results_csv_path":    display_csv_path,
            "results_csv_file":    RequestData.encode_file_contents(results_csv_path)   if results_csv_exists   else "",

            "model_size":          model_size,
            "model_path":          display_model_path,
            # "model_file":        RequestData.encode_file_contents(model_path), # This could be HUGE.

            # To have this model_file automatically downloaded in the browser we 
            # could do something like:
            #
            # let file = new File(model_file, `{model_name}.pt``, {type: "application/octet-stream"});
            # let downloadUrl = window.URL.createObjectURL(model_file);
            # let link = document.createElement('a');
            # link.style    = 'display:none';
            # link.href     = downloadUrl;
            # link.download = filename;
            # link.click();
            # window.URL.revokeObjectURL(downloadUrl);
            #
            # However, we should be providing sensible means to use the model from
            # the UI itself rather than asking users to download / upload themselves.
        }

    def get_dataset_info(self, data: RequestData) -> any:
        """ Returns an object representing the current state of the model """        

        # Already imported, so these won't do any database setup (hopefully),
        # but we need to 'import' again to get access to the namespace
        import fiftyone as fo
        import fiftyone.zoo as foz
        import fiftyone.utils.openimages as fouo

        dataset_name = data.get_value("dataset_name")
        if not dataset_name:
            return { "success": False, "error": "Dataset name not specified." }

        dataset_path = os.path.join(self.module_path, self.datasets_dirname, dataset_name)
        if not os.path.exists(dataset_path):
            return { "success": False, "error": "No dataset exists with this name." }

        dataset_size    = get_folder_size(dataset_path)

        # after the dataset has been created, the info file will be present.
        dataset_info_filename = self.get_dataset_info_filename(dataset_name)
        dataset_created       = os.path.exists(dataset_info_filename)

        # Trim the root from this path. This may cause gnashing of teeth to those
        # who want the full path, but we're going to have people posting screen
        # shots of their window and so we have to remove the sensitive info
        rootPrefix = "" # "&lt;app&gt;";
        display_dataset_path = dataset_path or ""
        if display_dataset_path.startswith(self.server_root_path):
            display_dataset_path = rootPrefix + display_dataset_path[len(self.server_root_path):]

        return { 
            "success":         True, 
            "training_dir":    dataset_path,
            "dataset_created": dataset_created,
            "dataset_size":    round(dataset_size / (1024 * 1000), 1),
            "dataset_path":    display_dataset_path,
        }

    def list_classes(self, data: RequestData) ->any:
        return { 
            "success": True, 
            "classes": self.available_classes
        }


    # UTILITY METHODS ----------------------------------------------------------

    def check_memory(self) -> bool:
        """ Check if we have enough memory, raises an error if not enough """

        if self.required_MB: 
            available_MB = psutil.virtual_memory().available / (1024 * 1000)
            if available_MB < self.required_MB:
                raise MemoryError(f"Need {self.required_MB}Mb, only {round(available_MB,0)}Mb available")
    

    def normalize_classlist(self, classes : List[str]) -> List[str]:
        """ 
        This method converts a list of classes to the normalized values used by
        Open Images. Class names are case sensitive. If a class can not be found,
        then an Exception is Raised to quickly abort the operation and report
        the error to the user so that they can correct the mistake. 
        """

        if not classes:
            raise Exception(f"The list of class names is empty.")

        # create the lookup if required.
        if not self.available_classes:
            # Already imported, so these won't do any database setup (hopefully),
            # but we need to 'import' again to get access to the namespace
            import fiftyone.utils.openimages as fouo
            self.available_classes = fouo.get_classes()

        if not self.available_classes_lower:
            self.available_classes_lower   = [class_name.lower() for class_name in self.available_classes]

        # TODO: Rework this to use a dictionary keyed by class.lower()

        classes_lower = [class_name.lower() for class_name in classes]
        found_classes = [] 
        for class_lower in classes_lower: 
            try: 
                idx = self.available_classes_lower.index(class_lower) 
                found_classes.append(self.available_classes[idx]) 
            except ValueError: 
                raise Exception(f"Cannot find class {class_lower} in available classes.") 

        return found_classes


    def init_fiftyone(self):

        # This module is reloaded by spawn.py inside numpy. There's some 
        # processing we need to do to import fiftyone, so let's do this only
        # when we're actually running the code, not each time we import this
        # module

        # We still need to import modules so we have access to the namespace,
        # but once a module has been imported within a module, it's just accessed
        # via a lookup, and doesn't actually go through all the init code.

        # Keep things neat, and also attempt to mitigate permission issues with the 
        # fiftyone mongodb by having it all sit under the current module's folder
        fiftyone_dirname = ModuleOptions.getEnvVariable("FIFTYONE_DATABASE_DIRNAME", "fiftyone")
        fiftyone_path = os.path.normpath(os.path.join(ModuleOptions.module_path, fiftyone_dirname))
        os.environ["FIFTYONE_DATABASE_DIR"] = fiftyone_path

        # We'll import and fail quickly if needed
        try:
            import fiftyone.zoo as foz
        except Exception as zoo_ex:
            # Clear the problem for next time
            shutil.rmtree(fiftyone_path)
            print("Unable to import and initialise the fiftyone.zoo package: " + str(zoo_ex))
            quit(1)

        try:
            import fiftyone as fo
        except Exception as ex:
            if 'fiftyone.core.service.DatabaseService failed to bind to port' in str(ex):
                print("Failed to connect to mongoDB server. Possibly it was left in a bad state") 
            else:
                print("Unable to import and initialise the fiftyone package: " + str(zoo_ex))
            quit(1)

        import fiftyone.utils.openimages as fouo

        # configure FiftyOne
        fo.config.default_ml_backend   = "torch"
        fo.config.dataset_zoo_dir      =  os.path.join(self.module_path, self.zoo_dirname)
        fo.config.show_progress_bars   = False
        fo.config.do_not_track         = True
        self.available_classes         = fouo.get_classes()
        self.available_classes_lower   = None
        
        print("*** FiftyOne imported successfully")


if __name__ == "__main__":
    YoloV5Trainer_adaptor().start_loop()