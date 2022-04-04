:: ============================================================================
::
:: CodeProject SenseAI Server Startup script 
::
:: Run this to START CodeProject SenseAI
:: 
:: Please ensure you have run Setup_SenseAI_Win.bat before you run this script.
:: Setup_SenseAI_Win will download everything you need and setup the Environment
:: while this script will start up the CodeProject SenseAI API server and
:: backend analysis services.
::
:: ============================================================================

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

:: Can be Debug or Release
set config=Release

:: The target platform
set platform=net5.0

:: The name of the Environment variable setup file
set settingsFile=CodeProject.SenseAI.json

:: Show output in wild, crazy colours
set techniColor=true

:: Debug overrides
rem set verbosity=info
rem set config=Debug


:: ----------------------------------------------------------------------------
:: Set Flags

set dotnetFlags=q
if "%verbosity%"=="info" set dotnetFlags=m
if "%verbosity%"=="loud" set dotnetFlags=n

if /i "%techniColor%" == "true" call :setESC

call :WriteLine Yellow "Preparing CodeProject.SenseAI Server" 


:: Before we start, let's set the root directory.
:: Also setup the required environment variables.
:: Doing the path calculations here so app will run if install directory 
:: renamed or moved in addition to running from a non-default directory.

:: ============================================================================
:: 1. Load Installation settings

call :Write White "Loading installation settings..."
(
    for /f "tokens=*" %%x in (' more ^< "%settingsFile%" ') do (
        set line=%%x
        rem remove quotes, change " : " to "=", remove spaces
        set line=!line:"=!
        set line=!line: : ==!
        set line=!line: =!
	    if not "!line:~0,1!" == "{" (
    	    if not "!line:~0,1!" == "}" (
                if "!line:~-1!" == "," set line=!line:~0,-1!
                echo set !line!
            )
        )

    )
) > "!settingsFile!.bat"
call !settingsFile!.bat
del !settingsFile!.bat
call :WriteLine Green "Done"

:: In case the installation has been moved to a different directory after installation
:: CodeProject SenseAI API Server
set CPSENSEAI_ROOTDIR=%cd%
:: Modules: DeepStack specific
set APPDIR=!CPSENSEAI_ROOTDIR!\!CPSENSEAI_APPDIR!\!CPSENSEAI_ANALYSISDIR!\DeepStack\intelligencelayer
set DATA_DIR=!CPSENSEAI_ROOTDIR!\!CPSENSEAI_APPDIR!\!CPSENSEAI_ANALYSISDIR!\DeepStack\datastore
set TEMP_PATH=!CPSENSEAI_ROOTDIR!\!CPSENSEAI_APPDIR!\!CPSENSEAI_ANALYSISDIR!\DeepStack\tempstore
set MODELS_DIR=!CPSENSEAI_ROOTDIR!\!CPSENSEAI_APPDIR!\!CPSENSEAI_ANALYSISDIR!\DeepStack\assets
:: Modules: CodeProject YOLO
:: ...


if "%verbosity%" NEQ "quiet" (
    call :WriteLine Yellow "Environment variable summary"
    call :WriteLine Yellow "CodeProject SenseAI"
    call :WriteLine DarkGreen "  CPSENSEAI_ROOTDIR     = !CPSENSEAI_ROOTDIR!
    call :WriteLine DarkGreen "  CPSENSEAI_APPDIR      = !CPSENSEAI_APPDIR!
    call :WriteLine DarkGreen "  CPSENSEAI_APIDIR      = !CPSENSEAI_APIDIR!
    call :WriteLine DarkGreen "  CPSENSEAI_ANALYSISDIR = !CPSENSEAI_ANALYSISDIR!
    call :WriteLine DarkGreen "  CPSENSEAI_PORT        = !PORT!
    call :WriteLine DarkGreen "  CPSENSEAI_PROFILE     = !PROFILE!
    call :WriteLine DarkGreen "  CPSENSEAI_PRODUCTION  = !CPSENSEAI_PRODUCTION!
    call :WriteLine DarkGreen "  CPSENSEAI_CONFIG      = !CPSENSEAI_CONFIG!
    call :WriteLine DarkGreen "  CPSENSEAI_BUILDSERVER = !CPSENSEAI_BUILDSERVER!
    call :WriteLine Yellow "Module: DeepStack"
    call :WriteLine DarkGreen "  APPDIR                = !APPDIR!"
    call :WriteLine DarkGreen "  PROFILE               = !PROFILE!"
    call :WriteLine DarkGreen "  CUDA_MODE             = !CUDA_MODE!"
    call :WriteLine DarkGreen "  DATA_DIR              = !DATA_DIR!"
    call :WriteLine DarkGreen "  TEMP_PATH             = !TEMP_PATH!"
    call :WriteLine DarkGreen "  PORT                  = !PORT!"
    REM call :WriteLine Yellow "Module: CodeProject YOLO"
    REM call :WriteLine DarkGreen ...
)

