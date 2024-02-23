:: Installation script :::::::::::::::::::::::::::::::::::::::::::::::::::::::::
::
::                          OCR
::
:: This script is called from the OCR directory using: 
::
::    ..\..\setup.bat
::
:: The setup.bat file will find this install.bat file and execute it.
::
:: For help with install scripts, notes on variables and methods available, tips,
:: and explanations, see /src/modules/install_script_help.md

@if "%1" NEQ "install" (
    echo This script is only called from ..\..\src\setup.bat
    @pause
    @goto:eof
)

:: Download the OCR models and store in /paddleocr
call "%sdkScriptsDirPath%\utils.bat" GetFromServer "models/" "ocr-en-pp_ocrv4-paddle.zip" "paddleocr" "Downloading OCR models..."

REM TODO: Check paddleocr created and has files
REM set moduleInstallErrors=...
