# ReadMe

This code was imported from the Yolov5-net GitHub repository at 
https://github.com/mentalstack/yolov5-net under the terms of its MIT license.

By including the source in our solution we achieve:

- a Yolo module runner that can be used by module authors.
- The ability to update the version of the onnx runtime to 1.10, removing the
  requirement to deploy some very large .PDB files with the application.
- the opportunity to improve performance of the module.  
- Casual observation shows that the time for prediction of objects increases 
  with the number of objects. This suggests that there may be performance 
  improvement opportunities in the post processing of the model inferencing output.


## MIT License

Copyright (c) 2021 Mentalstack

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.