# This file is to contain the core training code using the YoloV5 package. 
# It should expose simple methods or an object with methods that can be called 
# from the adapter.
# Import libraries specific to training
from datetime import datetime, timedelta
import os
import shutil
from common import JSON, timedelta_format
from module_logging import LogMethod
from module_runner import ModuleRunner, RequestData
import tqdm
from pyexpat import model
import yaml
from urllib.request import Request

from yolov5.train import parse_opt
from yolov5.train import main as train_main
from yolov5.utils.callbacks import Callbacks
from yolov5.utils.plots import plot_results

from utils import ProgressHandler, ActionStates, Actions, InitializationError

class YoloV5ModelTrainer:
    def __init__(self, runner:ModuleRunner, module_path:str, datasets_dirname:str, training_dirname:str,
                 models_dirname:str, weights_dirname:str, server_root_path:str,
                 python_plgs_dir:str):
        self.runner           = runner
        self.module_path      = module_path
        self.datasets_dirname = datasets_dirname
        self.training_dirname = training_dirname
        self.models_dirname   = models_dirname
        self.weights_dirname  = weights_dirname
        self.server_root_path = server_root_path
        self.python_pkgs_dir  = python_plgs_dir
        self.progress         = ProgressHandler()
        self.action_state     = ActionStates.Idle
        self.action_message   = ""
        self.cancel_requested = False
        
        self.model_name       = None
        self.dataset_name     = None
        self.progress         = ProgressHandler()
        self.custom_callbacks = None
        self.init_custom_callbacks()
        
    def cancel(self):
        cancelable_states = [ActionStates.Running, ActionStates.Initializing]
        if self.action_state in cancelable_states:
            self.cancel_requested = True
            self.action_state     = ActionStates.Cancelling
            self.action_message   = "Cancelling..."

     # The actual training ------------------------------------------------------

    def train_model(self, **kwargs) -> bool:
        """ Does the actual model training """
        self.model_name     = kwargs.get('model_name')
        self.dataset_name   = kwargs.get('dataset_name')
        num_epochs          = kwargs.get('epochs')
        model_size          = kwargs.get('model_size')
        hyp_type            = kwargs.get('hyp_type')
        
        self.num_epochs     = num_epochs
        self.action_state   = ActionStates.Initializing
        self.action_message = f"Preparing to train model '{self.model_name}'"

        self.progress.max = num_epochs
        self.progress.value = 0

        self.runner.log(LogMethod.Info|LogMethod.Server, {
            "message": f"Training the {self.model_name} model",
            "loglevel": "information"
        })

        training_project_dir = f'{self.training_dirname}/{self.model_name}'
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
        dataset_yaml_path = os.path.join(self.dataset_name, 'dataset.yaml')
        if not os.path.exists(dataset_yaml_path):
            dataset_yaml_path = os.path.join(self.datasets_dirname, self.dataset_name,'dataset.yaml')
            
        if not os.path.exists(dataset_yaml_path):
            raise FileNotFoundError(f"The Dataset {self.dataset_name} does not exist.")

        self.action_state   = ActionStates.Running
        kwargs['name']      = self.model_name
        kwargs['weights']   = f"{self.models_dirname}/{weights_filename}"
        kwargs['data']      = dataset_yaml_path
        kwargs['project']   = self.training_dirname
        kwargs['hyp']       = hyp_file_path
        
        return self.train(**kwargs)

    def resume_train_model(self, **kwargs) -> bool:
        """ Does the actual model training """
        self.model_name     = kwargs.get('model_name')
        self.action_state   = ActionStates.Initializing
        self.action_message = f"Preparing to resume training model '{self.model_name}'"
        self.runner.log(LogMethod.Info|LogMethod.Server, {
            "message": f"Resume Training model '{self.model_name}'",
            "loglevel": "information"
        })

        last_checkpoint = os.path.join(self.training_dirname, self.model_name, "weights", "last.pt")
        if not os.path.exists(last_checkpoint):
            raise FileNotFoundError(f"A checkpoint does not exist for {self.model_name}")

        # read the num_epoch for the opt.yaml file
        opt_yaml_path = os.path.join(self.training_dirname, self.model_name, "opt.yaml")
        if not os.path.exists(opt_yaml_path):
            raise FileNotFoundError(f"A opt.yaml file not exist for {self.model_name}")
        
        with open(opt_yaml_path, errors='ignore') as f:
            d = yaml.safe_load(f)
        
        # Get the number of epochs for which the model is being trained.
        num_epochs          = d['epochs']
        self.num_epochs     = num_epochs
        self.progress.max   = num_epochs
        self.progress.value = 0

        # Get the name of the dataset on which the model is being trained.
        dataset_name = d['data']
        parts = dataset_name.split(os.sep)
        if len(parts) > 1:
            dataset_name      = parts[len(parts) - 2]
            self.dataset_name = dataset_name

        self.action_state   = ActionStates.Running

        # pass the resume parameter to the train method with the checkpoint
        return self.train(resume = last_checkpoint)
       
    def train(self, **kwargs) -> bool:
        """ Does the call to train the model """
        self.cancel_requested               = False
        self.custom_callbacks.stop_training = False

        opt = parse_opt(True)
        for k, v in kwargs.items():
            setattr(opt, k, v)

        try:
            if not self.cancel_requested:
                self.training_start_time = datetime.now()

                train_main(opt, callbacks=self.custom_callbacks)

                duration   = (datetime.now() - self.training_start_time).total_seconds()
                time_spent = timedelta_format(timedelta(seconds=duration))

                if self.cancel_requested:
                    self.action_state    = ActionStates.Cancelled
                    self.action_message  = "Train Model Cancelled"
                    return False
                
                self.action_state   = ActionStates.Completed
                self.action_message = f"Model '{self.model_name}' training completed in {time_spent}"
                return True
            
        except Exception as e:
            self.runner.report_error(e, __file__, str(e))
            return False
            
        finally:
            self.cancel_requested = False

    def get_model_info(self, model_name:str) -> JSON:
        """ Returns an object representing the current state of the model """        

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

        model_size_mb = 0
        if os.path.exists(model_path):
            stats = os.stat(model_path)           
            model_size_mb = round(stats.st_size / (1024 * 1000), 1)

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

        model_created        = os.path.exists(pr_curve_path)

        results_csv_exists   = os.path.exists(results_csv_path)
        return_pr_curve      = os.path.exists(pr_curve_path)
        return_results_graph = os.path.exists(results_graph_path)

        return { 
            "success":             True, 
            "model_name":          model_name,
            "dataset_name":        self.dataset_name,
            "training_dir":        training_project_dir,
            "model_created":       model_created,

            "results_graph_path":  display_graph_path,
            "results_graph_image": RequestData.encode_file_contents(results_graph_path) if return_results_graph else "",

            "pr_curve_path":       display_curve_path,
            "pr_curve_image":      RequestData.encode_file_contents(pr_curve_path)      if return_pr_curve      else "",

            "results_csv_path":    display_csv_path,
            "results_csv_file":    RequestData.encode_file_contents(results_csv_path)   if results_csv_exists   else "",

            "model_size_mb":       model_size_mb,
            "model_path":          display_model_path,
        }

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


