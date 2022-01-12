:: ============================================================================
::
:: CodeProject SenseAI Server Installation Setup script 
::
:: Run this to SETUP CodeProject SenseAI
::
:: WELCOME TO CODEPROJECT SENSEAI!
:: 
:: Please ensure this script is run before you start exploring and integrating
:: CodeProject.SenseAI. This script will download the required pieces, move
:: them into place, run the setup commands and get you on your way.
::
:: Grab a coffee because it could take a while. But it'll be worth it.
::
:: ============================================================================


@echo off
cls
SETLOCAL EnableDelayedExpansion

:: ----------------------------------------------------------------------------
:: 0. Script settings.

:: The location of large packages that need to be downloaded
::  a. From AWS
set storageUrl=https://codeproject-ai.s3.ca-central-1.amazonaws.com/sense/installer/
::  b. From contrary GCP
rem    set storageUrl=https://storage.googleapis.com/codeproject-senseai/
::  c. Use a local directory rather than from online. Handy for debugging.
rem    set storageUrl=C:\Dev\CodeProject\CodeProject.AI\install\cached_downloads\

:: The name of the source directory
set srcDir=src

:: The name of the dir holding the backend analysis services
set analysisLayerDir=AnalysisLayer

:: The name of the dir holding the DeepStack analysis services
set deepstackDir=DeepStack

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


:: ----------------------------------------------------------------------------
:: 1. Download assets

:: move down 8 lines so the download/unzip progress doesn't hide the output.'
call :WriteLine Yellow "Setting up CodeProject.SenseAI" 
call :WriteLine White ""
call :WriteLine White "========================================================================"
call :WriteLine White ""
call :WriteLine White "                 CodeProject SenseAI Installer"
call :WriteLine White ""
call :WriteLine White "========================================================================"
call :WriteLine White ""

set analysisPath=%srcDir%\%analysisLayerDir%
set deepStackPath=%cd%\%analysisPath%\DeepStack

:: Warn the user about potential download size
set /a downloadSize=0
if not exist "%deepStackPath%\%pythonDir%" set /a downloadSize=downloadSize+25
if not exist "%deepStackPath%\%modelsDir%" set /a downloadSize=downloadSize+550

if !downloadSize! gtr 0 (
	choice /M "To continue I need to download !downloadSize!Mb of files. Is this OK"
	if errorlevel 2 goto:eof
)

:: Download required assets if needed
if exist "%deepStackPath%\%pythonDir%" (
    call :Write White "Checking Python interpreter..."
    call :WriteLine Green "Present"
) else (
    call :Download "%storageUrl%" "%deepStackPath%\" python37.zip "%pythonDir%"  ^
         "Downloading Python interpreter..."
)
if exist "%deepStackPath%\%modelsDir%" (
    call :Write White "Checking AI Models..."
    call :WriteLine Green "Present"
) else (
    call :Download "%storageUrl%" "%deepStackPath%\" models.zip "%modelsDir%" ^
         "Downloading AI Models..."
)


:: ----------------------------------------------------------------------------
:: 2. Setup Python environment

call :Write White "Creating a Python virtual environment..."

:: Create the Virtual Environment
"%deepStackPath%\%pythonDir%\python" -m venv "%deepStackPath%\venv"

:: Activate the Virtual Environment (so we can install packages)
set VIRTUAL_ENV=%deepStackPath%\venv\Scripts

if not defined PROMPT set PROMPT=$P$G
set PROMPT=(venv) !PROMPT!

set PYTHONHOME=
set PATH=%VIRTUAL_ENV%;%PATH%
call :WriteLine Green "Done"

