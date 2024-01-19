#!/bin/bash

# Module Packaging script. To be called from create_packages.sh

moduleId=$1
version=$2

configuration="Release"
target="net7.0"

# Build
dotnet build -c ${configuration} >/dev/null 2>/dev/null

# Zip it up.
pushd ./bin/${configuration}/${target}/  >/dev/null 2>/dev/null

tar -a -cf ../../../${moduleId}-${version}.zip   --exclude=*.development.* --exclude=*.docker.build.* \
   *.*  test/*

popd >/dev/null 2>/dev/null

# Cleanup
rm -rf /q ./bin/${configuration}/${target}/ # >/dev/null 2>/dev/null
rm -rf /q ./obj/${configuration}/${target}/ # >/dev/null 2>/dev/null
