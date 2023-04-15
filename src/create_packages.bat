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

:: Basic locations

:: The path to the directory containing the install scripts. Will end in "\"
set installerScriptsPath=%~dp0

:: The name of the source directory
set srcDir=src

:: The name of the dir, within the current directory, where install assets will
:: be downloaded
set downloadDir=downloads

:: The name of the dir holding the downloaded/sideloaded backend analysis services
set modulesDir=modules


:: Override some values via parameters ::::::::::::::::::::::::::::::::::::::::

:param_loop
    set arg_name=%~1
    set arg_value=%~2
    if not "!arg_name!" == "" (
        if not "!arg_name:--no-color=!" == "!arg_name!" set useColor=false
        REM if not "!arg_name:pathToInstall=!" == "!arg_name!" set installerScriptsPath=!arg_value!
    )
    shift
    shift
if not "!arg_name!"=="" goto param_loop

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
set sdkScriptsPath=%cd%\SDK\Scripts
if /i "%executionEnvironment%" == "Development" cd ..
set absoluteRootDir=%cd%
popd

set absoluteAppRootDir=!installerScriptsPath!

:: Platform can define where things are located :::::::::::::::::::::::::::::::

:: The location of directories relative to the root of the solution directory
set modulesPath=!absoluteAppRootDir!!modulesDir!
set downloadPath=!absoluteAppRootDir!!downloadDir!

:: Let's go
if /i "!useColor!" == "true" call "!sdkScriptsPath!\utils.bat" setESC
if /i "!executionEnvironment!" == "Development" (
    set scriptTitle=          Creating CodeProject.AI Module Downloads
) else (
    echo Can't run in Production. Exiting.
    goto:eof
)

set lineWidth=70

call "!sdkScriptsPath!\utils.bat" WriteLine 
call "!sdkScriptsPath!\utils.bat" WriteLine "!scriptTitle!" "DarkYellow" "Default" !lineWidth!
call "!sdkScriptsPath!\utils.bat" WriteLine 
call "!sdkScriptsPath!\utils.bat" WriteLine "========================================================================" "DarkGreen" 
call "!sdkScriptsPath!\utils.bat" WriteLine 
call "!sdkScriptsPath!\utils.bat" WriteLine "                   CodeProject.AI Packager                             " "DarkGreen" 
call "!sdkScriptsPath!\utils.bat" WriteLine 
call "!sdkScriptsPath!\utils.bat" WriteLine "========================================================================" "DarkGreen" 
call "!sdkScriptsPath!\utils.bat" WriteLine 


if /i "%verbosity%" neq "quiet" (
    call "!sdkScriptsPath!\utils.bat" WriteLine 
    call "!sdkScriptsPath!\utils.bat" WriteLine "executionEnvironment  = !executionEnvironment!"  !color_mute!
    call "!sdkScriptsPath!\utils.bat" WriteLine "installerScriptsPath  = !installerScriptsPath!"  !color_mute!
    call "!sdkScriptsPath!\utils.bat" WriteLine "sdkScriptsPath        = !sdkScriptsPath!"        !color_mute!
    call "!sdkScriptsPath!\utils.bat" WriteLine "absoluteAppRootDir    = !absoluteAppRootDir!"    !color_mute!
    call "!sdkScriptsPath!\utils.bat" WriteLine "modulesPath           = !modulesPath!"           !color_mute!
    call "!sdkScriptsPath!\utils.bat" WriteLine
)

:: And off we go...

set success=true

REM  Walk through the modules directory and call the package script in each dir
REM  TODO: This should be just a simple for /d %%D in ("!modulesPath!") do (
for /f "delims=" %%D in ('dir /a:d /b "!modulesPath!"') do (
    set packageModuleDir=%%~nxD
    set packageModuleId=!packageModuleDir!
    set packageModulePath=!modulesPath!\!packageModuleDir!

    if exist "!packageModulePath!\package.bat" (

        call "!sdkScriptsPath!\utils.bat" Write "Packaging module !packageModuleId!..." "White"

        pushd "!packageModulePath!" 

        REM Read the version from the modulesettings.json file and then pass this 
        REM version to the package.bat file.
        call "!sdkScriptsPath!\utils.bat" GetVersionFromModuleSettings "modulesettings.json" "Version"
        set packageVersion=!jsonValue!
        rem echo packageVersion is !packageVersion!

        rem Create module download package
        call package.bat !packageModuleId! !packageVersion!
        if errorlevel 1 call "!sdkScriptsPath!\utils.bat" WriteLine "Error in package.bat for !packageModuleDir!" "Red"

        popd
        
        rem Move package into modules download cache       
        rem echo Moving !packageModulePath!\!packageModuleId!-!version!.zip to !downloadPath!\modules\
        move /Y !packageModulePath!\!packageModuleId!-!packageVersion!.zip !downloadPath!\modules\  >NUL 2>&1

        if errorlevel 1 (
            call "!sdkScriptsPath!\utils.bat" WriteLine "Error" "Red"
        ) else (
            set success=false
            call "!sdkScriptsPath!\utils.bat" WriteLine "Done" "DarkGreen"
        )

        REM goto:eof
    )
)

call "!sdkScriptsPath!\utils.bat" WriteLine
call "!sdkScriptsPath!\utils.bat" WriteLine "                Modules packaging Complete" "White" "DarkGreen" !lineWidth!
call "!sdkScriptsPath!\utils.bat" WriteLine

if /i "!success!" == "false" exit /b 1