:: ============================================================================
:: 2. Activate Virtual Environment

call :Write White "Enabling our Virtual Environment..."

set deepstackDir=!CPSENSEAI_ROOTDIR!\!CPSENSEAI_APPDIR!\!CPSENSEAI_ANALYSISDIR!\DeepStack

:: Rewrite the pyvenv.cfg to point to the correct, absolute, python directory.
:: This may have changed if this folder has been moved. See also the discussion
:: https://bugs.python.org/issue39469 - Support for relative home path in pyvenv
(
echo home = !deepstackDir!\python37
echo include-system-site-packages = false
echo version = 3.7.9
) > "!deepstackDir!\venv\pyvenv.cfg"

:: Activate the Virtual Environment
set VIRTUAL_ENV=%deepstackDir%\venv\Scripts

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

:: ============================================================================
:: 3. Start front end server

:: In an installed, Production version of SenseAI, the server exe sits directly
:: in the /src/Server/Frontend folder. For the development environment the
:: server exe is in /src/Server/Frontend/bin/Debug/... folder. Hence we need to
:: update the location of the main executable.

cd !CPSENSEAI_ROOTDIR!\!CPSENSEAI_APPDIR!\!CPSENSEAI_APIDIR!\Server\FrontEnd

if /i "!CPSENSEAI_PRODUCTION!" == "true" (

    set CPSENSEAI_BUILDSERVER=False
    Set CPSENSEAI_CONFIG=Release
   
) else (

    if /i "!CPSENSEAI_BUILDSERVER!" == "true" (

        cd "!appFolder!" > nul

        if "%verbosity%"=="quiet" (
            dotnet build --configuration !CPSENSEAI_CONFIG! --nologo --verbosity !dotnetFlags! > nul
        ) else (
            dotnet build --configuration !CPSENSEAI_CONFIG! --nologo --verbosity !dotnetFlags!
        )

        REM Head down to the dev version of the exe.
        set appExe=bin\!platform!\!CPSENSEAI_CONFIG!\!platform!\win-x86\!appExe!
    )
)

call :WriteLine Yellow "Launching CodeProject.SenseAI Server" 

"!appExe!" --urls http://*:%port%

:: Pause and let backend services catch up (to be controlled via messages soon)
if "%startPythonDirectly %" == "true" Timeout /T 5 /NOBREAK >nul 2>nul

call :WriteLine Green "CodeProject.SenseAI Server is now live" 


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

:errorNoSettingsFile
call :WriteLine White  ""
call :WriteLine White  ""
call :WriteLine White  "------------------------------------------------------"
call :WriteLine Red    "Error: %settingsFile% settings file not found"
call :Write     Orange "Ensure you have run setup_dev_env_win.bat before "
call :WriteLine Orange "running this script"
goto:eof

:errorNoPythonVenv
call :WriteLine White ""
call :WriteLine White ""
call :WriteLine White "-------------------------------------------------------"
call :WriteLine Red "Error: Python Virtual Environment activation failed"
goto:eof