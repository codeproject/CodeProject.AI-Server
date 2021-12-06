:: CodeProject SenseAI Server install script
:: We assume we're in the source code /install directory.

:: syntax is: windows_install install_dir startupServer
::
:: eg to install in c:\setup and not start the server after installation:
::   windows_install c:\setup false
::
:: leave startupServer empty, or both empty, for defaults

@echo off
cls
setlocal enabledelayedexpansion

:: Enable the modules
set enableFaceDetection=true
set enableObjectDetection=true
set enableSceneDetection=true

:: verbosity can be: quiet | info | loud
set verbosity=quiet

:: If files are already present, then don't overwrite if this is false
set forceOverwrite=false

:: Whether to attempt to open the inbound port for the server (requires admin mode)
set attemptOpenPort=false

:: Whether to install python packages via PIP
set installPythonPackages=true

:: Whether to start the server on completion
set startupServer=true


:: Basic locations

REM The name of the CodeProject Sense App
set appName=CodeProject.SenseAI

REM The name of the CodeProject Sense App Executable file
set appExe=CodeProject.SenseAI.Server.exe

:: The name of the startup settings file
set settingsFile=CodeProject.SenseAI.config

:: The name of the Environment variable setup file
set envVariablesFile=set_environment.bat

REM The port to expose for the front end service
set port=5000

REM The full path to the directory holding the main app and it's sub-dirs
set senseAppDir=C:\CodeProject.SenseAI

REM The name of the dir holding the backend analysis services
set backendName=AnalysisLayer

REM The name of the dir holding the DeepStack analysis services
set deepstackModule=DeepStack

REM The name of the dir holding the frontend API server
set frontendName=API

REM The location of large packages that need to be downloaded
set storageUrl=https://codeproject-ai.s3.ca-central-1.amazonaws.com/sense/installer/

REM The name of the dir, within the current directory, where install assets will be downloaded to
set downloadName=downloads

REM The name of the dir containing the AI models themselves
set modelsDirName=assets

REM The name of the dir containing persisted data
set storeName=datastore

REM The name of the dir containing temporary data
set tempName=tempstore

REM Handle parameter overrides
if not "%1" == "" set senseAppDir=%1
if not "%2" == "" set startupServer=%2


:: -------------------------------------------------------------
:: Set Flags

set pipFlags=-q -q
set rmdirFlags=/q
set roboCopyFlags=/NFL /NDL /NJH /NJS /nc /ns  >nul 2>nul
set dotnetFlags=q
set activateFlags=

if "%verbosity%"=="info" set pipFlags=-q
if "%verbosity%"=="info" set rmdirFlags=/q
if "%verbosity%"=="info" set roboCopyFlags=/NFL /NDL /NJH
if "%verbosity%"=="info" set dotnetFlags=m
if "%verbosity%"=="info" set activateFlags=

if "%verbosity%"=="loud" set pipFlags=
if "%verbosity%"=="loud" set rmdirFlags=
if "%verbosity%"=="loud" set roboCopyFlags=
if "%verbosity%"=="loud" set dotnetFlags=n
if "%verbosity%"=="loud" set activateFlags=-Verbose


REM Robocopy:
REM  /NFL : No File List - don't log file names.
REM  /NDL : No Directory List - don't log directory names.
REM  /NJH : No Job Header.
REM  /NJS : No Job Summary.
REM  /NP  : No Progress - don't display percentage copied.
REM  /NS  : No Size - don't log file sizes.
REM  /NC  : No Class - don't log file classes.


:: ===============================================================================================
:: 1. Ensure directories are created and download required assets

:: Create some directories
call :Write White "Creating Directories..."

if not exist %downloadName%\                mkdir %downloadName%

if not exist %senseAppDir%                  mkdir %senseAppDir%
if not exist %senseAppDir%\%frontendName%\  mkdir %senseAppDir%\%frontendName%

