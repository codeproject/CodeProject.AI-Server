:: CodeProject.AI Server 
::
:: Install script for Windows
::
:: This script can be called in 2 ways:
::
::   1. From within the /src directory in order to setup the Development
::      environment.
::   2. From within an analysis module directory to setup just that module.
::
:: If called from within /src, then all analysis modules (in modules/ and
:: modules/ dirs) will be setup in turn, as well as the main SDK and demos.
::
:: If called from within a module's dir then we assume we're in the 
:: /src/modules/ModuleId directory (or modules/ModuleId in Production) for the
:: module "ModuleId". This script would typically be called via
::
::    ..\..\setup.bat
:: 
:: To toggle GPU and CUDA support (both true by default) use
::
::    ..\..\setup.bat [ enableGPU=(true|false) ] [ supportCUDA=(true|false) ]
::
:: This script will look for a install.bat script in the directory from whence
:: it was called, and execute that script. The install.bat script is
:: responsible for everything needed to ensure the module is ready to run.
::
:: HOW DOES THE SCRIPT KNOW WHICH MODE IT'S IN?
:: If the parent dir of directory from where this script was called is "src"
:: then we know we're in development mode, and we know we're not in the modules
:: or anaylysislayer folder, so we must be calling it to setup the main dev env.


@echo off
REM cls
setlocal enabledelayedexpansion
REM Set CodePage UTF-8 for our emojis
chcp 65001 >NUL

:: verbosity can be: quiet | info | loud
set verbosity=quiet

:: Should we use GPU enabled libraries? If true, then any requirements.gpu.txt 
:: python packages will be used if available, with a fallback to requirements.txt.
:: This allows us the change to use libraries that may support GPUs if the
:: hardware is present, but with the understanding that if there's no suitable
:: hardware the libraries must still work on CPU. Setting this to false means
:: do not load libraries that provide potential GPU support.
set enableGPU=true

:: Are we ready to support CUDA enabled GPUs? Setting this to true allows us to
:: test if there is CUDA enabled hardware, and if so, to request the 
:: requirements.cuda.txt python packages be installed, with a fallback to 
:: requirements.gpu.txt, then requirements.txt. 
:: DANGER: There is no assumption that the CUDA packages will work if there's 
:: no CUDA hardware. 
:: NOTE: CUDA packages will ONLY be installed used if CUDA hardware is found. 
::       Setting this to false means do not load libraries that provide potential
::       CUDA support.
:: NOTE: enableGPU must be true for this flag to work
set supportCUDA=true

:: Show output in wild, crazy colours
set useColor=true

:: Width of lines
set lineWidth=70

:: Whether or not modules can have their Python setup installed in the shared area
set allowSharedPythonInstallsForModules=true


:: Debug flags for downloads and installs

:: If files are already present, then don't overwrite if this is false
set forceOverwrite=false

:: If bandwidth is extremely limited, or you are actually offline, set this as true to
:: force all downloads to be retrieved from cached downloads. If the cached download
:: doesn't exist the install will fail.
set offlineInstall=false

REM For speeding up debugging
set skipPipInstall=false

REM Whether or not to install all python packages in one step (-r requirements.txt)
REM or step by step. Doing this allows the PIP manager to handle incompatibilities 
REM better.
REM ** WARNING ** There is a big tradeoff on keeping the users informed and speed/
REM reliability. Generally one-step shouldn't be needed. But it often is. And it
REM often doesn't actually solve problems either. Overall it's safer, but not a 
REM panacea
set oneStepPIP=false


:: Basic locations

:: The path to the directory containing the install scripts. Will end in "\"
set installerScriptsPath=%~dp0

:: The location of large packages that need to be downloaded (eg an AWS S3 bucket name)
set storageUrl=https://codeproject-ai.s3.ca-central-1.amazonaws.com/sense/installer/dev/

:: The name of the source directory
set srcDir=src

:: The name of the dir, within the current directory, where install assets will
:: be downloaded
set downloadDir=downloads

:: The name of the dir holding the installed runtimes
set runtimesDir=runtimes

:: The name of the dir holding the downloaded/sideloaded backend analysis services
set modulesDir=modules


:: Override some values via parameters ::::::::::::::::::::::::::::::::::::::::

