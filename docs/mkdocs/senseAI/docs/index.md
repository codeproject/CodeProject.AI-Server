---
title: CodeProject SenseAI Server
---

<span style='float:right;margin-top:-70px'>
![Windows](img/windows.svg){width="30" .middle} &nbsp;
![macOS](img/macos.svg){width="30" .middle} &nbsp;
![Ubuntu](img/Ubuntu.svg){width="30" .middle} &nbsp;
![Docker](img/docker.svg){width="30" .middle} &nbsp;
![Visual Studio](img/VisualStudio.svg){width="30" .middle} &nbsp;
![Visual Studio Code](img/VisualStudioCode.svg){width="30" .middle} 
</span>

<center>
<br>
![Sense A I](img/senseAI.svg){width="400"}
<br>
</center>
<br>
<center>

[Download Windows Installer](https://www.codeproject.com/ai/sense/latest.aspx){ .md-button .md-button--primary .tight } &nbsp;
[API Docs](./api/api_reference.md){ .md-button .tight } &nbsp;
[GitHub](https://github.com/codeproject/CodeProject.SenseAI){ .md-button .tight } &nbsp;
[Discussions](https://www.codeproject.com/Feature/SenseAI-Discussions.aspx){ .md-button .md-outline-button--primary .tight } 

</center>


CodeProject senseAI Server is a standalone, self-hosted, fast, free and Open Source Artificial Intelligence microserver for any 
platform, any language. It can be installed locally, required no off-device or out of network data
transfer, and is easy to use.

![Object detection](img/DetectThings.png "Object detection example")

**AI programming is something every single developer should be aware of**. We wanted a fun project we could use to help teach developers and get them involved in AI. We'll be using SenseAI as a focus for articles and exploration to make it fun and painless to learn AI programming.

**We got sick of fighting versions and libraries and models** and being blocked by tiny annoying things every step of the way. So we put put this together so we could save you the frustation. We'll take care of the housekeeping, you focus on the code.
  
**We don't always want our personal data in the cloud**. It should always be your choice as to who
sees your data. 

<big>**Help us make this something amazing!**</big>


## Cut to the chase: how do I play with it?

#### 1: Running and playing with the features

1. [**Download the latest version**](https://www.codeproject.com/ai/sense/latest.aspx), install, and launch the shortcut to the server's dashboard on your desktop.
2. On the dashboard, at the bottom, is a link to the demo playground. Open that and play!

#### 2: Running and debugging the code

Read our [quick guide](./devguide/install_dev.md) to setting up and running SenseAI in Visual Studio Code or Visual Studio.

## How do I use it in my application?

Here's an example of using the API for scene detection using a simple JavaScript call:

``` html
<html>
<body>
Detect the scene in this file: <input id="image" type="file" />
<input type="button" value="Detect Scene" onclick="detectScene(image)" />

<script>
function detectScene(fileChooser) {
    var formData = new FormData();
    formData.append('image', fileChooser.files[0]);

    fetch('http://localhost:5000/v1/vision/detect/scene', {
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

You can include the CodeProject SenseAI installer (or just a link to the latest version of the installer) in your own apps and installers and voila, you have an AI enabled app.


## What does it include?

CodeProject SenseAI includes

1. **A HTTP REST API Server.** The server listens for requests from other apps, passes them to the backend analysis services for processing, and then passes the results back to the caller. It runs as a simple self contained web service on your device.
2. **Backend Analysis services**.  The brains of the operation is in the analysis services sitting behind the front end API. All processing of data is done on the current machine. No calls to the cloud and no data leaving the device.
3. **The Source Code**, naturally.

## What can it do?

CodeProject SenseAI can currently

- Detect objects in images
- Detect faces in images
- Detect the type of scene represented in an image
- Recognise faces that have been registered with the service
- Pull out the most important sentences in text to generate a text summary
- Remove the background automatically from images
- Blur the background of images to produce a portrait effect
- Generate a sentiment score for text

We will be constantly expanding the feature list.

## Our Goals

1. **To promote AI development** and inspire the AI developer community to dive in and have a go. AI is here, it's in demand, and it's a huge paradigm change in the industry. Whether you like AI or not, developers owe it to themselves to experiment in and familiarise themselves with the  technology. This is CodeProject SenseAI: a demonstration, a playground, a learning tool, and a library and service that can be used out of the box.
2. **To make AI development *easy***. It's not that AI development is that hard. It's that there are so, so many options. Our architecture is designed to allow any AI implementation to find a home in our system, and for our service to be callable from any language.
3. **To focus on core use-cases**. We're deliberately not a solution for everyone. Instead we're a solution for common day-to-day needs. We will be adding dozens of modules and scores of AI capabilities to our system, but our goal is always clarity and simplicity over a 100% solution.
4. **To tap the expertise of the Developer Community**. We're not experts but we know a developer or two out there who are. The true power of CodeProject SenseAI comes from the contributions and improvements from our AI community.

## Roadmap

The following features will be added over the coming weeks and months

1. More modules and a streamlined plugin architecture
2. A GUI management system
3. GPU support
4. More analysis services