set backendDir=%senseAppDir%\%backendName%
if not exist %backendDir%                   mkdir %backendDir%
if not exist %backendDir%\%deepstackModule% mkdir %backendDir%\%deepstackModule%
if not exist %backendDir%\%deepstackModule%\%tempName%\        mkdir %backendDir%\%deepstackModule%\%tempName%
if not exist %backendDir%\%deepstackModule%\%storeName%\       mkdir %backendDir%\%deepstackModule%\%storeName%

call :WriteLine Green "Done"


call :WriteLine White "Downloading utilities and models: Starting."

REM Clean up directories to force a re-download if necessary
if /i "%forceOverwrite%" == "true" (
    if exist %downloadName%\%modelsDirName%   rmdir /s %rmdirFlags% %downloadName%\%modelsDirName%
    if exist %downloadName%\python37 rmdir /s %rmdirFlags% %downloadName%\python37

    if exist %backendDir%\%deepstackModule%\%modelsDirName% rmdir /s %rmdirFlags% %backendDir%\%deepstackModule%\%modelsDirName%
    if exist %backendDir%\python37   rmdir /s %rmdirFlags% %backendDir%\python37
)

REM Download whatever packages are missing 
if not exist %downloadName%\%modelsDirName% (
    if /i "%verbosity%" == "quiet" call :Write White "Downloading models..."
    PowerShell -NoProfile -ExecutionPolicy Bypass -Command ".\scripts\download.ps1 %storageUrl% %downloadName%/ models.zip %modelsDirName%"
    if /i "%verbosity%" == "quiet" call :WriteLine Green "Done"
)
if not exist %downloadName%\python37 (
    if /i "%verbosity%" == "quiet" call :Write White "Downloading Python interpreter..."
    PowerShell -NoProfile -ExecutionPolicy Bypass -Command ".\scripts\download.ps1 %storageUrl% %downloadName%/ python37.zip python37"
    if /i "%verbosity%" == "quiet" call :WriteLine Green "Done"
)

REM Move downloads to the installation backend
call :Write White "Moving assets to installation folder..."
if exist %downloadName%\%modelsDirName%   robocopy /e %downloadName%\%modelsDirName%   %backendDir%\%deepstackModule%\%modelsDirName%  !roboCopyFlags! > NUL
if exist %downloadName%\python37 robocopy /e %downloadName%\python37 %backendDir%\        !roboCopyFlags! > NUL
call :WriteLine Green "Done"

call :WriteLine White "Downloading utilities and models: Completed"

:: ===============================================================================================
:: 2. Create & Activate Virtual Environment

call :Write White "Creating Virtual Environment..."
%backendDir%\python37\python.exe -m venv %backendDir%\venv
call :WriteLine Green "Done"

call :Write White "Enabling our Virtual Environment..."

set VIRTUAL_ENV=%backendDir%\venv\Scripts

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
:: 3. Copy over the AI engine itself and install PIP packages

call :Write White "Copying over backend Python Intelligence layer..."
robocopy /e ..\src\AnalysisLayer\DeepStack\intelligencelayer %backendDir%\%deepstackModule% !roboCopyFlags! > NUL
call :WriteLine Green "Done"

