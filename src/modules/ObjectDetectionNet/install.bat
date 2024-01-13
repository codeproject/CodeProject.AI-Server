:: Installation script :::::::::::::::::::::::::::::::::::::::::::::::::::::::::
::
::                           .NET YOLO Object Detection
::
:: This script is only called from ..\..\setup.bat 
::
:: For help with install scripts, notes on variables and methods available, tips,
:: and explanations, see /src/modules/install_script_help.md

@if "%1" NEQ "install" (
    echo This script is only called from ..\..\setup.bat
    @pause
    @goto:eof
) 

:: Pull down the correct .NET image of ObjectDetectionNet based on this OS / GPU combo
if /i "!executionEnvironment!" == "Production" (
    set imageName=ObjectDetectionNet-CPU-!moduleVersion!.zip
    if /i "!installGPU!" == "true" (
        set imageName=ObjectDetectionNet-DirectML-!moduleVersion!.zip

        REM We can use CUDA on Windows but DirectML has proven to be faster
        REM if /i "!hasCUDA!" == "true" set imageName=ObjectDetectionNet-CUDA-!moduleVersion!.zip
    )

    call "%sdkScriptsDirPath%\utils.bat" GetFromServer "!imageName!" "" "Downloading !imageName!..."
) else (
    call "%sdkScriptsDirPath%\utils.bat" GetFromServer "ObjectDetectionNetNugets.zip" "LocalNugets" "Downloading Nuget packages..."
)

:: Download the YOLO models and store in /assets
call "%sdkScriptsDirPath%\utils.bat" GetFromServer "yolonet-models.zip" "assets" "Downloading YOLO ONNX models..."
call "%sdkScriptsDirPath%\utils.bat" GetFromServer "yolonet-custom-models.zip" "custom-models" "Downloading Custom YOLO ONNX models..."

REM TODO: Check assets created and has files
REM set moduleInstallErrors=...
