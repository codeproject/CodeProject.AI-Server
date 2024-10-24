:: =============================================================================
::
:: CodeProject.AI Server 
::
:: Install script for Windows
::
:: This script can be called in 2 ways:
::
::  1. From within the \src (or the root directory of the installation) in
::     order to setup the full system, including serer, SDKs, demos and modules.
::     This method is typically used for setting up the Development environment.
::
::  2. From within a module's directory (or demo or server folder) to setup just
::     that module, demo or the server
::
:: If this script is called from within the folder containing this script (that
:: is, the script is called as `setup.bat` and not from outside the script's
:: folder using `..\..\setup.bat`) then all modules (internal, external, and
:: demo) will be setup in turn, as well as the main SDK, server and demos clients.
::
:: If this script is called from within a module's dir then this script will work
:: out if it's being called from \modules\my_module, 
:: ..\..\CodeProject.AI-Modules\my_module, \src\demos\modules\my_demo (we ignore
:: pre-installed modules). This script would typically be called via
::
::    ..\..\setup.bat
::
:: This script will look for a module's `install.bat` install script in the
:: directory from whence it was called, and execute that script. The install.bat
:: script is responsible for everything needed to ensure the module is ready to
:: run. Note that the server and SDK also have their own install.bat scripts.
::
:: Parameters:
::
::    --no-color          - Do not use color when outputting text. 
::    --selftest-only     - Only perform self-test calls on modules. No setup.
::    --no-selftest       - Do not perform self-test calls on modules after setup
::    --modules-only      - Only install modules, not server, SDK or demos
::    --server-only       - Only install the server, not modules, SDK or demos
::    --verbosity option  - 'option' is quiet, info or loud.
::
:: =============================================================================

@echo off
REM cls

SetLocal EnableDelayedExpansion

REM Set CodePage UTF-8 for our emojis
chcp 65001 >NUL

:: verbosity can be: quiet | info | loud. Use --verbosity quiet|info|loud
set verbosity=quiet

:: The .NET version to install. NOTE: Only major version matters unless we use manual install
:: scripts, in which case we need to specify version. Choose version that works for all platforms
:: since the versions of these are not in always in sync
set dotNetTarget=net9.0
set dotNetRuntimeVersion=9.0.0
set dotNetSDKVersion=9.0.100

:: Show output in wild, crazy colours. Use --no-color to not use colour
set useColor=true

:: Width of lines
set lineWidth=70

:: Whether or not modules can have their Python setup installed in the shared area
set allowSharedPythonInstallsForModules=true

:: If you wish to allow external modules
set installExternalModules=true

:: Setup only the server, nothing else. Use --server-only
set setupServerOnly=false

:: If true, only install modules, not server, SDKs, or demos. Use --modules-only
set setupModulesOnly=false

:: Perform *only* the post install self tests. Use --selftest-only
set selfTestOnly=false

:: Perform self-tests unless this is false. Use --no-selftest
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
:: or step by step. Installing pips in one step allows the PIP manager to handle
:: incompatibilities better, but if one thing fails, the whole things fails.
:: Further: one-step means if you re-run the installer, the entire req file is
:: always re-processed, whereas if oneStep is false, each package is checked for
:: existence before running pip, speeding re-installs dramatically.
:: Finally: one-step is an awful user experience. Everything hangs for minutes.
:: Setting it to false provides far better (but maybe slower) feedback mechanism.
:: FOR PIP INCOMPATIBILITY ISSUES: Set this to true and verbosity to loud to get
:: excellent debug feedback from pip.
set oneStepPIP=false

:: Whether or not to use the jq utility for JSON parsing. If false, use ParseJSON
set useJq=false
set debug_json_parse=false

:: Basic locations

:: The path to the directory containing this setup script. Will end in "\"
set setupScriptDirPath=%~dp0

:: The path to the application root dir. This is 'src' in dev, or \ in production
:: This setup script always lives in the app root
set appRootDirPath=!setupScriptDirPath!

:: The location of large packages that need to be downloaded (eg an AWS S3 bucket
:: name). This will be overwritten using the value from appsettings.json
REM set assetStorageUrl=https://codeproject-ai.s3.ca-central-1.amazonaws.com/server/assets/
set assetStorageUrl=https://codeproject-ai-bunny.b-cdn.net/server/assets/
REM set assetStorageUrl=https://www.codeproject.com/ai/download/server/assets/

:: The name of the source directory (in development)
set srcDirName=src

:: The name of the app directory (in docker)
set appDirName=app

:: The name of the dir holding the installed runtimes
set runtimesDir=runtimes

:: The name of the dir holding the downloaded/sideloaded backend analysis services
set modulesDir=modules
set preInstalledModulesDir=preinstalled-modules
set externalModulesDir=CodeProject.AI-Modules

:: The name of the dir, relative to the root directory, containing the folder
:: where downloaded assets will be cached
set downloadDir=downloads

:: Name of the install assets folder. Downloads in <root>/downloads/modules/assets
:: Module packages will be stored in <root>/downloads/modules/packages
set assetsDir=assets

:: The name of the dir holding downloaded models for the modules. NOTE: this is 
:: not currently used, but here for future-proofing
set modelsDir=models

