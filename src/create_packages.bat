:: CodeProject.AI Server 
::
:: Create packages script for Windows
::
:: This script will look for a package.bat script each of the modules directories
:: and execute that script. The package.bat script is responsible for packaging
:: up everything needed for the module to be ready to install.

@echo off
REM cls
setlocal enabledelayedexpansion

:: verbosity can be: quiet | info | loud
set verbosity=quiet

:: Show output in wild, crazy colours
set useColor=true

:: Set this to false (or call script with --no-dotnet) to exclude .NET packages
:: This saves time to allow for quick packaging of the easier, non-compiled modules
set includeDotNet=true

:: Basic locations

:: The path to the directory containing the setup script. Will end in "\"
set setupScriptDirPath=%~dp0

:: The path to the application root dir. This is 'src' in dev, or / in production
:: This setup script always lives in the app root
set appRootDirPath=!setupScriptDirPath!

:: The name of the source directory
set srcDirName=src

:: The name of the dir, within the current directory, where install assets will
:: be downloaded
set downloadDir=downloads

:: The name of the dir holding the downloaded/sideloaded modules
set modulesDir=modules

:: The name of the dir holding the external modules
set externalModulesDir=CodeProject.AI-Modules

set sdkPath=!appRootDirPath!SDK

:: Whether or not to use the jq utility for JSON parsing
set useJq=false


:: Override some values via parameters ::::::::::::::::::::::::::::::::::::::::

:param_loop
    set arg_name=%~1
    set arg_value=%~2
    if not "!arg_name!" == "" (
        if not "!arg_name:--no-color=!" == "!arg_name!" set useColor=false
        if not "!arg_name:--no-dotnet=!" == "!arg_name!" set includeDotNet=false
        if not "!arg_name:--path-to-setup=!" == "!arg_name!" (
            set setupScriptDirPath=!arg_value!
            shift
        )
        if not "!arg_name:--verbosity=!" == "!arg_name!" (
            set verbosity=!arg_value!
            shift
        )
    )
    shift
if not "!arg_name!"=="" goto param_loop


:: This can be x86 (32-bit), AMD64 (Intel/AMD 64bit), ARM64 (Arm 64bit)
set architecture=%PROCESSOR_ARCHITECTURE%

:: A NOTE ON PLATFORM.
:: We use the full "x86_64" for architecture, but follow the common convention
:: of abbreviating this to "x64" when used in conjuntion with OS. So windows-x64
:: rather than windows-x86_64. To simplify further, if the platform value doesn't
:: have a suffix then it's assumed to be -x64. This may change in the future.
if /i "!architecture!" == "amd64" set architecture=x86_64
if /i "!architecture!" == "ARM64" set architecture=arm64

:: In Development, this script is in the /src folder. In Production there is no
:: /src folder; everything is in the root folder. So: go to the folder
:: containing this script and check the name of the parent folder to see if
:: we're in dev or production.
pushd "!setupScriptDirPath!"
for /f "delims=\" %%a in ("%cd%") do @set setupScriptDirName=%%~nxa
popd

set executionEnvironment=Production
if /i "%setupScriptDirName%" == "%srcDirName%" set executionEnvironment=Development

:: The absolute path to the setup script and the root directory. Note that
:: this script (and the SDK folder) is either in the /src dir or the root dir
pushd "!setupScriptDirPath!"
set sdkScriptsDirPath=%cd%\SDK\Scripts
if /i "%executionEnvironment%" == "Development" cd ..
set rootDirPath=%cd%
popd

set appRootDirPath=!setupScriptDirPath!

:: Platform can define where things are located :::::::::::::::::::::::::::::::

:: The location of directories relative to the root of the solution directory
set modulesDirPath=!appRootDirPath!!modulesDir!
set externalModulesDirPath=!appRootDirPath!\..\!externalModulesDir!
set downloadDirPath=!appRootDirPath!!downloadDir!