:param_loop
    set arg_name=%~1
    set arg_value=%~2
    if not "!arg_name!" == "" (
        if not "!arg_name:enableGPU=!" == "!arg_name!" (
            if /i "!arg_value!" == "true" ( 
                set enableGPU=true
            ) else ( 
                set enableGPU=false 
            )
        )

        if not "!arg_name:supportCUDA=!" == "!arg_name!" (
            if /i "!arg_value!" == "true" ( 
                set supportCUDA=true
            ) else ( 
                set supportCUDA=false 
            )
        )

        if not "!arg_name:--no-color=!" == "!arg_name!" set useColor=false

        REM if not "!arg_name:pathToInstall=!" == "!arg_name!" set installerScriptsPath=!arg_value!
    )
    shift
    shift
if not "!arg_name!"=="" goto param_loop


:: Pre-setup ::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::

:: If offline then force the system to use pre-downloaded files
if /i "%offlineInstall%" == "true" set forceOverwrite=false


:: Execution environment, setup mode and Paths ::::::::::::::::::::::::::::::::

:: If we're calling this script from the /src folder directly (and the /src
:: folder actually exists) then we're Setting up the dev environment. Otherwise
:: we're installing a module.
set setupMode=InstallModule
for /f "delims=\" %%a in ("%cd%") do @set CurrDirName=%%~nxa
if /i "%CurrDirName%" == "%srcDir%" set setupMode=SetupDevEnvironment

:: In Development, this script is in the /src folder. In Production there is no
:: /src folder; everything is in the root folder. So: go to the folder
:: containing this script and check the name of the parent folder to see if
:: we're in dev or production.
pushd "!installerScriptsPath!"
for /f "delims=\" %%a in ("%cd%") do @set CurrDirName=%%~nxa
popd
set executionEnvironment=Production
if /i "%CurrDirName%" == "%srcDir%" set executionEnvironment=Development

:: The absolute path to the installer script and the root directory. Note that
:: this script (and the SDK folder) is either in the /src dir or the root dir
pushd "!installerScriptsPath!"
set sdkPath=%cd%\SDK
set sdkScriptsPath=%cd%\SDK\Scripts
if /i "%executionEnvironment%" == "Development" cd ..
set absoluteRootDir=%cd%
popd

set absoluteAppRootDir=!installerScriptsPath!

:: Helper vars for OS, Platform (see note below), and system name. systemName is
:: a no-op here because nothing exciting happens on Windows. In the corresponding
:: .sh setup files, systemName can be docker, Raspberry Pi, WSL - all sorts of fun
:: things. It's here to just make switching between .bat and .sh scripts consistent

set os=windows
set platform=windows
set systemName=Windows

:: This can be x86 (32-bit), AMD64 (Intel/AMD 64bit), ARM64 (Arm 64bit)
set architecture=%PROCESSOR_ARCHITECTURE%

:: A NOTE ON PLATFORM.
:: We use the full "x86_64" for architecture, but follow the common convention
:: of abbreviating this to "x64" when used in conjuntion with OS. So windows-x64
:: rather than windows-x86_64. To simplify further, if the platform value doesn't
:: have a suffix then it's assumed to be -x64. This may change in the future.
if /i "!architecture!" == "amd64" set architecture=x86_64
if /i "!architecture!" == "ARM64" (
    set architecture=arm64
    set platform=windows-arm64
)

:: GPU / CPU support ::::::::::::::::::::::::::::::::::::::::::::::::::::::::::

set hasCUDA=false
if /i "!enableGPU!" == "true" (
    if /i "!supportCUDA!" == "true" (
        REM wmic PATH Win32_VideoController get Name | findstr /I /C:"NVIDIA" >NUL 2>&1
        where nvidia-smi >nul 2>nul
        if !errorlevel! EQU 0 set hasCUDA=true
    )
)
if /i "!hasCUDA!" == "false" set supportCUDA=false

set hasROCm=false
if /i "!enableGPU!" == "true" (
    where rocm-smi >nul 2>nul
    if !errorlevel! EQU 0 set hasROCm=true
)


:: The location of directories relative to the root of the solution directory
set runtimesPath=!absoluteAppRootDir!!runtimesDir!
set modulesPath=!absoluteAppRootDir!!modulesDir!
set downloadPath=!absoluteAppRootDir!!downloadDir!

:: Let's go
if /i "!useColor!" == "true" call "!sdkScriptsPath!\utils.bat" setESC
if /i "!setupMode!" == "SetupDevEnvironment" (
    set scriptTitle=          Setting up CodeProject.AI Development Environment
) else (
    set scriptTitle=             Installing CodeProject.AI Analysis Module
)

