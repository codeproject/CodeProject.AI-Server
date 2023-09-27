@Echo off
REM Module Packaging script. To be called from create_packages.bat

REM The executable for this module is downloaded from S3. There are multiple forms
REM of the executable, corresponding to different hardware, and the appropriate
REM exe will be downloaded based on the local hardware. 


set moduleId=%~1
set version=%~2

REM Method can be 'build' or 'publish'. 'publish' doesn't generate an exe
set Method=build


REM FIRST: We build or publish the .NET app in Release mode. For this module 
REM we're building 4 different versions, with each being (manually) uploaded to
REM S3. The install script simply pulls down and unpacks the correct version
REM based on the Hardware and OS.

set Configuration=Release
set Target=net7.0

set GpuTypes[0]=GPU_NONE
set GpuTypes[1]=GPU_CUDA
set GpuTypes[2]=GPU_DIRECTML
set GpuTypes[3]=GPU_OPENVINO

REM The csproj file uses the GpuTypes for clarity, but we don't want them as the 
REM file suffix

set FileSuffixes[0]=CPU
set FileSuffixes[1]=CUDA
set FileSuffixes[2]=DirectML
set FileSuffixes[3]=OpenVINO

echo.

set "index=0"
:BuildLoop
    if not defined GpuTypes[%index%] goto :endLoop

    call set GpuType=%%GpuTypes[%index%]%%
    call set FileSuffix=%%FileSuffixes[%index%]%%


    if /I "!Method!" == "Build" (
        echo   - Building ObjectDetectionNet for %GpuType%
        dotnet build -c %Configuration% --no-self-contained /p:DefineConstants=%GpuType% >nul 2>nul
    ) else (
        echo   - Publishing ObjectDetectionNet for %GpuType%
        dotnet publish -c %Configuration% --no-self-contained -p:UseAppHost=false /p:DefineConstants=%GpuType% >nul 2>nul
    )

    if errorlevel 1 (
        echo "!Method! FAILED. Cancelling"
        goto :endLoop
    )

    REM Zip it up. Note that we're excluding models because the install scripts  
    REM will pull them down separately

    if /i "!Method!" == "Build" (
        tar -caf ObjectDetectionNet-%FileSuffix%-%Version%.zip --exclude=assets --exclude=custom-models -C .\bin\%Configuration%\%Target%\ *
    ) else (
        tar -caf ObjectDetectionNet-%FileSuffix%-%Version%.zip --exclude=assets --exclude=custom-models -C ./bin/%Configuration%/%Target%/publish *
    )

    rem Cleanup
    del /s /f /q .\bin\%Configuration%\ >nul 2>nul
    del /s /f /q .\obj\%Configuration%\ >nul 2>nul

    REM Next...
    set /a "index+=1"
    GOTO :BuildLoop
:endLoop

REM ... and create the actual module package. It's just the install scripts. 
REM All assets are in S3.
tar -caf %moduleId%-%version%.zip --exclude=*.development.* --exclude=*.docker.build.* --exclude=*.log ^
    modulesettings.* install.sh install.bat
