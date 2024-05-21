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
:: If this script is called from within a module's dir then we assume we're in 
:: the /src/modules/my_module directory (or modules/my_module in Production) for
:: the module "my_module". This script would typically be called via
::
::    ..\..\setup.bat
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

:: Show output in wild, crazy colours
set useColor=true

:: Width of lines
set lineWidth=70

:: Whether or not modules can have their Python setup installed in the shared area
set allowSharedPythonInstallsForModules=true


:: Debug flags for downloads and installs

:: If you wish to allow external modules
set installExternalModules=false

:: Setup only the server, nothing else
set setupServerOnly=false

:: Perform *only* the post install self tests
set selfTestOnly=false

:: Perform self-tests unless this is false
set noSelfTest=false

:: If files are already present, then don't overwrite if this is false
set forceOverwrite=false

:: If bandwidth is extremely limited, or you are actually offline, set this as true to
:: force all downloads to be retrieved from cached downloads. If the cached download
:: doesn't exist the install will fail.
set offlineInstall=false

:: For speeding up debugging
set skipPipInstall=false

:: Whether or not to install all python packages in one step (-r requirements.txt)
:: or step by step. Doing this allows the PIP manager to handle incompatibilities 
:: better.
:: ** WARNING ** There is a big tradeoff on keeping the users informed and speed/
:: reliability. Generally one-step shouldn't be needed. But it often is. And it
:: often doesn't actually solve problems either. Overall it's safer, but not a 
:: panacea
set oneStepPIP=false

:: Whether or not to use the jq utility for JSON parsing. If false, use ParseJSON
set useJq=false
set debug_json_parse=false

:: Basic locations

:: The path to the directory containing this setup script. Will end in "\"
set setupScriptDirPath=%~dp0

:: The path to the application root dir. This is 'src' in dev, or / in production
:: This setup script always lives in the app root
set appRootDirPath=!setupScriptDirPath!

:: The location of large packages that need to be downloaded (eg an AWS S3 bucket name)
set storageUrl=https://codeproject-ai.s3.ca-central-1.amazonaws.com/server/assets/

:: The name of the source directory (in development)
set srcDirName=src

:: The name of the app directory (in docker)
set appDirName=app

:: The name of the dir, within the current directory, where install assets will
:: be downloaded
set downloadDir=downloads

:: The name of the dir holding the installed runtimes
set runtimesDir=runtimes

:: The name of the dir holding the downloaded/sideloaded backend analysis services
set modulesDir=modules
set preInstalledModulesDir=preinstalled-modules
set externalModulesDir=CodeProject.AI-Modules

:: The name of the dir holding downloaded models for the modules. NOTE: this is 
:: not currently used, but here for future-proofing
set modelsDir=models

:: The location of directories relative to the root of the solution directory
set sdkPath=!appRootDirPath!SDK
set sdkScriptsDirPath=!sdkPath!\Scripts

set runtimesDirPath=!appRootDirPath!!runtimesDir!

set modulesDirPath=!appRootDirPath!!modulesDir!
set preInstalledModulesDirPath=!appRootDirPath!!preInstalledModulesDir!
set externalModulesDirPath=!appRootDirPath!..\!externalModulesDir!
set modelsDirPath=!appRootDirPath!!modelsDir!
set downloadDirPath=!appRootDirPath!!downloadDir!

:: Who launched this script? user or server?
set launchedBy=user

:: Override some values via parameters ::::::::::::::::::::::::::::::::::::::::

:param_loop
    if "%~1"=="" goto :end_param_loop
    if "%~1"=="--launcher" (
        set "launchedBy=%~2"
        if /i "!launchedBy!" neq "server" if /i "!launchedBy!" neq "user" (
            set launchedBy=user
        )
        shift
        shift
        goto :param_loop
    )
    if "%~1"=="--verbosity" (
        set "verbosity=%~2"
        if /i "!verbosity!" neq "loud" if /i "!verbosity!" neq "info" if /i "!verbosity!" neq "quiet" (
            set verbosity=quiet
        )
        shift
        shift
        goto :param_loop
    )
    if "%~1"=="--selftest-only" (
        set selfTestOnly=true
        shift
        goto :param_loop
    )
    if "%~1"=="--no-selftest" (
        set noSelfTest=true
        shift
        goto :param_loop
    )
    if "%~1"=="--server-only" (
        set setupServerOnly=true
        shift
        goto :param_loop
    )
    if "%~1"=="--no-color" (
        set useColor=false
        shift
        goto :param_loop
    )
    shift
:end_param_loop

:: Pre-setup ::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::

:: If offline then force the system to use pre-downloaded files
if /i "%offlineInstall%" == "true" set forceOverwrite=false


:: Execution environment, setup mode and Paths ::::::::::::::::::::::::::::::::

:: If we're calling this script from the /src folder directly (and the /src
:: folder actually exists) then we're Setting up the dev environment. Otherwise
:: we're installing a module.
set setupMode=SetupModule
for /f "delims=\" %%a in ("%cd%") do @set CurrDirName=%%~nxa

