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
import errno
import platform
import time
import logging
import queue
import gc
import math

from datetime import datetime

import numpy as np
from PIL import Image, ImageOps

try:
    from pycoral.utils.dataset import read_label_file
    from pycoral.utils import edgetpu
except ImportError:
    logging.exception("Missing pycoral function. Perhaps you are using a funky version of pycoral?")
    exit()

from pycoral.adapters import detect

from options import Options


# Refresh the pipe once an hour. I'm unsure if this is needed.
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


class TPUException(Exception):
    pass


class DynamicInterpreter(object):
    def __init__(self, fname_list: list, tpu_name: str, queues: list, rebalancing_lock: threading.Lock):
        self.fname_list = fname_list
        self.tpu_name = tpu_name
        self.queues = queues
        self.rebalancing_lock = rebalancing_lock

        self.timings    = [0.0] * len(fname_list)
        self.q_len      = [0] * len(fname_list)
        self.exec_count = [0] * len(fname_list)

        try:
            self.delegate = edgetpu.load_edgetpu_delegate({'device': tpu_name})
        except Exception as in_ex:
            # If we fail to create even one of the interpreters then fail all. 
            # Throw exception and caller can try to recreate without the TPU.
            # An option here is to remove the failed TPU from the list
            # of TPUs and try the others. Maybe there's paired PCI cards
            # and a USB, and the USB is failing?
            logging.warning(f"Unable to load delegate for TPU {self.tpu_name}: {in_ex}")
            raise TPUException(self.tpu_name)


    def start(self, seg_idx: int, fbytes: bytes):
        logging.info(f"Loading {self.tpu_name}: {self.fname_list[seg_idx]}")

        try:
            self.interpreter = edgetpu.make_interpreter(fbytes, delegate=self.delegate)
        except Exception as in_ex:
            # If we fail to create even one of the interpreters then fail all. 
            # Throw exception and caller can try to recreate without the TPU.
            # An option here is to remove the failed TPU from the list
            # of TPUs and try the others. Maybe there's paired PCI cards
            # and a USB, and the USB is failing?
            logging.warning(f"Unable to create interpreter for TPU {self.tpu_name}: {in_ex}")
            raise TPUException(self.tpu_name)

        self.interpreter.allocate_tensors()
        self.input_details = self.interpreter.get_input_details()
        self.output_details = self.interpreter.get_output_details()

        # Start processing loop per TPU
        self.thread = threading.Thread(target=self._interpreter_runner, args=[seg_idx])
        self.thread.start()


    def _interpreter_runner(self, seg_idx: int):
        in_names  = [d['name']  for d in self.input_details ]
        out_names = [d['name']  for d in self.output_details]
        indices   = [d['index'] for d in self.output_details]
        first_in_name = in_names.pop(0)

        # Setup input/output queues
        in_q = self.queues[seg_idx]
        out_q = None
        if len(self.queues) > seg_idx+1:
            out_q = self.queues[seg_idx+1]

        # Input tensors for this interpreter
        input_tensors = {}
        for details in self.input_details:
            input_tensors[details['name']] = self.interpreter.tensor(details['index'])
        output_tensors = []
        if not out_q:
            for details in self.output_details:
                output_tensors.append(self.interpreter.tensor(details['index']))

        expected_input_size = np.prod(self.input_details[0]['shape'])
        interpreter_handle = self.interpreter._native_handle()

        # Run interpreter loop; consume & produce results
        while True:
            # Pull next input from the queue
            working_tensors = in_q.get()
            
            # Exit if the pipeline is done
            if working_tensors is False:
                logging.debug("Get EOF in tid {}".format(threading.get_ident()))
                self.interpreter = None
                self.input_details = None
                self.output_details = None
                if self.rebalancing_lock.locked():
                    self.rebalancing_lock.release()
                return

            start_inference_time = time.perf_counter_ns()

            # Set inputs beyond the first
            for name in in_names:
                input_tensors[name]()[0] = working_tensors[0][name]

            # Invoke_with_membuffer() directly on numpy memory,
            # but only works with a single input
            edgetpu.invoke_with_membuffer(interpreter_handle,
                                          working_tensors[0][first_in_name].ctypes.data,
                                          expected_input_size)

            if out_q:
                # Fetch results
                for name, index in zip(out_names, indices):
                    working_tensors[0][name] = self.interpreter.get_tensor(index)

                # Deliver to next queue in pipeline
                out_q.put(working_tensors)
            else:
                # Fetch pointer to results
                # Copy and convert to float
                # Deliver to final results queue
                working_tensors[1].put([t().astype(np.float32) for t in output_tensors])

            # Convert elapsed time to double precision ms
            self.timings[seg_idx] += (time.perf_counter_ns() - start_inference_time) / (1000.0 * 1000.0)
            self.q_len[seg_idx] += in_q.qsize()
            self.exec_count[seg_idx] += 1

    def __del__(self):
        # Print performance info
        t_str = ""
        q_str = ""
        c_str = ""
        for t, q, c in zip(self.timings, self.q_len, self.exec_count):
            if c > 0:
                avg_time = t / c
                avg_q = q / c
            else:
                avg_time = 0.0
                avg_q = 0.0
            t_str += " {:5.1f}".format(avg_time)
            q_str += " {:4.1f}".format(avg_q)
            c_str += " {:7d}".format(c)
        logging.info(f"{self.tpu_name} time, queue len, & count:{t_str}|{q_str}|{c_str}")

        self.interpreter = None
        self.delegate = None
        self.queues = None


