---
---
title: Running CodeProject.AI Server in Docker
tags:
  - CodeProject.AI
  - Docker
---

# Running CodeProject CodeProject.AI Server in Docker

The following commands will start the CodeProject.AI with a data directory shared with the host OS file system.  This allows the server to be restarted or upgrades without loss of information.
Internally, CodeProject.AI server runs on port 5000.  You can expose this to the host system as required.  In the commands shown below we just map to the host port 5000, except for macOS which is mapped to 5050 as MacOS uses 5000.

=== "Windows"

	``` batch title='Command line'
	docker run -p 5000:5000 --name CodeProject.AI-Server -d -v c:\ProgramData\CodeProject\AI:/usr/share/CodeProject/AI codeproject/codeprojectai-server
	```

=== "Linux"

	``` shell
	docker run -p 5000:5000 --name CodeProject.AI-Server -d -v /usr/share/CodeProject/AI:/usr/share/CodeProject/AI codeproject/codeprojectai-server 
	```

=== "macOS"

	For macOS it's important to choose a port other than 5000, since port 5000 is reserved. Here
	we'll use port 5500:

	``` shell
	docker run -p 5500:5000 --name CodeProject.AI-Server -d -v /usr/share/CodeProject/AI:/usr/share/CodeProject/AI codeproject/codeprojectai-server
	```


## Accessing the CodeProject CodeProject.AI Dashboard.
Open a browser and navigate to [http://localhost:5000](http://localhost:5000) (or [http://localhost:5500](http://localhost:5500) on macOS) to open the CodeProject.AI Dashboard.  This will provide you with details of the server operation.
## Play with the Server
We provide a sample application written in HTML and JavaScript that performs various AI operations.  Open [http://localhost:5000/vision.html](http://localhost:5000/vision.html) in a browser.  There is also a link to this at the bottom of the Dashboard.
## Get some test images
Load [http://localhost:5000/testdata.zip](http://localhost:5000/testdata.zip) (after launching CodeProject.AI server) to download some test images for use with the CodeProject.AI explorer
