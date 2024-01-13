#!/bin/bash

# CodeProject.AI Server and Analysis modules: Cleans debris, properly, for clean build
#
# Usage:
#   bash clean.sh  [build | install | installall | downloads | all]
#
# We assume we're in the /src directory

# import the utilities. This sets os, platform and architecture
source "$(dirname "$0")/../Scripts/utils.sh"

function removeFile() {
    local filePath=$1

    if [ "$doDebug" = true ]; then
        writeLine "Marked for removal: ${filePath}" "$color_error"
    else
        if [ -f "$filePath" ]; then
            writeLine "Removing ${filePath}" "$color_success"
            rm -f "$filePath"
        else
            writeLine "Not Removing ${filePath} (it doesn't exist)" "$color_mute"
        fi
    fi
}

function removeDir() {
    local dirPath=$1

    if [ "$doDebug" = true ]; then
        writeLine "Marked for removal: ${dirPath}" "$color_error"
    else
        if [ -d "$dirPath" ]; then
            writeLine "Removing ${dirPath}" "$color_success"
            rm  -r -f -d "$dirPath"
        else
            writeLine "Not Removing ${dirPath} (it doesn't exist)" "$color_mute"
        fi
    fi
}

function cleanSubDirs() {
    
    local basePath=$1
    local dirPattern=$2
    local excludeDirPattern=$3

    pushd "${basePath}" >/dev/null 2>/dev/null
    if [ $? -ne 0 ]; then
        writeLine "Can't navigate to ${basePath} (but that's probably OK)" "$color_warn"
        # popd  >/dev/null
        return
    fi

    if [ "$doDebug" = true ]; then
        if [ "${excludeDirPattern}" = "" ]; then
            writeLine "Removing folders in $(pwd) that match ${dirPattern}" "$color_info"
        else
            writeLine "Removing folders in $(pwd) that match ${dirPattern} except ${excludeDirPattern}" "$color_info"
        fi
    fi

    popd >/dev/null

    # Loop through all subdirs recursively
    # echo "** In ${basePath} searching for ${dirPattern} and ignoring ${excludeDirPattern}*" 

    IFS=$'\n'; set -f

    for dirName in $(find "$basePath" -type d -path "${dirPattern}"* ); do    

        dirMatched=true
        if [ "${excludeDirPattern}" != "" ]; then
            if [[ $dirName =~ ${excludeDirPattern} ]]; then
                dirMatched="false"
            fi
        fi

        if [ "$dirMatched" = true ]; then

            if [ "$doDebug" = true ]; then
                writeLine "Marked for removal: ${dirName}" "$color_error"
            else
                rm -r -f -d "$dirName"
            
                if [ -d "$dirName" ]; then    
                    writeLine "Unable to remove ${dirName}" "$color_error"
                else
                    writeLine "Removed ${dirName}" "$color_success"
                fi
            fi
        else
            if [ "$doDebug" = true ]; then
                writeLine "Not deleting ${dirName}" "$color_success"
            fi
        fi

    done

    unset IFS; set +f
}

function cleanFiles() {
    
    local basePath=$1
    local excludeFilePattern=$2

    pushd "${basePath}" >/dev/null 2>/dev/null

    if [ $? -ne 0 ]; then
        writeLine "Can't navigate to ${basePath} (but that's probably OK)" "$color_warn"
        # popd  >/dev/null
        return
    fi

    if [ "$doDebug" = true ]; then
        if [ "${excludeFilePattern}" = "" ]; then
            writeLine "Removing files in $(pwd)" "$color_warn"
        else
            writeLine "Removing files in $(pwd) except ${excludeFilePattern}" "$color_warn"
        fi
    fi

    IFS=$'\n'; set -f

    for fileName in $(find . -type f ); do    

        fileMatched=true
        if [ "${excludeFilePattern}" != "" ]; then
            if [[ $fileName == ${excludeFilePattern} ]]; then
                fileMatched="false"
            fi
        fi

        if [ "$fileMatched" = true ]; then

            if [ "$doDebug" = true ]; then
                writeLine "Marked for removal: ${fileName}" "$color_error"
            else
                rm -f "$fileName"
            
                if [ -f "$fileName" ]; then    
                    writeLine "Removed ${fileName}" $color_success
                else
                    writeLine "Unable to remove ${fileName}" "$color_error"
                fi
            fi
        else
            if [ "$doDebug" = true ]; then
                writeLine "Not deleting ${fileName}" "$color_success"
            fi
        fi

    done

    unset IFS; set +f

    popd >/dev/null
}

clear

installDir="$(pwd)"
pushd ../../.. >/dev/null
rootDir="$(pwd)"
popd >/dev/null

