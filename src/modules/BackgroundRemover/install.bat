:: Installation script :::::::::::::::::::::::::::::::::::::::::::::::::::::::::
::
::                           Background Remover
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

:: Location of models as per original repo
:: u2netp:          https://drive.google.com/uc?id=1tNuFmLv0TSNDjYIkjEdeH1IWKQdUA4HR
:: u2net:           https://drive.google.com/uc?id=1tCU5MM1LhRgGou5OpmpjBQbSrYIUoYab
:: u2net_human_seg: https://drive.google.com/uc?id=1ZfqwVxu-1XWC1xU1GHIP-FM_Knd_AX5j
:: u2net_cloth_seg: https://drive.google.com/uc?id=15rKbQSXQzrKCQurUjZFg8HqzZad8bcyz

:: Download the models and store in /models
call "%sdkScriptsDirPath%\utils.bat" GetFromServer "models/" "rembg-models.zip" "models" "Downloading Background Remover models..."

REM TODO: Check models created and has files
REM set moduleInstallErrors=...
