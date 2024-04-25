:: Installation script :::::::::::::::::::::::::::::::::::::::::::::::::::::::::
::
::                           LlamaChat
::
:: This script is only called using ..\..\src\setup.bat
::
:: For help with install scripts, notes on variables and methods available, tips,
:: and explanations, see /src/modules/install_script_help.md

@if "%1" NEQ "install" (
    echo This script is only called from ..\..\src\setup.bat
    @pause
    @goto:eof
)

REM Download the model file at installation so we can run without a connection to the Internet.
set sourceUrl=https://huggingface.co/TheBloke/Mistral-7B-Instruct-v0.2-GGUF/resolve/main/
set fileToGet=mistral-7b-instruct-v0.2.Q4_K_M.gguf

if not exist "!moduleDirPath!/models/!fileToGet!" (
    set destination=!downloadDirPath!\!modulesDir!\!moduleDirName!\!fileToGet!

    if not exist "!downloadDirPath!\!modulesDir!\!moduleDirName!" mkdir "!downloadDirPath!\!modulesDir!\!moduleDirName!"
    if not exist "!moduleDirPath!\models" mkdir "!moduleDirPath!\models"

    call "!sdkScriptsDirPath!\utils.bat" WriteLine "Downloading !fileToGet!" "!color_info!"

    powershell -command "Start-BitsTransfer -Source '!sourceUrl!!fileToGet!' -Destination '!destination!'"
    if errorlevel 1 (
        powershell -Command "Get-BitsTransfer | Remove-BitsTransfer"
        powershell -command "Start-BitsTransfer -Source '!sourceUrl!!fileToGet!' -Destination '!destination!'"
    )
    if errorlevel 1 (
        powershell -Command "Invoke-WebRequest '!sourceUrl!!fileToGet!' -OutFile '!destination!'"
        if errorlevel 1 (
            call "!sdkScriptsDirPath!\utils.bat" WriteLine "Download failed. Sorry." "!color_error!"
            set moduleInstallErrors=Unable to download !fileToGet!
        )
    )

    if exist "!destination!" (
        call "!sdkScriptsDirPath!\utils.bat" WriteLine "Moving !fileToGet! into the models folder." "!color_info!"
        move "!destination!" "!moduleDirPath!/models/" > nul
    ) else (
        call "!sdkScriptsDirPath!\utils.bat" WriteLine "Download faild. Sad face." "!color_warn!"
    )
) else (
    call "!sdkScriptsDirPath!\utils.bat" WriteLine "!fileToGet! already downloaded." "!color_success!"
)

REM set moduleInstallErrors=
