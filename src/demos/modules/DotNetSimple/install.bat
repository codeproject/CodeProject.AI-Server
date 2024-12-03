:: Installation script :::::::::::::::::::::::::::::::::::::::::::::::::::::::::
::
::                           .NET Simple Demo
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
    REM Often we just pull down the pre-compiled binaries from the CDN when in
    REM production. This saves having to install the .NET SDK. This is a demo so
    REM do nothing here.
    call "!utilsScript!" WriteLine "Production install not supported" "!color_info!"
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
