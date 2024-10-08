#!/bin/bash

# Module Packaging script. To be called by: bash ../../devops/build/create_packages.sh

moduleId=$1
version=$2

tar -caf ${moduleId}-${version}.zip --exclude=__pycache__  --exclude=*.development.* --exclude=*.log \
    *.py modulesettings.* requirements.* install.sh install.bat explore.html test/*