call "!sdkScriptsPath!\utils.bat" WriteLine 
call "!sdkScriptsPath!\utils.bat" WriteLine "!scriptTitle!" "DarkYellow" "Default" !lineWidth!
call "!sdkScriptsPath!\utils.bat" WriteLine 
call "!sdkScriptsPath!\utils.bat" WriteLine "========================================================================" "DarkGreen" 
call "!sdkScriptsPath!\utils.bat" WriteLine 
call "!sdkScriptsPath!\utils.bat" WriteLine "                   CodeProject.AI Installer                             " "DarkGreen" 
call "!sdkScriptsPath!\utils.bat" WriteLine 
call "!sdkScriptsPath!\utils.bat" WriteLine "========================================================================" "DarkGreen" 
call "!sdkScriptsPath!\utils.bat" WriteLine 


if /i "%verbosity%" neq "quiet" (
    call "!sdkScriptsPath!\utils.bat" WriteLine 
    call "!sdkScriptsPath!\utils.bat" WriteLine "setupMode             = !setupMode!"             !color_mute!
    call "!sdkScriptsPath!\utils.bat" WriteLine "executionEnvironment  = !executionEnvironment!"  !color_mute!
    call "!sdkScriptsPath!\utils.bat" WriteLine "installerScriptsPath  = !installerScriptsPath!"  !color_mute!
    call "!sdkScriptsPath!\utils.bat" WriteLine "sdkScriptsPath        = !sdkScriptsPath!"        !color_mute!
    call "!sdkScriptsPath!\utils.bat" WriteLine "absoluteAppRootDir    = !absoluteAppRootDir!"    !color_mute!
    call "!sdkScriptsPath!\utils.bat" WriteLine "runtimesPath          = !runtimesPath!"          !color_mute!
    call "!sdkScriptsPath!\utils.bat" WriteLine "modulesPath           = !modulesPath!"           !color_mute!
    call "!sdkScriptsPath!\utils.bat" WriteLine "downloadPath          = !downloadPath!"          !color_mute!
    call "!sdkScriptsPath!\utils.bat" WriteLine
)


:: Checks on GPU ability

call "!sdkScriptsPath!\utils.bat" WriteLine "Checking GPU support" "White" "Blue" !lineWidth!
call "!sdkScriptsPath!\utils.bat" WriteLine ""

call "!sdkScriptsPath!\utils.bat" Write "CUDA Present..."
if /i "%hasCUDA%" == "true" (
    call "!sdkScriptsPath!\utils.bat" WriteLine "True" !color_success!
) else (
    call "!sdkScriptsPath!\utils.bat" WriteLine "False" !color_warn!
)
call "!sdkScriptsPath!\utils.bat" Write "Allowing GPU Support: "
if "!enableGPU!" == "true" (call "!sdkScriptsPath!\utils.bat" WriteLine "Yes" !color_success!) else (call "!sdkScriptsPath!\utils.bat" WriteLine "No" !color_warn!)
call "!sdkScriptsPath!\utils.bat" Write "Allowing CUDA Support: "
if "!supportCUDA!" == "true" (call "!sdkScriptsPath!\utils.bat" WriteLine "Yes" !color_success!) else (call "!sdkScriptsPath!\utils.bat" WriteLine "No" !color_warn!)


:: Ensure directories are created and download required assets.

call "!sdkScriptsPath!\utils.bat" WriteLine
call "!sdkScriptsPath!\utils.bat" WriteLine "General CodeProject.AI setup" "White" "Blue" !lineWidth!
call "!sdkScriptsPath!\utils.bat" WriteLine

call "!sdkScriptsPath!\utils.bat" Write "Creating Directories..."
if not exist "!downloadPath!\" mkdir "!downloadPath!"
if not exist "!runtimesPath!\" mkdir "!runtimesPath!"
call "!sdkScriptsPath!\utils.bat" WriteLine "Done" "Green"

:: And off we go...

set success=true

REM Start with the core SDK
call "!sdkScriptsPath!\utils.bat" WriteLine
call "!sdkScriptsPath!\utils.bat" WriteLine "Processing Core SDK" "White" "Blue" !lineWidth!
call "!sdkScriptsPath!\utils.bat" WriteLine
set moduleDir=SDK
set modulePath=!absoluteAppRootDir!!moduleDir!
call "!modulePath!\install.bat" install
if errorlevel 1 set success=false


