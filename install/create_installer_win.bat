:: ============================================================================
::
:: CodeProject SenseAI Server create installer script. 
::
:: This script will build a directory full of everything needed to run
:: CodeProject.SenseAI. 
::
:: We assume we're in the source code /install directory.
::
:: Copyright CodeProject 2021
::
:: ============================================================================

@echo off
cls
setlocal enabledelayedexpansion


:: ----------------------------------------------------------------------------
:: 0. Script settings.

:: The location of the installation directory that will be created
set installationDir=c:\CodeProject.SenseAI.Package

:: The location of the installation package that will be created
set installationPackage=c:\CodeProject.SenseAI.Package.zip

:: The location of the solution root directory relative to this script
cd ..
set rootPath=%cd%

:: Whether or not to remove any existing installation directory
set cleanInstall=true

:: Whether or not to compress the final installation package. Currently fails
:: due to access denied error
set compressInstallation=true

:: Whether or not to remove the installation directory after it's been compressed
set removeInstallationFolder=false

:: verbosity can be: quiet | info | loud
set verbosity=quiet

:: Show output in wild, crazy colours
set techniColor=true

:: SenseAI specific :::::::::::::::::::::::::::::::::::::::::::::::::::::::::::

:: The name of the dir holding the frontend API server
set senseAPIDir=API

:: The name of the startup settings file
set settingsFile=CodeProject.SenseAI.json

:: .NET build configuration: [Debug | Release]
set config=Release

:: Where to put the Builds
set buildOutputDir=bin\InstallPackage

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

:: The name of the Environment variable setup file
set envVariablesFile=set_environment.bat

:: Shared :::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::

:: The name of the dir, within the current directory, where install assets will
:: be downloaded
set downloadDir=downloads

:: The name of the dir containing the Python interpreter
set pythonDir=python37

:: The name of the source directory
set srcDir=src

:: The name of the dir holding the backend analysis services
set analysisLayerDir=AnalysisLayer

:: The name of the demos directory
set demoDir=demos

:: For Debug
rem set cleanInstall=false
rem set compressInstallation=false
rem set verbosity=info

:: ----------------------------------------------------------------------------
:: Set Flags and misc.

set roboCopyFlags=/NFL /NDL /NJH /NJS /nc /ns  >nul 2>nul 
set dotnetFlags=q

if "%verbosity%"=="info" set dotnetFlags=m
if "%verbosity%"=="loud" set roboCopyFlags=/NFL /NDL /nc /ns 
if "%verbosity%"=="loud" set dotnetFlags=n

if /i "%techniColor%" == "true" call :setESC

:: ----------------------------------------------------------------------------
:: 1. Ensure correct permissions before we start

:: Only need elevated permissions if we are compressing the install
if /i "%compressInstallation%" NEQ "true" goto hasCorrectPermissions

:: We're compressing, so let's ensure we have permissions to do this
call :Write DarkYellow "Checking administrative privileges..."

set hasAdmin=true
for /f "tokens=*" %%O in ('FSUTIL dirty query %SystemDrive% 2^> nul') do (
    set line=%%O
    if "!line:denied=!" NEQ "!line!" set hasAdmin=false
)
if /i "!hasAdmin!" == "true" goto hasCorrectPermissions

:: ----------------------------------------------------------------------------
:: We don't have permissions needed. We can try and restart this script with
:: correct permissions, or we can just tell the user to restart in admin mode.
:: The latter is way less hassle.

set attemptRestart=false

if /i "%attemptRestart%" == "true" (
    REM Get the details of this script so we can relaunch it
    Set _batchFile=%~f0
    Set _Args=%*

    REM remove any quotes and add them back later (VBScript needs quotes doubled)
    Set _batchFile=!_batchFile:"=!

    REM Create and run a temporary VBScript to elevate this batch file
    del /s /f /q "%temp%\~ElevateMe.vbs" > nul
    (
        Echo Dim UAC : Set UAC = CreateObject^("Shell.Application"^)
        Echo UAC.ShellExecute "cmd", "/c ""!_batchFile! !_Args!"" ", "", "runas", 1
    ) > "%temp%\~ElevateMe.vbs"
    more "%temp%\~ElevateMe.vbs"
    cscript "%temp%\~ElevateMe.vbs" 
    Exit /B
) else (
    call :WriteLine Red "Insufficient privliges"
    call :WriteLine DarkYellow "To compress the installation package please restart this script in admin mode"
    goto:eof
)

