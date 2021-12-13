#!/bin/sh

## CodeProject SenseAI Analysis services (DeepStack module) startup script for Linux and macOS
##
## Usage:
##   . ./start.sh
##
## We assume we're in the AnalysisLayer/DeepStack directory

clear

set techniColor=true
set envVariablesFile=..\..\..\set_environment.bat

:: Before we start, set the current directory if necessary
if "%1" == "--no-color" set techniColor=false
if not "%2" == "" cd %2

:: ===============================================================================================
:: 0. Basic settings

:: verbosity can be: quiet | info | loud
set verbosity=quiet

if not "%verbosity%" == "quiet" call :WriteLine Yellow "Current Directory: %cd%"

:: ===============================================================================================
:: 1. Load environment variables

call :Write White "Loading installation settings..."
call !envVariablesFile!
call :WriteLine Green "Done"

set VENV_PATH=!APPDIR!\..\venv

if "%verbosity%" == "info" (
    call :WriteLine Yellow "APPDIR    = !APPDIR!"
    call :WriteLine Yellow "PROFILE   = !PROFILE!"
    call :WriteLine Yellow "CUDA_MODE = !CUDA_MODE!"
    call :WriteLine Yellow "DATA_DIR  = !DATA_DIR!"
    call :WriteLine Yellow "TEMP_PATH = !TEMP_PATH!"
    call :WriteLine Yellow "PORT      = !PORT!"
    call :WriteLine Yellow "VISION_FACE      = !VISION_FACE!"
    call :WriteLine Yellow "VISION_DETECTION = !VISION_DETECTION!"
    call :WriteLine Yellow "VISION_SCENE     = !VISION_SCENE!"
)

:: ===============================================================================================
:: 2. Activate Virtual Environment

call :Write White "Enabling our Virtual Environment..."

set VIRTUAL_ENV=!VENV_PATH!\Scripts
if not defined PROMPT set PROMPT=$P$G
set PROMPT=(venv) !PROMPT!
set PYTHONHOME=
set PATH=!VIRTUAL_ENV!;%PATH%

if errorlevel 1 goto errorNoPythonVenv
call :WriteLine Green "Done"

:: Ensure Python Exists
call :Write White "Checking for Python 3.7..."
python --version | find "3.7" > NUL
if errorlevel 1 goto errorNoPython
call :WriteLine Green "present"

if "%verbosity%"=="loud" where Python


:: ===============================================================================================
:: 3. Start back end analysis

call :Write White "Starting Analysis Services..."
if /i "!VISION_DETECTION!"   == "true" (
    START "CodeProject SenseAI Analysis Services" /B python !APPDIR!\%deepstackModule%\detection.py
)
if /i "!VISION_FACE!" == "true" (
    START "CodeProject SenseAI Analysis Services" /B python !APPDIR!\%deepstackModule%\face.py
)
if /i "!VISION_SCENE!"  == "true" (
    START "CodeProject SenseAI Analysis Services" /B python !APPDIR!\%deepstackModule%\scene.py
)
:: To start them all in one fell swoop...
:: START "CodeProject SenseAI Analysis Services" /B python !APPDIR!\%deepstackModule%\runAll.py
call :WriteLine Green "Done"

:: Wait forever. We need these processes to stay alive
pause > NUL

:: and we're done.
goto:eof


:: sub-routines

:WriteLine
SetLocal EnableDelayedExpansion
REM Remember that %%~N means "parameter N with the quotes removed"
if /i "%techniColor%" == "true" (
    powershell write-host -foregroundcolor %1 %~2
) else (
    Echo %~2
)
exit /b

:Write
SetLocal EnableDelayedExpansion
if /i "%techniColor%" == "true" (
    powershell write-host -foregroundcolor %1 -NoNewline %~2
) else (

    REM Writes have been causing errors to be raised.
    if errorlevel 1 Echo ErrorLevel is currently %ErrorLevel%
    <NUL set /p =%~2
    ver > nul
)
exit /b


:: Jump points

:errorNoPython
call :WriteLine White ""
call :WriteLine White ""
call :WriteLine White "---------------------------------------------------------------------------"
call :WriteLine Red "Error: Python not installed"
call :WriteLine Red "Go to https://www.python.org/downloads/ for the latest version of Python"
goto:eof

:errorNoPythonVenv
call :WriteLine White ""
call :WriteLine White ""
call :WriteLine White "---------------------------------------------------------------------------"
call :WriteLine Red "Error: Python Virtual Environment activation failed"
call :WriteLine Red "Go to https://www.python.org/downloads/ for the latest version"
goto:eof