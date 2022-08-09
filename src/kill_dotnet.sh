#!/bin/bash
ps axco pid,command | grep dotnet | awk '{ print $1; }' | xargs kill -9
ps axco pid,command | grep python | awk '{ print $1; }' | xargs kill -9