:: Installation script :::::::::::::::::::::::::::::::::::::::::::::::::::::::::
::
::                           Object Detection (YOLOv8)
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

:: Download the models from CodeProject's models folder and store in /assets
call "%sdkScriptsDirPath%\utils.bat" GetFromServer "models/" "models-yolo8-pt.zip"        "assets" "Downloading Standard YOLO models..."

REM TODO: Check assets created and has files
REM set moduleInstallErrors=...
