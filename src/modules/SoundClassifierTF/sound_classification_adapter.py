# Import our general libraries
import io
import os
import sys
import time

# Import the CodeProject.AI SDK. This will add to the PATH var for future imports
sys.path.append("../../SDK/Python")
from common import JSON
from request_data import RequestData
from module_runner import ModuleRunner

from scipy.io import wavfile
import numpy as np

# Import the method of the module we're wrapping
from options import Options

# Import the method of the module we're wrapping
import audio_params
from sound_classification import inference_waveform

class SoundClassification_adapter(ModuleRunner):

    def __init__(self):
        super().__init__()
        self.opts        = Options()
        self.classes     = list()
        self.sound_names = None
        self.classifier  = None # Lazy load later on


    def initialise(self) -> None:

        # if the module was launched outside of the server then the queue name 
        # wasn't set. This is normally fine, but here we want the queue to be
        # the same as the other object detection queues
        if not self.launched_by_server:
            self.queue_name = "soundclassifier_queue"

        # TODO: Read this file async
        with open(audio_params.AUDIO_CLASSES) as class_file:
            for line in class_file:
                self.classes.append(line.strip().split(" ")[2])

        self.sound_names = tuple(self.classes)

        self.can_use_GPU = self.system_info.hasTensorflowGPU or self.system_info.hasTorchMPS

        if self.system_info.hasTensorflowGPU:
            self.inference_device  = "GPU"
            self.inference_library = "TensorFlow"
        elif self.system_info.hasTorchMPS:
            self.inference_device  = "GPU"
            self.inference_library = "MPS"

        self._num_items_found = 0
        self._histogram       = {}
        

    def process(self, data: RequestData) -> JSON:

        # The route to here is /v1/sound/classify

        # if data.command == "list-custom":               # list all models available
        #    return { "success": True, "models": [ 'MobileNet SSD'] }

        if data.command == "classify": # or data.command == "custom":
            threshold: float  = float(data.get_value("min_confidence", self.opts.min_confidence))  
            response = self._do_classification(data, threshold)
        else:
            # await self.report_error_async(None, __file__, f"Unknown command {data.command}")
            self.report_error(None, __file__, f"Unknown command {data.command}")
            response = { "success": False, "error": "unsupported command" }

        return response


    def status(self) -> JSON:
        statusData = super().status()
        statusData["numItemsFound"] = self._num_items_found
        statusData["histogram"]     = self._histogram
        return statusData


    def update_statistics(self, response):
        super().update_statistics(response)
        if "success" in response and response["success"] and "predictions" in response:
            predictions = response["predictions"]
            self._num_items_found += len(predictions) 
            for prediction in predictions:
                label = prediction["label"]
                if label not in self._histogram:
                    self._histogram[label] = 1
                else:
                    self._histogram[label] += 1


    def selftest(self) -> JSON:
        
        # file_name = os.path.join("test", "klaxon.wav")
        file_name = os.path.join("test", "mixkit-dog-barking-twice-1.wav")

        request_data = RequestData()
        request_data.queue   = self.queue_name
        request_data.command = "classify"
        request_data.add_file(file_name)
        request_data.add_value("min_confidence", 0.2)

        result = self.process(request_data)
        print(f"Info: Self-test for {self.module_id}. Success: {result['success']}")
        # print(f"Info: Self-test output for {self.module_id}: {result}")

        return { "success": result['success'], "message": "Sound Classification test successful" }

    def _create_spectrogram(self, data, sample_rate):
        
        import matplotlib
        matplotlib.use('Agg')  # Use Agg backend (non-interactive)
        import matplotlib.pyplot as plt
        from PIL import Image
        from io import BytesIO

        spectrogram = None
        try:
            # Check if the audio is mono or stereo
            if len(data.shape) == 1:  # Mono audio
                data = data.reshape(-1, 1)  # Reshape to 2D array with one column
                num_channels = 1
            else:  # Stereo or more channels
                num_channels = data.shape[1]

            # Calculate and plot the spectrogram for each channel
            for channel in range(num_channels):
                plt.subplot(num_channels, 1, channel + 1)
                # cmap='viridis'
                # cmap='inferno' - not bad
                # cmap='spectral' - boring
                cmap='hsv'
                cmap='hot'
                cmap='jet'
                plt.specgram(data[:, channel], Fs=sample_rate, NFFT=1024, cmap=cmap)
                plt.ylabel(f'Channel {channel + 1}')

            # Save the plot

            # spectrogram = Image.fromarray(spectrum)

            fig, ax = plt.gcf(), plt.gca()
            fig.canvas.draw()

            width, height = fig.canvas.get_width_height()
            spectrogram = Image.frombytes('RGB', (width, height), fig.canvas.tostring_rgb())

            # Close and free memory
            plt.close()

            # spectrogram.show()

        except Exception as ex:
            print(ex)

        return spectrogram
    

    def _do_classification(self, data: RequestData, score_threshold: float):
        
        start_process_time = time.perf_counter()
    
        try:        
            with io.BytesIO(data.get_file_bytes(0)) as wav_stream:
                sample_rate, wav_data = wavfile.read(wav_stream)
                assert wav_data.dtype == np.int16, 'Bad sample type: %r' % wav_data.dtype
                samples = wav_data / 32768.0  # Convert to [-1.0, +1.0]

                spectrogram = self._create_spectrogram(wav_data, sample_rate)

            if not wav_data.any() or not sample_rate:
                return {
                    "success"     : False, 
                    "label"       : '', 
                    "message"     : '',
                    "error"       : "Unable to load sound",
                    "processMs"   : int((time.perf_counter() - start_process_time) * 1000),
                    "inferenceMs" : inference_time,
                }

            start_sample    = 0
            seconds_of_data = samples / sample_rate

            while start_sample < len(samples):
                sample_data = samples[start_sample:sample_rate] # Get 1 second worth of samples
                predictions, label_pred, prob, inference_time = inference_waveform(samples, sample_rate)
                start_sample += sample_rate
                break   # We'll stop after 1 second. We'll extend this later to return a list of predictions

            label = ''
            if prob > score_threshold and label_pred >= 0 and label_pred < len(self.classes):
                label = self.classes[label_pred]

            if prob < score_threshold:
                return {
                    "success"     : False, 
                    "label"       : '', 
                    "message"     : '',
                    "error"       : "Unable to classify sound",
                    "imageBase64":  RequestData.encode_image(spectrogram) if spectrogram else None,
                    "processMs"   : int((time.perf_counter() - start_process_time) * 1000),
                    "inferenceMs" : inference_time,
                }

            return {
                "success"     : True, 
                "label"       : label, 
                "confidence"  : prob,
                "message"     : f"Detected sound {label}",
                "imageBase64" :  RequestData.encode_image(spectrogram) if spectrogram else None,
                "processMs"   : int((time.perf_counter() - start_process_time) * 1000),
                "inferenceMs" : inference_time
            }

        except Exception as ex:
            # await self.report_error_async(ex, __file__)
            self.report_error(ex, __file__)
            return { "success": False, "error": "Error occurred on the server"}


if __name__ == "__main__":
    SoundClassification_adapter().start_loop()