:: The location of directories relative to the root of the solution directory
set sdkPath=!appRootDirPath!SDK

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
    if "%~1"=="--modules-only" (
        set setupModulesOnly=true
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

:: Assign a line feed to a var. KEEP THE EMPTY LINE EMPTY
(set LF=^

)

:: If offline then force the system to use pre-downloaded files
if /i "%offlineInstall%" == "true" set forceOverwrite=false


:: Execution environment, setup mode and Paths ::::::::::::::::::::::::::::::::

:: If we're calling this script from the /src folder directly (and the /src
:: folder actually exists) then we're Setting up the dev environment. Otherwise
:: we're installing a module.
set setupMode=SetupModule
for /f "delims=\" %%a in ("%cd%") do @set currentDirName=%%~nxa

:: Are we in \src? When executionEnvironment = "Development" this may be the case
if /i "%currentDirName%" == "%srcDirName%" set setupMode=SetupEverything

:: Are we in \app? (ie Docker)
if /i "%currentDirName%" == "%appDirName%" set setupMode=SetupEverything

:: Finally, test if this script is being run from within the directory holding
:: this script, meaning the root folder. It's not \src since we tested that, so
:: being in the root folder that isn't \src and isn't \app means we're in the root
:: folder of a native install
if /i "%cd%" == "%appRootDirPath%" set setupMode=SetupEverything

:: In Development, this script is in the /src folder. In Production there is no
:: /src folder; everything is in the root folder. So: go to the folder
:: containing this script and check the name of the parent folder to see if
:: we're in dev or production.
pushd "!setupScriptDirPath!"
for /f "delims=\" %%a in ("%cd%") do @set currentDirName=%%~nxa
popd

set executionEnvironment=Production
if /i "%currentDirName%" == "%srcDirName%" set executionEnvironment=Development

:: The absolute path to the installer script and the root directory. Note that
:: this script (and the SDK folder) is either in the /src dir or the root dir
pushd "!setupScriptDirPath!"
if /i "%executionEnvironment%" == "Development" cd ..
set rootDirPath=%cd%
popd

set runtimesDirPath=!rootDirPath!\!runtimesDir!
set modulesDirPath=!rootDirPath!\!modulesDir!
set preInstalledModulesDirPath=!rootDirPath!\!preInstalledModulesDir!
set externalModulesDirPath=!rootDirPath!\..\!externalModulesDir!
set modelsDirPath=!rootDirPath!\!modelsDir!
set downloadDirPath=!rootDirPath!\!downloadDir!
set downloadModuleAssetsDirPath=!downloadDirPath!\!modulesDir!\!assetsDir!
set utilsScriptsDirPath=!appRootDirPath!\scripts
set installScriptsDirPath=!rootDirPath!\devops\install
set utilsScript=!utilsScriptsDirPath!\utils.bat

:: Load vars in .env. This may update things like dotNetTarget
for /f "tokens=1,2 delims==" %%a in (!rootDirPath!\.env) do set %%a=%%b

:: Helper vars for OS, Platform (see note below), and system name. systemName is
:: a no-op here because nothing exciting happens on Windows. In the corresponding
:: .sh setup files, systemName can be docker, Raspberry Pi, WSL - all sorts of fun
:: things. It's here to just make switching between .bat and .sh scripts consistent

set os=windows
set os_name=Windows
set platform=windows
set systemName=Windows

:: Get the full OS name
call "!utilsScript!" getWindowsOSName os_name

:: Blank because we don't currently support an edge device running Windows. In
:: Linux this could be Raspberry Pi, Orange Pi, Radxa ROCK or Jetson
set edgeDevice=

:: This can be x86 (32-bit), AMD64 (Intel/AMD 64bit), ARM64 (Arm 64bit)
set architecture=%PROCESSOR_ARCHITECTURE%

:: A NOTE ON PLATFORM.
:: We use the full "x86_64" for architecture, but follow the common convention
:: of abbreviating this to "x64" when used in conjunction with OS. So windows-x64
:: rather than windows-x86_64. To simplify further, if the platform value doesn't
:: have a suffix then it's assumed to be -x64. This may change in the future.
if /i "!architecture!" == "arm64" (
    set architecture=arm64
    set platform=windows-arm64
)
:: Hack for AMD
if /i "!architecture!" == "amd64" set architecture=x86_64

:: Let's go
if /i "!useColor!" == "true" call "!utilsScript!" setESC
if /i "!setupMode!" == "SetupEverything" (
    set scriptTitle=          Setting up CodeProject.AI Development Environment
) else (
    set scriptTitle=             Installing CodeProject.AI Analysis Module
)

call "!utilsScript!" WriteLine 
call "!utilsScript!" WriteLine "!scriptTitle!" "DarkYellow" "Default" !lineWidth!
call "!utilsScript!" WriteLine 
call "!utilsScript!" WriteLine "======================================================================" "DarkGreen" 
call "!utilsScript!" WriteLine 
call "!utilsScript!" WriteLine "                   CodeProject.AI Installer                           " "DarkGreen" 
call "!utilsScript!" WriteLine 
call "!utilsScript!" WriteLine "======================================================================" "DarkGreen" 
call "!utilsScript!" WriteLine 

set mainSetupStarttime=%time%

