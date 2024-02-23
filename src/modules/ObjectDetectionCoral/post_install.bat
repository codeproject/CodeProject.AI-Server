:: Post-Installation script ::::::::::::::::::::::::::::::::::::::::::::::::::::
::
::                       Object Detection (Coral)
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

REM Install optimized Pillow-SIMD or fallback to keeping regular Pillow
REM Used for fast image image resizing operations with SSE4 or AVX2
REM See also: https://python-pillow.org/pillow-perf/

REM    THIS FAILS CATASTROPHICALLY. 
REM    
REM    REM Uninstall current Pillow and try installing Pillow-SIMD
REM    call "!sdkScriptsDirPath!\utils.bat" WriteLine "Upgrading PIP. Again..." "!color_info!"
REM    "!venvPythonCmdPath!" -m pip install -U --force-reinstall pip
REM    
REM    call "!sdkScriptsDirPath!\utils.bat" WriteLine "Uninstalling Pillow..." "!color_info!"
REM    "!venvPythonCmdPath!" -m pip uninstall --yes pillow >NUL
REM    
REM    call "!sdkScriptsDirPath!\utils.bat" WriteLine "Installing Pillow-SIMD..." "!color_info!"
REM    "!venvPythonCmdPath!" -m pip install pillow-simd >NUL
REM    
REM    REM If that didn't work, undo what we did and put back Pillow
REM    if errorlevel 1 (
REM        call "!sdkScriptsDirPath!\utils.bat" WriteLine "Pillow-SIMD install failed. Restoring Pillow..." "!color_warn!"
REM        "!venvPythonCmdPath!" -m pip uninstall --yes pillow-simd >NUL
REM        "!venvPythonCmdPath!" -m pip install Pillow>=4.0.0 >NUL
REM    }

REM TODO: Check assets created and has files
REM set moduleInstallErrors=...
