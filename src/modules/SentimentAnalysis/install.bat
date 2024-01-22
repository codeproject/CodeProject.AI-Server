:: Installation script :::::::::::::::::::::::::::::::::::::::::::::::::::::::::
::
::                           .NET YOLO Object Detection
::
:: This script is only called from ..\..\setup.bat 
::
:: For help with install scripts, notes on variables and methods available, tips,
:: and explanations, see /src/modules/install_script_help.md

@if "%1" NEQ "install" (
    echo This script is only called from ..\..\setup.bat
    @pause
    @goto:eof
)

if /i "!executionEnvironment!" == "Production" (
    call "!sdkScriptsDirPath!\utils.bat" WriteLine "No custom setup steps for this module." "!color_info!"
) else (
    :: If we're in dev-setup mode we'll build the module now so the self-test will work
    pushd "!moduleDirPath!"
    call "!sdkScriptsDirPath!\utils.bat" WriteLine "Building project..." "!color_info!"
    dotnet build -c Debug -o "!moduleDirPath!/bin/Debug/net7.0" >NUL
    popd
)

REM set moduleInstallErrors=