if /i "%installPythonPackages%" == "true" (

    call :Write White "Installing Python package manager..."
    python -m pip install --trusted-host pypi.python.org --trusted-host files.pythonhosted.org --trusted-host pypi.org --upgrade pip !pipFlags!
    call :WriteLine Green "Done"

    REM call :Write White "Installing Packages into Virtual Environment..."
    REM pip install -r %backendDir%\%deepstackModule%\requirements.txt !pipFlags!
    REM call :WriteLine Green "Success"

    REM We'll do this the long way so we can see some progress
    set currentOption=
    for /f "tokens=*" %%x in (%backendDir%\%deepstackModule%\requirements.txt) do (

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
)


:: ===============================================================================================
:: 4. Build and copy over front end API server

call :WriteLine White "Preparing front end API server"

:: Copy over the startup script
copy /Y "windows_start.bat" "%senseAppDir%" >nul 2>nul

:: Build the server executable and copy over
cd ..\src\API\Server\FrontEnd\

dotnet build --configuration Release --nologo --verbosity !dotnetFlags!
:: dotnet build --configuration Release --nologo --verbosity !dotnetFlags! --self-contained true  - .NET 6


robocopy /e bin/Release/net5.0 %senseAppDir%\%frontendName% !roboCopyFlags!

if /i "%attemptOpenPort%" == "true" (
    call :Write White "Opening port..."

    netstat /o /a /n | find /i "listening" | find ":%port%" >nul 2>nul && (
       call :WriteLine Green "Port %port% is open for listening"
    ) || (

        set RULE_NAME="Open Port %port% for %appName%"
        netsh advfirewall firewall show rule name=%RULE_NAME% >nul
        if not ERRORLEVEL 1 (
            call :WriteLine Green "Success"
        ) else (
            call :WriteLine Gray "Retrying..."
            netsh advfirewall firewall add rule name=%RULE_NAME% dir=in action=allow protocol=TCP localport=%port%
            if not ERRORLEVEL 1 call :WriteLine Green "Success"
        )
    )
)

:: ===============================================================================================
:: 5. Let's do this!


:: a) SET all Env. variables and store in a json file that will be loaded by the .NET API app

cd %senseAppDir%\%frontendName% 

:: For DeepStack
set APPDIR=%backendDir%\%deepstackModule%
set MODELS_DIR=%backendDir%\%deepstackModule%\%modelsDirName%
set DATA_DIR=%backendDir%\%deepstackModule%\%storeName%
set TEMP_PATH=%backendDir%\%deepstackModule%\%tempName%
set PROFILE=windows_native
set CUDA_MODE=False

set modulesEnabled=,
if /i "%enableFaceDetection%"   == "true" set modulesEnabled=!modulesEnabled!VISION_FACE,
if /i "%enableObjectDetection%" == "true" set modulesEnabled=!modulesEnabled!VISION_DETECTION,
if /i "%enableSceneDetection%"  == "true" set modulesEnabled=!modulesEnabled!VISION_SCENE,
set modulesEnabled=!modulesEnabled:~1,-1!

(
echo  {
:: SenseAI Application values
echo    "CPSENSEAI_APPDIR"   : "!senseAppDir!",
echo    "CPSENSEAI_PORT"     : "!port!",
echo    "CPSENSEAI_PROFILE"  : "!PROFILE!",
echo    "CPSENSEAI_FRONTEND" : "!frontendName!",
echo    "CPSENSEAI_BACKEND"  : "!backendName!",
echo    "CPSENSEAI_MODULES"  : "!modulesEnabled:,=;!",

:: DeepStack compatible values
echo    "APPDIR"             : "!APPDIR!",
echo    "PROFILE"            : "!PROFILE!",
echo    "CUDA_MODE"          : "!CUDA_MODE!",
echo    "DATA_DIR"           : "!DATA_DIR!",
echo    "TEMP_PATH"          : "!TEMP_PATH!",
echo    "MODELS_DIR"         : "!MODELS_DIR!",
echo    "PORT"               : "!PORT!",
echo    "VISION_FACE"        : "!enableFaceDetection!",
echo    "VISION_DETECTION"   : "!enableObjectDetection!",
echo    "VISION_SCENE"       : "!enableSceneDetection!"
echo  }
) > %senseAppDir%\%settingsFile%

:: Also create a .BAT file for easy Starting

