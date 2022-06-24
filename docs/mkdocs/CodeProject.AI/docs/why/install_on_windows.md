---
title: Installing CodeProject.AI Server on Windows
tags:
  - CodeProject.AI
  - Windows
---

# Installing CodeProject.AI Server on Windows

To **install CodeProject.AI** as a standalone service ready for integration with applications
such as HomeAssist or BlueIris, download the 
[latest installation package](https://www.codeproject.com/ai/latest.aspx).

Double click the installer. This will install the server as a Windows Service. CodeProject.AI
Server and the backend analysis services will now be running. The front-end server and the 
analysis services will automatically restart each time your machine is restarted.

To **explore CodeProject.AI** Click on the SensaAI playground link on the server dashboard. 

The server will, of course, need to be running for this test application to function. Sample images
can be found in the <i>TestData</i> folder under the <i>C:\Program Files\CodeProject\AI</i> folder


## Accessing the CodeProject CodeProject.AI Dashboard.
Open a browser and navigate to [http://localhost:5000](http://localhost:5000) to open the
CodeProject.AI Dashboard.  This will provide you with details of the server operation.

## Play with the Server
We provide a sample application written in HTML and JavaScript that performs various AI operations.
Open [http://localhost:5000/vision.html](http://localhost:5000/vision.html) in a browser. 
There is also a link to this at the bottom of the Dashboard.

## Get some test images

Load [http://localhost:5000/testdata.zip](http://localhost:5000/testdata.zip) (after launching 
CodeProject.AI server) to download some test images for use with the CodeProject.AI explorer.
