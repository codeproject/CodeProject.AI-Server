@Echo off
REM Module Packaging script. To be called from create_packages.bat

set moduleId=%~1
set version=%~2


tar -caf %moduleId%-%version%.zip --exclude=__pycache__  --exclude=*.development.* --exclude=*.log ^
    nltk_data\* *.py modulesettings.* requirements.* install.sh install.bat explore.html
