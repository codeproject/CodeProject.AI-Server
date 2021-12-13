:: CodeProject SenseAI Server startup script
:: We assume we're in the CodeProject SenseAI installed directory.

@echo off
cls
SETLOCAL EnableDelayedExpansion

:: Basic Settings

:: verbosity can be: quiet | info | loud
set verbosity=quiet

:: The name of the CodeProject Sense App Executable file
set appExe=CodeProject.SenseAI.Server.exe

:: Can be Debug or Releaes
set config=Debug

:: The target platform
set platform=net5.0

:: The name of the Environment variable setup file
set envVariablesFile=set_environment.bat

:: Show output in wild, crazy colours
set techniColor=true

:: -------------------------------------------------------------
:: Set Flags

set dotnetFlags=q
if "%verbosity%"=="info" set dotnetFlags=m
if "%verbosity%"=="loud" set dotnetFlags=n

if /i "%techniColor%" == "true" call :setESC

call :WriteLine Yellow "Preparing CodeProject.SenseAI Server" 

:: ===============================================================================================
:: 1. Load Installation settings

call :Write White "Loading installation settings..."
call !envVariablesFile!
call :WriteLine Green "Done"

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
:: 2. Activate Virtual Environment

call :Write White "Enabling our Virtual Environment..."

cd !APPDIR!\..
set VIRTUAL_ENV=%cd%\venv\Scripts

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

if "%verbosity%"=="loud" where Python.exe

:: ===============================================================================================
:: 3. Start front end server

:: a. Startup the backend Analysis services
call :Write White "Starting Analysis Services..."
if /i "!VISION_DETECTION!"   == "true" (
    START "CodeProject SenseAI Analysis Services" /B python !APPDIR!\detection.py
)
if /i "!VISION_FACE!" == "true" (
    START "CodeProject SenseAI Analysis Services" /B python !APPDIR!\face.py
)
if /i "!VISION_SCENE!"  == "true" (
    START "CodeProject SenseAI Analysis Services" /B python !APPDIR!\scene.py
)
:: To start them all in one fell swoop...
:: START "CodeProject SenseAI Analysis Services" /B python !APPDIR!\runAll.py
call :WriteLine Green "Done"

:: b. Build and startup the API server

set featureFlags=
if /i "!VISION_FACE!"      == "true" set featureFlags=!featureFlags! --VISION_FACE true
if /i "!VISION_DETECTION!" == "true" set featureFlags=!featureFlags! --VISION-DETECTION true
if /i "!VISION_SCENE!"     == "true" set featureFlags=!featureFlags! --VISION-SCENE true

:: In an installed, Production version of SenseAI, the server sits directly in the Server/Frontend
:: folder. For the development environment the server code is in /src/Server/Frontend. For Dev
:: we'll build the API server but leave it inside the :: /bin/Debug/... folder. Hence we need to
:: update the location of the main executable.

if /i "!CPSENSEAI_PRODUCTION!" == "true" (
   set CPSENSEAI_BUILDSERVER=False
   Set CPSENSEAI_COMFIG=Release

   cd %CPSENSEAI_ROOTDIR%\%CPSENSEAI_APIDIR%\Server\FrontEnd
) else (
    if /i "!CPSENSEAI_BUILDSERVER!" == "true" (
        cd %CPSENSEAI_ROOTDIR%\%CPSENSEAI_APPDIR%\%CPSENSEAI_APIDIR%\Server\FrontEnd
        dotnet build --configuration !CPSENSEAI_COMFIG! --nologo --verbosity !dotnetFlags!
        REM TODO Sort out the path issues with the build
        set appExe=bin\!platform!\!CPSENSEAI_COMFIG!\!platform!\!appExe!
    )
)

call :WriteLine Yellow "Launching CodeProject.SenseAI Server" 

!appExe! !featureFlags! --urls http://*:%port%

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

    if "%~2" == "" (
        Echo:
        exit /b 0
    )

    if /i "%techniColor%" == "true" (
        REM powershell write-host -foregroundcolor %1 %~2
        call :setColor %1 foreground
        echo !currentColor!%~2!resetColor!
    ) else (
        Echo %~2
    )
    exit /b 0

:Write

    if "%~2" == "" exit /b 0

    SetLocal EnableDelayedExpansion
    set resetColor=!ESC![0m

    if /i "%techniColor%" == "true" (
        REM powershell write-host -foregroundcolor %1 -NoNewline %~2
        call :setColor %1 foreground
        <NUL set /p =!currentColor!%~2!resetColor!
    ) else (
        <NUL set /p =%~2
    )
    exit /b 0

:: Jump ooints

:errorNoSettingsFile
call :WriteLine White ""
call :WriteLine White ""
call :WriteLine White "---------------------------------------------------------------------------"
call :WriteLine Red "Error: %settingsFile% settings file not found"
call :WriteLine ORange "Ensure you have run setup_dev_env_win.bat before running this script"
goto:eof


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