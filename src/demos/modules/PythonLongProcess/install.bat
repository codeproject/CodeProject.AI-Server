:: Installation script :::::::::::::::::::::::::::::::::::::::::::::::::::::::::
::
::                           PythonLongProcess
::
:: This script is only called using ..\..\..\src\setup.bat
::
:: For help with install scripts, notes on variables and methods available, tips,
:: and explanations, see /src/modules/install_script_help.md

@if "%1" NEQ "install" (
    echo This script is only called from ..\..\..\src\setup.bat
    @pause
    @goto:eof
)

REM Nothing to do here...

REM set moduleInstallErrors=
