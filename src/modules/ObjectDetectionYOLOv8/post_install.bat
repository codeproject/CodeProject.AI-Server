:: Post-Installation script ::::::::::::::::::::::::::::::::::::::::::::::::::::
::
::                       Object Detection (YOLOv8)
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

copy "!moduleDirPath!\patch\ultralytics\nn\tasks.py" "!packagesDirPath!\ultralytics\nn\."

REM TODO: Check assets created and has files
REM set moduleInstallErrors=...
