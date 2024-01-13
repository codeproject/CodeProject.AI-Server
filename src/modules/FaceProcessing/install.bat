:: Installation script :::::::::::::::::::::::::::::::::::::::::::::::::::::::::
::
::                           FaceProcessing
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

:: Download the YOLO models and store in /assets
call "%sdkScriptsDirPath%\utils.bat" GetFromServer "models/" "models-face-pt.zip" "assets" "Downloading Face models..."

:: Persisted data storage dir for dev mode
if not exist "%moduleDirPath%\datastore\" mkdir "%moduleDirPath%\datastore"

REM TODO: Check assets created and has files
REM set moduleInstallErrors=...
