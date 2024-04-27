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


:: Basic locations

:: The name of the dir, within the current directory, where install assets will
:: be downloaded
set downloadDir=downloads

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
set sdkDirPath=%rootDirPath%\%srcDir%\%sdkDir%
set sdkScriptsDirPath=%sdkDirPath%\Scripts


:: Override some values via parameters ::::::::::::::::::::::::::::::::::::::::

:param_loop
    set arg_name=%~1
    set arg_value=%~2
    if not "!arg_name!" == "" (
        if not "!arg_name:--no-color=!" == "!arg_name!" set useColor=false
        if not "!arg_name:--no-dotnet=!" == "!arg_name!" set includeDotNet=false

        REM No longer supporting this scenario
        REM if not "!arg_name:--path-to-setup=!" == "!arg_name!" (
        REM     set setupScriptDirPath=!arg_value!
        REM     shift
        REM )

        if not "!arg_name:--verbosity=!" == "!arg_name!" (
            set verbosity=!arg_value!
            shift
        )
    )
    shift
if not "!arg_name!"=="" goto param_loop


:: The location of directories relative to the root of the solution directory
set modulesDirPath=!rootDirPath!\%srcDir%\!modulesDir!
set externalModulesDirPath=!rootDirPath!\..\!externalModulesDir!
set downloadDirPath=!rootDirPath!\!downloadDir!

:: Let's go
if /i "!useColor!" == "true" call "!sdkScriptsDirPath!\utils.bat" setESC

set lineWidth=70

call "!sdkScriptsDirPath!\utils.bat" WriteLine 
call "!sdkScriptsDirPath!\utils.bat" WriteLine "Creating CodeProject.AI Module Downloads" "DarkYellow" "Default" !lineWidth!
call "!sdkScriptsDirPath!\utils.bat" WriteLine 
call "!sdkScriptsDirPath!\utils.bat" WriteLine "========================================================================" "DarkGreen" 
call "!sdkScriptsDirPath!\utils.bat" WriteLine 
call "!sdkScriptsDirPath!\utils.bat" WriteLine "                   CodeProject.AI Packager                             " "DarkGreen" 
call "!sdkScriptsDirPath!\utils.bat" WriteLine 
call "!sdkScriptsDirPath!\utils.bat" WriteLine "========================================================================" "DarkGreen" 
call "!sdkScriptsDirPath!\utils.bat" WriteLine 


if /i "%verbosity%" neq "quiet" (
    call "!sdkScriptsDirPath!\utils.bat" WriteLine 
    call "!sdkScriptsDirPath!\utils.bat" WriteLine "rootDirPath            = !rootDirPath!"         !color_mute!
    call "!sdkScriptsDirPath!\utils.bat" WriteLine "setupScriptDirPath     = !setupScriptDirPath!"     !color_mute!
    call "!sdkScriptsDirPath!\utils.bat" WriteLine "sdkScriptsDirPath      = !sdkScriptsDirPath!"      !color_mute!
    call "!sdkScriptsDirPath!\utils.bat" WriteLine "modulesDirPath         = !modulesDirPath!"         !color_mute!
    call "!sdkScriptsDirPath!\utils.bat" WriteLine "externalModulesDirPath = !externalModulesDirPath!" !color_mute!
    call "!sdkScriptsDirPath!\utils.bat" WriteLine
)

:: And off we go...

set success=true

pushd %sdkDirPath%\Utilities\ParseJSON
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
