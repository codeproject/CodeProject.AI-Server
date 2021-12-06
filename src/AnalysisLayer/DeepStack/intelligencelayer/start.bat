:: CodeProject SenseAI Server startup script
:: We assume we're in the CodeProject SenseAI installed directory.

@echo off
cls
SETLOCAL EnableDelayedExpansion

:: Before we start, set the current directory if necessary
if "%1" == "--no-color" set NO_COLOR=true
if not "%2" == "" cd %2


:: ===============================================================================================
:: 0. Basic settings

:: verbosity can be: quiet | info | loud
set verbosity=quiet

REM The location of large packages that need to be downloaded
set storageUrl=https://codeproject-ai.s3.ca-central-1.amazonaws.com/sense/installer/


:: ===============================================================================================
:: 1. Set environment variables

set APPDIR=.\
set PORT=5000
set PROFILE=windows_native
set CUDA_MODE=False
set DATA_DIR=..\data
set TEMP_PATH=..\temp

set VENV_PATH=venv
set INSTALL_DIR=..\..\..\..\install
set DOWNLOADS_PATH=%INSTALL_DIR%\downloads
set MODELS_DIRNAME=assets
set MODELS_DIR=%DOWNLOADS_PATH%\%MODELS_DIRNAME%

set VISION_FACE=True
set VISION_DETECTION=True
set VISION_SCENE=True

if "%verbosity%" == "info" (
    echo   APPDIR    = "!APPDIR!"
    echo   PROFILE   = "!PROFILE!"
    echo   CUDA_MODE = "!CUDA_MODE!"
    echo   DATA_DIR  = "!DATA_DIR!"
    echo   TEMP_PATH = "!TEMP_PATH!"
    echo   PORT      = "!PORT!"
    echo   VISION_FACE      = "!VISION_FACE!"
    echo   VISION_DETECTION = "!VISION_DETECTION!"
    echo   VISION_SCENE     = "!VISION_SCENE!"
)

:: ===============================================================================================
:: 1. Ensure directories are created and download required assets

if not "%verbosity%" == "quiet" echo Current Directory: %cd%

:: Create some directories
call :Write White "Creating Directories..."
if not exist %DOWNLOADS_PATH%  mkdir %DOWNLOADS_PATH%
if not exist %DATA_DIR%        mkdir %DATA_DIR%
if not exist %TEMP_PATH%       mkdir %TEMP_PATH%
call :WriteLine Green "Done"

:: Download, unzip, and move into place the Utilities and known Python version
call :WriteLine White "Download utilities and models..."

REM Download whatever packages are missing 
if not exist %DOWNLOADS_PATH%\%MODELS_DIRNAME% (
    PowerShell -NoProfile -ExecutionPolicy Bypass -Command "%INSTALL_DIR%\scripts\download.ps1 %storageUrl% %DOWNLOADS_PATH%\ models.zip %MODELS_DIRNAME%"
    if /i "%verbosity%" == "quiet" call :Write White "Downloading models Done"
)
if not exist %DOWNLOADS_PATH%\python37 (
    PowerShell -NoProfile -ExecutionPolicy Bypass -Command "%INSTALL_DIR%\scripts\download.ps1 %storageUrl% %DOWNLOADS_PATH%\ python37.zip python37"
    if /i "%verbosity%" == "quiet" call :Write White "Downloading Python interpreter Done"
)


:: ===============================================================================================
:: 2. Create & Activate Virtual Environment from scratch (instead of the above download/unpack/copy)

if not exist %VENV_PATH% (
    call :Write White "Creating Virtual Environment..."
    %DOWNLOADS_PATH%\python37\python37\python.exe -m venv %VENV_PATH%
    call :WriteLine Green "Done"
) else (
    call :WriteLine Gray "Virtual Environment exists"
)

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
:: 3. Install PIP packages

:: ASSUMPTION: If venv/Lib/site-packages/pip exists then no need to do this

call :Write White "Checking for required packages..."

if not exist %VENV_PATH%\Lib\site-packages\pip (

    call :WriteLine Yellow "Installing"

    :: Set Flags
    set pipFlags=-q -q
    if "%verbosity%"=="info" set pipFlags=-q
    if "%verbosity%"=="loud" set pipFlags=

    call :Write White "Installing Python package manager..."
    python -m pip install --trusted-host pypi.python.org --trusted-host files.pythonhosted.org --trusted-host pypi.org --upgrade pip !pipFlags!
    call :WriteLine Green "Done"

    REM call :Write White "Installing Packages into Virtual Environment..."
    REM pip install -r %backendDir%\%deepstackModule%\requirements.txt !pipFlags!
    REM call :WriteLine Green "Success"

    REM We'll do this the long way so we can see some progress
    set currentOption=
    for /f "tokens=*" %%x in (requirements.txt) do (

        set line=%%x

        if "!line!" == "" (
            set currentOption=
        ) else if "!line:~0,2!" == "##" (
            set currentOption=
        ) else if "!line:~0,12!" == "--find-links" (
            set currentOption=!line!
        ) else (
            call :Write White "    PIP: Installing !line!..."
            REM echo python.exe -m pip install !line! !currentOption! !pipFlags!
            python.exe -m pip install !line! !currentOption! !pipFlags!
            call :WriteLine Green "Done"

            set currentOption=
        )
    )
) else (
    call :WriteLine Green "present."
)

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
goto eof


:: sub-routines

:WriteLine
SetLocal EnableDelayedExpansion
if /i "!NO_COLOR!" == "true" (
    REM Echo %~2
) else (
    powershell write-host -foregroundcolor %1 %~2
    REM powershell write-host -foregroundcolor White -NoNewline
)
exit /b

:Write
SetLocal EnableDelayedExpansion
if /i "!NO_COLOR!" == "true" (
    Echo %~2
) else (
    powershell write-host -foregroundcolor %1 -NoNewline %~2
    REM powershell write-host -foregroundcolor White -NoNewline
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

:eof