:: Let's go
if /i "!useColor!" == "true" call "!sdkScriptsDirPath!\utils.bat" setESC
if /i "!executionEnvironment!" == "Development" (
    set scriptTitle=          Creating CodeProject.AI Module Downloads
) else (
    writeLine "Can't run in Production. Exiting" "Red"
    goto:eof
)

set lineWidth=70

call "!sdkScriptsDirPath!\utils.bat" WriteLine 
call "!sdkScriptsDirPath!\utils.bat" WriteLine "!scriptTitle!" "DarkYellow" "Default" !lineWidth!
call "!sdkScriptsDirPath!\utils.bat" WriteLine 
call "!sdkScriptsDirPath!\utils.bat" WriteLine "========================================================================" "DarkGreen" 
call "!sdkScriptsDirPath!\utils.bat" WriteLine 
call "!sdkScriptsDirPath!\utils.bat" WriteLine "                   CodeProject.AI Packager                             " "DarkGreen" 
call "!sdkScriptsDirPath!\utils.bat" WriteLine 
call "!sdkScriptsDirPath!\utils.bat" WriteLine "========================================================================" "DarkGreen" 
call "!sdkScriptsDirPath!\utils.bat" WriteLine 


if /i "%verbosity%" neq "quiet" (
    call "!sdkScriptsDirPath!\utils.bat" WriteLine 
    call "!sdkScriptsDirPath!\utils.bat" WriteLine "executionEnvironment   = !executionEnvironment!"   !color_mute!
    call "!sdkScriptsDirPath!\utils.bat" WriteLine "appRootDirPath         = !appRootDirPath!"         !color_mute!
    call "!sdkScriptsDirPath!\utils.bat" WriteLine "setupScriptDirPath     = !setupScriptDirPath!"     !color_mute!
    call "!sdkScriptsDirPath!\utils.bat" WriteLine "sdkScriptsDirPath      = !sdkScriptsDirPath!"      !color_mute!
    call "!sdkScriptsDirPath!\utils.bat" WriteLine "modulesDirPath         = !modulesDirPath!"         !color_mute!
    call "!sdkScriptsDirPath!\utils.bat" WriteLine "externalModulesDirPath = !externalModulesDirPath!" !color_mute!
    call "!sdkScriptsDirPath!\utils.bat" WriteLine
)

:: And off we go...

set success=true

pushd SDK\Utilities\ParseJSON
if not exist ParseJSON.exe (
    dotnet build /property:GenerateFullPaths=true /consoleloggerparameters:NoSummary -c Release >NUL
    if exist .\bin\Release\net7.0\ move .\bin\Release\net7.0\* . >nul
)
popd

REM  Walk through the modules directory and call the package script in each dir
rem Make this just "for /d %%D in ("%modulesDirPath%") do ("

for /f "delims=" %%D in ('dir /a:d /b "!modulesDirPath!"') do (

    set packageModuleDirName=%%~nxD
    set packageModuleDirPath=!modulesDirPath!\!packageModuleDirName!

    REM Bad assumption: A module's ID is same as the name of folder in which it lives.
    REM set packageModuleId=!packageModuleDirName!

    call "!sdkScriptsDirPath!\utils.bat" GetModuleIdFromModuleSettingsFile "!packageModuleDirPath!\modulesettings.json"
    set packageModuleId=!moduleSettingValue!

    if "!packageModuleId!" neq "" (
        call :DoModulePackage "!packageModuleId!" "!packageModuleDirName!" "!packageModuleDirPath!" errors
        if "!moduleInstallErrors!" NEQ "" set success=false
    )
)

