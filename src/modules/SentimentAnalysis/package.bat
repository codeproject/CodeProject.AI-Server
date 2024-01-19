@Echo off
REM Module Packaging script. To be called from create_packages.bat

set moduleId=%~1
set version=%~2

set Configuration=Release
set Target=net7.0

rem Build
dotnet build -c %Configuration%  >nul 

rem Create the module package
if exist ".\bin\windows\%Configuration%\%Target%\" (        REM No idea why this is happening.

    tar -caf %moduleId%-%version%.zip --exclude=*.development.* ^
        install.sh install.bat -C .\bin\windows\%Configuration%\%Target%\ *.*

    rem Cleanup
    del /s /f /q .\bin\windows\%Configuration%\%Target%\ >nul 2>nul
    del /s /f /q .\obj\windows\%Configuration%\%Target%\ >nul 2>nul

) else ( 

    tar -caf %moduleId%-%version%.zip --exclude=*.development.* ^
        install.sh install.bat explore.html -C .\bin\%Configuration%\%Target%\ *.*

    rem Cleanup
    del /s /f /q .\bin\%Configuration%\%Target%\ >nul 2>nul
    del /s /f /q .\obj\%Configuration%\%Target%\ >nul 2>nul

)

