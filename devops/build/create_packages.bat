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

:: Whether or not to use the jq utility for JSON parsing
set useJq=false

:: Set this to false (or call script with --no-dotnet) to exclude .NET packages
:: This saves time to allow for quick packaging of the easier, non-compiled modules
set includeDotNet=true

:: Width of lines
set lineWidth=70

:: Set this to true to create packages for modules in the ..\CodeProject.AI-Modules
:: folder
set createExternalModulePackages=true

::set debug_json_parse=true

set dotNetTarget=net9.0


:: Basic locations

:: The name of the dir, within the root directory, where packages will be stored
set packageDir=\downloads\modules\packages

:: The name of the dir holding the source code
set srcDir=src

:: The name of the dir holding the SDK
set sdkDir=SDK

:: The name of the dir holding the downloaded/sideloaded modules
set modulesDir=modules

:: The name of the dir holding the external modules
set externalModulesDir=CodeProject.AI-Modules


:: The path to the directory containing this script. Will end in "\"
set thisScriptDirPath=%~dp0

:: We're assuming this script lives in /devops/build
pushd %thisScriptDirPath%..\..
set rootDirPath=%cd%
popd
set sdkPath=%rootDirPath%\%srcDir%\%sdkDir%
set utilsScriptsDirPath=%rootDirPath%\%srcDir%\scripts
set utilsScript=%utilsScriptsDirPath%\utils.bat

:: Override some values via parameters ::::::::::::::::::::::::::::::::::::::::

:param_loop
    if "%~1"=="" goto :end_param_loop
    if "%~1"=="--verbosity" (
        set "verbosity=%~2"
        if /i "!verbosity!" neq "loud" if /i "!verbosity!" neq "info" if /i "!verbosity!" neq "quiet" (
            set verbosity=quiet
        )
        shift
        shift
        goto :param_loop
    )
    if "%~1"=="--no-dotnet" (
        set includeDotNet=false
        shift
        goto :param_loop
    )
    if "%~1"=="--no-color" (
        set useColor=false
        shift
        goto :param_loop
    )
    REM No longer supporting this scenario
    REM if "%~1"=="--path-to-setup" (
    REM     set "setupScriptDirPath=%~2"
    REM     shift
    REM     shift
    REM     goto :param_loop
    REM )
    shift
:end_param_loop

:: Load vars in .env. This may update things like dotNetTarget
for /f "tokens=1,2 delims==" %%a in (!rootDirPath!\.env) do set %%a=%%b

:: The location of directories relative to the root of the solution directory
set modulesDirPath=!rootDirPath!\!modulesDir!
set externalModulesDirPath=!rootDirPath!\..\!externalModulesDir!
set packageDirPath=!rootDirPath!\!packageDir!

if not exist "!packageDirPath!" mkdir "!packageDirPath!"


:: Let's go
if /i "!useColor!" == "true" call "!utilsScript!" setESC

set lineWidth=70

call "!utilsScript!" WriteLine 
call "!utilsScript!" WriteLine "Creating CodeProject.AI Module Downloads" "DarkYellow" "Default" !lineWidth!
call "!utilsScript!" WriteLine 
call "!utilsScript!" WriteLine "========================================================================" "DarkGreen" 
call "!utilsScript!" WriteLine 
call "!utilsScript!" WriteLine "                   CodeProject.AI Packager                             " "DarkGreen" 
call "!utilsScript!" WriteLine 
call "!utilsScript!" WriteLine "========================================================================" "DarkGreen" 
call "!utilsScript!" WriteLine 


if /i "%verbosity%" neq "quiet" (
    call "!utilsScript!" WriteLine 
    call "!utilsScript!" WriteLine "rootDirPath            = !rootDirPath!"            !color_mute!
    call "!utilsScript!" WriteLine "thisScriptDirPath      = !thisScriptDirPath!"      !color_mute!
    call "!utilsScript!" WriteLine "sdkPath                = !sdkPath!"                !color_mute!
    call "!utilsScript!" WriteLine "utilsScriptsDirPath    = !utilsScriptsDirPath!"    !color_mute!
    call "!utilsScript!" WriteLine "modulesDirPath         = !modulesDirPath!"         !color_mute!
    call "!utilsScript!" WriteLine "externalModulesDirPath = !externalModulesDirPath!" !color_mute!
    call "!utilsScript!" WriteLine
)

:: And off we go...

set success=true