for /f "delims=" %%D in ('dir /a:d /b "!externalModulesDirPath!"') do (

    set packageModuleDirName=%%~nxD
    set packageModuleDirPath=!modulesDirPath!\!packageModuleDirName!

    REM Bad assumption: A module's ID is same as the name of folder in which it lives.
    REM set packageModuleId=!packageModuleDirName!

    call "!sdkScriptsDirPath!\utils.bat" GetModuleIdFromModuleSettingsFile "!packageModuleDirPath!\modulesettings.json"
    set packageModuleId=!moduleSettingValue!

    if "!packageModuleId!" neq "" (
        call :DoModulePackage "!packageModuleId!" "!packageModuleDirName!" "!packageModuleDirPath!" errors
        if "!moduleInstallErrors!" NEQ "" set success=false
    )
)

call "!sdkScriptsDirPath!\utils.bat" WriteLine
call "!sdkScriptsDirPath!\utils.bat" WriteLine "                Modules packaging Complete" "White" "DarkGreen" !lineWidth!
call "!sdkScriptsDirPath!\utils.bat" WriteLine

if /i "!success!" == "false" exit /b 1
goto:eof


REM ============================================================================

REM Creates a package for a module 
:DoModulePackage moduleDirName moduleDirPath errors

    SetLocal EnableDelayedExpansion

    set packageModuleId=%~1
    set packageModuleDirName=%~2
    set packageModuleDirPath=%~3

    if /i "%verbosity%" neq "quiet" (
        call "!sdkScriptsDirPath!\utils.bat" WriteLine "packageModuleDirPath           = !packageModuleDirPath!" !color_mute!
    )

    if exist "!packageModuleDirPath!\package.bat" (

        set doPackage=true

        if "!includeDotNet!" == "false" if /i "!packageModuleId!" == "ObjectDetectionYOLOv5Net" set doPackage=false
        if "!includeDotNet!" == "false" if /i "!packageModuleId!" == "PortraitFilter"           set doPackage=false
        if "!includeDotNet!" == "false" if /i "!packageModuleId!" == "SentimentAnalysis"        set doPackage=false

        if "!doPackage!" == "false" (
            call "!sdkScriptsDirPath!\utils.bat" WriteLine "Skipping packaging module !packageModuleId!..." "DarkRed"
        ) else (
            REM Read the version from the modulesettings.json file and then pass this 
            REM version to the package.bat file.
            call "!sdkScriptsDirPath!\utils.bat" Write "Preparing !packageModuleId!..."
            call "!sdkScriptsDirPath!\utils.bat" GetValueFromModuleSettingsFile "!packageModuleDirPath!", "!packageModuleId!", "Version"
            set packageVersion=!moduleSettingsFileValue!
        )

        if "!packageVersion!" == "" (
            call "!sdkScriptsDirPath!\utils.bat" WriteLine "Unable to read version from modulesettings file. Skipping" "Red"
        ) else if "!doPackage!" == "true" (

            pushd "!packageModuleDirPath!" 
            if /i "%verbosity%" == "loud" cd

            rem Create module download package
            if exist package.bat (
                call "!sdkScriptsDirPath!\utils.bat" Write "Packaging !packageModuleId! !packageVersion!..." "White"
                call package.bat !packageModuleId! !packageVersion!
                if errorlevel 1 call "!sdkScriptsDirPath!\utils.bat" WriteLine "Error in package.bat for !packageModuleDirName!" "Red"

                rem Move package into modules download cache

                rem echo Moving !packageModuleDirPath!\!packageModuleId!-!version!.zip to !downloadDirPath!\!modulesDir!\
                move /Y !packageModuleDirPath!\!packageModuleId!-!packageVersion!.zip !downloadDirPath!\!modulesDir!\  >NUL

                if errorlevel 1 (
                    call "!sdkScriptsDirPath!\utils.bat" WriteLine "Error" "Red"
                    set success=false
                ) else (
                    call "!sdkScriptsDirPath!\utils.bat" WriteLine "done" "DarkGreen"
                )
            ) else (
                call "!sdkScriptsDirPath!\utils.bat" WriteLine "No package.bat file found..." "Red"
            )

            popd
        )
    )

    exit /b
