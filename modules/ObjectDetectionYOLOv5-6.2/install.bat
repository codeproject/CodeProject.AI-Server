:: Installation script :::::::::::::::::::::::::::::::::::::::::::::::::::::::::
::
::                           Object Detection (YOLOv5 6.2)
::
:: This script is only called from ..\..\src\setup.bat 
::
:: For help with install scripts, notes on variables and methods available, tips,
:: and explanations, see /modules/install_script_help.md

@if "%1" NEQ "install" (
    echo This script is only called from ..\..\src\setup.bat
    @pause
    @goto:eof
)

REM Backwards compatibility with Server 2.6.5
if "!utilsScript!" == "" if "!sdkScriptsDirPath!" NEQ "" set utilsScript=%sdkScriptsDirPath%\utils.bat

:: Download the YOLO models and custom models and store in /assets
call "%utilsScript%" GetFromServer "models/" "models-yolo5-pt.zip"        "assets" "Downloading Standard YOLO models..."
call "%utilsScript%" GetFromServer "models/" "custom-models-yolo5-pt.zip" "custom-models" "Downloading Custom YOLO models..."

REM TODO: Check assets created and has files
REM set moduleInstallErrors=...
