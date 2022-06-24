---
title: API Reference
tags:
  - api
---


# API reference

The API for CodeProject.AI is divided into categories Image, Vision, Text, and Status with
each category further broken into sub-topics.

This document will continually change and be updated to reflect the latest
server version and installed analysis modules


## Image

### Portrait Filter

Blurs the background behind the main subjects in an image.

``` title=''
POST: http://localhost:5000/v1/image/portraitfilter
```

**Platforms**

Windows, Linux, Docker

**Parameters**

 - **image** (File): The image to be filtered.

 - **strength** (Float): How much to blur the background (0.0 - 1.0).
   *Optional*. Defaults to 0.5

**Response**

``` json
{
  "success": (Boolean) // True if successful.
  "filtered_image": (Base64ImageData) // The base64 encoded image that has had its background blurred.
}
```


#### Example

```javascript
// Assume we have a HTML INPUT type=file control with ID=fileChooser
var formData = new FormData();
formData.append('image', fileChooser.files[0]);
formData.append("strength", 0.0);

var url = 'http://localhost:5000/v1/image/portraitfilter';

fetch(url, { method: "POST", body: formData})
      .then(response => {
           if (response.ok) {
               response.json().then(data => {
                   console.log("success: " + data.success)
                   // Assume we have an IMG tag named img1
                   img1.src = "data:image/png;base64," + data.filtered_image;
               })
           }
       });
```





### Background Remover

Removes the background from behind the main subjects in images.

``` title=''
POST: http://localhost:5000/v1/image/removebackground
```

**Platforms**

Windows, Linux, Docker

**Parameters**

 - **image** (File): The image to have its background removed.

 - **use_alphamatting** (Boolean): Whether or not to use alpha matting.
   *Optional*. Defaults to false

**Response**

``` json
{
  "success": (Boolean) // True if successful.
  "imageBase64": (Base64ImageData) // The base64 encoded image that has had its background removed.
}
```


#### Example

```javascript
// Assume we have a HTML INPUT type=file control with ID=fileChooser
var formData = new FormData();
formData.append('image', fileChooser.files[0]);
formData.append("use_alphamatting", false);

var url = 'http://localhost:5000/v1/image/removebackground';

fetch(url, { method: "POST", body: formData})
      .then(response => {
           if (response.ok) {
               response.json().then(data => {
                   console.log("success: " + data.success)
                   // Assume we have an IMG tag named img1
                   img1.src = "data:image/png;base64," + data.imageBase64;
               })
           }
       });
```



## Vision

### Object Detection

Detects multiple objects of 80 types in an image.

``` title=''
POST: http://localhost:5000/v1/vision/detection
```

The object detection module uses YOLO (You Only Look Once) to locate and classify the objects the
models have been trained on. At this point there are 80 different types of objects that can be
detected.

**Platforms**

Windows, Linux, macOS, macOS-Arm, Docker

**Note**. For Windows, Linux and Docker, the Object Detection services are provided by the .NET
YOLO implementation. For macOS and macOS Arm, Object detection is handled by the Python implementation.

**Parameters**

 - **image** (File): The HTTP File Object (image) to be analyzed.

 - **min_confidence** (Float): The minimum confidence level for an object will be detected. In the range 0.0 to 1.0. Default 0.4.

**Response**

``` json
{
  "success": (Boolean) // True if successful.
  "predictions": (Object) // An array of objects with the x_max, x_min, max, y_min, label and confidence.
}
```

#### Example

=== "Python"

    ``` python title=''
    import requests

    image_data = open("my_image.jpg","rb").read()

    response = requests.post("http://localhost:80/v1/vision/detection",
                             files={"image":image_data}).json()

    for object in response["predictions"]:
        print(object["label"])

    print(response)
    ```

=== "Javscript"

    ``` javascript title=''
    var formData = new FormData();
    formData.append('image', fileChooser.files[0]);

    fetch('http://localhost:5000/v1/vision/detect/scene', {
        method: "POST",
        body: formData
    })
    .then(response => {
        if (response.ok) response.json().then(data => {

            for (let prediction of data.predictions)
                console.log(prediction.label);

            console.log(JSON.stringify(data));
        });
    });
    ```


