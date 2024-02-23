#!/bin/bash

# Module Packaging script. To be called from create_packages.sh

moduleId=$1
version=$2

tar -caf ${moduleId}-${version}.zip --exclude=__pycache__  --exclude=*.development.* --exclude=*.log \
    patch/* utils/* *.py modulesettings.* requirements.* install.sh install.bat post_install.sh post_install.bat explore.html
