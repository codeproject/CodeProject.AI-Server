@Echo off
REM Module Packaging script. To be called from create_packages.bat

set moduleId=%~1
set version=%~2

REM NOTE: No install.bat. This doesn't work on Windows

tar -caf %moduleId%-%version%.zip --exclude=__pycache__  --exclude=*.development.* --exclude=*.log  ^
    utils\* *.py modulesettings.* requirements.* install.sh explore.html test\*