@Echo off
REM Module Packaging script. To be called from create_packages.bat

set moduleId=%~1
set version=%~2

set Configuration=Release
set Target=net7.0

rem Build
dotnet build -c %Configuration%  >nul 

rem Create the module package
tar -caf %moduleId%-%version%.zip --exclude=*.development.* ^
    .\test\* install.sh install.bat                         ^
    -C .\bin\%Configuration%\%Target%\ *.*

rem Cleanup
del /s /f /q .\bin\%Configuration%\%Target%\ >nul 2>nul
del /s /f /q .\obj\%Configuration%\%Target%\ >nul 2>nul