useColor=true
doDebug=false
lineWidth=70

dotNetModules=( "ObjectDetectionNet" "PortraitFilter" "SentimentAnalysis" )
pythonModules=( "ALPR" "BackgroundRemover" "Cartooniser" "FaceProcessing" "ObjectDetectionCoral" "ObjectDetectionYolo" "ObjectDetectionYoloRKNN" "OCR" "SceneClassifier" "SuperResolution" "TextSummary" "TrainingYoloV5" "YOLOv5-3.1" )


if [ "$1" = "" ]; then
    writeLine 'Solution Cleaner' 'White'
    writeLine
    writeLine  'clean.sh [build : install : installall : all]'
    writeLine  
    writeLine  '  build      - cleans build output (bin / obj)'
    writeLine  '  install    - removes current OS installation stuff (PIPs, downloads etc)'
    writeLine  '  installall - removes installation stuff for all platforms'
    writeLine  '  assets     - removes assets to force re-copy of downloads'
    writeLine  '  data       - removes user data stored by modules'
    writeLine  '  downloads  - removes downloads to force re-download'
    writeLine  '  all        - removes build and installation stuff for all platforms'
    
    if [ "$os" = "macos" ]; then
        writeLine
        writeLine  '  tools      - removes xcode GCC tools'
        writeLine  '  runtimes   - removes installed runtimes (Python only)'
    fi
    
    writeLine
    exit
fi

cleanBuild=false
cleanInstallCurrentOS=false
cleanInstallAll=false
cleanAssets=false
cleanUserData=false
cleanDownloadCache=false
cleanAll=false

cleanTools=false
cleanRuntimes=false

if [ "$1" = "build" ]; then          cleanBuild=true; fi
if [ "$1" = "install" ]; then        cleanInstallCurrentOS=true; fi
if [ "$1" = "installall" ]; then     cleanInstallAll=true; fi
if [ "$1" = "assets" ]; then         cleanAssets=true; fi
if [ "$1" = "data" ]; then           cleanUserData=true; fi
if [ "$1" = "download-cache" ]; then cleanDownloadCache=true; fi
if [ "$1" = "all" ]; then            cleanAll=true; fi

# not covered by "all"
if [ "$1" = "tools" ]; then      cleanTools=true; fi
if [ "$1" = "runtimes" ]; then   cleanRuntimes=true; fi

if [ "$cleanAll" = true ]; then
    cleanInstallAll=true
    cleanBuild=true
    cleanUserData=true
    cleanAssets=true
    cleanDownloadCache=true
fi

if [ "$cleanInstallCurrentOS" = true ] || [ "$cleanInstallAll" = true ]; then
    cleanBuild=true
    cleanAssets=true
    cleanUserData=true
fi

if [ "$cleanBuild" = true ]; then
    
    writeLine 
    writeLine "Cleaning Build" "White" "Blue" $lineWidth
    writeLine 

    removeDir "${rootDir}/src/server/bin/"
    removeDir "${rootDir}/src/server/obj/"

    removeDir "${rootDir}/src/SDK/NET/bin/"
    removeDir "${rootDir}/src/SDK/NET/obj/"

    for name in "${dotNetModules[@]}"
    do
        removeDir "${rootDir}/src/modules/${name}/bin/"
        removeDir "${rootDir}/src/modules/${name}/obj/"
    done

    cleanSubDirs "${rootDir}/src/modules/ObjectDetectionNet"   "bin"
    cleanSubDirs "${rootDir}/src/modules/PortraitFilter"       "bin"
    cleanSubDirs "${rootDir}/src/modules/SentimentAnalysis"    "bin"

    cleanSubDirs "${rootDir}/Installers/Windows" "bin/Debug/"
    cleanSubDirs "${rootDir}/Installers/Windows" "bin/Release/"
    cleanSubDirs "${rootDir}/Installers/Windows" "obj/Debug/"
    cleanSubDirs "${rootDir}/Installers/Windows" "obj/Release/"

    cleanSubDirs "${rootDir}/demos"              "bin/Debug/"
    cleanSubDirs "${rootDir}/demos"              "bin/Release/"
    cleanSubDirs "${rootDir}/demos"              "obj/Debug/"
    cleanSubDirs "${rootDir}/demos"              "obj/Release/"

    cleanSubDirs "${rootDir}/tests"              "bin/Debug/"
    cleanSubDirs "${rootDir}/tests"              "bin/Release/"
    cleanSubDirs "${rootDir}/tests"              "obj/Debug/"
    cleanSubDirs "${rootDir}/tests"              "obj/Release/"
fi

