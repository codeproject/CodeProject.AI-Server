::
:: Install Coral Drivers
::

@echo off
SetLocal EnableDelayedExpansion

REM Force admin mode
fsutil dirty query %systemdrive% >NUL
if not %ERRORLEVEL% == 0 (
    powershell Start -File "cmd '/K %~f0 runas'" -Verb RunAs
    rem powershell Start-Process -FilePath 'install_coral.bat'  -verb runas -ArgumentList  'elevated -NoExit -Command "cd %cd%"'
    exit /b
)

pushd "%~dp0"

set modulePath=%cd%
set sdkScriptsPath=%cd%\..\..\SDK\Scripts
set color_info=Yellow
call "!sdkScriptsPath!\utils.bat" setESC

rem echo modulePath = %modulePath%
rem echo sdkScriptsPath = %sdkScriptsPath%

call "!sdkScriptsPath!\utils.bat" WriteLine "Extracting EdgeTPU setup files" "!color_info!"
pushd install
mkdir edgetpu
tar -xf edgetpu_runtime.zip -C edgetpu 

:: We'll just use the existing install.bat that comes with the edgeTPU install instead of doing it
:: ourselves
call "!sdkScriptsPath!\utils.bat" WriteLine "Installing EdgeTPU support" "!color_info!"
pushd edgetpu
install.bat

popd
popd

call "!sdkScriptsPath!\utils.bat" WriteLine "Done"


REM Coral TPU setup
REM call "!sdkScriptsPath!\utils.bat" WriteLine "Installing UsbDk ==============================================" "!color_info!"
REM start /wait msiexec /i "%modulePath%\third_party\usbdk\UsbDk_1.0.21_x64.msi" /qb! /norestart
REM call "!sdkScriptsPath!\utils.bat" WriteLine

REM call "!sdkScriptsPath!\utils.bat" WriteLine "Installing Windows drivers ====================================" "!color_info!"
REM pnputil /add-driver "%modulePath%\third_party\coral_accelerator_windows\*.inf" /install
REM call "!sdkScriptsPath!\utils.bat" WriteLine

REM call "!sdkScriptsPath!\utils.bat" WriteLine "Installing performance counters ===============================" "!color_info!"
REM lodctr /M:"%modulePath%\third_party\coral_accelerator_windows\coral.man"
REM call "!sdkScriptsPath!\utils.bat" WriteLine
REM call "!sdkScriptsPath!\utils.bat" WriteLine

REM call "!sdkScriptsPath!\utils.bat" WriteLine "Copying edgetpu and libusb to System32 ========================" "!color_info!"

REM copy "%workingDir%\third_party\libedgetpu\throttled\x64_windows\edgetpu.dll" %systemroot%\system32
REM copy "%modulePath%\third_party\libedgetpu\direct\x64_windows\edgetpu.dll" "%systemroot%\system32\"
REM copy "%modulePath%\third_party\libusb_win\libusb-1.0.dll" "%systemroot%\system32\"
REM call "!sdkScriptsPath!\utils.bat" WriteLine