(
:: SenseAI Application values
echo set CPSENSEAI_APPDIR=!senseAppDir!
echo set CPSENSEAI_PORT=!port!
echo set CPSENSEAI_PROFILE=!PROFILE!
echo set CPSENSEAI_FRONTEND=!frontendName!
echo set CPSENSEAI_BACKEND=!backendName!
echo set CPSENSEAI_MODULES=!modulesEnabled:,=;!

:: DeepStack compatible values
echo set APPDIR=!APPDIR!
echo set PROFILE=!PROFILE!
echo set CUDA_MODE=!CUDA_MODE!
echo set DATA_DIR=!DATA_DIR!
echo set TEMP_PATH=!TEMP_PATH!
echo set MODELS_DIR=!MODELS_DIR!
echo set PORT=!PORT!
echo set VISION_FACE=!enableFaceDetection!
echo set VISION_DETECTION=!enableObjectDetection!
echo set VISION_SCENE=!enableSceneDetection!
) > %senseAppDir%\%envVariablesFile%


:: -- interlude --
:: If we don't need to go any futher, let's blow this popsicle stand
if /i not "%startupServer%" == "true" goto eof
:: -- interlude --


:: b) Load Installation settings

call :Write White "Loading installation settings..."

:: Load Json string from settings file
for /f "tokens=*" %%x in (%senseAppDir%\%settingsFile%) do (
    set line=%%x
	
	REM Remove brackets, commas and quotes
	set line=!line:}=! 
	set line=!line:{=!
	set line=!line:,=!
	set line=!line:"=!
	REM Convert ": " to "=". BE CAREFUL of drive letters: Order of operations matters
	set line=!line:: ==!
	REM Now remove the spaces
	set line=!line: =!
	
	if "%verbosity%" == "loud" echo Config File line: !line!

	if not "!line!" == "" Set !line!
)

call :WriteLine Green "Done"


:: c) Start the backend analysis processes

call :Write White "Starting Analysis Services..."

if /i "%enableObjectDetection%"   == "true" (
    START "CodeProject SenseAI Analysis Services" /B python %backendDir%\%deepstackModule%\detection.py
)
if /i "%enableFaceDetection%" == "true" (
    START "CodeProject SenseAI Analysis Services" /B python %backendDir%\%deepstackModule%\face.py
)
if /i "%enableSceneDetection%"  == "true" (
    START "CodeProject SenseAI Analysis Services" /B python %backendDir%\%deepstackModule%\scene.py
)
:: To start them all in one fell swoop...
:: START "CodeProject SenseAI Analysis Services" /B python %backendDir%\%deepstackModule%\runAll.py
call :WriteLine Green "Done"


:: d) Startup the API server

call :Write White "Starting API server..."

set featureFlags=
if /i "%enableFaceDetection%"   == "true" set featureFlags=!featureFlags! --VISION_FACE true
if /i "%enableObjectDetection%" == "true" set featureFlags=!featureFlags! --VISION-DETECTION true
if /i "%enableSceneDetection%"  == "true" set featureFlags=!featureFlags! --VISION-SCENE true

%appExe% !featureFlags! --urls http://*:%port%

call :WriteLine Green "Done"


:: and we're done.
goto eof


:: sub-routines

:Trim
SetLocal EnableDelayedExpansion
set Params=%*
for /f "tokens=1*" %%a in ("!Params!") do EndLocal & set %1=%%b
exit /b

:WriteLine
SetLocal EnableDelayedExpansion
powershell write-host -foregroundcolor %1 %~2
exit /b

:Write
SetLocal EnableDelayedExpansion
powershell write-host -foregroundcolor %1 -NoNewline %~2
exit /b

:: Jump points

:errorNoPython
call :WriteLine White ""
call :WriteLine White ""
call :WriteLine White "---------------------------------------------------------------------------"
call :WriteLine Red "Error: Python not installed"
call :WriteLine Red "Go to https://www.python.org/downloads/release/python-3712/ for Python 3.7"
goto:eof

:errorNoPythonVenv
call :WriteLine White ""
call :WriteLine White ""
call :WriteLine White "---------------------------------------------------------------------------"
call :WriteLine Red "Error: Python Virtual Environment activation failed"
call :WriteLine Red "Go to https://www.python.org/downloads/ for the latest version"
goto:eof

:eof