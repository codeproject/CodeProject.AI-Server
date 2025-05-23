[![made-for-VSCode](https://img.shields.io/badge/Made%20for-VSCode-1f425f.svg)](https://open.vscode.dev/codeproject/CodeProject.AI-Server/) [![made-with-dotnet](https://img.shields.io/badge/Made%20with-net9.0-blue)](https://dotnet.microsoft.com/) [![made-with-python](https://img.shields.io/badge/Made%20with-Python-orange)](https://www.python.org/) [![GitHub license](https://img.shields.io/badge/License-SSPL-green)](https://www.mongodb.com/licensing/server-side-public-license) [![Open Source Love svg2](https://badges.frapsoft.com/os/v2/open-source.svg?v=103)](https://github.com/ellerbrock/open-source-badges/)
<!--
&nbsp; &nbsp;

[![NVIDIA](https://img.shields.io/badge/NVIDIA-76B900?style=for-the-badge&logo=nvidia&logoColor=white)](https://nvidia.com)
[![Raspberry Pi](https://img.shields.io/badge/Raspberry%20Pi-A22846?style=for-the-badge&logo=Raspberry%20Pi&logoColor=white)](https://raspberrypi.com)
[![Apple Silicon](https://img.shields.io/badge/apple%20silicon-333333?style=for-the-badge&logo=apple&logoColor=white)](https://apple.com)
-->

# CodeProject.AI Server

 [**Download the latest version**](https://codeproject.github.io/codeproject.ai/latest.html)

A standalone, self-hosted, fast, free and Open Source Artificial Intelligence microserver for any 
platform, any language. It can be installed locally, required no off-device or out of network data
transfer, and is easy to use.

![Object detection](https://codeproject.github.io/codeproject.ai/img/DetectThings.png)

# Supported Platforms

<div style="width:75%;min-width:700px;margin:30px auto">

| <img src="https://codeproject.github.io/codeproject.ai/img/windows.svg" title="Windows" style="width:64px">  | <img src="https://codeproject.github.io/codeproject.ai/img/macos.svg" title="macOS" style="width:72px">  | <img src="https://codeproject.github.io/codeproject.ai/img/apple-silicon.svg" title="Apple Silicon" style="width:64px"> | <img src="https://codeproject.github.io/codeproject.ai/img/Ubuntu.svg" title="Ubuntu" style="width:64px">  | <img src="https://codeproject.github.io/codeproject.ai/img/RaspberryPi64.svg" title="Raspberry Pi arm64" style="width:64px"> | <img src="https://codeproject.github.io/codeproject.ai/img/docker.svg" title="Docker" style="width:64px">  |  <img src="https://codeproject.github.io/codeproject.ai/img/VisualStudio.svg" title="Visual Studio" style="width:64px">         |         <img src="https://codeproject.github.io/codeproject.ai/img/VisualStudioCode.svg" title="Visual Studio Code" style="width:64px">        |
| :------: |  :---: | :---------: | :-----: | :----: | :----: | :--------------------: | :-------------------: |
| Windows  | macOS  | macOS arm64 |  Ubuntu / Debian | Raspberry&nbsp;Pi arm64 |  Docker | Visual Studio<br>2019+ | Visual Studio<br>Code |

</div>


# Why

1. AI programming is something every single developer should be aware of. We wanted a fun project we could use to help teach developers and get them involved in AI. We'll be using CodeProject.AI as a focus for articles and exploration to make it fun and painless to learn AI programming.

3. We got sick of fighting versions and libraries and models and being blocked by tiny annoying things every step of the way. So we put put this together so we could save you the frustration. We'll take care of the housekeeping, you focus on the code.
  
2. We also got sick of needing to sign up to potentially expensive services for AI functionality. This  is something we need, and by sharing maybe you can use it too, and hopefully add your own modules and improvements along the way.

## Cut to the chase: how do I play with it?

### 1: Running and playing with the features

1. [**Download the latest version**](https://codeproject.github.io/codeproject.ai/latest.html), install, and launch the shortcut to the server's dashboard on your desktop.
2. On the dashboard, top and centre, is a link to the CodeProject.AI Explorer. Open that and play!

### 2: Running and debugging the code

1. Clone the CodeProject.AI-Server repository.
2. Make sure you have Visual Studio Code or Visual Studio 2019+ installed.
3. Run the setup script in /devops/install
4. Optionally pull all CodeProject.AI Modules by running the clone_repos script in /devops/install
5. Debug the front-end server application (see notes below, but it's easy)


## How do I use it in my application?

Here's an example of using the API for scene detection using a simple JavaScript call:

```html
<html>
<body>
Detect the scene in this file: <input id="image" type="file" />
<input type="button" value="Detect Scene" onclick="detectScene(image)" />

<script>
function detectScene(fileChooser) {
    var formData = new FormData();
    formData.append('image', fileChooser.files[0]);

    fetch('http://localhost:32168/v1/vision/detect/scene', {
        method: "POST",
        body: formData
    })
    .then(response => {
        if (response.ok) response.json().then(data => {
            console.log(`Scene is ${data.label}, ${data.confidence} confidence`)
        });
    });
}
</script>
</body>
</html>
```

You can include the CodeProject.AI installer (or just a link to the latest version of the installer) in your own apps and installers and voila, you have an AI enabled app.


## What does it include?

CodeProject.AI includes

1. **A HTTP REST API Server.** The server listens for requests from other apps, passes them to the backend analysis services for processing, and then passes the results back to the caller. It runs as a simple self contained web service on your device.
2. **Backend Analysis services**.  The brains of the operation is in the analysis services sitting behind the front end API. All processing of data is done on the current machine. No calls to the cloud and no data leaving the device.
3. **The Source Code**, naturally.

## What can it do?

It can run any AI module your imagination and patience can create. Current modules include

- Generative AI: LLMs for text generation, Text-to-image, and multi-modal LLMs (eg "tell me what's in this picture")
- Object Detection in images, including using custom models
- Faces detection and recognition images
- Scene recognition represented in an image
- Remove a background from an image
- Blur a background from an image
- Enhance the resolution of an image
- Pull out the most important sentences in text to generate a text summary
- Prove sentiment analysis on text
- Sound Classification

We will be constantly expanding the feature list.

## Our Goals

1. **To promote AI development** and inspire the AI developer community to dive in and have a go. AI is here, it's in demand, and it's a huge paradigm change in the industry. Whether you like AI or not, developers owe it to themselves to experiment in and familiarise themselves with the  technology. This is CodeProject.AI: a demonstration, an explorer, a learning tool, and a library and service that can be used out of the box.
2. **To make AI development *easy***. It's not that AI development is that hard. It's that there are so, so many options. Our architecture is designed to allow any AI implementation to find a home in our system, and for our service to be callable from any language.
3. **To focus on core use-cases**. We're deliberately not a solution for everyone. Instead we're a solution for common day-to-day needs. We will be adding dozens of modules and scores of AI capabilities to our system, but our goal is always clarity and simplicity over a 100% solution.
4. **To tap the expertise of the Developer Community**. We're not experts but we know a developer or two out there who are. The true power of CodeProject.AI comes from the contributions and improvements from our AI community.


#### Supported Development Environments

This current release works best with Visual Studio Code on Windows 10+. Ubuntu 22.04+, Debian and macOS (both Intel and Apple Silicon). Visual Studio 2019+ support is included for Windows 10+.

The current release provides support for CPU on each platform, DirectML on Windows, CUDA on Windows and Linux, support for Apple Silicon GPUs, RockChip NPUs and Coral.AI TPUs. Support depends on the module itself.


## How to Guides

 - [Installing CodeProject.AI on Windows](docs/Installing-on-Windows-11.md). For those who have CodeProject.AI integrated with Home Assist or Blue Iris
 - [Setting up the development environment](https://codeproject.github.io/codeproject.ai/devguide/install_dev.html) (spoiler: it's easy!)
 - [Running in Docker](https://codeproject.github.io/codeproject.ai/install/running_in_docker.html)
 - Setup or install issues? See the [FAQs](https://codeproject.github.io/codeproject.ai/faq/index.html)

I'll add this to the docs:

## Latest Version changes: 2.9

- Updated to .NET 9
- Support for Ubuntu 24.10
- Improved CUDA 12 support
- Improvements to CUDA support in Windows and Linux
- Further Windows arm64 fixes
- Further macOS arm64 fixes
- General dev environment setup fixes
- Fixes for Windows installer when wget is missing