REM Commented: WMIC not always available
:: REM Report disk space available
:: for /f "tokens=1,2 delims== " %%a in ('wmic logicaldisk where "DeviceID='%cd:~0,2%'" get FreeSpace^,Size^,VolumeName /format:list') do (
::     if "%%a"=="FreeSpace"  set freeSpaceBytes=%%b
::     if "%%a"=="Size"       set totalspacebytes=%%b
::     if "%%a"=="VolumeName" set volumeName=%%b
:: )
:: REM Anything over 2Gb kills this
:: REM set /a freeSpaceGb=!freeSpaceBytes! / 1073741824
:: REM set /a freeSpaceGbFraction=!freeSpaceBytes! %% 1073741824 * 10 / 1073741824
:: set /a freeSpaceGb=!freeSpaceBytes:~0,-4! / 1048576
:: set /a freeSpaceGbFraction=!freeSpaceBytes:~0,-4! %% 1048576 * 10 / 1048576
:: set /a totalSpaceGb=!totalspacebytes:~0,-4! / 1048576

for /f "tokens=6*" %%A in ('vol') do set volumeName=%%A
set driveRoot=%CD:~0,3%
for /f "usebackq" %%A in (`powershell -NoProfile -Command "(Get-PSDrive -PSProvider FileSystem | Where-Object { $_.Root -eq '!driveRoot!' }).Free + (Get-PSDrive -PSProvider FileSystem | Where-Object { $_.Root -eq '!driveRoot!' }).Used"`) do (
    set totalspacebytes=%%A
)
REM chop off last 3 digits (divide by 1000) then divide by 1024^2 to get Gb. This
REM is to avoid numerical overflow but results in bad maths.
set /a totalSpaceGb=!totalspacebytes:~0,-3! / 1048576

for /f "tokens=3" %%A in ('dir !driveRoot!') do set freeSpaceBytes=%%A
set freeSpaceBytes=!freeSpaceBytes:,=!
set /a freeSpaceGb=!freeSpaceBytes:~0,-3! / 1048576
set /a freeSpaceGbFraction=!freeSpaceBytes:~0,-3! %% 1048576 * 10 / 1048576

if "!volumeName!" == "" set volumeName=(No label)


REM call "!utilsScript!" WriteLine "!freeSpaceBytes! of !totalspacebytes! available on !VolumeName! (!os_name! !architecture! - !platform!)" !color_mute!
call "!utilsScript!" WriteLine "!freeSpaceGb!.!freeSpaceGbFraction!Gb of !totalSpaceGb!Gb available on !VolumeName! (!os_name! !architecture! - !platform!)" !color_mute!


:: Ensure directories are created and download required assets.

call "!utilsScript!" WriteLine
call "!utilsScript!" WriteLine "General CodeProject.AI setup" "White" "Blue" !lineWidth!
call "!utilsScript!" WriteLine

call "!utilsScript!" EnsureVCRedistInstalled

:: Before we start, ensure we can read the JSON config files        
call :SetupJSONParser
if errorlevel 1 goto:eof

:: Get assets endpoint
call "!utilsScript!" GetValueFromJsonFile "!appRootDirPath!server\appsettings.json" ".ModuleOptions.AssetStorageUrl"
if "!jsonFileValue!" NEQ "" set assetStorageUrl=!jsonFileValue!

:: Create some directories

call "!utilsScript!" Write "Creating Directories..."
if not exist "!runtimesDirPath!\" mkdir "!runtimesDirPath!"
if not exist "!downloadDirPath!\" mkdir "!downloadDirPath!"
if not exist "!downloadModuleAssetsDirPath!\" mkdir "!downloadModuleAssetsDirPath!\"
if not exist "!downloadDirPath!\!modelsDir!\" mkdir "!downloadDirPath!\!modelsDir!\"

call "!utilsScript!" WriteLine "done" "Green"
call "!utilsScript!" WriteLine ""

:: Output settings
if /i "%verbosity%" neq "quiet" (
    call "!utilsScript!" WriteLine 
    call "!utilsScript!" WriteLine "os, name, arch              = !os! !os_name! !architecture!" !color_mute!
    call "!utilsScript!" WriteLine "systemName, platform        = !systemName!, !platform!"      !color_mute!
    call "!utilsScript!" WriteLine "edgeDevice                  = !edgeDevice!"                  !color_mute!
    call "!utilsScript!" WriteLine "setupMode                   = !setupMode!"                   !color_mute!
    call "!utilsScript!" WriteLine "executionEnvironment        = !executionEnvironment!"        !color_mute!
    call "!utilsScript!" WriteLine "rootDirPath                 = !rootDirPath!"                 !color_mute!
    call "!utilsScript!" WriteLine "appRootDirPath              = !appRootDirPath!"              !color_mute!
    call "!utilsScript!" WriteLine "setupScriptDirPath          = !setupScriptDirPath!"          !color_mute!
    call "!utilsScript!" WriteLine "utilsScriptsDirPath         = !utilsScriptsDirPath!"         !color_mute!
    call "!utilsScript!" WriteLine "runtimesDirPath             = !runtimesDirPath!"             !color_mute!
    call "!utilsScript!" WriteLine "modulesDirPath              = !modulesDirPath!"              !color_mute!
    call "!utilsScript!" WriteLine "externalModulesDirPath      = !externalModulesDirPath!"      !color_mute!
    call "!utilsScript!" WriteLine "modelsDirPath               = !modelsDirPath!"               !color_mute!
    call "!utilsScript!" WriteLine "downloadDirPath             = !downloadDirPath!"             !color_mute!
    call "!utilsScript!" WriteLine "downloadModuleAssetsDirPath = !downloadModuleAssetsDirPath!" !color_mute!
    call "!utilsScript!" WriteLine "assetStorageUrl             = !assetStorageUrl!"             !color_mute!
    call "!utilsScript!" WriteLine
)

