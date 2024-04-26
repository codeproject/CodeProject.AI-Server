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

set GpuTypes[0]=CPU
set GpuTypes[1]=GPU_CUDA
set GpuTypes[2]=GPU_DIRECTML
set GpuTypes[3]=GPU_OPENVINO

REM The csproj file uses the GpuTypes for clarity, but we don't want them as the 
REM file suffix

set FileSuffixes[0]=CPU
set FileSuffixes[1]=CUDA
set FileSuffixes[2]=DirectML
set FileSuffixes[3]=OpenVINO

echo:

set "index=0"
:BuildLoop
    if not defined GpuTypes[%index%] goto :endLoop

    call set GpuType=%%GpuTypes[%index%]%%
    call set FileSuffix=%%FileSuffixes[%index%]%%

    if /I "%Method%" == "build" (
        echo   - Building ObjectDetectionYOLOv5Net for %GpuType%
        if /i "%verbosity%" == "quiet" (
            dotnet build -c %Configuration% --no-self-contained --no-incremental --force -o .\bin\%Configuration%\%FileSuffix%\ /p:DefineConstants=%GpuType% >NUL
        ) else (
            dotnet build -c %Configuration% --no-self-contained --no-incremental --force -o .\bin\%Configuration%\%FileSuffix%\ /p:DefineConstants=%GpuType% 
        )
    ) else (
        echo   - Publishing ObjectDetectionYOLOv5Net for %GpuType%
        if /i "%verbosity%" == "quiet" (
            dotnet publish -c %Configuration% --no-self-contained -p:UseAppHost=false /p:DefineConstants=%GpuType% >nul 
        ) else (
            dotnet publish -c %Configuration% --no-self-contained -p:UseAppHost=false /p:DefineConstants=%GpuType%
        )
    )

    if errorlevel 1 (
        echo "%Method% FAILED. Cancelling"
        goto :endLoop
    )

    REM Zip it up. Note that we're excluding models because the install scripts  
    REM will pull them down separately
   
    if /I "%Method%" == "build" (
        tar -caf ObjectDetectionYOLOv5Net-%FileSuffix%-%version%.zip --exclude=assets --exclude=custom-models -C .\bin\%Configuration%\%FileSuffix%\ *
    ) else (
        tar -caf ObjectDetectionYOLOv5Net-%FileSuffix%-%version%.zip --exclude=assets --exclude=custom-models -C .\bin\%Configuration%\%Target%publish *
    )
    
    rem Cleanup
    REM del /s /f /q .\bin\%Configuration%\ >nul 2>nul
    del /s /f /q .\obj\%Configuration%\ >nul 2>nul

    REM Next...
    set /a "index+=1"
    GOTO :BuildLoop
:endLoop

REM ... and create the actual module package. It's just the install scripts. 
REM All assets are in S3.
tar -caf %moduleId%-%version%.zip --exclude=*.development.* --exclude=*.docker.build.* --exclude=*.log ^
    modulesettings.* install.sh install.bat explore.html test\*
