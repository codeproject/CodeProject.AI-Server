#!/bin/bash

dotnet build -c Release --no-self-contained /p:DefineConstants=GPU_NONE