**Response**

``` text title=''
dog
person
person
{'predictions': [ {'x_max': 600, 'x_min': 400, 'y_min': 200, 'y_max': 400,
   'confidence': 95, 'label': 'car' },{'x_max': 100, 'x_min': 200, 'y_min': 50,
   'y_max': 100, 'confidence': 90, 'label': 'dog' },], 'success': True}
```



### Custom Object Detector

Detects objects based on a custom model. Models are stored as .pt files in /CustomDetection/assets directory, and to make a call to a specific model use /vision/custom/model-name, where 'model-name' is the name of the model's .pt file.

``` title=''
POST: http://localhost:5000/v1/vision/custom/model-name
```


**Platforms**

All

**Parameters**

 - **image** (File): The HTTP file object (image) to be analyzed.

 - **min_confidence** (Float): The minimum confidence level for an object will be detected. In the range 0.0 to 1.0. Default 0.4.

**Response**

``` json
{
  "success": (Boolean) // True if successful.
  "predictions": (Object) // An array of objects with the x_max, x_min, max, y_min, label and confidence.
}
```


#### Example

```javascript
// Assume we have a HTML INPUT type=file control with ID=fileChooser
var formData = new FormData();
formData.append('image', fileChooser.files[0]);
formData.append("min_confidence", 0.0);

// Assumes we have licence-plates.pt in the /assets dir
var url = 'http://localhost:5000/v1/vision/custom/licence-plates';

fetch(url, { method: "POST", body: formData})
      .then(response => {
           if (response.ok) {
               response.json().then(data => {
                   console.log("success: " + data.success)
                   console.log("predictions: " + JSON.stringify(data.predictions))
               })
           }
       });
        .catch (error => {
            console.log('Unable to complete API call: ' + error);
       });
```



### Custom Object Detector List Models

Returns a list of models available.

``` title=''
POST: http://localhost:5000/v1/vision/custom/list
```

**Platforms**

All

**Parameters**

(None)

**Response**

``` json
{
  "success": (Boolean) // True if successful.
  "models": (String) // An array of strings containing the names of the models installed.
}
```


#### Example

```javascript

var url = 'http://localhost:5000/v1/vision/custom/list';

fetch(url, { method: "POST", body: formData})
      .then(response => {
           if (response.ok) {
               response.json().then(data => {
                   console.log("success: " + data.success)
                   console.log("models: " + data.models)
               })
           }
       });
        .catch (error => {
            console.log('Unable to complete API call: ' + error);
       });
```


### Scene Classifier

``` title=''
POST: http://localhost:5000/v1/vision/scene
```

Classifies the scene in an image. It can recognise 365 different scenes.

**Platforms**

Windows, Linux, macOS, macOS-Arm, Docker

**Parameters**

 - **image** (File): The HTTP File Object (image) to be analyzed.

**Response**

```json
{
    "success": Boolean // True if successful.
    "label": Text // The classification of the scene such as 'conference_room'.
    "confidence": Float // The confidence in the classification in the range of 0.0 to 1.0.
}
```


### Face Detection

``` title=''
POST: http://localhost:5000/v1/vision/face
```

Detects faces in an image and returns the coordinates of the faces.

**Platforms**

Windows, Linux, macOS, macOS-Arm, Docker

**Parameters**

 - **image** (File): The HTTP File Object (image) to be analyzed.

 - **min_confidence** (Float): The minimum confidence level for an object will be detected. In the range 0.0 to 1.0. Default 0.4.

**Response**
```json
{
    "success": Boolean // True if successful.
    "predictions": Object // An array of objects with the x_max, x_min, max, y_min, and confidence.
}
```

**Example**
```json
{
    "success": True,
    "predictions": [
        {"x_max": 600, "x_min": 400, "y_min": 200, "y_max": 400, "confidence": 95},
        {"x_max": 100, "x_min": 200, "y_min": 50, "y_max": 100, "confidence": 90 },
    ]
}
```


### Register Face

Registers one or more images for a user for recognition. This trains the face recognition model and allows the face recognition to report back a userId based on an image you supply that may or may not contain that user's face.

