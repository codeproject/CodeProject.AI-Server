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
:: 2. Creates a folder "C:\Program Files\NVIDIA\CUDNN\v8.9" and extracts the cuDNN package
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

REM Force admin mode
cd /D "%~dp0"
if not "%1"=="am_admin" (powershell start -verb runas '%0' am_admin & exit /b)


REM Settings

set dryRun=false

set cuDNNLocation=https://developer.nvidia.com/rdp/cudnn-download
set cuDNNArchiveDownloadUrl=https://codeproject-ai.s3.ca-central-1.amazonaws.com/server/assets/libraries/

set CUDA10_cuDNN_version=8.4.1.50
set CUDA11_cuDNN_version=8.9.4.25
set CUDA12_cuDNN_version=8.9.7.29

set zLibLocation=http://www.winimage.com/zLibDll/
set zLibArchiveName=zlib123dllx64


REM Installer archive

call :GetCudaVersion

if "!cuda_major_version!" == "" (
    if /i "!dryRun!" == "true" (
        set cuda_major_version=12
    ) else (
        echo CUDA was not found. Exiting.
        goto:eof
    )
)
echo Found CUDA !cuda_major_version!

REM Get the name of the appropriate installer based on the major CUDA version. We
REM use this in the general case where no installer is present in the local folder
REM The cuDNN installer file has a name like cudnn-windows-x86_64-8.5.0.96_cuda11-archive.zip
REM Specific versions we have archived:
REM     cudnn-windows-x86_64-8.4.1.50_cuda10.2-archive.zip
REM     cudnn-windows-x86_64-8.9.4.25_cuda11-archive.zip
REM     cudnn-windows-x86_64-8.9.7.29_cuda12-archive.zip
REM Before downloading we will search the local folder to see if there is already
REM an installer present.

if "!cuda_major_version!" == "10" (
    set cuda_version=10.2
    set cuDNN_version=!CUDA10_cuDNN_version!
) else if "!cuda_major_version!" == "11" (
    set cuda_version=11
    set cuDNN_version=!CUDA11_cuDNN_version!
) else if "!cuda_major_version!" == "12" (
    set cuda_version=12
    set cuDNN_version=!CUDA12_cuDNN_version!
)
set cuDNNArchiveFilename=cudnn-windows-x86_64-!cuDNN_version!_cuda!cuda_version!-archive.zip
set cuDNNPattern=cudnn-windows-x86_64-*_cuda!cuda_version!-archive.zip
set "cuDNNRegex=cudnn-windows-x86_64-([0-9]*).([0-9]*).([0-9]*).([0-9]*)_cuda!cuda_version!-archive.zip"


REM Install

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

echo Searching for existing cuDNN installers !cuDNNPattern!
IF not exist "!cuDNNPattern!" (
    echo No cuDNN archive found. Downloading !cuDNNArchiveFilename!...
    powershell -command "Start-BitsTransfer -Source '!cuDNNArchiveDownloadUrl!!cuDNNArchiveFilename!' -Destination '!cuDNNArchiveFilename!'"
)