:: Report on GPU ability

:: GPU / CPU support ::::::::::::::::::::::::::::::::::::::::::::::::::::::::::

call "!utilsScript!" WriteLine "GPU support" "White" "DarkGreen" !lineWidth!
call "!utilsScript!" WriteLine ""

REM Test for CUDA drivers 
call "!utilsScript!" Write "CUDA Present..."

set hasCUDA=false
set hasCUDAToolkit=false

call "!utilsScript!" GetCudaVersion
if "!cuda_version!" neq "" set hasCUDA=true

if /i "!hasCUDA!" == "true" (

    REM CUDA Toolkit != CUDA drivers. We need the files and CUDA_PATH to be in place
    if exist "C:\Program Files\NVIDIA GPU Computing Toolkit\CUDA\v*" set hasCUDAToolkit=true
    if "%CUDA_PATH%" == "" ( set "hasCUDAToolkit=false" ) else ( set "hasCUDAToolkit=true" )

    call "!utilsScript!" GetCuDNNVersion
    if "!cuDNN_version!" == "" (
        call "!utilsScript!" WriteLine "Yes (CUDA !cuda_version!, No cuDNN found, CUDA Toolkit: !hasCUDAToolkit!)" !color_success!
    ) else (
        call "!utilsScript!" WriteLine "Yes (CUDA !cuda_version!, cuDNN !cuDNN_version!, CUDA Toolkit: !hasCUDAToolkit!)" !color_success!

        REM cuDNN install is a bag of loose parts.
        REM cuDNN is C:\Program Files\NVIDIA\CUDNN\v9.5\bin\12.6
        if /i "!PATH:C:\Program Files\NVIDIA\CUDNN\v!cuDNN_version!\bin\!cuda_version!=!" == "!PATH!" (
            call "!utilsScript!" WriteLine "Updating path to include cuDNN" !color_info!
            set PATH=!PATH!;C:\Program Files\NVIDIA\CUDNN\v!cuDNN_version!\bin\!cuda_version!\
            REM setx PATH !PATH!;C:\Program Files\NVIDIA\CUDNN\v!cuDNN_version!\bin\!cuda_version!\
            REM powershell -command "[Environment]::SetEnvironmentVariable('PATH', '!PATH!', 'Machine');
        )
    )
) else (
    call "!utilsScript!" WriteLine "No" !color_warn!
)

REM Test for AMD ROCm drivers 
call "!utilsScript!" Write "ROCm Present..."

set hasROCm=false
where rocm-smi >nul 2>nul
if "!errorlevel!" == "0" set hasROCm=true

if /i "%hasROCm%" == "true" (
    call "!utilsScript!" WriteLine "Yes" !color_success!
) else (
    call "!utilsScript!" WriteLine "No" !color_warn!
)


:: And off we go...

set "setupErrors="
set "moduleInstallErrors="

