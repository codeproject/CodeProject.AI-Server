:: CodeProject SenseAI Server startup script
:: We assume we're in the CodeProject SenseAI installed directory.

@echo off
cls
SETLOCAL EnableDelayedExpansion

:: Basic Settings

:: The name of the CodeProject Sense App
set appName=CodeProject.SenseAI

:: The name of the CodeProject Sense App Executable file
set appExe=CodeProject.SenseAI.Server.exe

:: The name of the startup settings file
set settingsFile=CodeProject.SenseAI.config

:: The name of the Environment variable setup file
set envVariablesFile=set_environment.bat

:: Whether to attempt to open the inbound port for the server (requires admin mode)
set attemptOpenPort=false

:: verbosity can be: quiet | info | loud
set verbosity=quiet

:: ===============================================================================================
:: 2. Load Installation settings

call :Write White "Loading installation settings..."

:: Easy way is to just run %envVariablesFile%, but that won't provide any summary

if not exist %settingsFile% goto noSettingsFile

:: Load Json string from settings file
for /f "tokens=*" %%x in (%settingsFile%) do (
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

:: ===============================================================================================
:: 2. Activate Virtual Environment

call :Write White "Enable our Virtual Environment..."

set VIRTUAL_ENV=!APPDIR!\..\venv\Scripts

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
:: 3. Open port if needed

if "%attemptOpenPort%" == "true" (
    call :Write White "Opening port..."

    netstat /o /a /n | find /i "listening" | find ":%CPSENSEAI_PORT%" >nul 2>nul && (
       call :WriteLine Green "Port %CPSENSEAI_PORT% is open for listening"
    ) || (

        set RULE_NAME="Open Port %CPSENSEAI_PORT% for %appName%"
        netsh advfirewall firewall show rule name=%RULE_NAME% >nul
        if not ERRORLEVEL 1 (
            call :WriteLine Green "Success"
        ) else (
            call :WriteLine Gray "Retrying..."
            netsh advfirewall firewall add rule name=%RULE_NAME% dir=in action=allow protocol=TCP localport=%CPSENSEAI_PORT%
            if not ERRORLEVEL 1 call :WriteLine Green "Success"
        )
    )
)

:: ===============================================================================================
:: 3. Start front end server

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

:: b. Startup the API server
set featureFlags=
if /i "!VISION_FACE!"      == "true" set featureFlags=!featureFlags! --VISION_FACE true
if /i "!VISION_DETECTION!" == "true" set featureFlags=!featureFlags! --VISION-DETECTION true
if /i "!VISION_SCENE!"     == "true" set featureFlags=!featureFlags! --VISION-SCENE true

cd %CPSENSEAI_APPDIR%\%CPSENSEAI_FRONTEND% 
%appExe% !featureFlags! --urls http://*:%port%


:: and we're done.
goto eof


:: sub-routines

:WriteLine
SetLocal EnableDelayedExpansion
powershell write-host -foregroundcolor %1 %~2
:: powershell write-host -foregroundcolor White -NoNewline
exit /b

:Write
SetLocal EnableDelayedExpansion
powershell write-host -foregroundcolor %1 -NoNewline %~2
:: powershell write-host -foregroundcolor White -NoNewline
exit /b

:: Jump ooints

:errorNoSettingsFile
call :WriteLine White ""
call :WriteLine White ""
call :WriteLine White "---------------------------------------------------------------------------"
call :WriteLine Red "Error: %appName% settings file not found"
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

:eof