rem when executionEnvironment = "Development" this may be the case
if /i "%CurrDirName%" == "%srcDirName%" (
    set setupMode=SetupEverything
    REM HACK: We're one folder deeper than we need to be for referencing externalModulesDir
    set externalModulesDirPath=!appRootDirPath!..\..\!externalModulesDir!
)

rem when executionEnvironment = "Production" this may be the case
if /i "$CurrDirName" == "%appDirName%" (
    REM HACK: We're one folder deeper than we need to be for referencing externalModulesDir
    set externalModulesDirPath=!appRootDirPath!..\..\!externalModulesDir!
)

:: In Development, this script is in the /src folder. In Production there is no
:: /src folder; everything is in the root folder. So: go to the folder
:: containing this script and check the name of the parent folder to see if
:: we're in dev or production.
pushd "!setupScriptDirPath!"
for /f "delims=\" %%a in ("%cd%") do @set CurrDirName=%%~nxa
popd
set executionEnvironment=Production
if /i "%CurrDirName%" == "%srcDirName%" set executionEnvironment=Development

:: The absolute path to the installer script and the root directory. Note that
:: this script (and the SDK folder) is either in the /src dir or the root dir
pushd "!setupScriptDirPath!"
if /i "%executionEnvironment%" == "Development" cd ..
set rootDirPath=%cd%
popd

:: Helper vars for OS, Platform (see note below), and system name. systemName is
:: a no-op here because nothing exciting happens on Windows. In the corresponding
:: .sh setup files, systemName can be docker, Raspberry Pi, WSL - all sorts of fun
:: things. It's here to just make switching between .bat and .sh scripts consistent

set os=windows
set platform=windows
set systemName=Windows

:: Blank because we don't currently support an edge device running Windows. In
:: Linux this could be Raspberry Pi, Orange Pi, Radxa ROCK or Jetson
set edgeDevice=

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

:: Let's go
if /i "!useColor!" == "true" call "!sdkScriptsDirPath!\utils.bat" setESC
if /i "!setupMode!" == "SetupEverything" (
    set scriptTitle=          Setting up CodeProject.AI Development Environment
) else (
    set scriptTitle=             Installing CodeProject.AI Analysis Module
)

call "!sdkScriptsDirPath!\utils.bat" WriteLine 
call "!sdkScriptsDirPath!\utils.bat" WriteLine "!scriptTitle!" "DarkYellow" "Default" !lineWidth!
call "!sdkScriptsDirPath!\utils.bat" WriteLine 
call "!sdkScriptsDirPath!\utils.bat" WriteLine "======================================================================" "DarkGreen" 
call "!sdkScriptsDirPath!\utils.bat" WriteLine 
call "!sdkScriptsDirPath!\utils.bat" WriteLine "                   CodeProject.AI Installer                           " "DarkGreen" 
call "!sdkScriptsDirPath!\utils.bat" WriteLine 
call "!sdkScriptsDirPath!\utils.bat" WriteLine "======================================================================" "DarkGreen" 
call "!sdkScriptsDirPath!\utils.bat" WriteLine 

set mainSetupStarttime=%time%

REM Report disk space available
for /f "tokens=1,2 delims== " %%a in ('wmic logicaldisk where "DeviceID='%cd:~0,2%'" get FreeSpace^,Size^,VolumeName /format:list') do (
    if "%%a"=="FreeSpace"  set freespacebytes=%%b
    if "%%a"=="Size"       set totalspacebytes=%%b
    if "%%a"=="VolumeName" set volumename=%%b
)
REM Anything ove 2Gb kills this
REM set /a freeSpaceGb=!freespacebytes! / 1073741824
REM set /a freeSpaceGbfraction=!freespacebytes! %% 1073741824 * 10 / 1073741824
set /a freeSpaceGb=!freespacebytes:~0,-4! / 1048576
set /a freeSpaceGbfraction=!freespacebytes:~0,-4! %% 1048576 * 10 / 1048576
set /a totalSpaceGb=!totalspacebytes:~0,-4! / 1048576

call "!sdkScriptsDirPath!\utils.bat" WriteLine "!freeSpaceGb!.!freeSpaceGbfraction!Gb of !totalSpaceGb!Gb available on !VolumeName!" !color_mute!