``` title=''
POST: http://localhost:5000/v1/vision/face/register
```

**Platforms**

Windows, Linux, macOS, macOS-Arm, Docker

**Parameters**

 - **imageN** (File): The one or more HTTP File Objects (images) to be registered.

 - **userid** (Text): The identifying string for the user.

**Response**

``` json
{
  "success": (Boolean) // True if successful.
  "Message": (Text) // face added
}
```


#### Example

```javascript
// Assume we have a HTML INPUT type=file control with ID=fileChooser
var formData = new FormData();
formData.append('imageN', fileChooser.files[0]);
formData.append("userid", '');

var url = 'http://localhost:5000/v1/vision/face/register';

fetch(url, { method: "POST", body: formData})
      .then(response => {
           if (response.ok) {
               response.json().then(data => {
                   console.log("success: " + data.success)
                   console.log("Message: " + data.Message)
               })
           }
       });
```


### List Registered Faces

``` title=''
POST: http://localhost:5000/v1/vision/face/list
```

Lists the users that have images registered.

**Platforms**

Windows, Linux, macOS, macOS-Arm, Docker

**Parameters**

(None)

**Response**

```json
{
    "success": Boolean // True if successful.
    "faces": Object // An array of the userid strings for users with registered images.
}
```

**Example**
```json
{
    "success": True,
    "faces": [ "Jane", "John" ]
}
```


### Delete Registered Face

Removes a userid and images from the Face Registration database.

``` title=''
POST: http://localhost:5000/v1/vision/face/delete
```

**Platforms**

Windows, Linux, macOS, macOS-Arm, Docker

**Parameters**

 - **userid** (Text): The identifying string for the user.

**Response**

``` json
{
  "success": (Boolean) // True if successful.
}
```


#### Example

```javascript
var formData = new FormData();
formData.append("userid", '');

var url = 'http://localhost:5000/v1/vision/face/delete';

fetch(url, { method: "POST", body: formData})
      .then(response => {
           if (response.ok) {
               response.json().then(data => {
                   console.log("success: " + data.success)
               })
           }
       });
```



### Face Recognizer

``` title=''
POST: http://localhost:5000/v1/vision/face/recognize
```

Recognizes all faces in an image and returns the userId and coordinates of each face in the image.
If a new (unregistered) face is detected then no userid for that face will be returned.

**Platforms**

Windows, Linux, macOS, macOS-Arm, Docker

**Parameters**

 - **image** (File): The HTTP File Object (image) to be analyzed.
 - **min_confidence** (Float): The minimum confidence level for an object will be detected. In the range 0.0 to 1.0. Default 0.4.

**Response**

```json
{
    "success": Boolean // True if successful.
    "predictions": Object // An array of objects with the userid, x_max, x_min, max, y_min, label and confidence.
}
```

**Example**
```json
{
    "success": True,
    "predictions": [
        {"x_max": 600, "x_min": 400, "y_min": 200, "y_max": 400, "confidence": 95, "label": "Jane"},
        {"x_max": 100, "x_min": 200, "y_min": 50, "y_max": 100, "confidence": 90, "label": "John" },
    ]
}
```

### Face Comparison

Compares two faces in two images and returns a value indicating how similar the faces are.

``` title=''
POST: http://localhost:5000/v1/vision/face/match
```

**Platforms**

Windows, Linux, macOS, macOS-Arm, Docker

**Parameters**

 - **image1** (File): First HTTP File Object (image) to be analyzed.

 - **image2** (File): Second HTTP File Object (image) to be analyzed.

**Response**

``` json
{
  "success": (Boolean) // True if successful.
  "similarity": (Float) // How similar the two images are, in the range of 0.0 to 1.0.
}
```


#### Example

```javascript
// Assume we have a HTML INPUT type=file control with ID=fileChooser
var formData = new FormData();
formData.append('image1', fileChooser.files[0]);
formData.append('image2', fileChooser.files[1]);

var url = 'http://localhost:5000/v1/vision/face/match';

fetch(url, { method: "POST", body: formData})
      .then(response => {
           if (response.ok) {
               response.json().then(data => {
                   console.log("success: " + data.success)
                   console.log("similarity: " + data.similarity.toFixed(2))
               })
           }
       });
```


