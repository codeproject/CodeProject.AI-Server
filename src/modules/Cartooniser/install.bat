:: Installation script :::::::::::::::::::::::::::::::::::::::::::::::::::::::::
::
::                           Cartooniser
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

:: Download the models and store in /models
call "%sdkScriptsDirPath%\utils.bat" GetFromServer "models/" "cartooniser-models.zip" "weights" "Downloading Cartooniser models..."

REM TODO: Check weights created and has files
REM set moduleInstallErrors=...
