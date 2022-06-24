#!/bin/bash
ps axco pid,command | grep dotnet | awk '{ print $1; }' | xargs kill -9