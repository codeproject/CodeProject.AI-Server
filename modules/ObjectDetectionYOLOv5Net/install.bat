:: Installation script :::::::::::::::::::::::::::::::::::::::::::::::::::::::::
::
::                           .NET YOLO Object Detection
::
:: This script is only called from ..\..\src\setup.bat 
::
:: For help with install scripts, notes on variables and methods available, tips,
:: and explanations, see /src/modules/install_script_help.md

@if "%1" NEQ "install" (
    echo This script is only called from ..\..\src\setup.bat
    @pause
    @goto:eof
) 

REM Backwards compatibility with Server 2.6.5
if "!utilsScript!" == "" if "!sdkScriptsDirPath!" NEQ "" set utilsScript=%sdkScriptsDirPath%\utils.bat

set installBinaries=false
if /i "!executionEnvironment!" == "Production" set installBinaries=true
if /i "!launchedBy!" == "server" set installBinaries=true

:: Pull down the correct .NET image of ObjectDetectionYOLOv5Net based on this OS / GPU combo
if /i "!installBinaries!" == "true" (
    set imageName=ObjectDetectionYOLOv5Net-CPU-!moduleVersion!.zip
    if /i "!installGPU!" == "true" (
        set imageName=ObjectDetectionYOLOv5Net-DirectML-!moduleVersion!.zip

        REM We can use CUDA on Windows but DirectML has proven to be faster
        REM if /i "!hasCUDA!" == "true" set imageName=ObjectDetectionYOLOv5Net-CUDA-!moduleVersion!.zip
    )

    call "%utilsScript%" GetFromServer "binaries/" "!imageName!" "bin" "Downloading !imageName!..."
) else (
    call "%utilsScript%" GetFromServer "libraries/" "ObjectDetectionYOLOv5NetNugets.zip" "LocalNugets" "Downloading Nuget packages..."

    :: If we're in dev-setup mode we'll build the module now so the self-test will work
    pushd "!moduleDirPath!"
    call "!utilsScript!" WriteLine "Building project..." "!color_info!"
    dotnet build -c Debug -o "!moduleDirPath!/bin/Debug/!dotNetTarget!" >NUL
    popd
)

:: Download the YOLO models and store in /assets
call "%utilsScript%" GetFromServer "models/" "yolonet-models.zip"        "assets" "Downloading YOLO ONNX models..."
call "%utilsScript%" GetFromServer "models/" "yolonet-custom-models.zip" "custom-models" "Downloading Custom YOLO ONNX models..."

REM TODO: Check assets created and has files
REM set moduleInstallErrors=...
