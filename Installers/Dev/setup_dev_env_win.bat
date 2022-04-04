:: CodeProject SenseAI Server 
::
:: Windows Development Environment install script
::
:: We assume we're in the source code /install directory.
::

@echo off
cls
setlocal enabledelayedexpansion

:: verbosity can be: quiet | info | loud
set verbosity=quiet

:: If files are already present, then don't overwrite if this is false
set forceOverwrite=false

:: Show output in wild, crazy colours
set techniColor=true

:: Platform can define where things are located
set platform=windows

:: Basic locations

:: The location of the solution root directory relative to this script
set rootPath=../..

:: SenseAI specific :::::::::::::::::::::::::::::::::::::::::::::::::::::::::::

:: The name of the dir holding the frontend API server
set senseAPIDir=API

:: TextSummary specific :::::::::::::::::::::::::::::::::::::::::::::::::::::::

set textSummaryDir=TextSummary

:: DeepStack specific :::::::::::::::::::::::::::::::::::::::::::::::::::::::::

:: The name of the dir holding the DeepStack analysis services
set deepstackDir=DeepStack

:: The name of the dir containing the Python code itself
set intelligenceDir=intelligencelayer

:: The name of the dir containing the AI models themselves
set modelsDir=assets

:: The name of the dir containing persisted DeepStack data
set datastoreDir=datastore

:: The name of the dir containing temporary DeepStack data
set tempstoreDir=tempstore

:: Yolo.Net specific
set yoloNetDir=CodeProject.SenseAI.AnalysisLayer.Yolo
set yoloModelsDir=yoloModels

:: Shared :::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::

:: The location of large packages that need to be downloaded
:: a. From AWS
set storageUrl=https://codeproject-ai.s3.ca-central-1.amazonaws.com/sense/installer/
:: b. Use a local directory rather than from online. Handy for debugging.
rem set storageUrl=C:\Dev\CodeProject\CodeProject.SenseAI\install\cached_downloads\

:: The name of the source directory
set srcDir=src

:: The name of the dir, within the current directory, where install assets will
:: be downloaded
set downloadDir=downloads

:: The name of the dir containing the Python interpreter
set pythonDir=python37

:: The name of the dir holding the backend analysis services
set analysisLayerDir=AnalysisLayer

:: Absolute paths :::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::
:: The absolute path to the root directory of CodeProject.SenseAI
set currentDir=%cd%
cd %rootPath%
set absoluteRootDir=%cd%
cd %currentDir%

:: The location of directories relative to the root of the solution directory
set analysisLayerPath=%absoluteRootDir%\%srcDir%\%analysisLayerDir%
set downloadPath=%absoluteRootDir%\Installers\%downloadDir%

if /i "%1" == "false" set techniColor=false
if /i "%techniColor%" == "true" call :setESC

:: Set Flags

set pipFlags=-q -q
set rmdirFlags=/q
set roboCopyFlags=/NFL /NDL /NJH /NJS /nc /ns  >nul 2>nul

if /i "%verbosity%"=="info" (
    set pipFlags=-q
    set rmdirFlags=/q
    set roboCopyFlags=/NFL /NDL /NJH
)

if /i "%verbosity%"=="loud" (
    set pipFlags=
    set rmdirFlags=
    set roboCopyFlags=
)

call :WriteLine Yellow "Setting up CodeProject.SenseAI Development Environment" 
call :WriteLine White ""
call :WriteLine White "========================================================================"
call :WriteLine White ""
call :WriteLine White "                 CodeProject SenseAI Installer"
call :WriteLine White ""
call :WriteLine White "========================================================================"
call :WriteLine White ""

:: ============================================================================
:: 1. Ensure directories are created and download required assets

:: Create some directories
call :Write White "Creating Directories..."

:: For downloading assets
if not exist "%downloadPath%\" mkdir "%downloadPath%"

:: For Text Summary 
set textSummaryPath=%analysisLayerPath%\%textSummaryDir%

