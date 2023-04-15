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

REM Coral TPU setup
call "!sdkScriptsPath!\utils.bat" WriteLine "Installing UsbDk ==============================================" "!color_info!"
REM See https://pi3g.com/2022/10/19/coral-usb-inference-not-working-on-windows-10-valueerror-failed-to-load-delegate-from-edgetpu-dll/
REM start /wait msiexec /i "%modulePath%\third_party\usbdk\UsbDk_1.0.22_x64.msi" /qb! /norestart
start /wait msiexec /i "%modulePath%\third_party\usbdk\UsbDk_1.0.21_x64.msi" /qb! /norestart
call "!sdkScriptsPath!\utils.bat" WriteLine

call "!sdkScriptsPath!\utils.bat" WriteLine "Installing Windows drivers ====================================" "!color_info!"
pnputil /add-driver "%modulePath%\third_party\coral_accelerator_windows\*.inf" /install
call "!sdkScriptsPath!\utils.bat" WriteLine

call "!sdkScriptsPath!\utils.bat" WriteLine "Installing performance counters ===============================" "!color_info!"
lodctr /M:"%modulePath%\third_party\coral_accelerator_windows\coral.man"
call "!sdkScriptsPath!\utils.bat" WriteLine
call "!sdkScriptsPath!\utils.bat" WriteLine

call "!sdkScriptsPath!\utils.bat" WriteLine "Copying edgetpu and libusb to System32 ========================" "!color_info!"

rem copy "%workingDir%\third_party\libedgetpu\throttled\x64_windows\edgetpu.dll" %systemroot%\system32
copy "%modulePath%\third_party\libedgetpu\direct\x64_windows\edgetpu.dll" "%systemroot%\system32\"
copy "%modulePath%\third_party\libusb_win\libusb-1.0.dll" "%systemroot%\system32\"
call "!sdkScriptsPath!\utils.bat" WriteLine