if /i "!setupMode!" == "SetupEverything" (

    REM Start with the CodeProject.AI SDK and Server
    if /i "!setupModulesOnly!" == "false" if /i "!selfTestOnly!" == "false" (

        call "!utilsScript!" WriteLine
        call "!utilsScript!" WriteLine "Processing CodeProject.AI SDK" "White" "DarkGreen" !lineWidth!
        call "!utilsScript!" WriteLine

        set moduleDirName=SDK
        set moduleDirPath=!appRootDirPath!!moduleDirName!
        set "moduleInstallErrors="

        REM Note that the SDK install will setup .NET since the SDK relies on it

        call :SaveState
        call "!moduleDirPath!\install.bat" install   
        if errorlevel 1 if "!moduleInstallErrors!" == "" set moduleInstallErrors=CodeProject.AI SDK install failed
        call :RestoreState

        if "!moduleInstallErrors!" NEQ "" set setupErrors=!setupErrors!!LF! - [SDK] !moduleInstallErrors!!LF!


        call "!utilsScript!" WriteLine
        call "!utilsScript!" WriteLine "Processing CodeProject.AI Server" "White" "DarkGreen" !lineWidth!
        call "!utilsScript!" WriteLine

        set moduleDirName=server
        set moduleDirPath=!appRootDirPath!!moduleDirName!
        set "moduleInstallErrors="

        call :SaveState
        call "!moduleDirPath!\install.bat" install
        if errorlevel 1 if "!moduleInstallErrors!" == "" set moduleInstallErrors=CodeProject.AI Server install failed
        call :RestoreState

        if "!moduleInstallErrors!" NEQ "" set setupErrors=!setupErrors!!LF! - [Server] !moduleInstallErrors!!LF!
    )

    REM Walk through the modules directory and call the setup script in each dir,
    REM as well as setting up the demos

    if /i "!setupServerOnly!" == "false" (
 
        call "!utilsScript!" WriteLine
        call "!utilsScript!" WriteLine "Processing Included CodeProject.AI Server Modules" "White" "DarkGreen" !lineWidth!
        call "!utilsScript!" WriteLine

        REM  TODO: This should be just a simple for /d %%D in ("!modulesDirPath!") do (
        for /f "delims=" %%D in ('dir /a:d /b "!modulesDirPath!"') do (
            set moduleDirName=%%~nxD
            set moduleDirPath=!modulesDirPath!\!moduleDirName!

            call "!utilsScript!" GetModuleIdFromModuleSettingsFile "!moduleDirPath!\modulesettings.json"
            set moduleId=!moduleSettingValue!

            call :DoModuleInstall "!moduleId!" "!moduleDirPath!" "Internal" errors
            if "!errors!" NEQ "" set setupErrors=!setupErrors!!LF! - [!moduleId!] !errors!!LF!
        )

        if /i "!installExternalModules!" == "true" (
            call "!utilsScript!" WriteLine
            call "!utilsScript!" WriteLine "Processing External CodeProject.AI Server Modules" "White" "DarkGreen" !lineWidth!
            call "!utilsScript!" WriteLine

            if exist !externalModulesDirPath! (
                for /f "delims=" %%D in ('dir /a:d /b "!externalModulesDirPath!"') do (
                    set moduleDirName=%%~nxD
                    set moduleDirPath=!externalModulesDirPath!\!moduleDirName!

                    call "!utilsScript!" GetModuleIdFromModuleSettingsFile "!moduleDirPath!\modulesettings.json"
                    set moduleId=!moduleSettingValue!

                    call :DoModuleInstall "!moduleId!" "!moduleDirPath!" "External" errors
                    if "!errors!" NEQ "" set setupErrors=!setupErrors!!LF! - [!moduleId!] !errors!!LF!
                )
            ) else (
                call "!utilsScript!" WriteLine "No external modules found" !color_mute!
            )
        )

        call "!utilsScript!" WriteLine
        call "!utilsScript!" WriteLine "Module setup Complete" "!color_success!"

        REM Install Demo clients
        if /i "!selfTestOnly!" == "false" if /i "!setupModulesOnly!" == "false" (
            if /i "!executionEnvironment!" == "Development" (
                call "!utilsScript!" WriteLine
                call "!utilsScript!" WriteLine "Processing Demo clients" "White" "Blue" !lineWidth!
                call "!utilsScript!" WriteLine

                set moduleDirName=clients
                set moduleDirPath=!rootDirPath!\src\demos\!moduleDirName!
                set "moduleInstallErrors="

                call :SaveState
                call "!moduleDirPath!\install.bat" install
                if errorlevel 1 if "!moduleInstallErrors!" == "" set moduleInstallErrors=failed to install
                call :RestoreState

                if "!moduleInstallErrors!" NEQ "" set setupErrors=!setupErrors!!LF! - [Demo clients] !moduleInstallErrors!!LF!
            )
        )

        REM Install Demo modules
        if /i "!executionEnvironment!" == "Development" (
            call "!utilsScript!" WriteLine
            call "!utilsScript!" WriteLine "Processing Demo Modules" "White" "Blue" !lineWidth!
            call "!utilsScript!" WriteLine

            set oldModulesDirPath=!modulesDirPath!
            set modulesDirPath=!rootDirPath!\src\demos\modules\
            for /f "delims=" %%D in ('dir /a:d /b "!modulesDirPath!"') do (
                set moduleDirName=%%~nxD
                set moduleDirPath=!modulesDirPath!\!moduleDirName!

                call "!utilsScript!" GetModuleIdFromModuleSettingsFile "!moduleDirPath!\modulesettings.json"
                set moduleId=!moduleSettingValue!

                call :DoModuleInstall "!moduleId!" "!moduleDirPath!" "Demo" errors
                if "!errors!" NEQ "" set setupErrors=!setupErrors!!LF! - [!moduleId!] !errors!!LF!
            )
            set modulesDirPath=!oldModulesDirPath!
        )
    )

) else (

    if /i "!setupServerOnly!" == "false" (

        REM Quick sanity check to ensure .NET is in place. .NET install is done
        REM in the SDK setup, but we do it here too since it's a null-op if it's
        REM in place already, and the SDK setup may not always be called.
        call "%utilsScript%" SetupDotNet !dotNetRuntimeVersion! aspnetcore
        if /i "!executionEnvironment!" == "Development" (
            call "%utilsScript%" SetupDotNet !dotNetSDKVersion! SDK
        )
            
        REM Install an individual module
        
        for %%I in (.) do set moduleDirName=%%~nxI

        if /i "!moduleDirName!" == "server" (       REM Not a module. The server

            if /i "!setupModulesOnly!" == "false" if /i "!selfTestOnly!" == "false" (
                set moduleDirPath=!appRootDirPath!\!moduleDirName!
                set "moduleInstallErrors="

                call :SaveState
                call "!moduleDirPath!\install.bat" install
                if errorlevel 1 if "!moduleInstallErrors!" == "" set moduleInstallErrors=failed to install
                call :RestoreState

                if "!moduleInstallErrors!" NEQ "" set setupErrors=!setupErrors!!LF! - [Server] !moduleInstallErrors!!LF!
            )
        ) else if /i "!moduleDirName!" == "clients" (   REM Not a module. The demo clients

            if /i "!setupModulesOnly!" == "false" if /i "!selfTestOnly!" == "false" (
                set moduleDirPath=!rootDirPath!\src\demos\!moduleDirName!
                set "moduleInstallErrors="

                call :SaveState
                call "!moduleDirPath!\install.bat" install
                if errorlevel 1 if "!moduleInstallErrors!" == "" set moduleInstallErrors=failed to install
                call :RestoreState

                if "!moduleInstallErrors!" NEQ "" set setupErrors=!setupErrors!!LF! - [Demo clients] !moduleInstallErrors!!LF!
            )
        ) else (

            REM No need to check "$selfTestOnly" here because this check is done
            REM in DoModuleInstall. We need to run doModuleInstall to have the
            REM selftest called

            for %%I in (..) do set parentDirName=%%~nxI
            for %%I in (..\..) do set parentParentDirName=%%~nxI

            if /i "!parentParentDirName!" == "demos" (                       REM Demo module

                set oldModulesDirPath=!modulesDirPath!
                set modulesDirPath=!rootDirPath!\src\demos\modules\

                set moduleDirPath=!modulesDirPath!\!moduleDirName!
                call "!utilsScript!" GetModuleIdFromModuleSettingsFile "!moduleDirPath!\modulesettings.json"
                set moduleId=!moduleSettingValue!

                call :DoModuleInstall "!moduleId!" "!moduleDirPath!" "Demo" errors
                if "!errors!" NEQ "" set setupErrors=!setupErrors!!LF! - [Demo modules] !errors!!LF!

                set modulesDirPath=!oldModulesDirPath!

            ) else if /i "!parentDirName!" == "!externalModulesDir!" (       REM External module

                set moduleDirPath=%cd%
                call "!utilsScript!" GetModuleIdFromModuleSettingsFile "!moduleDirPath!\modulesettings.json"
                set moduleId=!moduleSettingValue!

                call :DoModuleInstall "!moduleId!" "!moduleDirPath!" "External" errors
                if "!errors!" NEQ "" set setupErrors=!setupErrors!!LF! - [!moduleId!] !errors!!LF!

            ) else (                                                         REM Internal module

                set moduleDirPath=!modulesDirPath!\!moduleDirName!
                call "!utilsScript!" GetModuleIdFromModuleSettingsFile "!moduleDirPath!\modulesettings.json"
                set moduleId=!moduleSettingValue!

                call :DoModuleInstall "!moduleId!" "!moduleDirPath!" "Internal" errors
                if "!errors!" NEQ "" set setupErrors=!setupErrors!!LF! - [!moduleId!] !errors!!LF!
            )
        )
    )
)

:: =============================================================================
:: ...and we're done.

call "!utilsScript!" WriteLine
call "!utilsScript!" WriteLine "Setup complete" "White" "DarkGreen" !lineWidth!
call "!utilsScript!" WriteLine
call "!utilsScript!" timeSince "!mainSetupStarttime!" duration
call "!utilsScript!" WriteLine "Total setup time !duration!" "!color_info!"

if "!setupErrors!" == "" (
    exit /b 0
) else (

    call "!utilsScript!" WriteLine
    call "!utilsScript!" WriteLine "SETUP FAILED:" "!color_warn!"
    rem call "!utilsScript!" WriteLine "!setupErrors!" "!color_error!"
    echo !setupErrors!
    
    exit /b 1
)

REM Pop over the subroutine definitions and leave.
goto:eof


:SetupJSONParser

    if /i "!executionEnvironment!" == "Development" (
        call "%utilsScript%" SetupDotNet !dotNetSDKVersion! SDK
    )

    pushd !rootDirPath!\utils\ParseJSON
    if not exist ParseJSON.exe (
        call "!utilsScript!" Write "Building ParseJSON..."
        if /i "!verbosity!" == "quiet" (
            dotnet build /property:GenerateFullPaths=true /consoleloggerparameters:NoSummary -c Release >NUL
        ) else (
            dotnet build /property:GenerateFullPaths=true /consoleloggerparameters:NoSummary -c Release
        )
        if exist .\bin\Release\!dotNetTarget!\ move .\bin\Release\!dotNetTarget!\* . >nul

        if exist ParseJSON.exe (
            call "!utilsScript!" WriteLine "Success." !color_success!
        ) else (
            call "!utilsScript!" WriteLine "Failed. Exiting setup" !color_error!
            popd
            exit /b 1
        )
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

    REM The path to the python interpreter for this venv
    set venvPythonCmdPath=!virtualEnvDirPath!\Scripts\python.exe

    REM The location where python packages will be installed for this venv
    set packagesDirPath=%virtualEnvDirPath%\Lib\site-packages

    exit /b


REM Installs a module in the module's directory, and returns success 
:DoModuleInstall moduleId moduleDirPath ModuleType errors

    SetLocal EnableDelayedExpansion

    set moduleId=%~1
    set moduleDirPath=%~2
    set moduleType=%~3

    set moduleSetupStarttime=%time%

    REM Set the error message value for this module install operation
    set "moduleInstallErrors="

    REM Get the module name, version, runtime location and python version from
    REM the modulesettings.
    
    call "!utilsScript!" WriteLine
    call "!utilsScript!" Write "Reading !moduleId! settings" !color_mute!

    call "!utilsScript!" GetValueFromModuleSettingsFile "!moduleDirPath!", "!moduleId!", "Name"
    set moduleName=!moduleSettingsFileValue!
    call "!utilsScript!" Write "." !color_mute!

    call "!utilsScript!" GetValueFromModuleSettingsFile "!moduleDirPath!", "!moduleId!", "Version"
    set moduleVersion=!moduleSettingsFileValue!
    call "!utilsScript!" Write "." !color_mute!

    call "!utilsScript!" GetValueFromModuleSettingsFile "!moduleDirPath!", "!moduleId!", "LaunchSettings.Runtime"
    set runtime=!moduleSettingsFileValue!
    call "!utilsScript!" Write "." !color_mute!

    call "!utilsScript!" GetValueFromModuleSettingsFile "!moduleDirPath!", "!moduleId!", "LaunchSettings.RuntimeLocation"
    set runtimeLocation=!moduleSettingsFileValue!
    call "!utilsScript!" Write "." !color_mute!

    call "!utilsScript!" GetValueFromModuleSettingsFile "!moduleDirPath!", "!moduleId!", "LaunchSettings.FilePath"
    set moduleStartFilePath=!moduleSettingsFileValue!
    call "!utilsScript!" Write "." !color_mute!

    call "!utilsScript!" GetValueFromModuleSettingsFile "!moduleDirPath!", "!moduleId!", "GpuOptions.InstallGPU"
    set installGPU=!moduleSettingsFileValue!
    call "!utilsScript!" Write "." !color_mute!

    call "!utilsScript!" GetValueFromModuleSettingsFile "!moduleDirPath!", "!moduleId!", "InstallOptions.Platforms"
    set platforms=!moduleSettingsFileValue!
    call "!utilsScript!" Write "." !color_mute!

    call "!utilsScript!" WriteLine "done" !color_success!

    if "!moduleName!" == "" set moduleName=!moduleId!

    set announcement=Installing module !moduleName! !moduleVersion! (!moduleType!)
    call "!utilsScript!" WriteLine "!announcement!" "White" "Blue" !lineWidth!
    call "!utilsScript!" WriteLine

    REM remove spaces and [,] from ends of platforms value
    if /i "%verbosity%" == "loud" (
        call "!utilsScript!" WriteLine "Platform = !platform!, Platforms = !platforms!" !color_mute!
    )

    set "platformArray=!platforms: =!"
    set "platformArray=!platformArray:~1,-1!"

    set can_install=false
    for %%i in (!platformArray!) do (
        set "item=%%~i"
        REM echo Checking !platform! against !item!

        REM Excluded?
        if /i "!item!" == "^!!platform!" (
            REM echo Negative match of !platform! against !item!
            set can_install=false
            goto :end_platform_loop
        )

        REM alternative excluded. "ǃ" here is U+01c3, not the usual U+0021
        if /i "!item!" == "ǃ!platform!" (
            REM echo Negative match of !platform! against !item!
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
        call "!utilsScript!" WriteLine "This module cannot be installed on this system" !color_warn!
        exit /b
    )

    call :SaveState

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
        call "!utilsScript!" WriteLine
        call "!utilsScript!" WriteLine "Variable Dump" "White" "Blue" !lineWidth!
        call "!utilsScript!" WriteLine
        call "!utilsScript!" WriteLine "moduleName          = !moduleName!"          "!color_info!"
        call "!utilsScript!" WriteLine "moduleId            = !moduleId!"            "!color_info!"
        call "!utilsScript!" WriteLine "moduleVersion       = !moduleVersion!"       "!color_info!"
        call "!utilsScript!" WriteLine "runtime             = !runtime!"             "!color_info!"
        call "!utilsScript!" WriteLine "runtimeLocation     = !runtimeLocation!"     "!color_info!"
        call "!utilsScript!" WriteLine "installGPU          = !installGPU!"          "!color_info!"
        call "!utilsScript!" WriteLine "pythonVersion       = !pythonVersion!"       "!color_info!"
        call "!utilsScript!" WriteLine "virtualEnvDirPath   = !virtualEnvDirPath!"   "!color_info!"
        call "!utilsScript!" WriteLine "venvPythonCmdPath   = !venvPythonCmdPath!"   "!color_info!"
        call "!utilsScript!" WriteLine "packagesDirPath     = !packagesDirPath!"     "!color_info!"
        call "!utilsScript!" WriteLine "moduleStartFilePath = !moduleStartFilePath!" "!color_info!"
    )

    REM call "!utilsScript!" WriteLine "!moduleType! module install" !color_mute!

    if exist "!moduleDirPath!\install.bat" (

        REM If a python version has been specified then we'll automatically setup
        REM the correct python environment. We do this before the script runs so
        REM the script can use python in the script.
        if "!pythonVersion!" neq "" (
            if /i "!selfTestOnly!" == "false" (
                call "!utilsScript!" WriteLine "Installing Python !pythonVersion!"
                call "%utilsScript%" SetupPython
                if errorlevel 1 set moduleInstallErrors=Unable to install Python !pythonVersion!
                if "!moduleInstallErrors!" NEQ "" set setupErrors=!setupErrors!!LF! - !moduleInstallErrors!!LF!
            )
        )

        REM Install the module, but only if there were no issues installing
        REM python (or a python install wasn't needed)
        if /i "!moduleInstallErrors!" == "" if /i "!selfTestOnly!" == "false" (

            call "!moduleDirPath!\install.bat" install
            if errorlevel 1 if "!moduleInstallErrors!" == "" set moduleInstallErrors=failed to install

            if "!moduleInstallErrors!" NEQ "" set setupErrors=!setupErrors!!LF! - [!moduleName!] !moduleInstallErrors!!LF!
        )

        REM If a python version has been specified then we'll automatically
        REM look for, and install, the requirements file for the module, and
        REM then also the requirements file for the SDK since it'll be assumed
        REM the Python SDK will come into play.
        if /i "!selfTestOnly!" == "false" (
            if /i "!moduleInstallErrors!" == "" (
                if "!pythonVersion!" neq "" (
                    call "!utilsScript!" WriteLine "Installing Python packages for !moduleName!"

                    call "!utilsScript!" Write "Installing GPU-enabled libraries: " $color_info
                    if "!installGPU!" == "true" (
                        call "!utilsScript!" WriteLine "If available" !color_success!
                    ) else (
                        call "!utilsScript!" WriteLine "No" !color_warn!
                    )

                    call "!utilsScript!" InstallRequiredPythonPackages 
                    if errorlevel 1 set moduleInstallErrors=Unable to install Python packages
                    if "!moduleInstallErrors!" NEQ "" set setupErrors=!setupErrors!!LF! - [!moduleName!] !moduleInstallErrors!!LF!

                    REM With the move to having modules include our SDK PyPi, we no longer need this.
                    REM call "!utilsScript!" WriteLine "Installing Python packages for the CodeProject.AI Server SDK"
                    REM call "!utilsScript!" InstallRequiredPythonPackages "%sdkPath%\Python"
                    REM if errorlevel 1 set moduleInstallErrors=Unable to install Python packages for CodeProject SDK
                    REM if "!moduleInstallErrors!" NEQ "" set setupErrors=!setupErrors!!LF! - [!moduleName!] !moduleInstallErrors!!LF!
                )

                call "!utilsScript!" downloadModels 

            ) else (
                call "!utilsScript!" WriteLine "Skipping PIP installs and model downloads due to install error (!moduleInstallErrors!)" !color_warn!
            )
        )

        REM And finally, the post install script if one was provided
        if exist "!moduleDirPath!\post_install.bat" (
            if /i "!moduleInstallErrors!" == "" (
                if /i "!selfTestOnly!" == "false" (
                    call "!utilsScript!" WriteLine "Executing post-install script for !moduleName!"

                    call "!moduleDirPath!\post_install.bat" post-install
                    if errorlevel 1 if "!moduleInstallErrors!" == "" set moduleInstallErrors=Error running post-install script

                    if "!moduleInstallErrors!" NEQ "" set setupErrors=!setupErrors!!LF! - [!moduleName!] !moduleInstallErrors!!LF!
                )
            ) else (
                call "!utilsScript!" WriteLine "Skipping post install due to install error" !color_warn!
            )
        )

        if "!moduleInstallErrors!" NEQ "" call "!utilsScript!" WriteLine "Install failed: !moduleInstallErrors!" "!color_error!"

        REM Perform a self-test
        if /i "!noSelfTest!" == "false" if "!moduleInstallErrors!" == "" (

            set currentDir=%cd%
            cd "!moduleDirPath!"

            if /i "%verbosity%" == "quiet" (
                call "!utilsScript!" Write "Self test: "
            ) else (
                call "!utilsScript!" WriteLine "SELF TEST START ======================================================" !color_info!
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
                REM if /i "!executionEnvironment!" == "Development" set "exePath=.\bin\Debug\!dotNetTarget!\"

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
                    call "!utilsScript!" WriteLine  "!exePath!!moduleStartFilePath! does not exist" !color_error!
                )
            )

            if errorlevel 1 (
                set "moduleInstallErrors=Self test failed"
                call "!utilsScript!" WriteLine "Self-test failed" !color_error!
            ) else (
                if /i "!testRun!" == "true" (
                    call "!utilsScript!" WriteLine "Self-test passed" !color_success!
                ) else (
                    call "!utilsScript!" WriteLine "No self-test available" !color_warn!
                )
            )
            
            if /i "%verbosity%" NEQ "quiet" (
                call "!utilsScript!" WriteLine "SELF TEST END   ======================================================" !color_info!
            )
            
            cd "!currentDir!"
        )

    ) else (
        call "!utilsScript!" WriteLine "No install.bat present for !moduleName!" "!color_warn!"
    )

    call :RestoreState

    call "!utilsScript!" timeSince "!moduleSetupStarttime!" duration
    call "!utilsScript!" WriteLine "Module setup time !duration!" "!color_info!"

    REM return result
    EndLocal & set "%~4=%moduleInstallErrors%"

    exit /b


REM Saves the state of the installation environment 
:SaveState

    set stateCurrentDir=%cd%
    set stateVerbosity=!verbosity!
    set stateOneStepPIP=!oneStepPIP!
    set stateHasCUDA=!hasCUDA!
    set stateInstallGPU=!installGPU!

    exit /b

REM Restores the state of the installation environment 
:RestoreState

    cd "!stateCurrentDir!"
    set verbosity=!stateVerbosity!
    set oneStepPIP=!stateOneStepPIP!
    set hasCUDA=!stateHasCUDA!
    set installGPU=!stateInstallGPU!

    exit /b
