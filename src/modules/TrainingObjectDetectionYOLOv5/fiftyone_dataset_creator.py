# This file is to contain the core dataset creation code using the FiftyOne package
# to use images from Google.
#  It should expose simple methods or an object with methods that can be called 
#  from the adapter.

import os
from typing import List
from datetime import datetime, timedelta
import json
import shutil
from common import JSON, get_folder_size

from module_runner import ModuleRunner, RequestData
from utils import Actions, ActionStates, ProgressHandler, InitializationError

class YoloV5DatasetCreator:
    def __init__(self, runner:ModuleRunner, module_path:str, fiftyone_dirname:str, zoo_dirname:str, 
                 datasets_dirname:str, server_root_path:str):
        self.runner           = runner
        self.module_path      = module_path
        self.fiftyone_dirname = fiftyone_dirname
        self.zoo_dirname      = zoo_dirname
        self.datasets_dirname = datasets_dirname
        self.server_root_path = server_root_path
        self.progress         = ProgressHandler()
        self.action_state     = ActionStates.Idle
        self.action_message   = ""
        self.cancel_requested = False
        
        self.init_fiftyone()
        

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
        fiftyone_path = os.path.normpath(os.path.join(self.module_path, self.fiftyone_dirname))
        os.environ["FIFTYONE_DATABASE_DIR"] = fiftyone_path

        # We'll import and fail quickly if needed
        try:
            import fiftyone.zoo as foz
        except Exception as zoo_ex:
            # Clear the problem for next time
            shutil.rmtree(fiftyone_path)
            message = F"Unable to import and initialise the fiftyone.zoo package: {zoo_ex}"
            print(message)
            raise InitializationError(message)

        try:
            import fiftyone as fo
        except Exception as ex:
            if 'fiftyone.core.service.DatabaseService failed to bind to port' in str(ex):
                message = "Failed to connect to mongoDB server. Possibly it was left in a bad state"
            else:
                message = F"Unable to import and initialise the fiftyone package:{ex}"
            raise InitializationError(message)

        import fiftyone.utils.openimages as fouo

        # configure FiftyOne
        fo.config.default_ml_backend   = "torch"
        fo.config.dataset_zoo_dir      =  os.path.join(self.module_path, self.zoo_dirname)
        fo.config.show_progress_bars   = False
        fo.config.do_not_track         = True
        self.available_classes         = fouo.get_classes()
        self.available_classes_lower   = None
        
        print("*** FiftyOne imported successfully")
        

    def create_dataset(self, dataset_name:str, classes:List[str], num_images:int) -> bool:
        try:
            self.cancel_requested = False
            self.action_state   = ActionStates.Initializing
            self.progress.value = 0
         
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
            try:
                normalized_classes = self.normalize_classlist(classes)
            except Exception as ex:
                self.action_state    = ActionStates.Failed
                self.action_message  = str(ex)
                return False
        
            num_classes = len(normalized_classes)
            #  1 init, 5 for each class/split (4 loading, 1 exporting). 'units' are arbitrary here
            self.progress.max   = 1 + num_classes * 5 * len(export_splits) 
            self.action_message = "Acquiring training data"

            if fo.dataset_exists(dataset_name):
                fo.delete_dataset(dataset_name)

            self.progress.value += 1 # basic init done

            if self.cancel_requested:
                self.action_state    = ActionStates.Cancelled
                self.action_message  = "Create Dataset Cancelled"
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
                        self.action_state    = ActionStates.Cancelled
                        self.action_message  = "Create Dataset Cancelled"
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
                        self.action_state    = ActionStates.Cancelled
                        self.action_message  = "Create Dataset Cancelled"
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
        
        finally:
            self.cancel_requested = False
    

    def cancel(self):
        cancelable_states = [ActionStates.Running, ActionStates.Initializing]
        if self.action_state in cancelable_states:
            self.cancel_requested = True
            self.action_state     = ActionStates.Cancelling
            self.action_message   = "Cancelling..."
        

    def get_state(self) -> (ActionStates, str):
        return self.action_state, self.action_message
 
    
    def list_classes(self) -> JSON:
        return { 
            "success": True, 
            "classes": self.available_classes
        }


    def get_dataset_info(self, dataset_name:str) -> any:
        """ Returns an object representing the current state of the model """        

        # Already imported, so these won't do any database setup (hopefully),
        # but we need to 'import' again to get access to the namespace
        import fiftyone as fo
        import fiftyone.zoo as foz
        import fiftyone.utils.openimages as fouo

        if not dataset_name:
            return { "success": False, "error": "Dataset name not specified." }

        dataset_path = os.path.join(self.module_path, self.datasets_dirname, dataset_name)
        if os.path.exists(dataset_path):
            dataset_size    = get_folder_size(dataset_path)
        else:
            dataset_size    = 0

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
            "success":              True, 
            "dataset_name":         dataset_name,
            "dataset_dir":          dataset_path ,
            "dataset_created":      dataset_created,
            "dataset_size_mb":      round(dataset_size / (1024 * 1000), 1),
            "display_dataset_path": display_dataset_path,
        }
    

    def get_dataset_info_filename(self, dataset_name: str) -> str:
        return os.path.join(self.datasets_dirname, dataset_name, "info.json")


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

