@echo off
setlocal EnableDelayedExpansion

REM ============================================================================
REM This script sets up WSL on an external drive E:
REM Very handy if your primary drive is getting a little tight on space.
REM ============================================================================

REM Find the URL of the distribution you want to install from
REM https://docs.microsoft.com/en-us/windows/wsl/install-manual#downloading-distributions
set installUrl=https://aka.ms/wslubuntu2204

REM You'll find this if you extract the archive in the install URL. Good luck.
set distroName=Ubuntu_2204.1.7.0_x64

REM For naming folders
set installName=Ubuntu2204

REM Substitute the drive on which you want WSL to be installed if not E:
E:

REM Create a directory for our installation and change to it, we'll call it WSL-Ubuntu2204:
mkdir WSL-!installName!
cd WSL-!installName!

powershell -command "Start-BitsTransfer -Source '!installUrl!' -Description !installName! -Destination '!installName!.appx'"

REM If these fail, it could be becuase of hanging transfers
if errorlevel 1 (
    powershell -Command "Get-BitsTransfer | Remove-BitsTransfer"
    powershell -command "Start-BitsTransfer -Source '!installUrl!' -Description !installName! -Destination '!installName!-all.appx'"
)

REM unpack:
rename .\!installName!-all.appx .\!installName!-all.zip
tar -xf !installName!-all.zip 
if errorlevel 1 (
    powershell -command "Expand-Archive '!installName!-all.zip'
)
del .\!installName!-all.zip

cd !installName!-all

REM rename to .zip so that Expand-Archive will work
ren .\!distroName!.appx .\!distroName!.zip
tar -xf !distroName!.zip 
if errorlevel 1 (
    powershell -command "Expand-Archive '.\!distroName!.zip'
)
del .\!distroName!.zip

mv !distroName! ../!installName!
cd ..
rmdir -rf !installName!-all
cd !installName!

REM Now it exists, run it. This will install Ubuntu on WSL
ubuntu.exe