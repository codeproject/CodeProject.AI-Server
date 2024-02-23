# Lint as: python3
# Copyright 2023 Seth Price seth.pricepages@gmail.com
# Parts copyright 2019 Google LLC
#
# Licensed under the Apache License, Version 2.0 (the "License");
# you may not use this file except in compliance with the License.
# You may obtain a copy of the License at
#
#     https://www.apache.org/licenses/LICENSE-2.0
#
# Unless required by applicable law or agreed to in writing, software
# distributed under the License is distributed on an "AS IS" BASIS,
# WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
# See the License for the specific language governing permissions and
# limitations under the License.
import threading
import os
import platform
import time
import logging
import queue

from datetime import datetime

import numpy as np
from PIL import Image

# For Linux we have installed the pycoral libs via apt-get, not PIP in the venv,
# so make sure the interpreters can find the coral libraries
# COMMENTED: No longer the case. PyCoral now installed again via PIP
# import platform
# if platform.system() == "Linux":
#     import sys
#     version = sys.version_info
#     path = f"/usr/lib/python{version.major}.{version.minor}/site-packages/"
#     sys.path.insert(0, path)

try:
    from pycoral.utils.dataset import read_label_file
    from pycoral.utils.edgetpu import make_interpreter
    from pycoral.utils.edgetpu import list_edge_tpus
except ImportError:
    logging.exception("Missing pycoral function. Perhaps you are using a funky version of pycoral?")
    exit()

# Ok, here we need a few bugfixes in a custom pipeline runner...
import pipelined_model_runner as pipeline
from pycoral.adapters import detect

from options import Options


# Refresh the interpreters once an hour. I'm unsure if this is needed. There
# seems to be a memory leak in the C code that this doesn't fix. I believe the
# leak is related to the 'name' string here:
# https://github.com/google-coral/pycoral/blob/master/src/coral_wrapper.cc#L512
INTERPRETER_LIFESPAN_SECONDS = 3600

# Don't let the queues fill indefinitely until something more unexpected goes
# wrong. 1000 is arbitrarily chosen to block before things get ugly.
# It also implies that there are many threads calling into here and waiting on
# results. Our max queue lengths should never be more than
# calling_threads * tiles_per_image.
MAX_PIPELINE_QUEUE_LEN = 1000

# Warn if any TPU reads above this temperature C
# https://coral.ai/docs/pcie-parameters/#use-dynamic-frequency-scaling
WARN_TEMPERATURE_THRESHOLD_CELSIUS = 80

# Nothing should ever sit in a queue longer than this many seconds.
# 60 seconds is arbitrarily chosen to throw an error eventually.
MAX_WAIT_TIME = 60.0

# Check for longer than MAX_WAIT_TIME this often. Max wait could be long: we
# don't always want to wait this long when trying to shut things down
WATCHDOG_IDLE_SECS = 5.0


