#!/bin/bash

ps axco pid,command | grep -i dotnet | awk '{ print $1; }' | xargs kill -9 2>/dev/null
ps axco pid,command | grep -i python | awk '{ print $1; }' | xargs kill -9 2>/dev/null