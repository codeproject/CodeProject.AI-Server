:: Development mode setup script ::::::::::::::::::::::::::::::::::::::::::::::
::
::                           CodeProject.AI Demos
::
:: This script is only called from ..\src\setup.bat

@if "%1" NEQ "install" (
    echo This script is only called from ..\src\setup.bat
    @pause
    @goto:eof
)

set pythonLocation=Shared
set pythonVersion=3.9

call "%sdkScriptsPath%\utils.bat" SetupPython
call "%sdkScriptsPath%\utils.bat" InstallPythonPackages "%modulePath%\Python"

