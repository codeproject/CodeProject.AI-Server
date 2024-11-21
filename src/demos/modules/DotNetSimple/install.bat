:: Installation script :::::::::::::::::::::::::::::::::::::::::::::::::::::::::
::
::                           .NET YOLO Object Detection
::
:: This script is only called from ..\..\..\setup.bat 
::
:: For help with install scripts, notes on variables and methods available, tips,
:: and explanations, see /src/modules/install_script_help.md

@if "%1" NEQ "install" (
    echo This script is only called from ..\..\..\setup.bat
    @pause
    @goto:eof
) 
if /i "!executionEnvironment!" == "Production" (
    call "!utilsScript!" WriteLine "No custom setup steps for this module." "!color_info!"
) else (
    :: If we're in dev-setup mode we'll build the module now so the self-test will work
    pushd "!moduleDirPath!"
    call "!utilsScript!" WriteLine "Building project..." "!color_info!"
    if /i "%verbosity%" neq "quiet" (
        dotnet build -c Debug -o "!moduleDirPath!/bin/Debug/!dotNetTarget!"
    ) else (
        dotnet build -c Debug -o "!moduleDirPath!/bin/Debug/!dotNetTarget!" >NUL
    )
    popd
)

:: Download the YOLO models and store in /assets
call "%utilsScript%" GetFromServer "models/" "objectdetection-coco-yolov8-onnx-m.zip" "assets" "Downloading YOLO ONNX models..."

REM TODO: Check assets created and has files
REM set moduleInstallErrors=...