if /i "!setupMode!" == "SetupDevEnvironment" (

    REM  Walk through the modules directory and call the setup script in each dir
    REM  TODO: This should be just a simple for /d %%D in ("!modulesPath!") do (
    for /f "delims=" %%D in ('dir /a:d /b "!modulesPath!"') do (

        set moduleDir=%%~nxD
        set modulePath=!modulesPath!\!moduleDir!

        set pythonVersion=

        REM Read the module version from the modulesettings.json file 
        REM TODO: Get the module name, runtime location and python version from the modulesettings.
        set moduleVersion=
        if exist "!modulePath!\modulesettings.json" (
            call "!sdkScriptsPath!\utils.bat" GetValueFromModuleSettings "!modulePath!\modulesettings.json", "Version", moduleVersion
            REM call "!sdkScriptsPath!\utils.bat" GetValueFromModuleSettings "!modulePath!\modulesettings.json", "Runtime", runtime
            call "!sdkScriptsPath!\utils.bat" GetValueFromModuleSettings "!modulePath!\modulesettings.json", "RuntimeLocation", runtimeLocation
        )
        
        if exist "!modulePath!\install.bat" (

            set announcement=Processing module !moduleDir! !moduleVersion!
            call "!sdkScriptsPath!\utils.bat" WriteLine
            call "!sdkScriptsPath!\utils.bat" WriteLine "!announcement!" "White" "Blue" !lineWidth!
            call "!sdkScriptsPath!\utils.bat" WriteLine

            REM Install module
            call "!modulePath!\install.bat" install
            if errorlevel 1 set success=false

            REM If a python version has been specified then we'll automatically look for, and
            REM install, the requirements file for the module, and then also the requirements 
            REM file for the SDK since it'll be assumed the Python SDK will come into play.
            if "!pythonVersion!" neq "" (
                call "!sdkScriptsPath!\utils.bat" WriteLine "Installing Python packages for !moduleDir!" !color_info!
                call "%sdkScriptsPath%\utils.bat" InstallPythonPackages 

                call "!sdkScriptsPath!\utils.bat" WriteLine "Installing Python packages for the CodeProject.AI Server SDK" !color_info!
                call "%sdkScriptsPath%\utils.bat" InstallPythonPackages "%sdkPath%\Python"
            )
        )
    )

    call "!sdkScriptsPath!\utils.bat" WriteLine
    call "!sdkScriptsPath!\utils.bat" WriteLine "Module setup Complete" "Green"

    REM Setup Demos
    call "!sdkScriptsPath!\utils.bat" WriteLine
    call "!sdkScriptsPath!\utils.bat" WriteLine "Processing Demos" "White" "Blue" !lineWidth!
    call "!sdkScriptsPath!\utils.bat" WriteLine

    set moduleDir=demos
    set modulePath=!absoluteRootDir!\!moduleDir!
    call "!modulePath!\install.bat" install
    if errorlevel 1 set success=false

) else (

    REM Install an individual module

    for %%I in (.) do set moduleDir=%%~nxI
    set modulePath=%cd%

    set pythonVersion=

    REM Read the module version from the modulesettings.json file
    set moduleVersion=
    if exist "!modulePath!\modulesettings.json" (
        call "!sdkScriptsPath!\utils.bat" GetValueFromModuleSettings "!modulePath!\modulesettings.json", "Version", moduleVersion
    )

    if exist "!modulePath!\install.bat" (

        set announcement=Installing module !moduleDir! !moduleVersion!
        call "!sdkScriptsPath!\utils.bat" WriteLine
        call "!sdkScriptsPath!\utils.bat" WriteLine "!announcement!" "White" "Blue" !lineWidth!
        call "!sdkScriptsPath!\utils.bat" WriteLine

        REM Install module
        call "!modulePath!\install.bat" install
        if errorlevel 1 set success=false

        REM If a python version has been specified then we'll automatically look for, and
        REM install, the requirements file for the module, and then also the requirements 
        REM file for the SDK since it'll be assumed the Python SDK will come into play.
        if "!pythonVersion!" neq "" (
            call "!sdkScriptsPath!\utils.bat" WriteLine "Installing Python packages for !moduleDir!" !color_info!
            call "%sdkScriptsPath%\utils.bat" InstallPythonPackages 

            call "!sdkScriptsPath!\utils.bat" WriteLine "Installing Python packages for the CodeProject.AI Server SDK" !color_info!
            call "%sdkScriptsPath%\utils.bat" InstallPythonPackages "%sdkPath%\Python"
        )
    )
)

call "!sdkScriptsPath!\utils.bat" WriteLine
call "!sdkScriptsPath!\utils.bat" WriteLine "Setup complete" "White" "DarkGreen" !lineWidth!
call "!sdkScriptsPath!\utils.bat" WriteLine

if /i "!success!" == "false" exit /b 1

exit /b 0