:: For DeepStack
set deepStackPath=%analysisLayerPath%\%deepstackDir%
if not exist "%deepStackPath%\%tempstoreDir%\" mkdir "%deepStackPath%\%tempstoreDir%"
if not exist "%deepStackPath%\%datastoreDir%\" mkdir "%deepStackPath%\%datastoreDir%"

:: For Yolo.NET
set yoloNetPath=%analysisLayerPath%\%yoloNetDir%

call :WriteLine Green "Done"

call :Write White "Downloading utilities and models: "
call :WriteLine Gray "Starting"

set pythonInstallPath=%analysisLayerPath%\bin\%platform%\%pythonDir%

:: Clean up directories to force a re-download if necessary
if /i "%forceOverwrite%" == "true" (

    REM Force Re-download
    if exist "%downloadPath%\%platform%\%pythonDir%" rmdir /s "%rmdirFlags% %downloadPath%\%platform%\%pythonDir%"
    if exist "%downloadPath%\%modelsDir%"            rmdir /s "%rmdirFlags% %downloadPath%\%modelsDir%"
    if exist "%downloadPath%\%yoloModelsDir%"        rmdir /s "%rmdirFlags% %downloadPath%\%yoloModelsDir%"

    REM Force overwrite
    if exist "%pythonInstallPath%"         rmdir /s "%rmdirFlags% %pythonInstallPath%"
    if exist "%deepStackPath%\%modelsDir%" rmdir /s "%rmdirFlags% %deepStackPath%\%modelsDir%"
    if exist "%yoloNetPath%\%modelsDir%"   rmdir /s "%rmdirFlags% %yoloNetPath%\%modelsDir%"
)

:: Download whatever packages are missing 
if not exist "%pythonInstallPath" (
    if not exist "%downloadPath%\%platform%\" mkdir "%downloadPath%\%platform%"
    if not exist "%pythonInstallPath%" (
        call :Download "%storageUrl%" "%downloadPath%\%platform%\" "python37.zip" "%pythonDir%" ^
                       "Downloading Python interpreter..."
        if exist "%downloadPath%\%platform%\%pythonDir%" (
            robocopy /e "%downloadPath%\%platform%\%pythonDir% " "%pythonInstallPath% " !roboCopyFlags! > NUL
        )
    )
)
if not exist "%deepStackPath%\%modelsDir%" (
    call :Download "%storageUrl%" "%downloadPath%\" "models.zip" "%modelsDir%" ^
                   "Downloading models..."
    if exist "%downloadPath%\%modelsDir%" (
        robocopy /e "%downloadPath%\%modelsDir% " "%deepStackPath%\%modelsDir% " !roboCopyFlags! > NUL
    )
)
if not exist "%yoloNetPath%\%modelsDir%" (
    call :Download "%storageUrl%" "%downloadPath%\" "yolonet-models.zip" "%yoloModelsDir%" ^
                   "Downloading Yolo.Net models..."
    if exist %downloadPath%\%yoloModelsDir% (
        robocopy /e "%downloadPath%\%yoloModelsDir% " "%yoloNetPath%\%modelsDir% " !roboCopyFlags! > NUL
    )
)

call :WriteLine "Green" "Modules and models downloaded"

:: Copy over the startup script
:: call :Write White "Copying over startup script..."
:: copy /Y "Start_SenseAI_Win.bat" "!absoluteRootDir!" >nul 2>nul
:: :WriteLine Green "Done."


:: ============================================================================
:: 2. Create & Activate Virtual Environment: DeepStack specific / Python 3.7

call :Write White "Creating Virtual Environment..."
if exist "%pythonInstallPath%\venv" (
    call :WriteLine Green "Already present"
) else (
    "%pythonInstallPath%\python.exe" -m venv "%pythonInstallPath%\venv"
    call :WriteLine Green "Done"
)

call :Write White "Enabling our Virtual Environment..."
pushd "%pythonInstallPath%"

:: set PYTHONHOME="%cd%\venv\Scripts"
set VIRTUAL_ENV=%cd%\venv
set PYTHONHOME=
set PATH=!VIRTUAL_ENV!\Scripts;%PATH%