:: Install required packages
if not exist "%deepStackPath%\venv\Lib\site-packages\torch" (
    call :WriteLine White "Installing required Python packages."

    call :Write White "  - Installing Python package manager..."
    if /i "%verbosity%" == "quiet" (
        python -m pip install --trusted-host pypi.python.org ^
                              --trusted-host files.pythonhosted.org ^
                              --trusted-host pypi.org --upgrade pip !pipFlags! >nul 2>nul 
    ) else (
        python -m pip install --trusted-host pypi.python.org ^
                              --trusted-host files.pythonhosted.org ^
                              --trusted-host pypi.org --upgrade pip !pipFlags!
    )

    call :WriteLine Green "Done"

    REM This is the easy way, but doesn't provide any feedback
    REM pip install -r %deepStackPath%\%intelligenceDir%\requirements.txt !pipFlags!
    REM call :WriteLine Green "Success"

    REM We'll do this the long way so we can see some progress
    set currentOption=

    REM for the odd syntax see https://stackoverflow.com/a/22636725
    REM for /f "tokens=*" %%x in (""%deepStackPath%\IntelligenceLayer\requirements.txt"") do (

    for /f "tokens=*" %%x in (' more ^< "%deepStackPath%\IntelligenceLayer\requirements.txt" ') do (

        set line=%%x

        if "!line!" == "" (
            set currentOption=
        ) else if "!line:~0,2!" == "##" (
            set currentOption=
        ) else if "!line:~0,12!" == "--find-links" (
            set currentOption=!line!
        ) else (
           
            set module=!line!
            for /F "tokens=1,2 delims=#" %%a in ("!line!") do (
                set module=%%a
                set description=%%b
            )

            if "!description!" == "" set description=Installing !module!
            if "!module!" NEQ "" (
                call :Write White "  -!description!..."

                if /i "%verbosity%" == "quiet" (
                    python.exe -m pip install !module! !currentOption! !pipFlags! >nul 2>nul 
                ) else (
                    python.exe -m pip install !module! !currentOption! !pipFlags!
                )

                call :WriteLine Green "Done"
            )

            set currentOption=
        )
    )
    call :WriteLine Green "All packages installed."
) else (
    call :WriteLine Green "Python packages already present."
)

:: ----------------------------------------------------------------------------
:: 3. All done!

call :WriteLine Green ""
call :WriteLine Green ""
call :Write Green "Setup Complete. Please Run"
call :Write Yellow " Start_SenseAI_Win.bat"
call :WriteLine Green " to start the server."
rem start "" ./Welcome.html


goto:eof



:: ============================================================================
:: ============================================================================


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


:Download
    SetLocal EnableDelayedExpansion

    REM "https://codeproject-ai.s3.ca-central-1.amazonaws.com/sense/installer/"
    set storageUrl=%1
    set storageUrl=!storageUrl:"=!

    REM "downloads/" - relative to the current directory
    set downloadToDir=%2
    set downloadToDir=!downloadToDir:"=!

    REM eg packages_for_gpu.zip
    set fileToGet=%3
    set fileToGet=!fileToGet:"=!

    REM  eg packages
    set dirToSave=%4
    set dirToSave=!dirToSave:"=!

    REm output message
    set message=%5
    set message=!message:"=!

    if "!message!" == "" set message=Downloading !fileToGet!...

    REM Doesn't provide progress as %
    REM powershell Invoke-WebRequest -Uri !storageUrl: =!!fileToGet! ^
    REM                              -OutFile !downloadDir!!dirToSave!.zip

    call :Write White "!message!"

    powershell Start-BitsTransfer -Source "!storageUrl!!fileToGet!" ^
                                  -Destination "!downloadToDir!!dirToSave!.zip"
    if errorlevel 1 (
        call :WriteLine Red "An error occurred that could not be resolved."
        exit /b
    )

    if not exist "!downloadToDir!!dirToSave!.zip" (
        call :WriteLine Red "An error occurred that could not be resolved."
        exit /b
    )

    call :Write Yellow "Expanding..."

    REM Try tar first. If that doesn't work, fall back to pwershell (slow)
    set tarExists=true
    pushd "!downloadToDir!"
    mkdir "!dirToSave!"
    copy "!dirToSave!.zip" "!dirToSave!" > nul 2>nul
    pushd "!dirToSave!"
    tar -xf "!dirToSave!.zip" > nul 2>nul
    if "%errorlevel%" == "9009" set tarExists=false
    rm "!dirToSave!.zip" > nul 2>nul
    popd
    popd

    if "!tarExists!" == "false" (
        powershell Expand-Archive -Path "!downloadToDir!!dirToSave!.zip" ^
                                  -DestinationPath "!downloadToDir!" -Force
    )

    del /s /f /q "!downloadToDir!!dirToSave!.zip" > nul

    call :WriteLine Green "Done."

    exit /b
