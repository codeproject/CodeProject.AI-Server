[![made-for-VSCode](https://img.shields.io/badge/Made%20for-VSCode-1f425f.svg)](https://open.vscode.dev/codeproject/CodeProject.AI-Server/) [![made-with-python](https://img.shields.io/badge/Made%20with-Python-orange)](https://www.python.org/) [![GitHub license](https://img.shields.io/badge/license-SSPL-green)](https://www.mongodb.com/licensing/server-side-public-license) [![Open Source Love svg2](https://badges.frapsoft.com/os/v2/open-source.svg?v=103)](https://github.com/ellerbrock/open-source-badges/)

# CodeProject.AI Server

 [**Download the latest version**](https://www.codeproject.com/ai/latest.aspx)

A standalone, self-hosted, fast, free and Open Source Artificial Intelligence microserver for any 
platform, any language. It can be installed locally, required no off-device or out of network data
transfer, and is easy to use.

![Object detection](https://www.codeproject.com/ai/docs/img/DetectThings.png)

# Supported Platforms

<div style="width:75%;min-width:700px;margin:30px auto">

| <img src="https://www.codeproject.com/ai/docs/img/windows.svg" title="Windows" style="width:64px">  | <img src="https://www.codeproject.com/ai/docs/img/macos.svg" title="macOS" style="width:72px">  | <img src="https://www.codeproject.com/ai/docs/img/apple-silicon.svg" title="Apple Silicon" style="width:64px"> | <img src="https://www.codeproject.com/ai/docs/img/Ubuntu.svg" title="Ubuntu" style="width:64px">  | <img src="https://www.codeproject.com/ai/docs/img/RaspberryPi64.svg" title="Raspberry Pi arm64" style="width:64px"> | <img src="https://www.codeproject.com/ai/docs/img/docker.svg" title="Docker" style="width:64px">  |  <img src="https://www.codeproject.com/ai/docs/img/VisualStudio.svg" title="Visual Studio" style="width:64px">         |         <img src="https://www.codeproject.com/ai/docs/img/VisualStudioCode.svg" title="Visual Studio Code" style="width:64px">        |
| :------: |  :---: | :---------: | :-----: | :----: | :----: | :--------------------: | :-------------------: |
| Windows  | macOS  | macOS arm64 |  Ubuntu | Raspberry&nbsp;Pi arm64 |  Docker | Visual Studio<br>2019+ | Visual Studio<br>Code |

</div>


# Why

1. AI programming is something every single developer should be aware of. We wanted a fun project we could use to help teach developers and get them involved in AI. We'll be using CodeProject.AI as a focus for articles and exploration to make it fun and painless to learn AI programming.

3. We got sick of fighting versions and libraries and models and being blocked by tiny annoying things every step of the way. So we put put this together so we could save you the frustation. We'll take care of the housekeeping, you focus on the code.
  
2. We also got sick of needing to sign up to potentially expensive services for AI functionality. This  is something we need, and by sharing maybe you can use it too, and hopefully add your own modules and improvements along the way.

## Cut to the chase: how do I play with it?

### 1: Running and playing with the features

1. [**Download the latest version**](https://www.codeproject.com/ai/latest.aspx), install, and launch the shortcut to the server's dashboard on your desktop.
2. On the dashboard, top and centre, is a link to the CodeProject.AI Explorer. Open that and play!

### 2: Running and debugging the code

1. Clone the CodeProject.AI repository.
2. Make sure you have Visual Studio Code or Visual Studio 2019+ installed.
3. Run the setup script in /Installers/Dev
4. Debug the front-end server application (see notes below, but it's easy)


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

It can detect stuff!

CodeProject.AI can currently

- Detect objects in images, including using custom models
- Detect faces in images
- Detect the type of scene represented in an image
- Recognise faces that have been registered with the service
- Remove a background from an image
- Blur a background from an image
- Enhance the resolution of an image
- Pull out the most important sentences in text to generate a text summary
- Prove sentiment analysis on text

We will be constantly expanding the feature list.

## Our Goals

1. **To promote AI development** and inspire the AI developer community to dive in and have a go. AI is here, it's in demand, and it's a huge paradigm change in the industry. Whether you like AI or not, developers owe it to themselves to experiment in and familiarise themselves with the  technology. This is CodeProject.AI: a demonstration, an explorer, a learning tool, and a library and service that can be used out of the box.
2. **To make AI development *easy***. It's not that AI development is that hard. It's that there are so, so many options. Our architecture is designed to allow any AI implementation to find a home in our system, and for our service to be callable from any language.
3. **To focus on core use-cases**. We're deliberately not a solution for everyone. Instead we're a solution for common day-to-day needs. We will be adding dozens of modules and scores of AI capabilities to our system, but our goal is always clarity and simplicity over a 100% solution.
4. **To tap the expertise of the Developer Community**. We're not experts but we know a developer or two out there who are. The true power of CodeProject.AI comes from the contributions and improvements from our AI community.


#### Supported Development Environments

This current release works in Visual Studio 2019+ on Windows 10+, and Visual Studio Code on Windows 10+. Ubuntu and macOS (both Intel and Apple Silicon). 

The current release supports CPU on each platform, as well as nVidia CUDA GPUs on Windows. Future releases will expand GPU support to Docker and other cards.


## How to Guides

 - [Installing CodeProject.AI on your machine](https://www.codeproject.com/ai/docs/why/install_on_windows.html). For those who have CodeProject.AI integrated with Home Assist or Blue Iris
 - [Setting up the development environment](https://www.codeproject.com/ai/docs/devguide/install_dev.html) (spoiler: it's easy!)
 - [Running in Docker](https://www.codeproject.com/ai/docs/why/running_in_docker.html)
 - Setup or install issues? See [Common Errors](https://www.codeproject.com/ai/docs/devguide/common_errors.html)