pushd "!rootDirPath!\utils\ParseJSON"
if not exist ParseJSON.exe (
    if /i "%verbosity%" neq "quiet" (
        dotnet build /property:GenerateFullPaths=true /consoleloggerparameters:NoSummary -c Release
        if exist .\bin\Release\!dotNetTarget!\ move .\bin\Release\!dotNetTarget!\* .
    ) else (
        dotnet build /property:GenerateFullPaths=true /consoleloggerparameters:NoSummary -c Release >NUL
        if exist .\bin\Release\!dotNetTarget!\ move .\bin\Release\!dotNetTarget!\* . >nul
    )
) else (
    REM call "!utilsScript!" WriteLine "ParseJSON present"
)
popd

REM  Walk through the modules directory and call the package script in each dir
rem Make this just "for /d %%D in ("%modulesDirPath%") do ("

for /f "delims=" %%D in ('dir /a:d /b "!modulesDirPath!"') do (

    set packageModuleDirName=%%~nxD
    set packageModuleDirPath=!modulesDirPath!\!packageModuleDirName!

    call "!utilsScript!" GetModuleIdFromModuleSettingsFile "!packageModuleDirPath!\modulesettings.json"
    set packageModuleId=!moduleSettingValue!

    if /i "%verbosity%" neq "quiet" call "!utilsScript!" WriteLine "Processing !packageModuleDirName! (!packageModuleId!)"

    if "!packageModuleId!" neq "" (
        call :DoModulePackage "!packageModuleId!" "!packageModuleDirName!" "!packageModuleDirPath!" errors
        if "!moduleInstallErrors!" NEQ "" set success=false
    )
)

if /i "!createExternalModulePackages!" == "true" (
    for /f "delims=" %%D in ('dir /a:d /b "!externalModulesDirPath!"') do (

        set packageModuleDirName=%%~nxD
        set packageModuleDirPath=!externalModulesDirPath!\!packageModuleDirName!

        call "!utilsScript!" GetModuleIdFromModuleSettingsFile "!packageModuleDirPath!\modulesettings.json"
        set packageModuleId=!moduleSettingValue!

        if /i "%verbosity%" neq "quiet" call "!utilsScript!" WriteLine "Processing !packageModuleDirName! (!packageModuleId!)"

        if "!packageModuleId!" neq "" (
            call :DoModulePackage "!packageModuleId!" "!packageModuleDirName!" "!packageModuleDirPath!" errors
            if "!moduleInstallErrors!" NEQ "" set success=false
        )
    )
)

call "!utilsScript!" WriteLine
call "!utilsScript!" WriteLine "                Modules packaging Complete" "White" "DarkGreen" !lineWidth!
call "!utilsScript!" WriteLine

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
        call "!utilsScript!" WriteLine "packageModuleDirPath           = !packageModuleDirPath!" !color_mute!
    )

    if exist "!packageModuleDirPath!\package.bat" (

        set doPackage=true

        if "!includeDotNet!" == "false" if /i "!packageModuleId!" == "ObjectDetectionYOLOv5Net" set doPackage=false
        if "!includeDotNet!" == "false" if /i "!packageModuleId!" == "PortraitFilter"           set doPackage=false
        if "!includeDotNet!" == "false" if /i "!packageModuleId!" == "SentimentAnalysis"        set doPackage=false

        if "!doPackage!" == "false" (
            call "!utilsScript!" WriteLine "Skipping packaging .NET module !packageModuleId!..." "Gray"
        ) else (
            REM Read the version from the modulesettings.json file and then pass this 
            REM version to the package.bat file.
            call "!utilsScript!" Write "Preparing !packageModuleId!..."
            call "!utilsScript!" GetValueFromModuleSettingsFile "!packageModuleDirPath!", "!packageModuleId!", "Version"
            set packageVersion=!moduleSettingsFileValue!

            if "!packageVersion!" == "" (
                call "!utilsScript!" WriteLine "Unable to read version from modulesettings file. Skipping" "Red"
            ) else (

                pushd "!packageModuleDirPath!" 
                if /i "%verbosity%" == "loud" cd

                rem Create module download package
                if exist package.bat (
                    call "!utilsScript!" Write "Packaging !packageModuleId! !packageVersion!..." "White"
                    call package.bat !packageModuleId! !packageVersion!
                    if errorlevel 1 call "!utilsScript!" WriteLine "Error in package.bat for !packageModuleDirName!" "Red"

                    rem Move package into modules download cache

                    rem echo Moving !packageModuleDirPath!\!packageModuleId!-!version!.zip to !packageDirPath!\
                    move /Y !packageModuleDirPath!\!packageModuleId!-!packageVersion!.zip !packageDirPath!\  >NUL

                    if errorlevel 1 (
                        call "!utilsScript!" WriteLine "Error" "Red"
                        set success=false
                    ) else (
                        call "!utilsScript!" WriteLine "done" "DarkGreen"
                    )
                ) else (
                    call "!utilsScript!" WriteLine "No package.bat file found..." "Red"
                )

                popd
            )
        )        
    )

    exit /b