if /i "%verbosity%" neq "quiet" (
    call "!sdkScriptsDirPath!\utils.bat" WriteLine 
    call "!sdkScriptsDirPath!\utils.bat" WriteLine "os, arch               = !os! !architecture!"      !color_mute!
    call "!sdkScriptsDirPath!\utils.bat" WriteLine "systemName, platform   = !systemName!, !platform!" !color_mute!
    call "!sdkScriptsDirPath!\utils.bat" WriteLine "edgeDevice             = !edgeDevice!"             !color_mute!
    call "!sdkScriptsDirPath!\utils.bat" WriteLine "setupMode              = !setupMode!"              !color_mute!
    call "!sdkScriptsDirPath!\utils.bat" WriteLine "executionEnvironment   = !executionEnvironment!"   !color_mute!
    call "!sdkScriptsDirPath!\utils.bat" WriteLine "rootDirPath            = !rootDirPath!"            !color_mute!
    call "!sdkScriptsDirPath!\utils.bat" WriteLine "appRootDirPath         = !appRootDirPath!"         !color_mute!
    call "!sdkScriptsDirPath!\utils.bat" WriteLine "setupScriptDirPath     = !setupScriptDirPath!"     !color_mute!
    call "!sdkScriptsDirPath!\utils.bat" WriteLine "sdkScriptsDirPath      = !sdkScriptsDirPath!"      !color_mute!
    call "!sdkScriptsDirPath!\utils.bat" WriteLine "runtimesDirPath        = !runtimesDirPath!"        !color_mute!
    call "!sdkScriptsDirPath!\utils.bat" WriteLine "modulesDirPath         = !modulesDirPath!"         !color_mute!
    call "!sdkScriptsDirPath!\utils.bat" WriteLine "externalModulesDirPath = !externalModulesDirPath!" !color_mute!
    call "!sdkScriptsDirPath!\utils.bat" WriteLine "modelsDirPath          = !modelsDirPath!"          !color_mute!
    call "!sdkScriptsDirPath!\utils.bat" WriteLine "downloadDirPath        = !downloadDirPath!"        !color_mute!
    call "!sdkScriptsDirPath!\utils.bat" WriteLine
)

:: Ensure directories are created and download required assets.

call "!sdkScriptsDirPath!\utils.bat" WriteLine
call "!sdkScriptsDirPath!\utils.bat" WriteLine "General CodeProject.AI setup" "White" "Blue" !lineWidth!
call "!sdkScriptsDirPath!\utils.bat" WriteLine

:: Create some directories

call "!sdkScriptsDirPath!\utils.bat" Write "Creating Directories..."
if not exist "!runtimesDirPath!\" mkdir "!runtimesDirPath!"
if not exist "!downloadDirPath!\" mkdir "!downloadDirPath!"
if not exist "!downloadDirPath!\!modulesDir!\" mkdir "!downloadDirPath!\!modulesDir!\"
if not exist "!downloadDirPath!\!modelsDir!\" mkdir "!downloadDirPath!\!modelsDir!\"

call "!sdkScriptsDirPath!\utils.bat" WriteLine "done" "Green"
call "!sdkScriptsDirPath!\utils.bat" WriteLine ""

:: Report on GPU ability

:: GPU / CPU support ::::::::::::::::::::::::::::::::::::::::::::::::::::::::::

call "!sdkScriptsDirPath!\utils.bat" WriteLine "GPU support" "White" "DarkGreen" !lineWidth!
call "!sdkScriptsDirPath!\utils.bat" WriteLine ""

REM Test for CUDA drivers 
call "!sdkScriptsDirPath!\utils.bat" Write "CUDA Present..."

set hasCUDA=false

call "!sdkScriptsDirPath!\utils.bat" GetCudaVersion
if "!cuda_version!" neq "" set hasCUDA=true

if /i "!hasCUDA!" == "true" (
    call "!sdkScriptsDirPath!\utils.bat" GetCuDNNVersion
    if "!cuDNN_version!" == "" (
        call "!sdkScriptsDirPath!\utils.bat" WriteLine "Yes (CUDA !cuda_version!, No cuDNN found)" !color_success!
    ) else (
        call "!sdkScriptsDirPath!\utils.bat" WriteLine "Yes (CUDA !cuda_version!, cuDNN !cuDNN_version!)" !color_success!
    )
) else (
    call "!sdkScriptsDirPath!\utils.bat" WriteLine "No" !color_warn!
)

REM Test for AMD ROCm drivers 
call "!sdkScriptsDirPath!\utils.bat" Write "ROCm Present..."

set hasROCm=false
where rocm-smi >nul 2>nul
if "!errorlevel!" == "0" set hasROCm=true

if /i "%hasROCm%" == "true" (
    call "!sdkScriptsDirPath!\utils.bat" WriteLine "Yes" !color_success!
) else (
    call "!sdkScriptsDirPath!\utils.bat" WriteLine "No" !color_warn!
)

REM quick detour to ensure ParseJSON is installed
if /i "!executionEnvironment!" == "Development" (
    pushd !sdkPath!\Utilities\ParseJSON
    if not exist ParseJSON.exe (
        call "!sdkScriptsDirPath!\utils.bat" WriteLine "Building ParseJSON"
        if /i "!verbosity!" == "quiet" (
            dotnet build /property:GenerateFullPaths=true /consoleloggerparameters:NoSummary -c Release >NUL
        ) else (
            dotnet build /property:GenerateFullPaths=true /consoleloggerparameters:NoSummary -c Release
        )
        if exist .\bin\Release\net7.0\ move .\bin\Release\net7.0\* . >nul
    )
    popd
)


:: And off we go...

set success=true

