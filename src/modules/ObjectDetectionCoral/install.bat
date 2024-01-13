:: Installation script :::::::::::::::::::::::::::::::::::::::::::::::::::::::::
::
::                         ObjectDetection (Coral)
::
:: This script is only called from ..\..\src\setup.bat
::
:: For help with install scripts, notes on variables and methods available, tips,
:: and explanations, see /src/modules/install_script_help.md

@if "%1" NEQ "install" (
    echo This script is only called from ..\..\src\setup.bat
    @pause
    @goto:eof
)

:: Install supporting Libraries
if not exist edgetpu_runtime (
    call "%sdkScriptsDirPath%\utils.bat" GetFromServer "edgetpu_runtime_20221024.zip" "." "Downloading edge TPU runtime..."
)
if exist edgetpu_runtime (

    net session > NUL
    IF %ERRORLEVEL% EQU 0 (
        call "!sdkScriptsDirPath!\utils.bat" WriteLine "Installing the edge TPU libraries..." "!color_info!"
        pushd edgetpu_runtime
        call install.bat
        popd

        REM TODO: Check the edge TPU install worked
        REM set moduleInstallErrors=...
    ) else (
        call "!sdkScriptsDirPath!\utils.bat" WriteLine "=================================================================================" "!color_info!"
        call "!sdkScriptsDirPath!\utils.bat" WriteLine "Please run !moduleDirPath!\edgetpu_runtime\install.bat to complete this process." "!color_info!"
        call "!sdkScriptsDirPath!\utils.bat" WriteLine "=================================================================================" "!color_info!"
    )
)

:: Download the MobileNet TFLite models and store in /assets
call "%sdkScriptsDirPath%\utils.bat" GetFromServer "objectdetect-coral-models.zip" "assets" "Downloading MobileNet models..."

REM TODO: Check assets created and has files
REM set moduleInstallErrors=...
