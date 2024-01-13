#!/bin/bash

# Module Packaging script. To be called from create_packages.sh

moduleId=$1
version=$2

tar -caf ${moduleId}-${version}.zip --exclude=__pycache__  --exclude=data --exclude=*.development.* --exclude=*.log \
    audio/* vggish/* *.py modulesettings.* requirements.* install.sh explore.html test/*