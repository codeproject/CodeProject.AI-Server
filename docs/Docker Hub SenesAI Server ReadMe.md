[![Open in Visual Studio Code](https://open.vscode.dev/badges/open-in-vscode.svg)](https://open.vscode.dev/codeproject/CodeProject.SenseAI) [![made-with-python](https://img.shields.io/badge/Made%20with-Python-orange)](https://www.python.org/) [![GitHub license](https://img.shields.io/badge/license-SSPL-green)](https://www.mongodb.com/licensing/server-side-public-license) [![Open Source Love svg2](https://badges.frapsoft.com/os/v2/open-source.svg?v=103)](https://github.com/ellerbrock/open-source-badges/)

# CodeProject SenseAI Server

A standalone, self-hosted, fast, free and Open Source Artificial Intelligence microserver for any 
platform, any language. It can be installed locally, required no off-device or out of network data
transfer, and is easy to use.

See [SenseAI Overview](https://www.codeproject.com/AI/index.aspx) and [CodeProject SenseAI Server: AI the easy way.](https://www.codeproject.com/Articles/5322557/CodeProject-SenseAI-Server-AI-the-easy-way() for more details.

# Why

1. AI programming is something every single developer should be aware of. We wanted a fun project we could use to help teach developers and get them involved in AI. We'll be using SenseAI as a focus for articles and exploration to make it fun and painless to learn AI programming.

1. We got sick of fighting versions and libraries and models and being blocked by tiny annoying things every step of the way. So we put put this together so we could save you the frustration. We'll take care of the housekeeping, you focus on the code.
  
1. We also got sick of needing to sign up to potentially expensive services for AI functionality. This  is something we need, and by sharing maybe you can use it too, and hopefully add your own modules and improvements along the way.

# Running the CodeProject SenseAI Server in Docker
The following commands will start the SenseAI with a data directory shared with the host OS file system.  This allows the server to be restarted or upgrades without loss of information.
Internally, CodeProject SenseAI server runs on port 5000.  You can expose this to the host system as required.  In the commands shown below we just map to the host port 5000, except for macOS which is mapped to 5050 as MacOS uses 5000.
##### For Windows

```
docker run -p 5000:5000 --name SenseAI-Server -d -v c:\ProgramData\CodeProject\SenseAI:/usr/share/CodeProject/SenseAI codeproject/senseai-server
```

##### For Linux

```
docker run -p 5000:5000 --name SenseAI-Server -d -v /usr/share/CodeProject/SenseAI:/usr/share/CodeProject/SenseAI codeproject/senseai-server 
```

##### For macOS, choose a port other than 5000:

```
docker run -p 5500:5000 --name SenseAI-Server -d -v /usr/share/CodeProject/SenseAI:/usr/share/CodeProject/SenseAI codeproject/senseai-server
```
## Accessing the CodeProject SenseAI Dashboard.
Open a browser and navigate to[ http://localhost:5000](http://localhost:5000) to open the SenseAI Dashboard.  This will provide you with details of the server operation.
## Play with the Server
We provide a sample application written in HTML and JavaScript that performs various AI operations.  Open [http://localhost:5000/vision.html](http://localhost:5000/vision.html) in a browser.  There is also a link to this at the bottom of the Dashboard.
## Get some test images
Load http://localhost:5000/testdata.zip (after launching SenseAI server) to download some test images for use with the SenseAI playground
