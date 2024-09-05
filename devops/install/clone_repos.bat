:: CodeProject.AI Server: Clone all repos
::
:: We assume we're in the /devops/install directory

@echo off
cls
setlocal enabledelayedexpansion

set modules=CodeProject.AI-ALPR                          ^
            CodeProject.AI-ALPR-RKNN                     ^
            CodeProject.AI-BackgroundRemover             ^
            CodeProject.AI-Cartoonizer                   ^
            CodeProject.AI-FaceProcessing                ^
            CodeProject.AI-LlamaChat                     ^
            CodeProject.AI-MultiModeLLM                  ^
            CodeProject.AI-ObjectDetectionCoral          ^
            CodeProject.AI-ObjectDetectionYOLOv5-3.1     ^
            CodeProject.AI-ObjectDetectionYOLOv8         ^
            CodeProject.AI-ObjectDetectionYoloRKNN       ^
            CodeProject.AI-TrainingObjectDetectionYOLOv5 ^
            CodeProject.AI-OCR                           ^
            CodeProject.AI-PortraitFilter                ^
            CodeProject.AI-SceneClassifier               ^
            CodeProject.AI-SentimentAnalysis             ^
            CodeProject.AI-SoundClassifierTF             ^
            CodeProject.AI-SuperResolution               ^
            CodeProject.AI-TextSummary                   ^
            CodeProject.AI-Text2Image


pushd ..\..
set rootDir=%cd%
popd

set externalModulesDir=!rootDir!\..\CodeProject.AI-Modules

if not exist !externalModulesDir! (
    pushd !rootDir!\..
    mkdir CodeProject.AI-Modules
    popd
)

if exist !externalModulesDir! (
    pushd !externalModulesDir!

    echo.
    echo Cloning all missing CodeProject.AI module projects
    for %%x in (!modules!) do (
        if not exist %%x git clone https://github.com/codeproject/%%x
    )
    echo.
    echo Done
    popd
)