class TPURunner(object):

    def __init__(self):
        """
        Init object and do a check for the temperature file. Right now
        the temperature file would only be supported on Linux systems
        with the TPU installed on the PCIe bus. The Windows value is in
        the registry.
        """
        
        self.watchdog_idle_secs           = WATCHDOG_IDLE_SECS
        self.max_idle_secs_before_recycle = MAX_WAIT_TIME
        self.interpreter_lifespan_secs    = INTERPRETER_LIFESPAN_SECONDS
        self.max_pipeline_queue_length    = MAX_PIPELINE_QUEUE_LEN
        self.warn_temperature_thresh_C    = WARN_TEMPERATURE_THRESHOLD_CELSIUS

        self.device_type          = None  # The type of device in use (TPU or CPU, but we're going to ignore CPU here)
        self.device_count         = 0     # Number of devices (TPUs or single CPU) we end up using
        self.segment_count        = 1     # Number of segment files used (or 1 if a single file is used)   
        self.interpreters         = []    # The model interpreters
        self.interpreters_created = None  # When were the interpreters created?
        self.model_name           = None  # Name of current model in use
        self.model_size           = None  # Size of current model in use
        self.labels               = None  # set of labels for this model
        self.runners              = None  # Pipeline(s) to run the model

        self.next_runner_idx      = 0     # Which runner to execute on next?
        self.postboxes            = None  # Store output
        self.postmen              = None
        self.runner_lock          = threading.Lock()
        
        self.last_check_timer     = None
        self.last_check_interpreter_creation_timer = None
        self.printed_shape_map    = {}

        self.watchdog_time        = None
        self.watchdog_shutdown    = False
        self.watchdog_thread      = threading.Thread(target=self._watchdog)
        self.watchdog_thread.start()

        logging.info(f"{Image.__name__} version: {Image.__version__}")

        # Find the temperature file
        # https://coral.ai/docs/pcie-parameters/
        temp_fname_formats = ['/dev/apex_{}/temp',
                              '/sys/class/apex/apex_{}/temp']
        self.temp_fname_format = None

        tpu_count = len(list_edge_tpus())

        if platform.system() == "Linux":
            for fn in temp_fname_formats:
                for i in range(tpu_count):
                    if os.path.exists(fn.format(i)):
                        self.temp_fname_format = fn
                        logging.info("Found temperature file at: "+fn.format(i))
                        return
            logging.debug("Unable to find a temperature file")


    def _watchdog(self):
        self.watchdog_time = time.time()
        while not self.watchdog_shutdown:
            if any(self.interpreters) and \
               time.time() - self.watchdog_time > self.max_idle_secs_before_recycle*2:
                 logging.warning("No work in {} seconds, watchdog shutting down TPU runners!".format(self.max_idle_secs_before_recycle*2))
                 self.runner_lock.acquire(timeout=self.max_idle_secs_before_recycle)
                 self._delete()
                 self.runner_lock.release()
                 # Pipeline will reinitialize itself as needed
            time.sleep(self.watchdog_idle_secs)

        logging.debug("Watchdog caught shutdown in {}".format(threading.get_ident()))

                    
    def _post_service(self, r: pipeline.PipelinedModelRunner, q: queue.Queue):
        """
        A worker thread that loops to pull results from the pipelined model
        runner and deliver them to the requesting thread's queue. This is the
        main interface for receiving results from a pipeline runner.
        
        There are timeouts in the queues' usage here, but we don't expect them
        to ever be blocked. The intention is so if anything starts going 'off
        the rails' in regards to work synchronization, it blows up one of our
        queues first instead of giving us a more difficult OOM or deadlock
        condition.
        
        When a 'NOOP' is enqueued in the pipeline, it finishes its work and
        shuts down. That condition is detected here and exits the thread.
        """
        while True:
            # Get the next result from runner N
            rs = r.pop()

            # Alert the watchdog that there is activity. Down, boy.
            self.watchdog_time = time.time()
            
            # Exit if the pipeline is done
            if not rs:
                logging.debug("Popped EOF in tid {}".format(threading.get_ident()))
                return
            logging.debug("Popped results in tid {}".format(threading.get_ident()))
                
            # Get the next receiving queue and deliver the results.
            # Neither of these get() or put() operations should be blocking
            # in the normal case.
            q.get(timeout=self.max_idle_secs_before_recycle).put(rs, timeout=self.max_idle_secs_before_recycle)


    def _get_tpu_devices(self):
        """Returns list of device names in usb:N or pci:N format.

        This function prefers returning PCI Edge TPU first.

        Returns:
        list of devices in pci:N and/or usb:N format

        Raises:
        RuntimeError: if not enough devices are available
        """
        edge_tpus = list_edge_tpus()

        num_pci_devices = sum(1 for device in edge_tpus if device['type'] == 'pci')
        logging.debug("{} PCIe TPUs detected".format(num_pci_devices))

        return ['pci:%d' % i for i in range(min(len(edge_tpus), num_pci_devices))] + \
               ['usb:%d' % i for i in range(max(0, len(edge_tpus) - num_pci_devices))]


    def _get_model_filenames(self, options: Options, tpu_list) -> list[str]:
        """
        Returns a list of filenames based on the list of available TPUs and 
        supplied model and segment filenames. If we don't have segment filenames
        (ie just a complete TPU model filename) then return that. If we have
        more than one list of segment files then use the list of files that best
        matches the number of TPUs we have, otherwise use the single list we
        have. If all else fails return the single TPU filename as a list.
        NOTE: This method also updates self.device_count and self.segment_count
              based on the choice of whether it uses a single model or a set of
              segment file names
        """

        # if TPU no-show then default is CPU
        self.device_type   = 'CPU'
        self.device_count  = 1  # CPU. At this point we don't know if we have TPU
        self.segment_count = 1  # Single CPU model file
        if not any(tpu_list):
            return []

        self.device_type   = 'TPU'

        # If TPU found then default is single TPU model file (no segments)
        self.device_count  = len(tpu_list)  # TPUs. We've at least found one
        self.segment_count = 1              # Single TPU model name at this point
        if not any(options.tpu_segments_lists):
            return [options.model_tpu_file]
            
        # We have a list of segment files
        if isinstance(options.tpu_segments_lists[0], list):
            # Look for a good match between available TPUs and segment counts
            # Prioritize first match
            for fname_list in options.tpu_segments_lists:
                segment_count = len(fname_list)
                if segment_count <= self.device_count and \
                   segment_count % self.device_count == 0:
                    self.segment_count = segment_count
                    return fname_list
        else:
            # Only one list of segments; use it regardless of even match to TPU count
            segment_count = len(options.tpu_segments_lists)
            if segment_count <= self.device_count:
                self.segment_count = segment_count
                self.device_count  = (self.device_count // segment_count) * segment_count
                return options.tpu_segments_lists

        # Couldn't find a good fit, use single segment
        return [options.model_tpu_file]


    # Should be called while holding runner_lock (if called at run time)
    def init_interpreters(self, options: Options) -> str:
        """
        Initializes the interpreters with the TFLite models.
        
        Also loads and initializes the pipeline runners. To do this, it needs
        to figure out if we're using segmented pipelines, if we can load all
        the segments to the TPUs, and how to allocate them. For example, if
        we have three TPUs and request a model that contains two segments,
        we will load the two segments into two TPUs. If we have four TPUs
        and load the same model, we will create two pipeline runners with
        two segments each.
        
        We also kick off the 'postmen' here that are threads responsible for
        transferring output from the pipeline to queues held by each thread
        requesting inference. These queues block until results are available
        for the thread. The postmen and queues are necessary because otherwise
        there would be no way of keeping track of which results align with
        which incoming request.
        
        The postmen each have 'postboxes' that are queues that they deliver
        results into. The next item in the postbox aligns with the next result
        in the pipeline. The items in the postboxes are themselves queues. This
        is because the thread requesting work is blocked waiting for a result
        to be placed in each of these queues (it's the postman's responsibility
        to make this transfer.) When the requesting thread's result is enqueued
        it is unblocked and proceeds to process the results.
        """

        self.interpreters = []
        self.runners      = []

        tpu_list          = self._get_tpu_devices()

        # This will update self.device_count and self.segment_count
        tpu_model_files   = self._get_model_filenames(options, tpu_list)
        
        # Read labels
        self.labels       = read_label_file(options.label_file) if options.label_file else {}

        # Initialize EdgeTPU interpreters.
        self.device_type = "TPU"
        for i in range(self.device_count):
                    
            # If we have a segmented file, alloc all segments into all TPUs, but 
            # no more than that. For CPU there will only be a single file.
            fname = tpu_model_files[i % len(tpu_model_files)]

            if os.path.exists(fname):
                logging.debug(f"Loading: {fname}")
            else:
                # No TPU file. If we can't load one of the files, something's
                # very wrong, so quit the whole thing
                logging.error(f"TPU file {fname} doesn't exist")
                self.interpreters = []
                break
            
            try:
                interpreter = make_interpreter(fname, device=tpu_list[i], delegate=None)
                self.interpreters.append(interpreter)
            except Exception as in_ex:
                # If we fail to create even one of the interpreters then fail all. 
                # TODO: An option here is to remove the failed TPU from the list
                #       of TPUs and try the others. Maybe there's paired PCI cards
                #       and a USB, and the USB is failing?
                logging.error(f"Unable to create interpreter for TPU {tpu_list[i]}: {in_ex}")
                self.interpreters = []
                break
            

        if any(self.interpreters):
            logging.debug(f"Loaded {len(self.interpreters)} TPUs")
        else:          
            # Fallback to CPU if no luck with TPUs
            logging.info("No Coral TPUs found or able to be initialized. Using CPU.")
            self.device_count  = 1
            self.segment_count = 1
            self.device_type   = "CPU"

            try:
                # First pass is to try the edgeTPU library to create the interpreter
                # for the CPU file. Can't say I've ever had success with this
                interpreter = make_interpreter(options.model_cpu_file, device="cpu", delegate=None)
                self.interpreters = [interpreter]
            except Exception as ex:
                logging.warning(f"Unable to create interpreter for CPU using edgeTPU library: {ex}")
                self.interpreters  = []
                self.device_count  = 0
                self.segment_count = 0
                self.device_type   = None
                """
                # We could fall back to plain TF-Lite but this class is for TPU
                # processing, not CPU. We will just return empty handed and let
                # the caller fallback to whatever CPU solution they have handy.
                try:
                    import tflite_runtime.interpreter as tflite
                    interpreter = tflite.Interpreter(options.model_cpu_file, None)
                    self.interpreters = [interpreter]
                except Exception as ex:
                    logging.warning("Error creating interpreter: " + str(ex))
                    self.interpreters = []
                """

        # Womp womp
        if not any(self.interpreters):
            return self.device_type

        # Initialize interpreters
        for i in self.interpreters:
            i.allocate_tensors()

        self.interpreters_created = datetime.now()

        # Initialize runners/pipelines
        for i in range(0, self.device_count, self.segment_count):
            interpreter = self.interpreters[i:i+self.segment_count]

            # NOTE: The PipelinedModuleRunner class calls 
            #       _pywrap_coral.PipelinedModelRunnerWrapper which requires an
            #       interpreter created by the edgeTPU library, not the plain
            #       TFLite library
            runner      = pipeline.PipelinedModelRunner(interpreter)
            runner.set_input_queue_size(self.max_pipeline_queue_length)
            runner.set_output_queue_size(self.max_pipeline_queue_length)
            self.runners.append(runner)

        # Sanity check
        assert len(self.runners)*self.segment_count == self.device_count, "No. runners MUST equal no. devices"

        # Setup postal queue
        self.postboxes = []
        self.postmen   = []

        for r in self.runners:
            # Start the queue. Set a size limit to keep the queue from going
            # off the rails. An OOM condition will bring everything else down.
            q = queue.Queue(maxsize=self.max_pipeline_queue_length)
            self.postboxes.append(q)

            # Start the receiving worker
            t = threading.Thread(target=self._post_service, args=[r, q])
            t.start()
            self.postmen.append(t)

        # Get input and output tensors.
        self.input_details  = self.interpreters[0].get_input_details()[0]
        self.output_details = self.interpreters[-1].get_output_details()[0]

        # Print debug
        logging.debug("{}, device & segment counts: {} & {}\n"
                      .format(self.device_type,
                              self.device_count,
                              self.segment_count))
        logging.debug("Interpreter count: {}\n".format(len(self.interpreters)))
        logging.debug(f"Input details: {self.input_details}\n")
        logging.debug(f"Output details: {self.output_details}\n")

        return self.device_type


    def _periodic_check(self, options: Options):
        """
        Run a periodic check to ensure the temperatures are good and we don't
        need to (re)initialize the interpreters/workers/pipelines. The system
        is setup to refresh the TF interpreters once an hour.
        
        I suspect that many of the problems reported with the use of the Coral
        TPUs were due to overheating chips. There were a few comments along the
        lines of: "Works great, but after running for a bit it became unstable
        and crashed. I had to back way off and it works fine now" This seems
        symptomatic of the TPU throttling itself as it heats up, reducing its
        own workload, and giving unexpected results to the end user.

        Discussion on TPU temperatures:
        https://coral.ai/docs/m2-dual-edgetpu/datasheet/
        https://github.com/magic-blue-smoke/Dual-Edge-TPU-Adapter/issues/7
        """
        now_ts = datetime.now()
        
        # Force if we've changed the model
        force = False

        if not self.runners:
            logging.debug("No runners found. Recreating.")
            force = True

        if options.model_name != self.model_name or \
           options.model_size != self.model_size:
            self.model_name = options.model_name
            self.model_size = options.model_size
            logging.debug("Model change detected. Forcing model reload.")
            force = True

        # Check to make sure we aren't checking too often
        if any(self.interpreters) and self.last_check_timer != None and \
           not force and (now_ts - self.last_check_timer).total_seconds() < 10:
            return True
        
        self.last_check_timer = now_ts
        
        # Check temperatures
        if self.temp_fname_format != None:
            msg = "TPU {} is {}C and will likely be throttled"
            temp_arr = []
            for i in range(len(self.interpreters)):
                if os.path.exists(self.temp_fname_format.format(i)):
                    with open(self.temp_fname_format.format(i), "r") as fp:
                        # Convert from millidegree C to degree C
                        temp = int(fp.read()) // 1000
                        temp_arr.append(temp)            
                        if self.warn_temperature_thresh_C <= temp:
                            logging.warning(msg.format(i, temp))
            if any(temp_arr):
                logging.debug("Temperatures: {} avg; {} max; {} total".format(
                                                sum(temp_arr) // len(temp_arr),
                                                max(temp_arr),
                                                len(temp_arr)))
            else:
                logging.warning("Unable to find temperatures!")

        # Once an hour, refresh the interpreters
        if any(self.interpreters):
            current_age_sec = (now_ts - self.interpreters_created).total_seconds()
            if force or current_age_sec > self.interpreter_lifespan_secs:
                logging.info("Refreshing the Tensorflow Interpreters")

                # Close all existing work before destroying...
                with self.runner_lock:
                    self._delete()
                    # Re-init while we still have the lock
                    self.init_interpreters(options)

        # (Re)start them if needed
        if not any(self.interpreters):
            with self.runner_lock:
                self.init_interpreters(options)

        return any(self.interpreters)


    def __del__(self):
        with self.runner_lock:
            self._delete()
        self.watchdog_shutdown = True
        self.watchdog_thread.join(timeout=self.watchdog_idle_secs*2)


    def _delete(self):
        """
        Close and delete each of the pipelines and interpreters while flushing
        existing work.
        """
        # Close each of the pipelines
        if self.runners:
            for i in range(len(self.runners)):
                self.runners[i].push({})

        # The above should trigger all workers to end. If we are deleting in an
        # off-the-rails context, it's likely that something will have trouble
        # closing out its work.
        if self.postmen:
            for t in self.postmen:
                logging.debug("Joining thread {} for TPURunner._delete()".format(t.native_id))
                t.join(timeout=self.max_idle_secs_before_recycle)
                if t.is_alive():
                    logging.warning("Thread didn't join!")

        if self.runners:
            for i in range(len(self.runners)):
                self.runners[i] = None

        # Delete
        self.postmen        = None
        self.postboxes      = None
        self.runners        = None
        self.interpreters   = []


    def interpreters_ok(self, options:Options) -> bool:
        
        """ Check we have valid interpreters """
        if any(self.interpreters):
            return True
        

        # Force if we've changed the model
        force = options.model_name != self.model_name or \
                options.model_size != self.model_size

        # Check to make sure we aren't checking too often
        now_ts = datetime.now()
        if force \
           or not self.last_check_interpreter_creation_timer \
           or (now_ts - self.last_check_interpreter_creation_timer).total_seconds() > 120:
            self.last_check_interpreter_creation_timer = now_ts
            return self._periodic_check(options)
        
        return False


    def process_image(self,
                      options:Options,
                      image: Image,
                      score_threshold: float) -> (list[detect.Object], int):
        """
        Execute all the default image processing operations.
        
        Will take an image and:
        - Initialize TPU pipelines.
        - Tile it.
        - Normalize each tile.
        - Run inference on the tile.
        - Collate results.
        - Remove duplicate results.
        - Return results as Objects.
        - Return inference timing.
        """

        # Recreate the interpreters if they are stale, but also check if we can
        # and have created interpreters. It's not always successful...
        if not self._periodic_check(options):
            return None, 0

        all_objects = []
        all_queues  = []

        name = self.input_details['name']
        _, m_height, m_width, _ = self.input_details['shape']
        i_width, i_height = image.size
        
        # Potentially resize & pipeline a number of tiles
        for rs_image, rs_loc in self._get_tiles(options, image):
            rs_queue = queue.Queue(maxsize=1)
            all_queues.append((rs_queue, rs_loc))
            logging.debug("Enqueuing tile in pipeline {}".format(self.next_runner_idx))

            with self.runner_lock:
                # Push the resampled image and where to put the results.
                # There shouldn't be any wait time to put() into the queue, so
                # if we are blocked here, something has gone off the rails.
                self.runners[self.next_runner_idx].push({name: rs_image})
                self.postboxes[self.next_runner_idx].put(rs_queue,
                                                         timeout=self.max_idle_secs_before_recycle)

                # Increment the next runner to use
                self.next_runner_idx = \
                                (1 + self.next_runner_idx) % len(self.runners)

        # Wait for the results here
        tot_inference_time = 0
        for rs_queue, rs_loc in all_queues:
            # Wait for results
            # We may have to wait a few seconds at most, but I'd expect the
            # pipeline to clear fairly quickly.
            start_inference_time = time.perf_counter()
            result = rs_queue.get(timeout=self.max_idle_secs_before_recycle)
            tot_inference_time += time.perf_counter() - start_inference_time
            assert result

            boxes, class_ids, scores, count = self._decode_result(result, score_threshold)
            
            logging.debug("BBox scaling params: {}x{}, ({},{}), {:.2f}x{:.2f}".
                format(m_width, m_height,*rs_loc))

            # Create Objects for each valid result
            for i in range(int(count[0])):
                if scores[0][i] < score_threshold:
                    continue
                    
                ymin, xmin, ymax, xmax = boxes[0][i]
                
                bbox = detect.BBox(xmin=max(xmin, 0.0),
                                   ymin=max(ymin, 0.0),
                                   xmax=min(xmax, 1.0),
                                   ymax=min(ymax, 1.0)).\
                                    scale(m_width, m_height).\
                                        translate(rs_loc[0], rs_loc[1]).\
                                            scale(rs_loc[2], rs_loc[3])
                                          
                all_objects.append(detect.Object(id=int(class_ids[0][i]),
                                                 score=float(scores[0][i]),
                                                 bbox=bbox.map(int)))
        
        # Convert to ms
        tot_inference_time = int(tot_inference_time * 1000)

        # Remove duplicate objects
        unique_indexes = self._non_max_suppression(all_objects, options.iou_threshold)
        
        return ([all_objects[i] for i in unique_indexes], tot_inference_time)
        
        
    def _decode_result(self, result, score_threshold: float):
        
        result_list = list(result.values())
        if len(result_list) == 4:
            # Easy case with SSD MobileNet & EfficientDet_Lite
            if result_list[3].size == 1:
                return result_list
            else:
                return (result_list[1], result_list[3], result_list[0], result_list[2])

        min_value = np.iinfo(self.output_details['dtype']).min
        max_value = np.iinfo(self.output_details['dtype']).max
        logging.debug("Scaling output values in range {} to {}".format(min_value, max_value))

        output_zero = self.output_details['quantization'][1]
        output_scale = self.output_details['quantization'][0]
        
        # Decode YOLO result
        boxes     = []
        class_ids = []
        scores    = []
        for dict_values in result_list:
            j, k = dict_values[0].shape

                    # YOLOv8 is flipped for some reason. We will use that to decide if we're
                    # using a v8 or v5-based network.
            if j < k:
                rs = self._yolov8_non_max_suppression(
                    (dict_values.astype('float32') - output_zero) * output_scale,
                    conf_thres=score_threshold)
            else:
                rs = self._yolov5_non_max_suppression(
                    (dict_values.astype('float32') - output_zero) * output_scale,
                    conf_thres=score_threshold)

            for a in rs:
                for r in a:
                    boxes.append(r[0:4])
                    class_ids.append(int(r[5]))
                    scores.append(r[4])

        return ([boxes], [class_ids], [scores], [len(scores)])


    def _xywh2xyxy(self, xywh):
        # Convert nx4 boxes from [x, y, w, h] to [x1, y1, x2, y2] where xy1=top-left, xy2=bottom-right
        xyxy = np.copy(xywh)
        xyxy[:, 1] = xywh[:, 0] - xywh[:, 2] / 2  # top left x
        xyxy[:, 0] = xywh[:, 1] - xywh[:, 3] / 2  # top left y
        xyxy[:, 3] = xywh[:, 0] + xywh[:, 2] / 2  # bottom right x
        xyxy[:, 2] = xywh[:, 1] + xywh[:, 3] / 2  # bottom right y
        return xyxy

    
    def _nms(self, dets, scores, thresh):
        '''
        dets is a numpy array : num_dets, 4
        scores ia  nump array : num_dets,
        '''

        x1 = dets[:, 0]
        y1 = dets[:, 1]
        x2 = dets[:, 2]
        y2 = dets[:, 3]

        areas = (x2 - x1 + 1e-9) * (y2 - y1 + 1e-9)
        order = scores.argsort()[::-1] # get boxes with more ious first
        
        keep = []
        while order.size > 0:
            i = order[0] # pick maxmum iou box
            other_box_ids = order[1:]
            keep.append(i)
            
            xx1 = np.maximum(x1[i], x1[other_box_ids])
            yy1 = np.maximum(y1[i], y1[other_box_ids])
            xx2 = np.minimum(x2[i], x2[other_box_ids])
            yy2 = np.minimum(y2[i], y2[other_box_ids])
            
            w = np.maximum(0.0, xx2 - xx1 + 1e-9) # maximum width
            h = np.maximum(0.0, yy2 - yy1 + 1e-9) # maxiumum height
            inter = w * h
              
            ovr = inter / (areas[i] + areas[other_box_ids] - inter)
            
            inds = np.where(ovr <= thresh)[0]
            order = order[inds + 1]

        return np.array(keep)


    def _yolov8_non_max_suppression(self, prediction, conf_thres=0.25, iou_thres=0.45,
                                    labels=(), max_det=3000):

        nc = prediction.shape[1] - 4  # number of classes
        bs = prediction.shape[0]  # batch size
        nm = prediction.shape[1] - nc - 4
        mi = 4 + nc  # mask start index

        xc = np.amax(prediction[:, 4:mi], 1) > conf_thres  # candidates

        prediction = prediction.transpose(0,2,1)  # shape(1,84,6300) to shape(1,6300,84)

        # Checks
        assert 0 <= conf_thres <= 1, f'Invalid Confidence threshold {conf_thres}, valid values are between 0.0 and 1.0'
        assert 0 <= iou_thres <= 1, f'Invalid IoU {iou_thres}, valid values are between 0.0 and 1.0'

        # Settings
        min_wh, max_wh = 2, 4096  # (pixels) minimum and maximum box width and height
        max_nms = 30000  # maximum number of boxes into torchvision.ops.nms()
        time_limit = 10.0  # seconds to quit after

        t = time.time()
        output = [np.zeros((0, 6))] * prediction.shape[0]
        for xi, x in enumerate(prediction):  # image index, image inference
            # Apply constraints
            x = x[xc[xi]]  # confidence

            # If none remain process next image
            if not x.shape[0]:
                continue

            # Box (center x, center y, width, height) to (x1, y1, x2, y2)
            box = self._xywh2xyxy(x[:, :4])

            # Detections matrix nx6 (xyxy, conf, cls)
            conf = np.amax(x[:, 4:], axis=1, keepdims=True)
            j = np.argmax(x[:, 4:], axis=1).reshape(conf.shape)
            x = np.concatenate((box, conf, j.astype(float)), axis=1)[conf.flatten() > conf_thres]

            # Check shape
            n = x.shape[0]  # number of boxes
            if not n:  # no boxes
                continue
            elif n > max_nms:  # excess boxes
                x = x[x[:, 4].argsort(descending=True)[:max_nms]]  # sort by confidence

            # Batched NMS
            c = x[:, 5:6] * max_wh  # classes
            boxes, scores = x[:, :4] + c, x[:, 4]  # boxes (offset by class), scores
            
            i = self._nms(boxes, scores, iou_thres)  # NMS
            
            if i.shape[0] > max_det:  # limit detections
                i = i[:max_det]

            output[xi] = x[i]
            if (time.time() - t) > time_limit:
                logging.warning(f'NMS time limit {time_limit}s exceeded')
                break  # time limit exceeded

        return output        

        
    def _yolov5_non_max_suppression(self,
        							prediction,
        							conf_thres=0.25,
        							iou_thres=0.45,
        							max_det=300):
        # Checks
        assert 0 <= conf_thres <= 1, f"Invalid Confidence threshold {conf_thres}, valid values are between 0.0 and 1.0"
        assert 0 <= iou_thres <= 1, f"Invalid IoU {iou_thres}, valid values are between 0.0 and 1.0"

        bs = prediction.shape[0]  # batch size
        nc = prediction.shape[2] - 5  # number of classes
        xc = prediction[..., 4] > conf_thres  # candidates

        # Settings
        # min_wh = 2  # (pixels) minimum box width and height
        max_wh = 7680  # (pixels) maximum box width and height
        max_nms = 30000  # maximum number of boxes into torchvision.ops.nms()
        time_limit = 0.5 + 0.05 * bs  # seconds to quit after

        t = time.time()
        mi = 5 + nc  # mask start index
        output = [np.zeros((0, 6))] * bs
        for xi, x in enumerate(prediction):  # image index, image inference
            # Apply constraints
            x = x[xc[xi]]  # confidence
        
            # If none remain process next image
            if not x.shape[0]:
                continue

            # Compute conf
            x[:, 5:] *= x[:, 4:5]  # conf = obj_conf * cls_conf

            # Box/Mask
            box = self._xywh2xyxy(x[:, :4])  # center_x, center_y, width, height) to (x1, y1, x2, y2)
            mask = x[:, mi:]  # zero columns if no masks

            # Detections matrix nx6 (xyxy, conf, cls)
            conf = np.amax(x[:, 5:], axis=1, keepdims=True)
            j = np.argmax(x[:, 5:], axis=1).reshape(conf.shape)
            x = np.concatenate((box, conf, j.astype(float)), axis=1)[conf.flatten() > conf_thres]

            # Check shape
            n = x.shape[0]  # number of boxes
            if not n:  # no boxes
                continue
            elif n > max_nms:  # excess boxes
                x = x[x[:, 4].argsort(descending=True)[:max_nms]]  # sort by confidence

            # Batched NMS
            c = x[:, 5:6] * max_wh  # classes
            boxes, scores = x[:, :4] + c, x[:, 4]  # boxes (offset by class), scores
            i = self._nms(boxes, scores, iou_thres)  # NMS
            i = i[:max_det]  # limit detections

            output[xi] = x[i]
            if (time.time() - t) > time_limit:
                LOGGER.warning(f"WARNING ⚠️ NMS time limit {time_limit:.3f}s exceeded")
                break  # time limit exceeded

        return output

        
    def _non_max_suppression(self, objects, threshold):
        """Returns a list of indexes of objects passing the NMS.

        Args:
        objects: result candidates.
        threshold: the threshold of overlapping IoU to merge the boxes.

        Returns:
        A list of indexes containing the objects that pass the NMS.
        """
        if len(objects) <= 0:
            return []
        if len(objects) == 1:
            return [0]

        boxes = np.array([o.bbox for o in objects])
        x_mins = boxes[:, 0]
        y_mins = boxes[:, 1]
        x_maxs = boxes[:, 2]
        y_maxs = boxes[:, 3]

        areas   = (x_maxs - x_mins) * (y_maxs - y_mins)
        scores  = [o.score for o in objects]
        indexes = np.argsort(scores)
        
        logging.debug("Starting NMS with {} objects".format(len(objects)))

        selected_indexes = []
        while indexes.size != 0:

            selected_index = indexes[-1]
            selected_indexes.append(selected_index)

            overlapped_x_mins = np.maximum(x_mins[selected_index], x_mins[indexes[:-1]])
            overlapped_y_mins = np.maximum(y_mins[selected_index], y_mins[indexes[:-1]])
            overlapped_x_maxs = np.minimum(x_maxs[selected_index], x_maxs[indexes[:-1]])
            overlapped_y_maxs = np.minimum(y_maxs[selected_index], y_maxs[indexes[:-1]])

            width  = np.maximum(0, overlapped_x_maxs - overlapped_x_mins)
            height = np.maximum(0, overlapped_y_maxs - overlapped_y_mins)

            intersections = width * height
            unions = areas[indexes[:-1]] + areas[selected_index] - intersections
            ious   = intersections / unions

            if np.isnan(np.sum(ious)):
                logging.warning("Zero area detected, ignoring")
                indexes = np.delete(
                    indexes, np.concatenate(([len(indexes) - 1], np.where(not np.isnan(ious))[0])))
                continue
            
            indexes = np.delete(
                indexes, np.concatenate(([len(indexes) - 1], np.where(ious > threshold)[0])))

        logging.debug("Finishing NMS with {} objects".format(len(selected_indexes)))
        return selected_indexes
        
        
    def _resize_and_chop_tiles(self,
                               options: Options,
                               image: Image,
                               m_width,
                               m_height):
        """
        Run the image resizing in an independent process pool.
        
        Image resizing is one of the more expensive things we're doing here.
        It's expensive enough that it may take as much CPU time as inference
        under some circumstances. The Lanczos resampling kernel in particular
        is expensive, but results in quality output.
        
        For example, see the resizing performance charts here:
        https://python-pillow.org/pillow-perf
        
        Pillow is the highly optimized version of PIL and it only runs at
        ~100 MP/sec when making a thumbnail with the Lanczos kernel. That's
        only 12.6 4k frames per second, maximum, in a Python process. We are
        hoping to process more than that with TPU hardware.
        
        We can also improve performance by installing
        the 'pillow-simd' Python library. And improve it even more by
        re-compiling it to use AVX2 instructions. See:
        https://github.com/uploadcare/pillow-simd#pillow-simd
        """
        i_width, i_height = image.size

        # What tile dim do we want?
        tiles_x = int(max(1, round(i_width / (options.downsample_by * m_width))))
        tiles_y = int(max(1, round(i_height / (options.downsample_by * m_height))))
        logging.debug("Chunking to {} x {} tiles".format(tiles_x, tiles_y))

        # Resample image to this size
        resamp_x = int(m_width  + (tiles_x - 1) * (m_width  - options.tile_overlap))
        resamp_y = int(m_height + (tiles_y - 1) * (m_height - options.tile_overlap))
        logging.debug("Resizing to {} x {} for tiling".format(resamp_x, resamp_y))
        
        # It'd be useful to print this once at the beginning of the run
        key = "{} {}".format(*image.size)
        if key not in self.printed_shape_map:
            logging.info(
                "Mapping {} image to {}x{} tiles".format(image.size, tiles_x, tiles_y))
            self.printed_shape_map[key] = True

        # Chop & resize image piece
        resamp_img = image.convert('RGB').resize((resamp_x, resamp_y),
                                                 Image.LANCZOS)

        input_zero = self.input_details['quantization'][1]
        input_scale = self.input_details['quantization'][0]

        # Do chunking
        tiles = []
        for x_off in range(0, resamp_x - options.tile_overlap, m_width - options.tile_overlap):
            for y_off in range(0, resamp_y - options.tile_overlap, m_height - options.tile_overlap):
                cropped_arr = np.asarray(resamp_img.crop((x_off,
                                                          y_off,
                                                          x_off + m_width,
                                                          y_off + m_height)), dtype=np.float32)
                logging.debug("Resampled image tile {} at offset {}, {}".format(cropped_arr.shape, x_off, y_off))

                # Normalize from 8-bit image to whatever the input is
                cropped_arr = (cropped_arr/(256.0 * input_scale)) + input_zero

                tiles.append((cropped_arr.astype(self.input_details['dtype']),
                              (x_off, y_off, i_width/resamp_x, i_height/resamp_y)))
        return tiles


    def _get_tiles(self, options: Options, image: Image):
        """
        Returns an iterator that yields image tiles and associated location.
        
        For tiling, we use the philosophy that it makes the most sense to
        keep the pixel downsampling multiplier somewhat constant and resample
        the image to fit multiples of the tensor input dimensions. The default
        option is a multiplier of roughly 6, which should give us two tiles for
        an image with HD or 4k dimensions. Anything larger or more
        square-shaped will be mapped to just be a single tile. This is
        intentionally kept conservative. To tile more aggressively, reduce the
        multiplier down from 6. Run time will go up, as the number of
        inferences will go up with more tiles.
        
        If we don't tile the images, we end up with bad options:
        - We stretch the image to fit the tensor input. In the case of a 4k
        video stream, this basically doubles the height of images by stretching
        them. Warping an image like this doesn't seem like it would improve AI
        performance.
        - We keep the aspect ratio the same and pad the image. In the case of
        a 4k image, this is wasting ~44% of the potential input data as simply
        padding. In the case of our smallest 300x300 model, a full 131x300
        pixels are wasted.
        
        It makes more sense to me to split the image in two; resulting in two
        tiles that are each neither very warped or have wasted input pixels.
        The downside is, of course, that we are doing twice as much work.
        """
        _, m_height, m_width, _ = self.input_details['shape']

        # This function used to be multi-process, but it seems Pillow handles
        # that better and faster than we would. So we just call into tile-
        # generation as a function here.
        return self._resize_and_chop_tiles(options, image, m_width, m_height)