IF exist "!cuDNNPattern!" (
    for /F "usebackq delims=" %%f in (`dir /B /A-D /O-N !cuDNNPattern!`) do (

        REM We have a cuDNN archive. Get the archive name with and without extension
        set cuDNNInstallerFilename=%%~nxf
        set cuDNNInstallerNameNoExt=%%~nf

        echo Found !cuDNNInstallerFilename!

        REM Get the version. Filename is similar to cudnn-windows-x86_64-8.5.0.96_cuda11-archive.zip,
        REM where the version here is 8.5.0.96. We only need major/minor.

        for /f "delims=" %%i in ('
            powershell -c "'!cuDNNInstallerFilename!' -replace '!cuDNNRegex!','$1.$2'"
        ') do set version=%%i

        if "!version!" == "" (
            echo No installer available.
            goto:eof
        )
        echo Found cuDNN installer for version !version!. Expanding...

        REM Expand the archive

        rem echo Expanding...
    
        set tarExists=true
        tar -xf "!cuDNNInstallerFilename!" > nul 2>nul
        if "%errorlevel%" == "9009" set tarExists=false

        if "!tarExists!" == "false" (
            powershell -command "Expand-Archive -Path '!cuDNNInstallerFilename!' -DestinationPath '!cuDNNInstallerNameNoExt!' -Force"
        )

        REM Move the directories into C:\Program Files\NVIDIA\CUDNN\v<version>

        echo Installing cuDNN files...

        if not exist "C:\Program Files\NVIDIA" mkdir "C:\Program Files\NVIDIA" > NUL
        if not exist "C:\Program Files\NVIDIA\CUDNN" mkdir "C:\Program Files\NVIDIA\CUDNN" > NUL
        if not exist "C:\Program Files\NVIDIA\CUDNN\v!version!\" mkdir "C:\Program Files\NVIDIA\CUDNN\v!version!\" > NUL

        robocopy /e "!cuDNNInstallerNameNoExt! " "C:\Program Files\NVIDIA\CUDNN\v!version! " /MOVE /NC /NS /NJS /NJH > NUL

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

        if /i "!dryRun!" == "false" ( 
            REM We need to set the PATH variable. Some caveats:
            REM 1. When you set the PATH variable, %PATH% will not reflect the update you just made, so
            REM    doing "PATH = PATH + change1" followed by "PATH = PATH + change2" results in just Change2 
            REM    being added. So: do all the changes in one fell swoop.
            REM 2. We can't use setx /M PATH "%PATH%;C:\Program Files\NVIDIA\CUDNN\v!version!\zlib\dll_x64"
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
        )

        echo done.

        REM Only process the first archive we find
        call :InstallChecks
    )
)


goto:eof

:InstallChecks

    if /i "!cuDNNInstalled!" == "false" ( 
        echo No cuDNN archive found.
        echo Please download CUDA 11 from !cuDNNLocation!
        pause
        goto:eof
    ) else if /i "!zLibInstalled!" == "false" ( 
        echo No ZLib found.
        echo Please download ZLib from !zLibLocation!!zLibArchiveName!.zip
        pause
        goto:eof
    )

    exit /b

:GetCudaVersion

    rem setlocal enabledelayedexpansion

    :: Use nvcc to find the CUDA version
    where nvcc >nul 2>&1
    if !errorlevel! == 0 (
        REM Get the line containing "release x.y"
        for /f "tokens=*" %%i in ('nvcc --version ^| findstr /C:"release"') do set cudaLine=%%i
        REM Get the 5th token in the line when split by , and spaces
        for /f "tokens=5 delims=, " %%a in ("!cudaLine!") do set cuda_version=%%a
    ) else (
        REM Backup attempt: Use nvidia-smi to find the CUDA version
        where nvidia-smi >nul 2>&1
        if !errorlevel! == 0 (
            REM Get the line containing "CUDA Version x.y"
            for /f "tokens=*" %%i in ('nvidia-smi ^| findstr /C:"CUDA Version"') do set cudaLine=%%i
            REM Get the 9th token in the line when split by spaces
            for /f "tokens=9 delims= " %%a in ("!cudaLine!") do set cuda_version=%%a
        ) else (
            REM echo Unable to find nvcc or nvidia-smi
        )
    )

    REM echo cudaLine = !cudaLine!
    REM echo GetCudaVersion version: !cuda_version!

    if "!cuda_version!" neq "" (
        for /f "tokens=1,2 delims=." %%a in ("!cuda_version!") do (
            set "cuda_major_version=%%a"
            exit /b
        )
    )

    REM pass back values as in params 1 and 2
    REM set "%~1=!cuda_version!"
    REM set "%~2=!cuda_major_version!"

    exit /b    
