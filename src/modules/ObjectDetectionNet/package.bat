@Echo off
REM Module Packaging script. To be called from create_packages.bat

set moduleId=%~1
set version=%~2

:: The EXE is downloaded from S3 based on the local hardware. Not including
:: these files here deliberately
:: ALTERNATIVELY: Include the base CPU version, then only download GPU/OpenVINO/CUDA 
::                if needed


REM FIRST: We build the .NET app in Release mode. For this module we're building 4 
REM different versions, with each being uploaded to S3. The install script simply
REM pulls down and unpacks the correct version based on the Hardware and OS.

set Configuration=Release
set Target=net7.0

set GpuTypes[0]=GPU_NONE
set GpuTypes[1]=GPU_CUDA
set GpuTypes[2]=GPU_DIRECTML
set GpuTypes[3]=GPU_OPENVINO

:: The csproj file uses the GpuTypes folr clarity, but we don't want them as the file suffix
set FileSuffixes[0]=CPU
set FileSuffixes[1]=CUDA
set FileSuffixes[2]=DirectML
set FileSuffixes[3]=OpenVINO

set "index=0"
:BuildLoop
    if not defined GpuTypes[%index%] goto :endLoop

    call set GpuType=%%GpuTypes[%index%]%%
    call set FileSuffix=%%FileSuffixes[%index%]%%

    REM Build
    echo Building ObjectDetectionNet (%GpuType%)
    dotnet build -c %Configuration% --no-self-contained /p:DefineConstants=%GpuType%

    REM Zip it up. Note that we're excluding models because the istall scripts will 
    REM pull them down separately
    tar -caf ObjectDetectionNet-%FileSuffix%-%Version%.zip --exclude=assets --exclude=custom-models -C .\bin\%Configuration%\%Target%\ *

    rem Cleanup
    del /s /f /q .\bin\%Configuration%\%Target%\ >nul 2>nul
    del /s /f /q .\obj\%Configuration%\%Target%\ >nul 2>nul

    REM Next...
    set /a "index+=1"
    GOTO :BuildLoop
:endLoop

REM ... and create the actual module package. It's just the install scripts. All assets are in S3.
tar -caf %moduleId%-%version%.zip install.sh install.bat modulesettings.json
