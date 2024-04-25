:: Development mode setup script ::::::::::::::::::::::::::::::::::::::::::::::
::
::                           CodeProject.AI Demos
::
:: This script is only called from ..\..\setup.bat
::
:: For help with install scripts, notes on variables and methods available, tips,
:: and explanations, see /src/modules/install_script_help.md

@if "%1" NEQ "install" (
    echo This script is only called from ..\src\setup.bat
    @pause
    @goto:eof
)

:: set verbosity=info
set runtimeLocation=Shared
set pythonVersion=3.9

REM Can't do this
REM call :SetupPythonPaths "%runtimeLocation%" %pythonVersion%

REM This is a copy and paste ===================================================

REM Name based on version (eg version is 3.8, name is then python38)
set pythonName=python!pythonVersion:.=!

REM The path to the python installation, either local or shared. The
REM virtual environment will live in here
if /i "!runtimeLocation!" == "Local" (
    set pythonDirPath=!moduleDirPath!\bin\!os!\!pythonName!
) else (
    set pythonDirPath=!runtimesDirPath!\bin\!os!\!pythonName!
)
set virtualEnvDirPath=!pythonDirPath!\venv

REM The path to the python intepreter for this venv
set venvPythonCmdPath=!virtualEnvDirPath!\Scripts\python.exe

REM The location where python packages will be installed for this venvvenv
set packagesDirPath=%virtualEnvDirPath%\Lib\site-packages

REM ============================================================================


:: the Python Demo is in <root>\src\demos\clients\Python
call "%sdkScriptsDirPath%\utils.bat" SetupPython
call "%sdkScriptsDirPath%\utils.bat" InstallRequiredPythonPackages "%moduleDirPath%\Python"
