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

call "%sdkScriptsPath%\utils.bat" SetupPython 3.9 "Shared"
if errorlevel 1 exit /b 1

call "%sdkScriptsPath%\utils.bat" InstallPythonPackages 3.9 "%modulePath%\Python" "Shared"
if errorlevel 1 exit /b 1
