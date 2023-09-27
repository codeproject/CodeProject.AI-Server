#!/bin/bash

dotnet build -c Release --no-self-contained /p:DefineConstants=GPU_NONE
dotnet build -c Release --no-self-contained /p:DefineConstants=GPU_CUDA
dotnet build -c Release --no-self-contained /p:DefineConstants=GPU_DIRECTML
dotnet build -c Release --no-self-contained /p:DefineConstants=GPU_OPENVINO
