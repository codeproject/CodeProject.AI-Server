:: ===============================================================================================
::
:: CodeProject SenseAI Server Installation Setup script 
::
:: WELCOME TO CODEPROJECT SENSEAI!
:: 
:: Please ensure this script is run before you start exploring and integrating CodeProject.SenseAI.
:: This script will download the required pieces, move them into place, run the setup commands and
:: get you on your way.
::
:: Grab a coffee because it could take a while. But it'll be worth it. We promise.
::
:: ===============================================================================================


@echo off
cls
SETLOCAL EnableDelayedExpansion

:: -----------------------------------------------------------------------------------------------
:: 0. Script settings.

:: The location of large packages that need to be downloaded
:: a. From contrary GCP
rem set storageUrl=https://storage.googleapis.com/codeproject-senseai/
:: b. From AWS
set storageUrl=https://codeproject-ai.s3.ca-central-1.amazonaws.com/sense/installer/
:: c. Use a local directory rather than from online. Handy for debugging.
:: set storageUrl=C:\Dev\CodeProject\CodeProject.AI\install\cached_downloads\

:: The name of the dir, within the current directory, where install assets will be downloaded
set downloadDir=AnalysisLayer

:: The name of the dir containing the Python interpreter
set pythonDir=python37

:: The name of the dir containing the AI models themselves
set modelsDir=assets

:: Show output in wild, crazy colours
set techniColor=true

:: verbosity can be: quiet | info | loud
set verbosity=quiet

:: Set the noise level when installing Python packages
set pipFlags=-q

:: ...and some misc stuff
if /i "%verbosity%"   == "info" set pipFlags=
if /i "%techniColor%" == "true" call :setESC


:: -----------------------------------------------------------------------------------------------
:: 1. Download assets

:: move down 8 lines so the download/unzip progress doesn't hide the output.'
call :WriteLine White ""
call :WriteLine White ""
call :WriteLine White "========================================================================"
call :WriteLine White ""
call :WriteLine White "                 CodeProject SenseAI Installer"
call :WriteLine White ""
call :WriteLine White "========================================================================"
call :WriteLine White ""

:: For downloading assets
if exist %downloadDir%\%pythonDir% (
    call :Write White "Checking Python interpreter..."
    call :WriteLine Green "Present"
) else (
    call :Download %storageUrl% %downloadDir%\ python37.zip %pythonDir%  ^
         "Downloading Python interpreter..."
)
if exist %downloadDir%\%modelsDir% (
    call :Write White "Checking AI Models..."
    call :WriteLine Green "Present"
) else (
    call :Download %storageUrl% %downloadDir%\ models.zip %modelsDir% ^
         "Downloading AI Models..."
)


:: -----------------------------------------------------------------------------------------------
:: 2. Setup Python environment

call :Write White "Creating a Python virtual environment..."

:: Create the Virtual Environment
%cd%\%downloadDir%\%pythonDir%\python -m venv AnalysisLayer\venv

:: Activate the Virtual Environment (so we can install packages)
set VIRTUAL_ENV=%cd%\AnalysisLayer\venv\Scripts

if not defined PROMPT set PROMPT=$P$G
set PROMPT=(venv) !PROMPT!

set PYTHONHOME=
set PATH=%VIRTUAL_ENV%;%PATH%
call :WriteLine Green "Done"

:: Install required packages
if not exist AnalysisLayer\venv\Lib\site-packages\torch (
    call :WriteLine White "Installing required Python packages. This could take a while"

    call :Write White "  - Installing Python package manager..."
    if /i "%verbosity%" == "quiet" (
        python -m pip install --trusted-host pypi.python.org --trusted-host files.pythonhosted.org ^
                                        --trusted-host pypi.org --upgrade pip !pipFlags! >nul 2>nul 
    ) else (
        python -m pip install --trusted-host pypi.python.org --trusted-host files.pythonhosted.org ^
                                        --trusted-host pypi.org --upgrade pip !pipFlags!
    )

    call :WriteLine Green "Done"

    REM This is the easy way, but doesn't provide any feedback on what's going on
    REM pip install -r %deepStackPath%\%intelligenceDir%\requirements.txt !pipFlags!
    REM call :WriteLine Green "Success"

    REM We'll do this the long way so we can see some progress
    set currentOption=
    for /f "tokens=*" %%x in (AnalysisLayer\IntelligenceLayer\requirements.txt) do (

        set line=%%x

        if "!line!" == "" (
            set currentOption=
        ) else if "!line:~0,2!" == "##" (
            set currentOption=
        ) else if "!line:~0,12!" == "--find-links" (
            set currentOption=!line!
        ) else (
            call :Write White "    PIP: Installing !line!..."
            if /i "%verbosity%" == "quiet" (
                python.exe -m pip install !line! !currentOption! !pipFlags! >nul 2>nul 
            ) else (
                python.exe -m pip install !line! !currentOption! !pipFlags!
            )
            call :WriteLine Green "Done"

            set currentOption=
        )
    )
    call :WriteLine Green "All packages installed."
) else (
    call :WriteLine Green "Python packages already present."
)

:: -----------------------------------------------------------------------------------------------
:: 3. All done!

call :WriteLine Green ""
call :WriteLine Green ""
call :WriteLine Green "Setup Complete"
call :Write Green "Run"
call :Write Yellow " Start_SenseAI_Win.bat"
call :WriteLine Green " to start the server."
start "" ./Welcome.html


goto:eof



:: ===============================================================================================
:: ===============================================================================================


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


:Download
    SetLocal EnableDelayedExpansion

    REM "https://codeproject-ai.s3.ca-central-1.amazonaws.com/sense/installer/"
    set storageUrl=%1 

    REM "downloads/" - relative to the current directory
    set downloadToDir=%2

    REM eg packages_for_gpu.zip
    set fileToGet=%3

    REM  eg packages
    set dirToSave=%4

    REM output message
    set message=%5

    if "!message!" == "" set message="Downloading !fileToGet!..."

    REM Doesn't provide progress as %
    REM powershell Invoke-WebRequest -Uri !storageUrl: =!!fileToGet! -OutFile !downloadDir!!dirToSave!.zip

    call :Write White !message!
    powershell Start-BitsTransfer -Source !storageUrl: =!!fileToGet! ^
                                  -Destination !downloadToDir!!dirToSave!.zip
    if errorlevel 1 (
        call :WriteLine Red "An error occurred that could not be resolved."
        exit /b
    )

    if not exist !downloadToDir!!dirToSave!.zip (
        call :WriteLine Red "An error occurred that could not be resolved."
        exit /b
    )

    call :Write White "Expanding..."

    REM Try tar first. If that doesn't work, fall back to pwershell (slow)
    set tarExists=true
    pushd !downloadToDir!
    mkdir !dirToSave!
    copy !dirToSave!.zip !dirToSave! > nul 2>nul
    pushd !dirToSave!
    tar -xf !dirToSave!.zip > nul 2>nul
    if "%errorlevel%" == "9009" set tarExists=false
    rm !dirToSave!.zip > nul 2>nul
    popd
    popd

    if "!tarExists!" == "false" (
        powershell Expand-Archive -Path !downloadToDir!!dirToSave!.zip -DestinationPath !downloadToDir! -Force
    )

    del /s /f /q !downloadToDir!!dirToSave!.zip > nul

    call :WriteLine Green "Done."

    exit /b