:hasCorrectPermissions
call :WriteLine Green "Success"


:: ============================================================================
:: 2. Do the heavy lifting of ensuring the dev environment is setup so we can 
::    just do a bunch of copies

pushd install
call setup_dev_env_win.bat %techniColor%
popd
if errorlevel 1 goto:eof

call :WriteLine Yellow "Creating CodeProject.SenseAI Installation Package" 


:: ============================================================================
:: 3. Remove the install folder if it exists

if /i "!cleanInstall!" == "true" (
    call :Write White "Ensuring the installation folder is reset and clean..."
    if exist "%installationDir%" rd "%installationDir%" /S /Q > NUL
    call :WriteLine Green "Done."
)

:: ============================================================================
:: 4. Ensure directories are created

:: Create some directories
call :Write White "Creating Directories..."

if not exist "%installationDir%"    mkdir "%installationDir%"
if not exist "%installationDir%\%srcDir%\%analysisLayerDir%" mkdir "%installationDir%\%srcDir%\%analysisLayerDir%"

:: For CodeProject.SenseAI
set senseAIInstallPath=%installationDir%\%srcDir%\%senseAPIDir%
if not exist "%senseAIInstallPath%" mkdir "%senseAIInstallPath%"

:: For DeepStack
:: Deepstack gets copied over in one fell swoop. No need to create dirs

call :WriteLine Green "Done"


:: ============================================================================
:: 5. Copy over the server code, models and virtual environment

:: For CodeProject.SenseAI

:: Build server
call :WriteLine White "Building API Server [%config%]..."
set serverPath=%rootPath%\%srcDir%\%senseAPIDir%\Server\FrontEnd
pushd "%serverPath%"

REM Note: The carrot character means "line continuation"
REM For .NET 6 add the "-p:PublishSingleFile=true" parameter to make the output a single file
if /i "%verbosity%"=="quiet" (
    dotnet publish --configuration %config% -r win-x64 --self-contained ^
                   -o "%buildOutputDir%" --nologo --verbosity !dotnetFlags! > nul
) else (
    dotnet publish --configuration %config% -r win-x64 --self-contained ^
                   -o "%buildOutputDir%" --nologo --verbosity !dotnetFlags!
)
popd

:: Copy over
call :Write White "Moving API Server to installation folder..."
if /i "%verbosity%"=="quiet" (
    robocopy /E "%serverPath%\%buildOutputDir% " ^
             "%installationDir%\%srcDir%\%senseAPIDir%\Server\FrontEnd " ^
             /XF *.pdb !roboCopyFlags! > nul
) else (
    robocopy /E "%serverPath%\%buildOutputDir% " ^
             "%installationDir%\%srcDir%\%senseAPIDir%\Server\FrontEnd " ^
             /XF *.pdb !roboCopyFlags!
)
call :WriteLine Green "Done."

:: For DeepStack
set analysisPath=%rootPath%\%srcDir%\%analysisLayerDir%\

call :Write White "Moving Analysis services to installation folder..."

:: Note that Python and the models will be downloaded by the Setup script, and
:: venv will be generated dynamically by the same script
REM Note the space before the closing "  https://stackoverflow.com/a/30244061
if /i "%verbosity%"=="quiet" (
    robocopy "%analysisPath% " "%installationDir%\%srcDir%\%analysisLayerDir% " ^
             /XD venv "%modelsDir%" "%pythonDir%" /XF faceembedding.db /E !roboCopyFlags! > nul
) else (
    robocopy "%analysisPath% " "%installationDir%\%srcDir%\%analysisLayerDir% " ^
             /XD venv "%modelsDir%" "%pythonDir%" s/XF faceembedding.db /E !roboCopyFlags!
)
call :WriteLine Green "Done."

:: Copy over the setup and startup scripts

call :Write White "Copying over startup files..."
pushd "%rootPath%\install"
copy /Y "Setup_SenseAI_Win.bat" "!installationDir!" >nul 2>nul
copy /Y "Start_SenseAI_Win.bat" "!installationDir!" >nul 2>nul
copy /Y "..\docs\Welcome.html"  "!installationDir!" >nul 2>nul
popd
call :WriteLine Green "Done."

call :Write White "Reloading base installation settings..."
call "!rootPath!\!envVariablesFile!"
call :WriteLine Green "Done"

call :Write White "Updating installation Environment variables..."