class DynamicPipeline(object):
    
    def __init__(self, tpu_list: list, fname_list: list):
        seg_count = len(fname_list)
        assert seg_count <= len(tpu_list), f"More segments than TPUs to run them! {seg_count} vs {len(tpu_list)}"

        self.max_pipeline_queue_length    = MAX_PIPELINE_QUEUE_LEN
        
        self.fname_list   = fname_list
        self.tpu_list     = tpu_list
        self.interpreters = [[]  for i in range(seg_count)]

        # Input queues for each segment; if we go over maxsize, something went wrong
        self.queues = [queue.Queue(maxsize=self.max_pipeline_queue_length) for i in range(seg_count)]

        # Lock for internal reorganization
        self.balance_lock = threading.Lock()

        # Lock for interpreter use
        self.rebalancing_lock = threading.Lock()

        # Read file data
        self.fbytes_list = []
        for fname in fname_list:
            if not os.path.exists(fname):
                # No TPU file. If we can't load one of the files, something's
                # very wrong, so quit the whole thing
                logging.error(f"TFLite file {fname} doesn't exist")
                self.interpreters = []
                raise FileNotFoundError(
                    errno.ENOENT, os.strerror(errno.ENOENT), fname)

            with open(fname, "rb") as fd:
                self.fbytes_list.append(fd.read())

        with self.balance_lock:
            self._init_interpreters()

    def _init_interpreters(self):
        # Set a Time To Live for balancing so we don't thrash
        self.balance_ttl  = len(self.tpu_list) * 2
        start_boot_time = time.perf_counter_ns()

        # Fill TPUs with interpreters
        for i, tpu_name in enumerate(self.tpu_list):
            seg_idx = i % len(self.fname_list)

            i = DynamicInterpreter(self.fname_list, tpu_name, self.queues, self.rebalancing_lock)
            i.start(seg_idx, self.fbytes_list[seg_idx])
            self.interpreters[seg_idx].append(i)

        self.first_name = self.interpreters[0][0].input_details[0]['name']
        
        boot_time = (time.perf_counter_ns() - start_boot_time) / (1000.0 * 1000.0)
        logging.info(f"Initialized pipeline interpreters in {boot_time:.1f}ms")


    def enqueue(self, in_tensor, out_q: queue.Queue):
        with self.balance_lock:
            if not self.first_name:
                self._init_interpreters()

        self.queues[0].put(({self.first_name: in_tensor}, out_q))


    def _eval_timings(self, interpreter_counts):
        # How much time are we allocating for each segment
        time_alloc = []
        VALID_CNT_THRESH = 50

        for seg_i in range(len(self.interpreters)):
            # Find average runtime for this segment
            avg_times = []
            for interpreters in self.interpreters:
                avg_times += [i.timings[seg_i] / i.exec_count[seg_i] for i in interpreters if i.exec_count[seg_i] > VALID_CNT_THRESH]

            if avg_times:
                avg_time = sum(avg_times) / len(avg_times)
            else:
                return 0, 0, 0.0, None

            # Adjust for number of TPUs allocated to it
            if interpreter_counts[seg_i] > 0:
                time_alloc.append(avg_time / interpreter_counts[seg_i])
            else:
                # No interpreters result inf time
                time_alloc.append(float('inf'))

        min_gt1_t = float('inf')
        min_gt1_i = -1
        max_t = 0.0
        max_i = -1

        # Find segments that maybe should swap
        for i, t in enumerate(time_alloc):
            # Max time needs to be shortened so add an interpreter.
            if t > max_t:
                max_t = t
                max_i = i

            # Min time needs to be lengthened so rem an interpreter,
            # but only if it has more than one interpreter
            if t < min_gt1_t and len(self.interpreters[i]) > 1:
                min_gt1_t = t
                min_gt1_i = i

        # Only eval swapping max time segment if we have many samples in the current setup
        for i in self.interpreters[max_i]:
            if i.exec_count[max_i] < VALID_CNT_THRESH:
                return min_gt1_i, max_i, max(time_alloc), None

        # Undo avg interp count adjustment for TPU-to-TPU comparisons
        max_t = max([i.timings[max_i] / i.exec_count[max_i] for i in self.interpreters[max_i]])

        # See if we can do better than the current max time by swapping segments between TPUs
        swap_i = None
        swap_t = float('inf')
        for interp_i, interpreters in enumerate(self.interpreters):
            # Doesn't make sense to pull a TPU from a queue just to re-add it.
            if interp_i == max_i:
                continue

            # Test all TPUs in this segment
            for i in interpreters:
                # If TPU hasn't yet been tried for this segment or ...
                if i.exec_count[max_i] < VALID_CNT_THRESH:
                    return min_gt1_i, max_i, max(time_alloc), interp_i    

                # Only calc valid time after a few runs
                new_max_t = 0.0
                if i.exec_count[max_i] > VALID_CNT_THRESH:
                    new_max_t = i.timings[max_i] / i.exec_count[max_i] 
                new_swap_t = 0.0
                if i.exec_count[interp_i] > VALID_CNT_THRESH:
                    new_swap_t = i.timings[interp_i] / i.exec_count[interp_i] 

                # If TPU has already found to be faster on this segment
                # and we aren't making the other segment the new worst
                # and we are choosing the best available candidate.
                if max_t-0.5 > new_max_t and max_t > new_swap_t and swap_t > new_max_t:
                    swap_i = interp_i
                    swap_t = new_max_t 

        return min_gt1_i, max_i, max(time_alloc), swap_i    

    
    def balance_queues(self):
        # Don't bother if someone else is working on balancing
        if len(self.queues) <= 1 or len(self.tpu_list) < 2 or self.balance_ttl <= 0 or \
           not self.balance_lock.acquire(blocking=False):
            return

        interpreter_counts = [len(i) for i in self.interpreters]
        min_i, max_i, current_max, swap_i = self._eval_timings(interpreter_counts)
        interpreter_counts[min_i] -= 1
        interpreter_counts[max_i] += 1
        _, _, new_max, _ = self._eval_timings(interpreter_counts)

        if new_max+1.0 < current_max:
            # 1st Priority: Allocate more TPUs to slow segments
            logging.info(f"Re-balancing from queue {min_i} to {max_i} (max from {current_max:.2f} to {new_max:.2f})")

            realloc_interp = self._rem_interpreter_from(min_i)

            # Add to large (too-slow) queue
            realloc_interp.start(max_i, self.fbytes_list[max_i])
            self.interpreters[max_i].append(realloc_interp)

        elif swap_i is not None:
            # 2nd Priority: Swap slow segments with faster ones to see if we can
            # run them faster. Hopefully still a good way to optimize for
            # heterogenous hardware.
            logging.info(f"Auto-tuning between queues {swap_i} and {max_i}")

            # Stop them
            new_max  = self._rem_interpreter_from(swap_i)
            new_swap = self._rem_interpreter_from(max_i)

            # Swap them
            new_max.start(max_i, self.fbytes_list[max_i])
            self.interpreters[max_i].append(new_max)

            new_swap.start(swap_i, self.fbytes_list[swap_i])
            self.interpreters[swap_i].append(new_swap)
        else:
            # Return if we don't want to swap
            self.balance_lock.release()
            return

        self.balance_ttl -= 1
        self.balance_lock.release()
        self.print_queue_len()


    def _rem_interpreter_from(self, interp_i):
        # Sending False kills the processing loop
        self.rebalancing_lock.acquire()
        self.queues[interp_i].put(False)

        # This is ugly, but I can't think of something better
        # Threads are blocked by queues. Queues may not have a stream
        # of work cycling them. Therefore must kill with an
        # enqueued command. But we don't know which thread picks
        # up the command from the queue.

        # Block & wait
        realloc_interp = None
        with self.rebalancing_lock:
            for idx, interpreter in enumerate(self.interpreters[interp_i]):
                if not interpreter.interpreter:
                    realloc_interp = self.interpreters[interp_i].pop(idx)
                    break

        if not realloc_interp:
            logging.warning("Unable to find killed interpreter")
            self.balance_lock.release()
        return realloc_interp


    def print_queue_len(self):
        len_str = ""
        seg_str = ""
        for i, q in zip(self.interpreters, self.queues):
            len_str += " {:2}".format(q.qsize())
            seg_str += " {:2}".format(len(i))
        logging.info(f"Queue len: ({len_str}); Segment alloc: ({seg_str})")


    def __del__(self):
        self.delete()


    def _halt_interpreters(self, seg_idx: int):
        
        if not self.interpreters or seg_idx < 0 or seg_idx >= len(self.interpreters):
            return
        
        # Insert EOF to each queue
        for i in self.interpreters[seg_idx]:
            self.queues[seg_idx].put(False)

        # Wait for threads to finish
        for interpreter in self.interpreters[seg_idx]:
            t = interpreter.thread
            logging.debug("Joining thread {} for DynamicPipeline.delete()".format(t.native_id))
            t.join(timeout=MAX_WAIT_TIME)
            if t.is_alive():
                logging.warning("Pipe thread didn't join!")


    def delete(self):
        # Kill interpreters. Maybe refresh later; maybe delete object.
        with self.balance_lock:
            # Insert EOF to each queue
            # Wait for threads to finish
            # Init structures
            for q_idx, q in enumerate(self.queues):
                self._halt_interpreters(q_idx)
                self.queues[q_idx] = queue.Queue(maxsize=self.max_pipeline_queue_length)

                if self.interpreters and len(self.interpreters) > q_idx:
                    self.interpreters[q_idx] = []

        self.first_name = None