<br>

## Text

### Sentiment Analysis

Determines if the supplied text has a positive or negative sentiment

``` title=''
POST: http://localhost:5000/v1/text/sentiment
```

**Platforms**

Windows, Linux, Docker

**Parameters**

 - **text** (Text): The text to be analyzed.

**Response**

``` json
{
  "success": (Boolean) // True if successful.
  "is_positive": (Boolean) // Whether the input text had a positive sentiment.
  "positive_probability": (Float) // The probability the input text has a positive sentiment.
}
```


#### Example

```javascript
var formData = new FormData();
formData.append("text", '');

var url = 'http://localhost:5000/v1/text/sentiment';

fetch(url, { method: "POST", body: formData})
      .then(response => {
           if (response.ok) {
               response.json().then(data => {
                   console.log("success: " + data.success)
                   console.log("is_positive: " + data.is_positive)
                   console.log("positive_probability: " + data.positive_probability.toFixed(2))
               })
           }
       });
```



### Text Summary

Summarizes some content by selecting a number of sentences that are most representitive of the content.

``` title=''
POST: http://localhost:5000/v1/text/summarize
```

**Platforms**

Windows, Linux, macOS, macOS-Arm, Docker

**Parameters**

 - **text** (Text): The text to be summarized

 - **num_sentences** (Integer): The number of sentences to produce.

**Response**

``` json
{
  "success": (Boolean) // True if successful.
  "summary": (Text) // The summarized text.
}
```


#### Example

```javascript
var formData = new FormData();
formData.append("text", '');
formData.append("num_sentences", 0);

var url = 'http://localhost:5000/v1/text/summarize';

fetch(url, { method: "POST", body: formData})
      .then(response => {
           if (response.ok) {
               response.json().then(data => {
                   console.log("success: " + data.success)
                   console.log("summary: " + data.summary)
               })
           }
       });
```

<br>

## Status

### Server Logs

``` title=''
GET: /v1/log/list?count=<count>&last_id=<lastid>
```

Gets up to 20 log entries, starting from id = <lastid>. The "<lastid>" value can be omitted. What's
returned is an array of entries

**Platforms**

Windows, Linux, macOS, macOS-Arm, Docker

**Parameters**

 - **lastid** - the ID of the last entry that was retrieved, in order to send only new log entries
 - **count** - The number of entries to return

**Response**

```json
{
    "id": Integer, The id of the log entry
    "timestamp": A datetime value. The timestamp as UTC time of the log entry
    "entry": Text. The text of the entry itself
}
```


### Server Ping

A server ping. Just so you can easily tell if it's alive

``` title=''
GET: /v1/status/ping
```
**Platforms**

Windows, Linux, macOS, macOS-Arm, Docker

**Response**

```json
{ 
    "success": true 
}
```

If all is good.

### Server Update Available

A note on whether an update is available

``` title=''
GET: /v1/status/updateavailable
```
**Platforms**

Windows, Linux, macOS, macOS-Arm, Docker

**Response** 

```json 
{
    "success"         : true/false,
    "message"         : "An update to version X  is available" / "You have the latest",
    "version"         : <version object>,
    "updateAvailable" : true/false
};
```

Where version object is

```json 
"versionInfo": {
    "Major": 1,
    "Minor": 1,
    "Patch": 0,
    "PreRelease": "Beta",
    "Build": 0,
    "SecurityUpdate": false,
    "File": "CodeProject.AI.Server-1.1.0.zip",
    "ReleaseNotes": "Added Text Summary module"
}
```

<!--

Image Enhance
http://localhost:80/v1/vision/enhance
The image enhance API enlarges your image by 4X the original width and height, while simulatenously increasing the quality of the image.

parameters [POST]Response
{
    "success": True,
    "base64": ".........mabKicgSdq/3fSo6UfcH0pATmhEgST3phPHNKe1RuetUJn//2Q==",
    "width": 1840
    "height": 1036
}

-->