if /i "!setupMode!" == "SetupEverything" (

    REM Start with the CodeProject.AI SDK and Server

    if /i "!selfTestOnly!" == "false" (

        call "!sdkScriptsDirPath!\utils.bat" WriteLine
        call "!sdkScriptsDirPath!\utils.bat" WriteLine "Processing CodeProject.AI SDK" "White" "DarkGreen" !lineWidth!
        call "!sdkScriptsDirPath!\utils.bat" WriteLine

        set currentDir=%cd%
        set moduleDirName=SDK
        set moduleDirPath=!appRootDirPath!!moduleDirName!

        REM This will setup .NET since the SDK relies on it
        call "!moduleDirPath!\install.bat" install   

        if errorlevel 1 set success=false
        if "!moduleInstallErrors!" NEQ "" set success=false
        if "!moduleInstallErrors!" == "" if /i "!success!" == "false" set moduleInstallErrors=CodeProject.AI SDK install failed
        cd "!currentDir!"


        call "!sdkScriptsDirPath!\utils.bat" WriteLine
        call "!sdkScriptsDirPath!\utils.bat" WriteLine "Processing CodeProject.AI Server" "White" "DarkGreen" !lineWidth!
        call "!sdkScriptsDirPath!\utils.bat" WriteLine

        set currentDir=%cd%
        set moduleDirName=server
        set moduleDirPath=!appRootDirPath!!moduleDirName!

        call "!moduleDirPath!\install.bat" install   

        if errorlevel 1 set success=false
        if "!moduleInstallErrors!" NEQ "" set success=false
        if "!moduleInstallErrors!" == "" if /i "!success!" == "false" set moduleInstallErrors=CodeProject.AI Server install failed
        cd "!currentDir!"
    )

    REM Walk through the modules directory and call the setup script in each
    REM dir, as well as setting up the demos

    if /i "!setupServerOnly!" == "false" (

        REM Before we start, ensure we can read the modulesettings files        
        call :SetupJSONParser
   
        call "!sdkScriptsDirPath!\utils.bat" WriteLine
        call "!sdkScriptsDirPath!\utils.bat" WriteLine "Processing Included CodeProject.AI Server Modules" "White" "DarkGreen" !lineWidth!
        call "!sdkScriptsDirPath!\utils.bat" WriteLine

        REM  TODO: This should be just a simple for /d %%D in ("!modulesDirPath!") do (
        for /f "delims=" %%D in ('dir /a:d /b "!modulesDirPath!"') do (
            set moduleDirName=%%~nxD
            set moduleDirPath=!modulesDirPath!\!moduleDirName!

            call "!sdkScriptsDirPath!\utils.bat" GetModuleIdFromModuleSettingsFile "!moduleDirPath!\modulesettings.json"
            set moduleId=!moduleSettingValue!

            call :DoModuleInstall "!moduleId!" "!moduleDirPath!" errors
            if "!moduleInstallErrors!" NEQ "" set success=false
        )

        if /i "!installExternalModules!" == "true" (
            call "!sdkScriptsDirPath!\utils.bat" WriteLine
            call "!sdkScriptsDirPath!\utils.bat" WriteLine "Processing External CodeProject.AI Server Modules" "White" "DarkGreen" !lineWidth!
            call "!sdkScriptsDirPath!\utils.bat" WriteLine

            if exist !externalModulesDirPath! (
                for /f "delims=" %%D in ('dir /a:d /b "!externalModulesDirPath!"') do (
                    set moduleDirName=%%~nxD
                    set moduleDirPath=!externalModulesDirPath!\!moduleDirName!

                    call "!sdkScriptsDirPath!\utils.bat" GetModuleIdFromModuleSettingsFile "!moduleDirPath!\modulesettings.json"
                    set moduleId=!moduleSettingValue!

                    call :DoModuleInstall "!moduleId!" "!moduleDirPath!" errors
                    if "!moduleInstallErrors!" NEQ "" set success=false
                )
            ) else (
                call "!sdkScriptsDirPath!\utils.bat" WriteLine "No external modules found" !color_mute!
            )
        )

        call "!sdkScriptsDirPath!\utils.bat" WriteLine
        call "!sdkScriptsDirPath!\utils.bat" WriteLine "Module setup Complete" "Green"

        REM Install Demo clients
        if /i "!selfTestOnly!" == "false" (
            call "!sdkScriptsDirPath!\utils.bat" WriteLine
            call "!sdkScriptsDirPath!\utils.bat" WriteLine "Processing Demo clients" "White" "Blue" !lineWidth!
            call "!sdkScriptsDirPath!\utils.bat" WriteLine

            set currentDir=%cd%
            set moduleDirName=clients
            set moduleDirPath=!rootDirPath!\src\demos\!moduleDirName!
            call "!moduleDirPath!\install.bat" install

            REM Don't really care about demos enough to fail the entire setup script
            REM if errorlevel 1 set success=false
            cd "!currentDir!"
        )

        REM Install Demo modules
        call "!sdkScriptsDirPath!\utils.bat" WriteLine
        call "!sdkScriptsDirPath!\utils.bat" WriteLine "Processing Demo Modules" "White" "Blue" !lineWidth!
        call "!sdkScriptsDirPath!\utils.bat" WriteLine

        set oldModulesDirPath=!modulesDirPath!
        set modulesDirPath=!rootDirPath!\src\demos\modules\
        for /f "delims=" %%D in ('dir /a:d /b "!modulesDirPath!"') do (
            set moduleDirName=%%~nxD
            set moduleDirPath=!modulesDirPath!\!moduleDirName!

            call "!sdkScriptsDirPath!\utils.bat" GetModuleIdFromModuleSettingsFile "!moduleDirPath!\modulesettings.json"
            set moduleId=!moduleSettingValue!

            call :DoModuleInstall "!moduleId!" "!moduleDirPath!" errors

            if "!moduleInstallErrors!" NEQ "" set success=false
        )
        set modulesDirPath=!oldModulesDirPath!
    )

) else (

    if /i "!setupServerOnly!" == "false" (

        REM Quick sanity check to ensure .NET is in place
        call "%sdkScriptsDirPath%\utils.bat" SetupDotNet 7.0.405 SDK
        
        REM Before we start, ensure we can read the modulesettings files        
        call :SetupJSONParser        

        REM Install an individual module
        
        for %%I in (.) do set moduleDirName=%%~nxI

        if /i "!moduleDirName!" == "server" (
            REM Not a module. The server
            if /i "!selfTestOnly!" == "false" (
                set moduleDirPath=!appRootDirPath!\!moduleDirName!

                set currentDir=%cd%
                call "!moduleDirPath!\install.bat" install
                cd "!currentDir!"
            )
        ) else if /i "!moduleDirName!" == "clients" (
            REM Not a module. The demo clients
            if /i "!selfTestOnly!" == "false" (
                set moduleDirPath=!rootDirPath!\src\demos\!moduleDirName!

                set currentDir=%cd%
                call "!moduleDirPath!\install.bat" install
                cd "!currentDir!"
            )
        ) else (

            REM No need to check "$selfTestOnly" here because this check is done
            REM in DoModuleInstall. We need to run doModuleInstall to have the
            REM selftest called

            for %%I in (..) do set parentDirName=%%~nxI
            for %%I in (..\..) do set parentParentDirName=%%~nxI

            if /i "!parentParentDirName!" == "demos" (
                call "!sdkScriptsDirPath!\utils.bat" WriteLine "Demo module install" !color_info!
                set oldModulesDirPath=!modulesDirPath!
                set modulesDirPath=!rootDirPath!\src\demos\modules\

                set moduleDirPath=!modulesDirPath!\!moduleDirName!
                call "!sdkScriptsDirPath!\utils.bat" GetModuleIdFromModuleSettingsFile "!moduleDirPath!\modulesettings.json"
                set moduleId=!moduleSettingValue!

                call :DoModuleInstall "!moduleId!" "!moduleDirPath!" errors

                set modulesDirPath=!oldModulesDirPath!
            ) else if /i "!parentDirName!" == "!externalModulesDir!" (
                call "!sdkScriptsDirPath!\utils.bat" WriteLine "External module install" !color_info!

                set moduleDirPath=%cd%

                call "!sdkScriptsDirPath!\utils.bat" GetModuleIdFromModuleSettingsFile "!moduleDirPath!\modulesettings.json"
                set moduleId=!moduleSettingValue!

                call :DoModuleInstall "!moduleId!" "!moduleDirPath!" errors
            ) else (                                                           REM Internal module
                set moduleDirPath=!modulesDirPath!\!moduleDirName!

                call "!sdkScriptsDirPath!\utils.bat" GetModuleIdFromModuleSettingsFile "!moduleDirPath!\modulesettings.json"
                set moduleId=!moduleSettingValue!

                call :DoModuleInstall "!moduleId!" "!moduleDirPath!" errors
            )
            if "!moduleInstallErrors!" NEQ "" set success=false
        )
    )
)

call "!sdkScriptsDirPath!\utils.bat" WriteLine
call "!sdkScriptsDirPath!\utils.bat" WriteLine "Setup complete" "White" "DarkGreen" !lineWidth!
call "!sdkScriptsDirPath!\utils.bat" WriteLine

call "!sdkScriptsDirPath!\utils.bat" timeSince "!mainSetupStarttime!" duration
call "!sdkScriptsDirPath!\utils.bat" WriteLine "Total setup time !duration!" "!color_info!"

if /i "!success!" == "false" exit /b 1

exit /b 0


REM Pop over the DoModuleInstall definition and leave.
goto:eof

:SetupJSONParser

    pushd !sdkPath!\Utilities\ParseJSON
    if not exist ParseJSON.exe (
        call "!sdkScriptsDirPath!\utils.bat" WriteLine "Building ParseJSON"
        dotnet build /property:GenerateFullPaths=true /consoleloggerparameters:NoSummary -c Release >NUL
        if exist .\bin\Release\net7.0\ move .\bin\Release\net7.0\* . >nul
    )
    popd
    exit /b


:SetupPythonPaths

    set runtimeLocation=%~1
    set pythonVersion=%~2

    REM No Python
    if "!pythonVersion!" == "" (
        set "pythonDirPath="
        set "virtualEnvDirPath="
        set "venvPythonCmdPath="
        set "packagesDirPath="
        exit /b
    )

    REM Name based on version (eg version is 3.8, name is then python38)
    set pythonName=python!pythonVersion:.=!

    REM The path to the python installation, either local or shared. The
    REM virtual environment will live in here
    if /i "!runtimeLocation!" == "Local" (
        set pythonDirPath=!moduleDirPath!\bin\!os!\!pythonName!
    ) else (
        set pythonDirPath=!runtimesDirPath!\bin\!os!\!pythonName!
    )
    set virtualEnvDirPath=!pythonDirPath!\venv

    REM The path to the python intepreter for this venv
    set venvPythonCmdPath=!virtualEnvDirPath!\Scripts\python.exe

    REM The location where python packages will be installed for this venvvenv
    set packagesDirPath=%virtualEnvDirPath%\Lib\site-packages

    exit /b

REM Installs a module in the module's directory, and returns success 
:DoModuleInstall moduleId moduleDirPath errors

    SetLocal EnableDelayedExpansion

    set moduleId=%~1
    set moduleDirPath=%~2

    set moduleSetupStarttime=%time%

    REM Get the module name, version, runtime location and python version from
    REM the modulesettings.
    
    call "!sdkScriptsDirPath!\utils.bat" WriteLine
    call "!sdkScriptsDirPath!\utils.bat" Write "Reading !moduleId! settings" !color_mute!

    call "!sdkScriptsDirPath!\utils.bat" GetValueFromModuleSettingsFile "!moduleDirPath!", "!moduleId!", "Name"
    set moduleName=!moduleSettingsFileValue!
    call "!sdkScriptsDirPath!\utils.bat" Write "." !color_mute!

    call "!sdkScriptsDirPath!\utils.bat" GetValueFromModuleSettingsFile "!moduleDirPath!", "!moduleId!", "Version"
    set moduleVersion=!moduleSettingsFileValue!
    call "!sdkScriptsDirPath!\utils.bat" Write "." !color_mute!

    call "!sdkScriptsDirPath!\utils.bat" GetValueFromModuleSettingsFile "!moduleDirPath!", "!moduleId!", "LaunchSettings.Runtime"
    set runtime=!moduleSettingsFileValue!
    call "!sdkScriptsDirPath!\utils.bat" Write "." !color_mute!

    call "!sdkScriptsDirPath!\utils.bat" GetValueFromModuleSettingsFile "!moduleDirPath!", "!moduleId!", "LaunchSettings.RuntimeLocation"
    set runtimeLocation=!moduleSettingsFileValue!
    call "!sdkScriptsDirPath!\utils.bat" Write "." !color_mute!

    call "!sdkScriptsDirPath!\utils.bat" GetValueFromModuleSettingsFile "!moduleDirPath!", "!moduleId!", "LaunchSettings.FilePath"
    set moduleStartFilePath=!moduleSettingsFileValue!
    call "!sdkScriptsDirPath!\utils.bat" Write "." !color_mute!

    call "!sdkScriptsDirPath!\utils.bat" GetValueFromModuleSettingsFile "!moduleDirPath!", "!moduleId!", "GpuOptions.InstallGPU"
    set installGPU=!moduleSettingsFileValue!
    call "!sdkScriptsDirPath!\utils.bat" Write "." !color_mute!

    call "!sdkScriptsDirPath!\utils.bat" GetValueFromModuleSettingsFile "!moduleDirPath!", "!moduleId!", "InstallOptions.Platforms"
    set platforms=!moduleSettingsFileValue!
    call "!sdkScriptsDirPath!\utils.bat" Write "." !color_mute!

    call "!sdkScriptsDirPath!\utils.bat" WriteLine "done" !color_success!

    if "!moduleName!" == "" set moduleName=!moduleId!

    set announcement=Installing module !moduleName! !moduleVersion!
    call "!sdkScriptsDirPath!\utils.bat" WriteLine "!announcement!" "White" "Blue" !lineWidth!
    call "!sdkScriptsDirPath!\utils.bat" WriteLine

    REM remove spaces and [,] from ends of platforms value
    if /i "%verbosity%" == "loud" (
        call "!sdkScriptsDirPath!\utils.bat" WriteLine "Platform = !platform!, Platforms = !platforms!" !color_mute!
    )

    set "platformArray=!platforms: =!"
    set "platformArray=!platformArray:~1,-1!"

    set can_install=false
    for %%i in (!platformArray!) do (
        set "item=%%~i"
        REM echo Checking !platform! against !item!

        REM Excluded?
        if /i "!item!" == "^!!platform!" (
            set can_install=false
            goto :end_platform_loop
        )

        REM Included?
        if /i "!item!" == "!platform!" set can_install=true
        REM Maybe do a check for WindowsMajorVersion and WindowsMajorVersion-architecture
        if /i "!item!" == "all"        set can_install=true
    )
:end_platform_loop

    if /i "!can_install!" == "false" (
        call "!sdkScriptsDirPath!\utils.bat" WriteLine "This module cannot be installed on this system" !color_warn!
        exit /b 1
    )

    if /i "!allowSharedPythonInstallsForModules!" == "false" (
        REM if moduleDirPath contains '/modules/' and runtimeLocation != 'Local'
        if /i "!moduleDirPath:\modules\=!" neq "!moduleDirPath!" (
            if /i "!runtimeLocation!" neq "Local" (
                call :WriteLine "Downloaded modules must have local Python install. Changing install location" "!color_warn!"
                set runtimeLocation=Local
            )
        )
    )

    REM For python, the runtime is in the form "Python3.8", so get the "3.8".
    REM However, we also allow just "python" meaning "use whatever is default"
    REM TODO: Allow 'python<=3.9' type specifiers so it will use the native python
    REM       if it's <= 3.9, or install and use 3.9 otherwise
    set pythonVersion=
    if /i "!runtime!" == "python" (

        REM Get current Python version, and trim down to just major.minor
        set currentPythonVersion=
        for /f "tokens=2 delims= " %%v in ('python --version 2^>^&1') do (
            set currentPythonVersion=%%v
            echo Default Python version is: !currentPythonVersion!
        )
        if !currentPythonVersion! neq "" (
            for /f "tokens=1,2 delims=." %%a in ("!currentPythonVersion!") do (
                set major=%%a
                set minor=%%b
            )
            set pythonVersion=!major!.!minor!
        )

        REM fallback
        if "!pythonVersion!" == "" set pythonVersion=3.9

    ) else if /i "!runtime:~0,6!" == "python" (
        set pythonVersion=!runtime:~6!
    )

    call :SetupPythonPaths "!runtimeLocation!" !pythonVersion!

    if /i "!verbosity!" neq "quiet" (
        set announcement=Variable Dump
        call "!sdkScriptsDirPath!\utils.bat" WriteLine
        call "!sdkScriptsDirPath!\utils.bat" WriteLine "!announcement!" "White" "Blue" !lineWidth!
        call "!sdkScriptsDirPath!\utils.bat" WriteLine
        call "!sdkScriptsDirPath!\utils.bat" WriteLine "moduleName          = !moduleName!"          "!color_info!"
        call "!sdkScriptsDirPath!\utils.bat" WriteLine "moduleId            = !moduleId!"            "!color_info!"
        call "!sdkScriptsDirPath!\utils.bat" WriteLine "moduleVersion       = !moduleVersion!"       "!color_info!"
        call "!sdkScriptsDirPath!\utils.bat" WriteLine "runtime             = !runtime!"             "!color_info!"
        call "!sdkScriptsDirPath!\utils.bat" WriteLine "runtimeLocation     = !runtimeLocation!"     "!color_info!"
        call "!sdkScriptsDirPath!\utils.bat" WriteLine "installGPU          = !installGPU!"          "!color_info!"
        call "!sdkScriptsDirPath!\utils.bat" WriteLine "pythonVersion       = !pythonVersion!"       "!color_info!"
        call "!sdkScriptsDirPath!\utils.bat" WriteLine "virtualEnvDirPath   = !virtualEnvDirPath!"   "!color_info!"
        call "!sdkScriptsDirPath!\utils.bat" WriteLine "venvPythonCmdPath   = !venvPythonCmdPath!"   "!color_info!"
        call "!sdkScriptsDirPath!\utils.bat" WriteLine "packagesDirPath     = !packagesDirPath!"     "!color_info!"
        call "!sdkScriptsDirPath!\utils.bat" WriteLine "moduleStartFilePath = !moduleStartFilePath!" "!color_info!"
    )

    REM Set the global error message value
    set moduleInstallErrors=

    if exist "!moduleDirPath!\install.bat" (

        REM If a python version has been specified then we'll automatically setup
        REM the correct python environment. We do this before the script runs so
        REM the script can use python in the script.
        if "!pythonVersion!" neq "" (
            if /i "!selfTestOnly!" == "false" (
                call "!sdkScriptsDirPath!\utils.bat" WriteLine "Installing Python !pythonVersion!"
                call "%sdkScriptsDirPath%\utils.bat" SetupPython
                if errorlevel 1 set moduleInstallErrors=Unable to install Python !pythonVersion!
            )
        )

        REM Install the module, but only if there were no issues installing
        REM python (or a python install wasn't needed)
        if /i "!moduleInstallErrors!" == "" if /i "!selfTestOnly!" == "false" (
            set currentDir=%cd%
            call "!moduleDirPath!\install.bat" install
            REM if errorlevel 1 if "!moduleInstallErrors!" == "" (
            REM    set moduleInstallErrors=!moduleName! failed to install
            REM )
            cd "!currentDir!"
        )

        REM If a python version has been specified then we'll automatically
        REM look for, and install, the requirements file for the module, and
        REM then also the requirements file for the SDK since it'll be assumed
        REM the Python SDK will come into play.
        if /i "!selfTestOnly!" == "false" (
            if /i "!moduleInstallErrors!" == "" (
                if "!pythonVersion!" neq "" (
                    call "!sdkScriptsDirPath!\utils.bat" WriteLine "Installing Python packages for !moduleName!"

                    call "!sdkScriptsDirPath!\utils.bat" Write "Installing GPU-enabled libraries: " $color_info
                    if "!installGPU!" == "true" (call "!sdkScriptsDirPath!\utils.bat" WriteLine "If available" !color_success!) else (call "!sdkScriptsDirPath!\utils.bat" WriteLine "No" !color_warn!)

                    call "!sdkScriptsDirPath!\utils.bat" InstallRequiredPythonPackages 
                    if errorlevel 1 set moduleInstallErrors=Unable to install Python packages for !moduleName!

                    call "!sdkScriptsDirPath!\utils.bat" WriteLine "Installing Python packages for the CodeProject.AI Server SDK"
                    call "!sdkScriptsDirPath!\utils.bat" InstallRequiredPythonPackages "%sdkPath%\Python"
                    if errorlevel 1 set moduleInstallErrors=Unable to install Python packages for CodeProject SDK
                )

                call "!sdkScriptsDirPath!\utils.bat" downloadModels 

            ) else (
                call "!sdkScriptsDirPath!\utils.bat" WriteLine "Skipping PIP installs and model downloads due to install error (!moduleInstallErrors!)" !color_warn!
            )
        )

        REM And finally, the post install script if one was provided
        if exist "!moduleDirPath!\post_install.bat" (
            if /i "!moduleInstallErrors!" == "" (
                if /i "!selfTestOnly!" == "false" (
                    call "!sdkScriptsDirPath!\utils.bat" WriteLine "Executing post-install script for !moduleName!"
                    set currentDir=%cd%
                    call "!moduleDirPath!\post_install.bat" post-install
                    if errorlevel 1 set moduleInstallErrors=Error running post-install script
                    cd "!currentDir!"
                )
            ) else (
                call "!sdkScriptsDirPath!\utils.bat" WriteLine "Skipping post install due to install error (!moduleInstallErrors!)" !color_warn!
            )
        )

        REM Perform a self-test
        if /i "!noSelfTest!" == "false" if "!moduleInstallErrors!" == "" (

            set currentDir=%cd%
            cd "!moduleDirPath!"

            if /i "%verbosity%" == "quiet" (
                call "!sdkScriptsDirPath!\utils.bat" Write "Self test: "
            ) else (
                call "!sdkScriptsDirPath!\utils.bat" WriteLine "SELF TEST START ======================================================" !color_info!
            )

            REM TODO: Load these values from the module settings and set them as env variables
            REM   CPAI_MODULE_ID, CPAI_MODULE_NAME, CPAI_MODULE_PATH, CPAI_MODULE_ENABLE_GPU,
            REM   CPAI_ACCEL_DEVICE_NAME, CPAI_HALF_PRECISION"
            REM Then load and set all env vars in modulesettings "EnvironmentVariables" collection

            set testRun=false
            if "!pythonVersion!" NEQ "" (

                set testRun=true
                if /i "%verbosity%" == "quiet" (
                    "!venvPythonCmdPath!" "!moduleStartFilePath!" --selftest >NUL
                ) else (
                    "!venvPythonCmdPath!" "!moduleStartFilePath!" --selftest
                )

            ) else if /i "!runtime!" == "dotnet" (

                REM should probably generalise this to:
                REM   !Runtime! "!moduleStartFilePath!" --selftest

                set "exePath=.\"
                REM if /i "!executionEnvironment!" == "Development" set "exePath=.\bin\Debug\net7.0\"

                if exist "!exePath!!moduleStartFilePath!" (
                    set testRun=true
                    if /i "%verbosity%" == "quiet" (
                        REM if the filepath is a DLL
                        REM dotnet "!exePath!!moduleStartFilePath!" --selftest >NUL
                        "!exePath!!moduleStartFilePath!" --selftest >NUL
                    ) else (
                        REM if the filepath is a DLL
                        REM dotnet "!exePath!!moduleStartFilePath!" --selftest
                        "!exePath!!moduleStartFilePath!" --selftest
                    )
                ) else (
                    call "!sdkScriptsDirPath!\utils.bat" WriteLine  "!exePath!!moduleStartFilePath! does not exist" !color_error!
                )

            )

            if "%errorlevel%" == "0" (
                if /i "!testRun!" == "true" (
                    call "!sdkScriptsDirPath!\utils.bat" WriteLine "Self-test passed" !color_success!
                ) else (
                    call "!sdkScriptsDirPath!\utils.bat" WriteLine "No self-test available" !color_warn!
                )
            ) else (
                call "!sdkScriptsDirPath!\utils.bat" WriteLine "Self-test failed" !color_error!
            )
            
            if /i "%verbosity%" NEQ "quiet" (
                call "!sdkScriptsDirPath!\utils.bat" WriteLine "SELF TEST END   ======================================================" !color_info!
            )
            
            cd "!currentDir!"
        )

    ) else (
        call "!sdkScriptsDirPath!\utils.bat" WriteLine "No install.bat present for !moduleName!" "!color_warn!"
    )

    call "!sdkScriptsDirPath!\utils.bat" timeSince "!moduleSetupStarttime!" duration
    call "!sdkScriptsDirPath!\utils.bat" WriteLine "Module setup time !duration!" "!color_info!"

    REM return result
    set %~3=!moduleInstallErrors!

    exit /b