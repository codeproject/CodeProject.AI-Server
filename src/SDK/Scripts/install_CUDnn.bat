:: CodeProject.AI Server 
::
:: Windows cuDNN install script
::
:: BEFORE YOU START: Make sure you have 
::
:: a) CUDA 11.7 drivers (https://www.nvidia.com/Download/index.aspx) installed, and
:: b) CUDA Toolkit 11.7 (https://developer.nvidia.com/cuda-downloads) installed.
::
:: What this script does:
:: 
:: 1. Downloads the cuDNN package (v8.9.4.96 for CUDA 11)
::
:: 2. Creates a folder "C:\Program Files\NVIDIA\CUDNN\v8.5" and extracts the cuDNN package
::    into that folder. There will be bin, lib and include folders, plus a LICENSE file.
::   
:: 3. Adds this path to the PATH environment variable: 
::    setx /M PATH = Path + "%PATH%;C:\Program Files\NVIDIA\CUDNN\v8.9\bin"
::
:: 4. Downloads ZLib from WinImage (http://www.winimage.com/zLibDll/zlib123dllx64.zip) and extracts
::    into a folder. Since it's being used by cuDNN it's easier to just extract into the
::    cuDNN folder: "C:\Program Files\NVIDIA\CUDNN\v8.9\zlib
::    
:: 5. Add this path to the PATH environment variable: 
::    setx /M PATH "%PATH%;C:\Program Files\NVIDIA\CUDNN\v8.9\zlib\dll_x64"
::
:: What you need to do: just double click this bat file in Windows

@echo off
cls
setlocal enabledelayedexpansion

set cuDNNLocation=https://developer.nvidia.com/rdp/cudnn-download
REM set cuDNNArchiveName=cudnn-windows-x86_64-8.5.0.96_cuda11-archive
set cuDNNArchiveName=cudnn-windows-x86_64-8.9.4.25_cuda11-archive
set cuDNNArchiveDownloadUrl=https://codeproject-ai.s3.ca-central-1.amazonaws.com/sense/installer/dev/
set cuDNNPattern=cudnn-windows-x86_64-*.zip
set cuDNNRegex=cudnn-windows-x86_64-([0-9]*).([0-9]*).([0-9]*).([0-9]*)_cuda11-archive

set zLibLocation=http://www.winimage.com/zLibDll/
set zLibArchiveName=zlib123dllx64

REM Force admin mode
cd /D "%~dp0"
if not "%1"=="am_admin" (powershell start -verb runas '%0' am_admin & exit /b)


echo ========================================================================
echo.
echo             Setting up cuDNN for CodeProject.AI Server
echo.
echo ========================================================================
echo.

set cuDNNInstalled=false
set zLibInstalled=false

:: Walk through the modules directory to find the cuDNN setup file, starting newest to oldest,
:: but before we start lets ensure we attempt to have at least one version present.

IF not exist "!cuDNNPattern!" (
    echo No cuDNN archive found. Downloading !cuDNNArchiveName!.zip...
    powershell -command "Start-BitsTransfer -Source '!cuDNNArchiveDownloadUrl!!cuDNNArchiveName!.zip' -Destination '!cuDNNArchiveName!.zip'"
)

IF exist "!cuDNNPattern!" (
    for /F "usebackq delims=" %%f in (`dir /B /A-D /O-N !cuDNNPattern!`) do (

        REM We have a cuDNN archive. Get the archive name with and without extension
        set cuDNNZip=%%~nxf
        set cuDNNDir=%%~nf

        rem echo "Found !cuDNNZip! 

        REM Get the version. Filename is similar to cudnn-windows-x86_64-8.5.0.96_cuda11-archive.zip,
        REM where the version here is 8.5.0.96. We only need major/minor.

        for /f "delims=" %%i in ('
            powershell -c "'!cuDNNDir!' -replace '!cuDNNRegex!','$1.$2'"
        ') do set version=%%i

        echo Found cuDNN installer for version !version!. Expanding...


        REM Expand the archive

        rem echo Expanding...
    
        set tarExists=true
        tar -xf "!cuDNNZip!" > nul 2>nul
        if "%errorlevel%" == "9009" set tarExists=false

        if "!tarExists!" == "false" (
            powershell -command "Expand-Archive -Path '!cuDNNZip!' -DestinationPath '!cuDNNDir!' -Force"
        )


        REM Move the directoris into C:\Program Files\NVIDIA\CUDNN\v<version>

        echo Installing cuDNN files...

        if not exist "C:\Program Files\NVIDIA" mkdir "C:\Program Files\NVIDIA" > NUL
        if not exist "C:\Program Files\NVIDIA\CUDNN" mkdir "C:\Program Files\NVIDIA\CUDNN" > NUL
        if not exist "C:\Program Files\NVIDIA\CUDNN\v!version!\" mkdir "C:\Program Files\NVIDIA\CUDNN\v!version!\" > NUL

        robocopy /e "!cuDNNDir! " "C:\Program Files\NVIDIA\CUDNN\v!version! " /MOVE /NC /NS /NJS /NJH > NUL

        set cuDNNInstalled=true

        echo Installing ZLib

        REM Next step is to grab ZLib and intall that. We'll place it next to the cuDNN files in 
        REM C:\Program Files\NVIDIA\CUDNN\v<version>\zLib

        if not exist "!zLibArchiveName!.zip" (
            powershell -command "Start-BitsTransfer -Source '!zLibLocation!!zLibArchiveName!.zip' -Destination '!zLibArchiveName!.zip'"
        )

        if exist "!zLibArchiveName!.zip" (
            echo Expanding ZLib...
            if "!tarExists!" == "true" (
                if not exist "!zLibArchiveName!" mkdir "!zLibArchiveName!" > NUL
                copy !zLibArchiveName!.zip !zLibArchiveName!\ > NUL
                pushd !zLibArchiveName! > NUL
                tar -xf "!zLibArchiveName!.zip" > nul 2>nul
                del !zLibArchiveName!.zip > NUL
                popd > NUL
            ) else (
                powershell -command "Expand-Archive -Path '!zLibArchiveName!.zip' -DestinationPath '!zLibArchiveName!' -Force"
            )

            echo Installing ZLib...
            if not exist "C:\Program Files\NVIDIA\CUDNN\v!version!\zlib" mkdir "C:\Program Files\NVIDIA\CUDNN\v!version!\zlib"
            robocopy /e "!zLibArchiveName! " "C:\Program Files\NVIDIA\CUDNN\v!version!\zlib\ " /MOVE /NC /NS /NJS /NJH > NUL

            set zLibInstalled=true
        )


        REM We need to set the PATH variable. Some caveats:
        REM 1. When you set the PATH variable, %PATH% will not reflect the update you just made, so
        REM    doing "PATH = PATH + change1" followed by "PATH = PATH + change2" results in just Change2 
        REM    being added. So: do all the changes in one fell swoop.
        REM 2. WWe can't use setx /M PATH "%PATH%;C:\Program Files\NVIDIA\CUDNN\v!version!\zlib\dll_x64"
        REM    because setx truncates the path to 1024 characters. In 2022. Insanity.
        REM 3. Only update the path if we need to. Check for existance before modifying!

        echo Updating PATH environment variable...

        set newPath=!PATH!

        REM Add ZLib path if it hasn't already been added
        if "!zLibInstalled!" == "true" ( 
            if /i "!PATH:C:\Program Files\NVIDIA\CUDNN\v!version!\zlib=!" == "!PATH!" (
                set newPath=!newPath!;C:\Program Files\NVIDIA\CUDNN\v!version!\zlib\dll_x64
            )
        )
        REM Add cuDNN path if it hasn't already been added
        if /i "!PATH:C:\Program Files\NVIDIA\CUDNN\v!version!\bin=!" == "!PATH!" (
            set newPath=!newPath!;C:\Program Files\NVIDIA\CUDNN\v!version!\bin
        )

        if /i "!newPath" NEQ "!PATH!" (
            rem echo New Path is !newPath!
            powershell -command "[Environment]::SetEnvironmentVariable('PATH', '!newPath!','Machine');
        )

        echo Done.

        REM Only process the first archive we find
        goto installChecks
    )
)

:installChecks

if "!cuDNNInstalled!" == "false" ( 
    echo No cuDNN archive found.
    echo Please download CUDA 11 from !cuDNNLocation!
) else if "!zLibInstalled!" == "false" ( 
    echo No ZLib found.
    echo Please download ZLib from !zLibLocation!!zLibArchiveName!.zip
)

pause