class TPURunner(object):
    def __init__(self, tpu_limit: int = -1):
        """
        Init object and do a check for the temperature file. Right now
        the temperature file would only be supported on Linux systems
        with the TPU installed on the PCIe bus. The Windows value is in
        the registry.
        """
        
        # Tricky because MAX_WAIT_TIME is intended to relatively quickly handle an error condition
        # before there are significant user-facing problems, whereas idling for N seconds isn't an
        # error condition.
        self.max_idle_secs_before_recycle = MAX_WAIT_TIME * 20
        self.watchdog_idle_secs           = WATCHDOG_IDLE_SECS

        self.pipe_lifespan_secs           = INTERPRETER_LIFESPAN_SECONDS
        self.warn_temperature_thresh_C    = WARN_TEMPERATURE_THRESHOLD_CELSIUS

        self.device_type          = None  # The type of device in use (TPU or CPU, but we're going to ignore CPU here)
        self.pipe                 = None
        self.pipe_created         = None  # When was the pipe created?
        self.model_name           = None  # Name of current model in use
        self.model_size           = None  # Size of current model in use
        self.labels               = None  # set of labels for this model

        self.runner_lock          = threading.Lock()
        
        self.last_check_time      = None
        self.printed_shape_map    = {}

        self.watchdog_time        = None
        self.watchdog_shutdown    = False
        self.watchdog_thread      = threading.Thread(target=self._watchdog)
        self.watchdog_thread.start()

        logging.info(f"edgetpu version: {edgetpu.get_runtime_version()}")
        logging.info(f"{Image.__name__} version: {Image.__version__}")

        # Find the temperature file
        # https://coral.ai/docs/pcie-parameters/
        temp_fname_formats = ['/dev/apex_{}/temp',
                              '/sys/class/apex/apex_{}/temp']
        self.temp_fname_format = None
        self.tpu_limit = tpu_limit

        tpu_count = len(edgetpu.list_edge_tpus())
        if tpu_limit >= 0:
            tpu_count = min(tpu_count, tpu_limit)

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
            if self.pipe and self.pipe.first_name is None and \
                time.time() - self.watchdog_time > self.max_idle_secs_before_recycle:
                logging.warning("No work in {} seconds, watchdog shutting down TPUs.".format(self.max_idle_secs_before_recycle))
                self.runner_lock.acquire(timeout=MAX_WAIT_TIME)
                if self.pipe:
                    self.pipe.delete()
                self.runner_lock.release()
                # Pipeline will reinitialize itself as needed
            time.sleep(self.watchdog_idle_secs)

        logging.debug("Watchdog caught shutdown in {}".format(threading.get_ident()))

    @staticmethod
    def get_tpu_devices(tpu_limit: int = -1):
        """Returns list of device names in usb:N or pci:N format.

        This function prefers returning PCI Edge TPU first.

        Returns:
        list of devices in pci:N and/or usb:N format

        Raises:
        RuntimeError: if not enough devices are available
        """
        edge_tpus = edgetpu.list_edge_tpus()

        num_pci_devices = sum(1 for device in edge_tpus if device['type'] == 'pci')
        logging.debug("{} PCIe TPUs detected".format(num_pci_devices))

        tpu_l = ['pci:%d' % i for i in range(min(len(edge_tpus), num_pci_devices))] + \
                ['usb:%d' % i for i in range(max(0, len(edge_tpus) - num_pci_devices))]

        if tpu_limit > 0:
            return tpu_l[:tpu_limit]
        else:
            return tpu_l


    def _get_model_filenames(self, options: Options, tpu_list: list) -> list:
        """
        Returns a list of filenames based on the list of available TPUs and 
        supplied model and segment filenames. If we don't have segment filenames
        (ie just a complete TPU model filename) then return that. If we have
        more than one list of segment files then use the list of files that best
        matches the number of TPUs we have, otherwise use the single list we
        have. If all else fails return the single TPU filename as a list.
        """

        # if TPU no-show then default is CPU
        self.device_type   = 'CPU'
        if not any(tpu_list):
            return []

        device_count  = len(tpu_list)  # TPUs. We've at least found one
        self.device_type   = 'Multi-TPU'
        if device_count == 1:
            self.device_type   = 'TPU'

        # If TPU found then default is single TPU model file (no segments)
        if not any(options.tpu_segments_lists) or device_count == 1:
            return [options.model_tpu_file]
            
        # We have a list of segment files
        if isinstance(options.tpu_segments_lists, dict):
            # Look for a good match between available TPUs and segment counts
            # Prioritize first match. Note we have only tested up to 8 TPUs,
            # so best performance above that can probably be had by extrapolation.
            device_count = min(device_count, 8)
            if device_count in options.tpu_segments_lists:
                return options.tpu_segments_lists[device_count]
        else:
            # Only one list of segments; use it regardless of even match to TPU count
            if len(options.tpu_segments_lists) <= device_count:
                return options.tpu_segments_lists

        # Couldn't find a good fit, use single segment
        return [options.model_tpu_file]


    # Should be called while holding runner_lock (if called at run time)
    def init_pipe(self, options: Options) -> tuple:
        """
        Initializes the pipe with the TFLite models.
        
        To do this, it needs
        to figure out if we're using segmented pipelines, if we can load all
        the segments to the TPUs, and how to allocate them. For example, if
        we have three TPUs and request a model that contains two segments,
        we will load the two segments into two TPUs.
        """

        error = ""

        tpu_list          = TPURunner.get_tpu_devices(self.tpu_limit)
        self.model_name   = options.model_name
        self.model_size   = options.model_size

        # This will update self.device_count and self.segment_count
        tpu_model_files   = self._get_model_filenames(options, tpu_list)
        
        # Read labels
        try:
            self.labels   = read_label_file(options.label_file) if options.label_file else {}
        except:
            labels = {}

        # Initialize EdgeTPU pipe.
        self.device_type = "Multi-TPU"

        try:
            self.pipe = DynamicPipeline(tpu_list, tpu_model_files)
        except TPUException as tpu_ex:
            self.pipe = None
            logging.warning(f"TPU Exception creating interpreter: {tpu_ex}")
            error = "Failed to create interpreter (Coral issue)"
        except FileNotFoundError as ex:
            self.pipe = None
            logging.warning(f"Model file not found: {ex}")
            error = "Model file not found. Please download the model if possible"
        except Exception as ex:
            self.pipe = None
            logging.warning(f"Exception creating interpreter: {ex}")
            error = "Unable to create the interpreter"

        if not self.pipe:
            logging.warning(f"No Coral TPUs found or able to be initialized. Using CPU.")
            try:
                # Try the edgeTPU library to create the interpreter for the CPU
                # file. Can't say I've ever had success with this
                self.pipe = DynamicPipeline(["cpu"], [options.model_cpu_file])
                self.device_type = "CPU"
            except Exception as ex:
                logging.warning(f"Unable to create interpreter for CPU using edgeTPU library: {ex}")
                self.device_type = None
                error = error + ". Unable to create interpreter for CPU using edgeTPU library"
                # Raising this exception kills everything dead. We can still fallback, so don't do this
                # raise

        if self.device_type:
            self.pipe_created = datetime.now()

            self.input_details  = self.pipe.interpreters[0][0].input_details[0]
            self.output_details = self.pipe.interpreters[-1][0].output_details[0]

            # Print debug
            logging.info("{} device & segment counts: {} & {}"
                        .format(self.device_type,
                                len(self.pipe.tpu_list),
                                len(self.pipe.fname_list)))
            logging.debug(f"Input details: {self.input_details}")
            logging.debug(f"Output details: {self.output_details}")

        return (self.device_type, error)


    def _periodic_check(self, options: Options, force: bool = False,
                        check_temp: bool = True, check_refresh: bool = True) -> tuple:
        """
        Run a periodic check to ensure the temperatures are good and we don't
        need to (re)initialize the interpreters/workers/pipelines. The system
        is setup to refresh the TF interpreters once an hour.

        @param options       - options for creating interpreters
        @param force         - force the recreation of interpreters
        @param check_temp    - perform a temperature check (PCIe only)
        @param check_refresh - check for, and refresh, old interpreters
        
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
        error  = None
        now_ts = datetime.now()
        
        if not self.pipe:
            logging.debug("No pipe found. Recreating.")
            force = True

        # Force if we've changed the model
        if options.model_name != self.model_name or \
           options.model_size != self.model_size:
            logging.debug("Model change detected. Forcing model reload.")
            force = True

        # Check to make sure we aren't checking too often
        if self.pipe and self.last_check_time != None and \
           not force and (now_ts - self.last_check_time).total_seconds() < 10:
            return True, None

        self.last_check_time = now_ts
        
        # Check temperatures
        if check_temp:
            if self.temp_fname_format != None and self.pipe:
                msg = "TPU {} is {}C and will likely be throttled"
                temp_arr = []
                for i in range(len(self.pipe.tpu_list)):
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

        # Once an hour, refresh the pipe
        if (force or check_refresh) and self.pipe:
            current_age_sec = (now_ts - self.pipe_created).total_seconds()
            if force or current_age_sec > self.pipe_lifespan_secs:
                logging.info("Refreshing the TFLite Interpreters")

                # Close all existing work before destroying...
                self._delete()

                # Re-init while we still have the lock
                try:
                    (device, error) = self.init_pipe(options)
                except:
                    self.pipe = None

        # (Re)start them if needed
        if not self.pipe:
            logging.info("Initializing the TFLite Interpreters")
            try:
                (device, error) = self.init_pipe(options)
            except:
                self.pipe = None

        if self.pipe:
            self.pipe.balance_queues()

        return (bool(self.pipe), error)

    def __del__(self):
        with self.runner_lock:
            self._delete()
        self.watchdog_shutdown = True
        self.watchdog_thread.join(timeout=self.watchdog_idle_secs*2)

    def _delete(self):
        # Close pipeline
        if self.pipe:
            self.pipe.delete()
            self.pipe = None

    def pipeline_ok(self) -> bool:
        """ Check we have valid interpreters """
        with self.runner_lock:
            return bool(self.pipe and any(self.pipe.interpreters))

    def process_image(self,
                      options:Options,
                      image: Image,
                      score_threshold: float) -> (list, int, str):
        while True:
            try:
                return self._process_image(options, image, score_threshold)
            except queue.Empty:
                logging.warning("Queue stalled; refreshing interpreters.")
                with self.runner_lock:
                    self._periodic_check(options, force=True)


    def _process_image(self,
                       options:Options,
                       image: Image,
                       score_threshold: float) -> (list, int, str):
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

        Note that the image object is modified in place to resize it
        for in input tensor.
        """
        all_objects = []
        all_queues  = []
        _, m_height, m_width, _ = self.input_details['shape']
        
        # Potentially resize & pipeline a number of tiles
        for rs_image, rs_loc in self._get_tiles(options, image):
            rs_queue = queue.Queue(maxsize=1)
            all_queues.append((rs_queue, rs_loc))
            logging.debug("Enqueuing tile in pipeline")

            with self.runner_lock:
                # Recreate the pipe if it is stale, but also check if we can
                # and have created the pipe. It's not always successful...
                (pipe_ok, error) = self._periodic_check(options)
                if not pipe_ok:
                    return None, 0, error

            self.pipe.enqueue(rs_image, rs_queue)   

        # Wait for the results here
        tot_inference_time = 0
        for rs_queue, rs_loc in all_queues:
            # Wait for results
            # We may have to wait a few seconds at most, but I'd expect the
            # pipeline to clear fairly quickly.
            start_inference_time = time.perf_counter()
            result = rs_queue.get(timeout=MAX_WAIT_TIME)
            tot_inference_time += time.perf_counter() - start_inference_time
            assert result

            boxes, class_ids, scores, count = self._decode_result(result, score_threshold)
            
            logging.debug("BBox scaling params: {}x{}, ({},{}), {:.2f}x{:.2f}".
                format(m_width, m_height, *rs_loc))

            # Create Objects for each valid result
            for i in range(int(count[0])):
                if scores[0][i] < score_threshold:
                    continue
                    
                ymin, xmin, ymax, xmax = boxes[0][i]
                
                bbox = detect.BBox(xmin=(max(xmin, 0.0)*m_width  + rs_loc[0])*rs_loc[2],
                                   ymin=(max(ymin, 0.0)*m_height + rs_loc[1])*rs_loc[3],
                                   xmax=(min(xmax, 1.0)*m_width  + rs_loc[0])*rs_loc[2],
                                   ymax=(min(ymax, 1.0)*m_height + rs_loc[1])*rs_loc[3])

                all_objects.append(detect.Object(id=int(class_ids[0][i]),
                                                 score=float(scores[0][i]),
                                                 bbox=bbox.map(int)))
        
        # Convert to ms
        tot_inference_time = int(tot_inference_time * 1000)

        # Remove duplicate objects
        unique_indexes = self._non_max_suppression(all_objects, options.iou_threshold)
        self.watchdog_time = time.time()
        
        return ([all_objects[i] for i in unique_indexes], tot_inference_time, None)
        
        
    def _decode_result(self, result_list, score_threshold: float):
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
                    (dict_values - output_zero) * output_scale,
                    conf_thres=score_threshold)
            else:
                rs = self._yolov5_non_max_suppression(
                    (dict_values - output_zero) * output_scale,
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
        xyxy[:, 1] = xywh[:, 0] - xywh[:, 2] * 0.5  # top left x
        xyxy[:, 0] = xywh[:, 1] - xywh[:, 3] * 0.5  # top left y
        xyxy[:, 3] = xywh[:, 0] + xywh[:, 2] * 0.5  # bottom right x
        xyxy[:, 2] = xywh[:, 1] + xywh[:, 3] * 0.5  # bottom right y
        return xyxy

    
    def _nms(self, dets, scores, thresh):
        '''
        dets is a numpy array : num_dets, 4
        scores is a numpy array : num_dets,
        '''

        x1 = dets[:, 0]
        y1 = dets[:, 1]
        x2 = dets[:, 2]
        y2 = dets[:, 3]

        areas = (x2 - x1 + 1e-9) * (y2 - y1 + 1e-9)
        order = scores.argsort()[::-1] # get boxes with more ious first
        
        keep = []
        while order.size > 0:
            i = order[0] # pick maximum iou box
            other_box_ids = order[1:]
            keep.append(i)
            
            xx1 = np.maximum(x1[i], x1[other_box_ids])
            yy1 = np.maximum(y1[i], y1[other_box_ids])
            xx2 = np.minimum(x2[i], x2[other_box_ids])
            yy2 = np.minimum(y2[i], y2[other_box_ids])
            
            w = np.maximum(0.0, xx2 - xx1 + 1e-9) # maximum width
            h = np.maximum(0.0, yy2 - yy1 + 1e-9) # maximum height
            inter = w * h
              
            ovr = inter / (areas[i] + areas[other_box_ids] - inter)
            
            inds = np.where(ovr <= thresh)[0]
            order = order[inds + 1]

        return np.array(keep)


    def _yolov8_non_max_suppression(self, prediction, conf_thres=0.25, iou_thres=0.45,
                                    labels=(), max_det=3000):

        nc = prediction.shape[1] - 4  # number of classes
        mi = 4 + nc  # mask start index

        xc = np.amax(prediction[:, 4:mi], 1) > conf_thres  # candidates

        prediction = prediction.transpose(0,2,1)  # shape(1,84,6300) to shape(1,6300,84)

        # Checks
        assert 0 <= conf_thres <= 1, f'Invalid Confidence threshold {conf_thres}, valid values are between 0.0 and 1.0'
        assert 0 <= iou_thres <= 1, f'Invalid IoU {iou_thres}, valid values are between 0.0 and 1.0'

        # Settings
        _, max_wh = 2, 4096  # (pixels) minimum and maximum box width and height
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
        xc = prediction[..., 4] > conf_thres  # candidates

        # Settings
        # min_wh = 2  # (pixels) minimum box width and height
        max_wh = 7680  # (pixels) maximum box width and height
        max_nms = 30000  # maximum number of boxes into torchvision.ops.nms()
        time_limit = 0.5 + 0.05 * bs  # seconds to quit after

        t = time.time()
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
                logging.warning(f"WARNING ⚠️ NMS time limit {time_limit:.3f}s exceeded")
                break  # time limit exceeded

        return output

        
    def _non_max_suppression(self, objects: list, threshold: float) -> list:
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
                               m_width: int,
                               m_height: int):
        """
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

        # Fit image within target size
        resamp_x = int(m_width  + (tiles_x - 1) * (m_width  - options.tile_overlap))
        resamp_y = int(m_height + (tiles_y - 1) * (m_height - options.tile_overlap))

        # Chop & resize image piece
        if image.mode != 'RGB':
            image = image.convert('RGB')
        image.thumbnail((resamp_x, resamp_y), Image.LANCZOS)
        logging.debug("Resizing to {} x {} for tiling".format(image.width, image.height))

        # Rescale the input from uint8
        input_zero = float(self.input_details['quantization'][1])
        input_scale = 1.0 / (255.0 * self.input_details['quantization'][0])

        # It'd be useful to print this once at the beginning of the run
        key = "{} {}".format(*image.size)
        if key not in self.printed_shape_map:
            logging.info(
                "Mapping {} image to {}x{} tiles".format(image.size, tiles_x, tiles_y))
            self.printed_shape_map[key] = True

        # Do chunking
        # Image will not be an even fit in at least one dimension; space tiles appropriately.
        tiles = []
        step_x = 1
        if tiles_x > 1:
            step_x = int(math.ceil((image.width - m_width)/(tiles_x-1)))
        step_y = 1
        if tiles_y > 1:
            step_y = int(math.ceil((image.height - m_height)/(tiles_y-1)))

        for x_off in range(0, max(image.width - m_width, 0) + tiles_x, step_x):
            for y_off in range(0, max(image.height - m_height, 0) + tiles_y, step_y):
                # Adjust contrast on a per-chunk basis; we will likely be quantizing the image during scaling
                image_chunk = ImageOps.autocontrast(image.crop((x_off,
                                                                y_off,
                                                                x_off + m_width,
                                                                y_off + m_height)), 1)
                # Normalize to whatever the input is
                cropped_arr = np.asarray(image_chunk, np.float32) * input_scale + input_zero

                logging.debug("Resampled image tile {} at offset {}, {}".format(cropped_arr.shape, x_off, y_off))
                resamp_info = (x_off, y_off, i_width/image.width, i_height/image.height)

                tiles.append((cropped_arr.astype(self.input_details['dtype']), resamp_info))


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

