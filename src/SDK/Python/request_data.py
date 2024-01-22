
import base64
import io
from io import BytesIO
import wave
import json

from PIL import Image
from common import JSON
# from logging import LogMethod

class RequestData:
    """
    Contains information on the request passed in by a client for an AI
    inference operation, and provides helper methods to access this information
    """

    # Constructor
    def __init__(self, json_request_data: str = None):

        self._verbose_exceptions = True

        if json_request_data:    
            request_data    = json.JSONDecoder().decode(json_request_data)
            self.request_id = request_data.get("reqid", "")
            self.payload    = request_data["payload"]
        else:
            self.request_id = ""

        if not hasattr(self, 'payload') or not self.payload:
            self.payload = {
                "queue":      "N/A",
                "urlSegments": None,
                "command":     None,
                "files" :      [ ],
                "values" :     [ ]
            }

        self._queue_name = self.payload.get("queue"       "N/A")
        self._segments   = self.payload.get("urlSegments", None)
        self._command    = self.payload.get("command",     None)
        self.value_list  = self.payload.get("values",      None)
        self.files       = self.payload.get("files",       None)
       
    @staticmethod
    def clamp(value, min_value, max_value) -> any:
        """ Clamps a value between min_value and max_value inclusive """
        return max(min(max_value, value), min_value)
    
    @staticmethod
    def restrict(value, values, default_value) -> str:
        """ Restricts a string to a set of values """
        return value if value in values else default_value

    @staticmethod
    def encode_image(image: Image, image_format: str = "PNG") -> str:
        """
        Encodes an Image as a base64 encoded string
        """
        with BytesIO() as buffered:
            image.save(buffered, format=image_format)
            img_dataB64_bytes : bytes = base64.b64encode(buffered.getvalue())
            img_dataB64 : str = img_dataB64_bytes.decode("ascii");

            return img_dataB64
     
    @staticmethod
    def encode_file_contents(file_name: str) -> str:
        """
        Reads the content of a binary file and returns the Base64 encoding of
        the file contents. 
        On error, returns None.
        """
        try:
            # Open the binary file and read its contents
            with open(file_name, 'rb') as f:
                file_contents = f.read()
                f.close()

            # Encode the binary data as a base64 string
            encoded_file_contents = base64.b64encode(file_contents).decode('ascii')

            return encoded_file_contents
        except:
            return None

    @property
    def queue(self) -> str:
        """ Gets the name of the queue """
        return self._queue
      
    @queue.setter
    def queue(self, queue_name) -> None:
        """ Sets the name of the queue """
        self._queue = queue_name
        self.payload["queue"] = queue_name

    @property
    def command(self) -> str:
        """ Gets the command to be sent to the module """
        return self._command
      
    @command.setter
    def command(self, command_name) -> None:
        """ Sets the command to be sent to the module """
        self._command = command_name
        self.payload["command"] = command_name

    @property
    def segments(self):
        """ Gets the segments of the URL that was used to make the API call """
        return self._segments
      
    @segments.setter
    def segments(self, segments) -> None:
        """ Sets the segments of the URL that was used to make the API call """
        self._segments = segments
        self.payload["urlSegments"] = segments

    def json(self) -> JSON:
        json_request_data = {
            "reqid": "",
            "payload": self.payload
        }
        request_data_str = json.JSONEncoder().encode(json_request_data) 
        return request_data_str

    def add_value(self, key: str, value: any) -> None:
        if not key:
            return None       
        self.payload["values"].append({"key": key, "value" : [value]})

    def add_file(self, file_name: str) -> None:
        if not file_name:
            return
        self.payload["files"].append({ "data": RequestData.encode_file_contents(file_name) })

    def get_image(self, index : int) -> Image:
        """
        Gets an image from the requests 'files' array that was passed in as 
        part of a HTTP POST.
        Param: index - the index of the image to return
        Returns: An image if successful; None otherwise.

        NOTE: It's probably worth helping out users by sniffing EXIF data and
        rotating images prior to passing them to modules. This could be done
        client side (https://github.com/exif-js/exif-js/blob/master/exif.js) or
        call PIL.ImageOps.exif_transpose here. See
        https://pillow.readthedocs.io/en/latest/reference/ImageOps.html#PIL.ImageOps.exif_transpose
        """

        try:
            if self.files is None or len(self.files) <= index:
                return None

            img_file    = self.files[index]
            img_dataB64 = img_file["data"]
            img_bytes   = base64.b64decode(img_dataB64)
            
            with io.BytesIO(img_bytes) as img_stream:
                img = Image.open(img_stream).convert("RGB")
                return img

        except Exception as ex:

            if self._verbose_exceptions:
                print(f"Error getting image {index} from request")

            """
            err_msg = "Unable to get image from request"
            if self._verbose_exceptions:
                err_msg = "Error in get_image: " + str(ex)

            self.log(LogMethod.Error|LogMethod.Server, {
                "message": err_msg,
                "method": sys._getframe().f_code.co_name,
                "process": self.queue_name,
                "filename": __file__,
                "exception_type": ex.__class__.__name__
            })
            """
            return None

    def get_file_bytes(self, index : int) -> bytearray:
        """
        Gets a byte array from a file from the requests 'files' array that was
        passed in as part of a HTTP POST.
        Param: index - the index of the WAV file to return
        Returns: An image if successful; None otherwise.

        Example usage: Reading a WAV file:
            wav_bytes = request_data.get_wave_bytes(0)
            with io.BytesIO(wav_bytes) as wav_stream:
                with wave.open(wav_stream, 'rb') as wav_file:
                    frames       = wav_file.readframes(wav_file.getnframes())
                    sample_width = wav_file.getsampwidth()
                    channels     = wav_file.getnchannels()
                    frame_rate   = wav_file.getframerate()  
        """

        try:
            if self.files is None or len(self.files) <= index:
                return None

            file_data    = self.files[index]
            file_dataB64 = file_data["data"]
            file_bytes   = base64.b64decode(file_dataB64)

            return file_bytes

        except Exception as ex:
            if self._verbose_exceptions:
                print(f"Error getting WAV file {index} from request")
            return None

    def get_value(self, key : str, defaultValue : str = None) -> str:
        """
        Gets a value from the HTTP request Form send by the client
        Param: key - the name of the key holding the data in the form collection
        Returns: The data if successful; None otherwise.
        Remarks: Note that HTTP forms contain multiple values per key (a string
        array) to allow for situations like checkboxes, where a set of checkbox
        controls share a name but have unique IDs. The form will contain an
        array of values for the shared name. 
        ** WE ONLY RETURN THE FIRST VALUE HERE **
        """

        try:
            # value_list is a list. Note that in a HTML form, each element may
            # have multiple values 
            if self.value_list is None:
                return defaultValue

            for value in self.value_list:
                if value["key"] == key :
                    return value["value"][0]
        
            return defaultValue

        except Exception as ex:
            if self._verbose_exceptions:
                print(f"Error getting {key} from request data payload: {str(ex)}")
            return defaultValue

    def get_int(self, key : str, defaultValue : int = None) -> int:

        value = self.get_value(key)
        if value is None:
            return defaultValue
        
        try:
            return int(value)
        except:
            return defaultValue
        
    def get_float(self, key : str, defaultValue : float = None) -> float:

        value = self.get_value(key)
        if value is None:
            return defaultValue
        
        try:
            return float(value)
        except:
            return defaultValue 
        
    def get_bool(self, key : str, defaultValue : bool = None) -> bool:

        value = self.get_value(key)
        if value is None:
            return defaultValue
        
        return value.lower() in [ 'y', 'yes', 't', 'true', 'on', '1' ]