:: For CodeProject.SenseAI
set CPSENSEAI_ROOTDIR=!installationDir!
set CPSENSEAI_APPDIR=!srcDir!
set CPSENSEAI_APIDIR=!senseAPIDir!
set CPSENSEAI_ANALYSISDIR=!analysisLayerDir!
set CPSENSEAI_CONFIG=Release
set CPSENSEAI_BUILDSERVER=False
set CPSENSEAI_PRODUCTION=True

:: For DeepStack
set APPDIR=%%CPSENSEAI_ROOTDIR%%\%srcDir%\%analysisLayerDir%\%deepstackDir%\%intelligenceDir%
set MODELS_DIR=%%CPSENSEAI_ROOTDIR%%\%srcDir%\%analysisLayerDir%\%deepstackDir%\%modelsDir%
set DATA_DIR=%%CPSENSEAI_ROOTDIR%%\%srcDir%\%analysisLayerDir%\%deepstackDir%\%datastoreDir%
set TEMP_PATH=%%CPSENSEAI_ROOTDIR%%\%srcDir%\%analysisLayerDir%\%deepstackDir%\%tempstoreDir%
set PORT=5000
set VISION_FACE=True
set VISION_DETECTION=True
set VISION_SCENE=True
pushd install
call save_environment !installationDir!\!envVariablesFile! !installationDir!\!settingsFile!
popd
call :WriteLine Green "Done."


:: ============================================================================
:: 6. Prepare the demos

:: Do we need this given the HTML version has feature parity?
set includeDotnetDemo=false

if /i "%includeDotnetDemo%" == "true" (
    REM Build .NET demo
    call :Write White "Building .NET demo [%config%] ..."
    cd "%rootPath%\%demoDir%\dotNET\CodeProject.SenseAI.Playground"

    if /i "%verbosity%"=="quiet" (
        dotnet publish --configuration %config% -o "%buildOutputDir%" ^
                       -p:PublishSingleFile=true --nologo !dotnetFlags! > nul
    ) else (
        dotnet publish --configuration %config% -o "%buildOutputDir%" ^
                       -p:PublishSingleFile=true --nologo !dotnetFlags!
    )
    call :WriteLine Green "Done."
)

:: Copy demos

call :Write White "Coping demos to installation..."

if not exist "%installationDir%\%demoDir%" mkdir "%installationDir%\%demoDir%" > nul
cd "%installationDir%\%demoDir%"

if /i "%includeDotnetDemo%" == "true" (
    if not exist Playground mkdir Playground
    robocopy /e "%rootPath%\%demoDir%\dotNET\CodeProject.SenseAI.Playground\%buildOutputDir% " ^
                                Playground /XF *.pdb !roboCopyFlags! > nul
)

if not exist Javascript mkdir Javascript
robocopy /e "%rootPath%\%demoDir%\Javascript " Javascript !roboCopyFlags! > nul

call :WriteLine Green "Done."


:: Copy test data

call :Write White "Coping test data to installation..."
if not exist TestData mkdir TestData
robocopy /e "%rootPath%\%demoDir%\TestData" ^
            "%installationDir%\%demoDir%\TestData" !roboCopyFlags! > nul
call :WriteLine Green "Done."


:: ============================================================================
:: 7. Compress the final package if required
if /i "%compressInstallation%" == "true" (

    call :WriteLine White "Compressing installation package..."
    if exist "%installationPackage%" del "%installationPackage%" > nul 2>nul

    REM Try tar first. If that doesn't work, fall back to pwershell (slow)
    set tarExists=true

    if /i "%verbosity%"=="quiet" (
        tar -caf "%installationPackage%" --cd "%installationDir%" *> nul 2>nul
    ) else (
        tar -cvaf "%installationPackage%" --cd "%installationDir%" *"
    )

    if "%errorlevel%" == "9009" set tarExists=false

    if "!tarExists!" == "false" (
        powershell Compress-Archive -Force -Path "%installationDir%\*" ^
                                -DestinationPath "%installationPackage%" ^
                                -CompressionLevel Optimal
    )

    if ErrorLevel 0 (
        if exist "%installationPackage%" (
            if /i "%removeInstallationFolder%" == "true" (
                call :Write White "Removing installation folder..."
                rmdir "%installationDir%" /s /q > nul
                rem del /s /f /q "%installationDir%" > nul
                call :WriteLine Green "Done"
            )
        )
    )
)


:: ============================================================================
:: and we're done.


call :WriteLine Yellow "Installation folder creation complete" 
call :WriteLine White ""
call :WriteLine White ""

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
