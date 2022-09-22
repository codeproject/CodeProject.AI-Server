# ReadMe
This code was imported from the Yolov5-net GitHub repository at https://github.com/mentalstack/yolov5-net under the terms of its MIT license.

By including the source in our solution we achieve:

- a Yolo module runner that can be used by module authors.
- The ability to update the version of the onnx runtime to 1.10, removing the requirement to deploy some very large .PDB files with the application.
- the opportunity to improve performance of the module.  
  - Casual observation shows that the time for prediction of objects increases with the number of objects. This suggests that there may be performance improvement opportunities in the post processing of the model inferencing output.

