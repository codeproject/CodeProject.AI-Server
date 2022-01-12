:: CodeProject SenseAI Analysis services (DeepStack module) startup script for Windows
::
:: Usage:
::   start.bat
::
:: We assume we're in the AnalysisLayer/DeepStack directory

@echo off
cls
SETLOCAL EnableDelayedExpansion

set CPSENSEAI_ROOTDIR=%cd%\..\..\..\..\
set envVariablesFile=!CPSENSEAI_ROOTDIR!\set_environment.bat
set techniColor=true

:: Before we start, set the current directory if necessary
if "%1" == "--no-color" set techniColor=false
if not "%2" == "" cd %2

:: ===============================================================================================
:: 0. Basic settings

:: verbosity can be: quiet | info | loud
set verbosity=quiet

if not "%verbosity%" == "quiet" call :WriteLine Yellow "Current Directory: %cd%"
if /i "%techniColor%" == "true" call :setESC

:: ===============================================================================================
:: 1. Load environment variables

call :Write White "Loading installation settings..."
call "!envVariablesFile!"
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
    START "CodeProject SenseAI Analysis Services" /B python "!APPDIR!\%deepstackModule%\detection.py"
)
if /i "!VISION_FACE!" == "true" (
    START "CodeProject SenseAI Analysis Services" /B python "!APPDIR!\%deepstackModule%\face.py"
)
if /i "!VISION_SCENE!"  == "true" (
    START "CodeProject SenseAI Analysis Services" /B python "!APPDIR!\%deepstackModule%\scene.py"
)
:: To start them all in one fell swoop...
:: START "CodeProject SenseAI Analysis Services" /B python "!APPDIR!\%deepstackModule%\runAll.py"
call :WriteLine Green "Done"

:: Wait forever. We need these processes to stay alive
pause > NUL

:: and we're done.
goto:eof


:: sub-routines

:setESC
    for /F "tokens=1,2 delims=#" %%a in ('"prompt #$H#$E# & echo on & for %%b in (1) do rem"') do (
      set ESC=%%b
      exit /B 0
    )
    exit /B 0

:setColor
    REM echo %ESC%[4m - Underline
    REM echo %ESC%[7m - Inverse

    if /i "%2" == "foreground" (
        REM Foreground Colours
        if /i "%1" == "Black"       set currentColor=!ESC![30m
        if /i "%1" == "DarkRed"     set currentColor=!ESC![31m
        if /i "%1" == "DarkGreen"   set currentColor=!ESC![32m
        if /i "%1" == "DarkYellow"  set currentColor=!ESC![33m
        if /i "%1" == "DarkBlue"    set currentColor=!ESC![34m
        if /i "%1" == "DarkMagenta" set currentColor=!ESC![35m
        if /i "%1" == "DarkCyan"    set currentColor=!ESC![36m
        if /i "%1" == "Gray"        set currentColor=!ESC![37m
        if /i "%1" == "DarkGray"    set currentColor=!ESC![90m
        if /i "%1" == "Red"         set currentColor=!ESC![91m
        if /i "%1" == "Green"       set currentColor=!ESC![92m
        if /i "%1" == "Yellow"      set currentColor=!ESC![93m
        if /i "%1" == "Blue"        set currentColor=!ESC![94m
        if /i "%1" == "Magenta"     set currentColor=!ESC![95m
        if /i "%1" == "Cyan"        set currentColor=!ESC![96m
        if /i "%1" == "White"       set currentColor=!ESC![97m
    ) else (
        REM Background Colours
        if /i "%1" == "Black"       set currentColor=!ESC![40m
        if /i "%1" == "DarkRed"     set currentColor=!ESC![41m
        if /i "%1" == "DarkGreen"   set currentColor=!ESC![42m
        if /i "%1" == "DarkYellow"  set currentColor=!ESC![43m
        if /i "%1" == "DarkBlue"    set currentColor=!ESC![44m
        if /i "%1" == "DarkMagenta" set currentColor=!ESC![45m
        if /i "%1" == "DarkCyan"    set currentColor=!ESC![46m
        if /i "%1" == "Gray"        set currentColor=!ESC![47m
        if /i "%1" == "DarkGray"    set currentColor=!ESC![100m
        if /i "%1" == "Red"         set currentColor=!ESC![101m
        if /i "%1" == "Green"       set currentColor=!ESC![102m
        if /i "%1" == "Yellow"      set currentColor=!ESC![103m
        if /i "%1" == "Blue"        set currentColor=!ESC![104m
        if /i "%1" == "Magenta"     set currentColor=!ESC![105m
        if /i "%1" == "Cyan"        set currentColor=!ESC![106m
        if /i "%1" == "White"       set currentColor=!ESC![107m
    )
    exit /B 0

:WriteLine
    SetLocal EnableDelayedExpansion
    set resetColor=!ESC![0m

    set str=%~2

    if "!str!" == "" (
        Echo:
        exit /b 0
    )

    if /i "%techniColor%" == "true" (
        REM powershell write-host -foregroundcolor %1 !str!
        call :setColor %1 foreground
        echo !currentColor!!str!!resetColor!
    ) else (
        Echo !str!
    )
    exit /b 0

:Write

    set str=%~2

    if "!str!" == "" exit /b 0

    SetLocal EnableDelayedExpansion
    set resetColor=!ESC![0m

    if /i "%techniColor%" == "true" (
        REM powershell write-host -foregroundcolor %1 -NoNewline !str!
        call :setColor %1 foreground
        <NUL set /p =!currentColor!!str!!resetColor!
    ) else (
        <NUL set /p =!str!
    )
    exit /b 0


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