if [ "$cleanInstallCurrentOS" = true ]; then

    writeLine 
    writeLine "Cleaning ${platform} Install" "White" "Blue" $lineWidth
    writeLine 

    # Clean .NET build dirs
    for name in "${dotNetModules[@]}"
    do
        removeDir "${rootDir}/src/modules/${name}/bin/"
        removeDir "${rootDir}/src/modules/${name}/obj/"
    done

    # Clean shared python venvs
    removeDir "${rootDir}/src/runtimes/bin/${os}/"

    # Clean module python venvs
    for name in "${pythonModules[@]}"
    do
        removeDir "${rootDir}/src/modules/${name}/bin/${os}/"
    done
fi

if [ "$cleanUserData" = true ]; then

    writeLine 
    writeLine "Cleaning User data" "White" "Blue" $lineWidth
    writeLine 

    removeDir "${rootDir}/src/modules/FaceProcessing/datastore/"
fi

if [ "$cleanInstallAll" = true ]; then

    writeLine 
    writeLine "Cleaning install for all platforms" "White" "Blue" $lineWidth
    writeLine 

    # Clean shared python installs and venvs
    removeDir "${rootDir}/src/runtimes/bin"

    # Clean module python venvs
    for name in "${pythonModules[@]}"
    do
        removeDir "${rootDir}/src/modules/${name}/bin/"
    done
fi

if [ "$cleanAssets" = true ]; then

    writeLine 
    writeLine "Cleaning assets" "White" "Blue" $lineWidth
    writeLine 

    removeDir "${rootDir}/src/modules/ALPR/paddleocr"
    removeDir "${rootDir}/src/modules/BackgroundRemover/models"
    removeDir "${rootDir}/src/modules/Cartooniser/weights"
    removeDir "${rootDir}/src/modules/FaceProcessing/assets"
    removeDir "${rootDir}/src/modules/ObjectDetectionCoral/assets"
    removeDir "${rootDir}/src/modules/ObjectDetectionCoral/edgetpu_runtime"
    removeDir "${rootDir}/src/modules/ObjectDetectionNet/assets"
    removeDir "${rootDir}/src/modules/ObjectDetectionNet/custom-models"
    removeDir "${rootDir}/src/modules/ObjectDetectionNet/LocalNugets"
    removeDir "${rootDir}/src/modules/ObjectDetectionYolo/assets"
    removeDir "${rootDir}/src/modules/ObjectDetectionYolo/custom-models"
    removeDir "${rootDir}/src/modules/ObjectDetectionYoloRKNN/assets"
    removeDir "${rootDir}/src/modules/ObjectDetectionYoloRKNN/custom-models"
    removeDir "${rootDir}/src/modules/OCR/paddleocr"
    removeDir "${rootDir}/src/modules/SceneClassifier/assets"
    removeDir "${rootDir}/src/modules/TrainingYoloV5/datasets"
    removeDir "${rootDir}/src/modules/TrainingYoloV5/fiftyone"
    removeDir "${rootDir}/src/modules/TrainingYoloV5/training"
    removeDir "${rootDir}/src/modules/TrainingYoloV5/zoo"
    removeDir "${rootDir}/src/modules/YOLOv5-3.1/assets"
    removeDir "${rootDir}/src/modules/YOLOv5-3.1/custom-models"
fi

if [ "$cleanDownloadCache" = true ]; then

    writeLine 
    writeLine "Cleaning download cache" "White" "Blue" $lineWidth
    writeLine 

    for path in ${rootDir}/src/downloads/* ; do
        if [ -d "$path" ] && [ "$path" != "${rootDir}/src/downloads/modules" ]; then
            removeDir "${path}"
        fi
    done

    for path in ${rootDir}/src/downloads/modules/* ; do
        if [ -d "$path" ] && [ "$path" != "${rootDir}/src/downloads/modules/readme.txt" ]; then
            removeFile "${path}"
        fi
    done
fi

if [ "$os" = "macos" ]; then

    if [ "$cleanTools" = true ]; then
        writeLine 
        writeLine "Removing xcode command line tools" "White" "Blue" $lineWidth
        writeLine 

        cleanSubDirs "/Library/Developer" "CommandLineTools"
    fi
    
    if [ "$cleanRuntimes" = true ]; then
        writeLine 
        writeLine "Removing Python 3.8 and 3.9" "White" "Blue" $lineWidth
        writeLine 

        # Python < 3.9 needs Rosetta to run on M1 chips, so uninstall accordingly
        if [ "$platform" = "macos-arm64" ]; then
            arch -x86_64 /usr/local/bin/brew uninstall python@3.8
        else
            brew uninstall python@3.8
        fi

        brew uninstall python@3.9

        if [ "$platform" = "macos" ]; then
            brew uninstall onnxruntime
        fi
    fi
fi