[![Open in Visual Studio Code](https://open.vscode.dev/badges/open-in-vscode.svg)](https://open.vscode.dev/codeproject/CodeProject.SenseAI) [![made-with-python](https://img.shields.io/badge/Made%20with-Python-orange)](https://www.python.org/) [![GitHub license](https://img.shields.io/badge/license-SSPL-green)](https://www.mongodb.com/licensing/server-side-public-license) [![Open Source Love svg2](https://badges.frapsoft.com/os/v2/open-source.svg?v=103)](https://github.com/ellerbrock/open-source-badges/)

# CodeProject SenseAI Server

Standalone, self-hosted, fast, free and Open Source Artificial Intelligence microserver for any platform, any language.

## What is CodeProject SenseAI Server?

CodeProject SenseAI is a self contained server that allows other applications to easily include AI processing as part of their service. CodeProject SenseAI is a simple HTTP based REST service that is fully self contained, installed locally, and requires no off-device processing.

Here's a sample of the Scene Detection API

```html
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

We will be constantly expanding the feature list.

## Why CodeProject SenseAI

We want AI development to be something every developer can access, and we want integrating AI into applications to be straightforward.

AI development involves multiple formats, training techniques, large amounts of data, inscrutible choices and often expensive cloud services. We don't want that. The AI that we include in our applications should be

- **Completely self contained**. No sign-in, no reliance on Cloud servers, nothing to download at runtime. Everything you need already in place
- **Safe and secure**. Data processed as part of the application logic should never leave the local environment. It should not be parsed, read, inspected or stored by anyone else other than you.
- **Always open**. CodeProject is based on Open Source development and so CodeProject SenseAI will also be Open Source.
- **Cross Platform** We don't often have the luxury of picking a single platform anymore. There needs to be support for Windows, Linux, macOS, and Rasperry Pi to name a few.
- **Cross Architecture** CPU and GPU support, of course
- **Fast and lightweight** Necessary for a self-contained distributable system
- **Extendable**. AI is expanding so rapidly, and there are so many wonderful solutions popping up daaily. Adding new capabilities must be as simple as dropping in the code and registering the new capabilities with the front end server
  
## Our Goals

1. **To promote AI development** and inspire the AI developer community to dive in and have a go. AI is here, it's in demand, and it's a huge paradigm change in the industry. Whether you like AI or not, developers owe it to themselves to experiment in and familiarise themselves with the  technology. This is CodeProject SenseAI: a demonstration, a playground, a learning tool, and a library and service that can be used out of the box.
2. **To make AI development *easy***. It's not that AI development is that hard. It's that there are so, so many options. Our architecture is designed to allow any AI implementation to find a home in our system, and for our service to be callable from any language.
3. **To focus on core use-cases**. We're deliberately not a solution for everyone. Instead we're a solution for common day-to-day needs. We will be adding dozens of modules and scores of AI capabilities to our system, but our goal is always clarity and simplicity over a 100% solution.
4. **To tap the expertise of the Developer Community**. We're not experts but we know a developer or two out there who are. The true power of CodeProject SenseAI comes from the contributions and improvements from our AI community.

## How to download and install CodeProject.SenseAI

#### Supported Environments

This is an Alpha release and so support is constrained solely to Windows 10+ using CPU acceleration. Future releases will include other Operating Systems as well as GPU support.

### Installing CodeProject.SenseAI

To **install CodeProject.SenseAI** as a standalone service ready for integration with applications such as HomeAssist or BlueIris, download the [installation package](https://codeproject-ai.s3.ca-central-1.amazonaws.com/sense/installer/CodeProject.SenseAI.Package.zip).

Unzip the download and double click the <code>Start_SenseAI_Win.bat</code> script. This will start the API server and the backend analysis services. Rerun that script whenever you want to launch the service.

To **explore CodeProject.SenseAI** open the <code>/demos/Javascript/</code> folder and double click on the <code>Vision.html</code> page. The server will, of course, need to be running for this test application to function. Sample images can be found in the <code>TestData</code> folder under the <code>demos</code> folder

### Setting up the development environment

If you wish to debug or make enhancements to the code then you should install:

 1. **Visual Studio Code** or **Visual Studio 2019+**. [VS Code](https://code.visualstudio.com/download) is available on Windows, macOS and Linux. Visual Studio is available on Windows and macOS. We've tested against both, but not against other IDEs at this point
 2. **Python**. Either via the Visual Studio Installer, or [download python directly](https://www.python.org/downloads/)
 3. **.NET 5** Download [.NET 5 here](https://dotnet.microsoft.com/download/dotnet/5.0).

#### If you are using VS Code

You'll need the following extensions

1. [Python extension for Visual Studio Code](https://marketplace.visualstudio.com/items?itemName=ms-python.python)
2. [C# extension for Visual Studio Code](https://marketplace.visualstudio.com/items?itemName=ms-dotnettools.csharp)

To build and run:

1. Clone the [CodeProject.SenseAI repository](https://github.com/codeproject/CodeProject.SenseAI.git) from GitHub
2. run /install/setup_dev_env_win.bat. This will download required assets, setup the Python
   virtual environment and set environment variables.
3. Open the main application folder in VSCode
4. Click the "Run and Debug" button on the left hand tab bar (the arrow)
5. From the dropdown at the top of the window, select CodeProject.SenseAI Playground  
![Choosing a launch profile](docs/images/Choose-launch.png "Choosing a launch profile")
6. CLick the green arrow next to the dropdown

The Demo application (CodeProject.SenseAI Playground) should launch after the code has built and the Python backed fully installed.


#### If you are using Visual Studio

1. Clone the [CodeProject.SenseAI repository](https://github.com/codeproject/CodeProject.SenseAI.git) from GitHub
2. Ensure you have the Python workflow enabled in Visual Studio. While not critical, it does help with debugging.
3. run /install/setup_dev_env_win.bat. This will download required assets, setup the Python virtual environment and set environment variables. **This will take several minutes, so please be patient.**
4. Open the solution in Visual Studio and build the entire solution
5. To run the Server and the demo application in DEBUG you can either
    1. Start both the projects in debug mode by 
        1. In Solution Explorer, open demos / .NET and right-click on CodeProject.SenseAI.Playground and choose Debug -> Start new instance.
        2. In Solution Explorer, open src / API / Server and right-click on FrontEnd and choose Debug -> Start new instance. 
    2. Configure Visual Studio to start multiple projects by
        1. In Solution Explorer, right-click on the solution and select **Set Startup Projects...** and configure Multiple startup projects as shown below.
          ![Set Startup Projects](docs/images/Set-Startup_Projects.png)
    3. Now when you start with or without debugging, both the Server and demo projects with start.  Also, this will be  shown on the toolbar as shown below.  
       ![Mutliple Project Toolbar](docs/images/Mulitple-Project-Toolbar.png)

6. In Solution Explorer, open src / AnalysisLayer. Right click on DeepStack and choose <code>Open Folder in File Explorer</code>. Double click on the start.bat script.  This script will ensure that the Python virtual environment is enabled and environment variables set.  


At this point the Playground application should be indicting it has a connection to the API server, and the servwe should be dispatching requests to the backend Analysis layer.

#### Common Errors

**Server startup failed**

```
System.ComponentModel.Win32Exception (2): The system cannot find the file specified.
   at System.Diagnostics.Process.StartWithCreateProcess(ProcessStartInfo startInfo)
```

Did you run the set_dev_env_win_ script? Was it successful? If not, debug the issues (or start with
a clean install) and try again.

**Port already in use**

If you see:
```
Unable to start Kestrel.
System.IO.IOException: Failed to bind to address http://127.0.0.1:5000: address already in use.
```
Either you have CodeProject.SenseAI already running, or another application is using port 5000. Either shut down any application using port 5000, or change the port CodeProject.SenseAI uses. You can change the external port that CodeProject.SenseAI uses by editing the <code>set_environment.bat</code> file changing the value of the <code>PORT</code> variable. In the demo app there is a Port setting you will need to edit to match the new port.

## Roadmap

The following features will be added over the coming weeks and months

1. An actual installer for end users
2. A GUI management system
3. GPU support
4. More analysis services

The following platforms will be supported soon

- macOS / Linux
- Rasperry Pi

## With Thanks

Our initial motivation, and the source of some of our initial Python modules, was from the wonderful work done on DeepStack. As per the GPL licencing of DeepStack we have included all our updates to the DeepStack code we've used in our CodeProject SenseAI repository.