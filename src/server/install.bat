:: Setup script ::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::
::
::                    CodeProject.AI SDK Setup
::
:: This script is called from the server directory using: 
::
::    ..\setup.bat
::
:: The setup.bat file will find this install.bat file and execute it.
::
:: For help with install scripts, notes on variables and methods available, tips,
:: and explanations, see /src/modules/install_script_help.md

@if "%1" NEQ "install" (
    echo This script is only called from ..\setup.bat
    @pause
    @goto:eof
)

REM Nothing to be done here...

REM set moduleInstallErrors=...
