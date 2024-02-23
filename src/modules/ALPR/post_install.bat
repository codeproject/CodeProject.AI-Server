:: Post-Installation script ::::::::::::::::::::::::::::::::::::::::::::::::::::
::
::                          ALPR
::
:: The setup.bat file will find this post_install.bat file and execute it.
::
:: For help with install scripts, notes on variables and methods available, tips,
:: and explanations, see /src/modules/install_script_help.md

@if "%1" NEQ "post-install" (
    echo This script is only called from ..\..\setup.bat
    @pause
    @goto:eof
)

REM We have a patch to apply for everyone else due to a bad truthy test on a
REM multi-dimensional array in paddleocr 2.7.0.3
call "%sdkScriptsDirPath%\utils.bat" WriteLine "Applying PaddleOCR patch"
copy "!moduleDirPath!\patch\paddleocr-2.7.0.3\paddleocr.py" "!packagesDirPath!\paddleocr\."

REM TODO: Check assets created and has files
REM set moduleInstallErrors=...