set pythonInterpreterPath="!VIRTUAL_ENV!\python3"

if not defined PROMPT set PROMPT=$P$G
set PROMPT=(venv) !PROMPT!

popd
call :WriteLine Green "Done"

:: Ensure Python Exists
call :Write White "Checking for Python 3.7..."
python --version | find "3.7" > NUL
if errorlevel 1 goto errorNoPython
call :WriteLine Green "present"

if "%verbosity%"=="loud" where Python


:: ============================================================================
:: 3a. Install PIP packages for Python analysis services

call :Write White "Installing Python package manager..."
python -m pip install --trusted-host pypi.python.org ^
                      --trusted-host files.pythonhosted.org ^
                      --trusted-host pypi.org --upgrade pip !pipFlags!
call :WriteLine Green "Done"

call :Write White "Checking for required packages..."

:: ASSUMPTION: If venv\Lib\site-packages\torch exists then no need to do this
if not exist "!VIRTUAL_ENV!\Lib\site-packages\torch" (

    call :WriteLine Yellow "Installing"

    REM call :Write White "Installing Packages into Virtual Environment..."
    REM pip install -r %deepStackPath%\%intelligenceDir%\requirements.txt !pipFlags!
    REM call :WriteLine Green "Success"

    REM We'll do this the long way so we can see some progress

    set currentOption=
    for /f "tokens=*" %%x in (' more ^< "%deepStackPath%\%intelligenceDir%\requirements.txt" ') do (
        set line=%%x

        if "!line!" == "" (
            set currentOption=
        ) else if "!line:~0,2!" == "##" (
            set currentOption=
        ) else if "!line:~0,12!" == "--find-links" (
            set currentOption=!line!
        ) else (
           
            REM  breakup line into module name and description
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
) else (
    call :WriteLine Green "present."
)

:: ============================================================================
:: 3b. Install PIP packages for TextSummary

call :Write White "Installing required Text Processing packages..."
pip install -r %textSummaryPath%\requirements.txt !pipFlags!
call :WriteLine Green "Success"


:: ============================================================================
:: and we're done.

call :WriteLine Yellow "Development Environment setup complete" 
call :WriteLine White ""
call :WriteLine White ""

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
    SetLocal EnableDelayedExpansion
    set resetColor=!ESC![0m

    set str=%~2
    if "!str!" == "" exit /b 0

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
    call :Write White "!message!"

    if exist "!downloadToDir!!dirToSave!.zip" (
        call :Write Yellow "already exists..."
    ) else (

        REM Doesn't provide progress as % 
        REM powershell Invoke-WebRequest -Uri !storageUrl: =!!fileToGet! ^
        REM                              -OutFile !downloadPath!!dirToSave!.zip

        REM ***
        REM If you get the error "Start-BitsTransfer : A positional parameter cannot 
        REM be found that accepts argument" then ensure paths you supply don't have
        REM trailing spaces
        REM ***
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
    )

    call :Write Yellow "Expanding..."

    REM Try tar first. If that doesn't work, fall back to pwershell (slow)
    set tarExists=true
    pushd "!downloadToDir!"
    if not exist "!dirToSave!" mkdir "!dirToSave!"
    copy "!dirToSave!.zip" "!dirToSave!" > nul 2>nul
    pushd "!dirToSave!"
    tar -xf "!dirToSave!.zip" > nul 2>nul
    if "%errorlevel%" == "9009" set tarExists=false
    del /s /f /q "!dirToSave!.zip" > nul
    popd
    popd

    if "!tarExists!" == "false" (
        powershell Expand-Archive -Path "!downloadToDir!!dirToSave!.zip" ^
                                  -DestinationPath "!downloadToDir!" -Force
    )

    REM del /s /f /q "!downloadToDir!!dirToSave!.zip" > nul

    call :WriteLine Green "Done."

    exit /b


:: Jump points

:errorNoPython
call :WriteLine White ""
call :WriteLine White ""
call :WriteLine White "-------------------------------------------------------"
call :WriteLine Red "Error: Python not installed"
goto:eof
