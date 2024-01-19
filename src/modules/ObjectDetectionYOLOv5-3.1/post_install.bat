:: Post-Installation script ::::::::::::::::::::::::::::::::::::::::::::::::::::
::
::                       Object Detection (YOLOv5 3.1)
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

if "!cuda_major_version!" == "11" (
    copy "!moduleDirPath!\patch\torch\cuda11\upsampling.py" "!packagesDirPath!\torch\nn\modules\."
) else if "!cuda_major_version!" == "12" (
    copy "!moduleDirPath!\patch\torch\cuda11\upsampling.py" "!packagesDirPath!\torch\nn\modules\."
)

REM TODO: Check assets created and has files
REM set moduleInstallErrors=...
