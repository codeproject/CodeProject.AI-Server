:: Installation script :::::::::::::::::::::::::::::::::::::::::::::::::::::::::
::
::                         ObjectDetection (Coral)
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

REM Do this in requirements file. Otherwise we need the TFLite wheel too
REM if "%pythonVersion%" == "3.7" (
REM     set "wheel=https://github.com/google-coral/pycoral/releases/download/v2.0.0/pycoral-2.0.0-cp37-cp37m-win_amd64.whl"
REM ) else if "%pythonVersion%" == "3.8" (
REM     set "wheel=https://github.com/google-coral/pycoral/releases/download/v2.0.0/pycoral-2.0.0-cp38-cp38-win_amd64.whl"
REM ) else if "%pythonVersion%" == "3.9" (
REM     set "wheel=https://github.com/google-coral/pycoral/releases/download/v2.0.0/pycoral-2.0.0-cp39-cp39-win_amd64.whl"
REM )
REM call "!sdkScriptsDirPath!\utils.bat" InstallPythonPackagesByName %wheel% "PyCoral API" 

:: Install supporting Libraries
if not exist edgetpu_runtime (
    REM edgetpu_runtime_20221024 is badly zipped. edgetpu_runtime-20221024 is better
    REM call "%sdkScriptsDirPath%\utils.bat" GetFromServer "libraries/" "edgetpu_runtime_20221024.zip" "." "Downloading edge TPU runtime..."
    call "%sdkScriptsDirPath%\utils.bat" GetFromServer "libraries/" "edgetpu_runtime-20221024.zip" "edgetpu_runtime" "Downloading edge TPU runtime..."
)
if exist edgetpu_runtime (

    net session > NUL
    IF %ERRORLEVEL% EQU 0 (
        call "!sdkScriptsDirPath!\utils.bat" WriteLine "Installing the edge TPU libraries..." "!color_info!"
        pushd edgetpu_runtime
        call install.bat
        popd

        REM TODO: Check the edge TPU install worked
        REM set moduleInstallErrors=...
    ) else (
        call "!sdkScriptsDirPath!\utils.bat" WriteLine "=================================================================================" "!color_info!"
        call "!sdkScriptsDirPath!\utils.bat" WriteLine "Please run !moduleDirPath!\edgetpu_runtime\install.bat to complete this process." "!color_info!"
        call "!sdkScriptsDirPath!\utils.bat" WriteLine "=================================================================================" "!color_info!"
    )
)

:: Download the TFLite/edgeTPU models and store in /assets
:: call "%sdkScriptsDirPath%\utils.bat" GetFromServer "models/" "objectdetect-coral-multitpu-models.zip" "assets" "Downloading MobileNet models..."

call "%sdkScriptsDirPath%\utils.bat" GetFromServer "models/" "objectdetection-efficientdet-large-edgetpu.zip" "assets" "Downloading EfficientDet (large) models..."
call "%sdkScriptsDirPath%\utils.bat" GetFromServer "models/" "objectdetection-efficientdet-medium-edgetpu.zip" "assets" "Downloading EfficientDet (medium) models..."
call "%sdkScriptsDirPath%\utils.bat" GetFromServer "models/" "objectdetection-efficientdet-small-edgetpu.zip" "assets" "Downloading EfficientDet (small) models..."
call "%sdkScriptsDirPath%\utils.bat" GetFromServer "models/" "objectdetection-efficientdet-tiny-edgetpu.zip" "assets" "Downloading EfficientDet (tiny) models..."

call "%sdkScriptsDirPath%\utils.bat" GetFromServer "models/" "objectdetection-mobilenet-large-edgetpu.zip" "assets" "Downloading MobileNet (large) models..."
call "%sdkScriptsDirPath%\utils.bat" GetFromServer "models/" "objectdetection-mobilenet-medium-edgetpu.zip" "assets" "Downloading MobileNet (medium) models..."
call "%sdkScriptsDirPath%\utils.bat" GetFromServer "models/" "objectdetection-mobilenet-small-edgetpu.zip" "assets" "Downloading MobileNet (small) models..."
call "%sdkScriptsDirPath%\utils.bat" GetFromServer "models/" "objectdetection-mobilenet-tiny-edgetpu.zip" "assets" "Downloading MobileNet (tiny) models..."

:: call "%sdkScriptsDirPath%\utils.bat" GetFromServer "models/" "objectdetection-yolov5-large-edgetpu.zip" "assets" "Downloading YOLOv5 (large) models..."
:: call "%sdkScriptsDirPath%\utils.bat" GetFromServer "models/" "objectdetection-yolov5-medium-edgetpu.zip" "assets" "Downloading YOLOv5 (medium) models..."
:: call "%sdkScriptsDirPath%\utils.bat" GetFromServer "models/" "objectdetection-yolov5-small-edgetpu.zip" "assets" "Downloading YOLOv5 (small) models..."
:: call "%sdkScriptsDirPath%\utils.bat" GetFromServer "models/" "objectdetection-yolov5-tiny-edgetpu.zip" "assets" "Downloading YOLOv5 (tiny) models..."

call "%sdkScriptsDirPath%\utils.bat" GetFromServer "models/" "objectdetection-yolov8-large-edgetpu.zip" "assets" "Downloading YOLOv8 (large) models..."
call "%sdkScriptsDirPath%\utils.bat" GetFromServer "models/" "objectdetection-yolov8-medium-edgetpu.zip" "assets" "Downloading YOLOv8 (medium) models..."
call "%sdkScriptsDirPath%\utils.bat" GetFromServer "models/" "objectdetection-yolov8-small-edgetpu.zip" "assets" "Downloading YOLOv8 (small) models..."
call "%sdkScriptsDirPath%\utils.bat" GetFromServer "models/" "objectdetection-yolov8-tiny-edgetpu.zip" "assets" "Downloading YOLOv8 (tiny) models..."

REM TODO: Check assets created and has files
REM set moduleInstallErrors=...
