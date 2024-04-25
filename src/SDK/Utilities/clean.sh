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

externalModulesDir="${rootDir}/../CodeProject.AI-Modules"


useColor=true
doDebug=false
lineWidth=70

dotNetModules=( "ObjectDetectionYOLOv5Net" "PortraitFilter" "SentimentAnalysis" )
pythonModules=( "ALPR" "ALPR-RKNN" "BackgroundRemover" "Cartooniser" "FaceProcessing" \
                "LlamaChat" "ObjectDetectionYOLOv5-3.1" "ObjectDetectionYOLOv5-6.2"   \
                "ObjectDetectionYOLOv8" "TrainingObjectDetectionYOLOv5" "OCR"         \
                "SceneClassifier" "SoundClassifierTF" "SuperResolution" "TextSummary" "Text2Image")
dotNetExternalModules=()
pythonExternalModules=( "CodeProject.AI-ALPR" "CodeProject.AI-ObjectDetectionCoral"   \
                        "CodeProject.AI-ALPR-RKNN" "CodeProject.AI-ObjectDetectionYoloRKNN" )

dotNetDemoModules=( "DotNetLongProcess" "DotNetSimple" )
pythonDemoModules=( "PythonLongProcess" "PythonSimple" )


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

    for dirName in "${dotNetModules[@]}"
    do
        removeDir "${rootDir}/src/modules/${dirName}/bin/"
        removeDir "${rootDir}/src/modules/${dirName}/obj/"
        rm "${rootDir}/src/modules/${dirName}/${dirName}-*"
    done
    for dirName in "${dotNetExternalModules[@]}"
    do
        removeDir "${externalModulesDir}/${dirName}/bin/"
        removeDir "${externalModulesDir}/${dirName}/obj/"
        rm "${externalModulesDir}/${dirName}/${dirName}-*"
    done
    for dirName in "${dotNetDemoModules[@]}"
    do
        removeDir "${rootDir}/src/modules/${dirName}/bin/"
        removeDir "${rootDir}/src/modules/${dirName}/obj/"
        rm "${rootDir}/src/modules/${dirName}/${dirName}-*"
    done

    cleanSubDirs "${rootDir}/Installers/Windows" "bin/Debug/"
    cleanSubDirs "${rootDir}/Installers/Windows" "bin/Release/"
    cleanSubDirs "${rootDir}/Installers/Windows" "obj/Debug/"
    cleanSubDirs "${rootDir}/Installers/Windows" "obj/Release/"

    removeDir "${rootDir}/src/SDK/Utilities/ParseJSON/bin"
    removeDir "${rootDir}/src/SDK/Utilities/ParseJSON/obj"
    rm "${rootDir}/src/SDK/Utilities/ParseJSON/ParseJSON.deps.json"
    rm "${rootDir}/src/SDK/Utilities/ParseJSON/ParseJSON.dll"
    rm "${rootDir}/src/SDK/Utilities/ParseJSON/ParseJSON.exe"
    rm "${rootDir}/src/SDK/Utilities/ParseJSON/ParseJSON.runtimeconfig.json"
    rm "${rootDir}/src/SDK/Utilities/ParseJSON/ParseJSON.xml"

    cleanSubDirs "${rootDir}/src/demos/clients"      "bin/Debug/"
    cleanSubDirs "${rootDir}/src/demos/clients"      "bin/Release/"
    cleanSubDirs "${rootDir}/src/demos/clients"      "obj/Debug/"
    cleanSubDirs "${rootDir}/src/demos/clients"      "obj/Release/"

    cleanSubDirs "${rootDir}/tests"              "bin/Debug/"
    cleanSubDirs "${rootDir}/tests"              "bin/Release/"
    cleanSubDirs "${rootDir}/tests"              "obj/Debug/"
    cleanSubDirs "${rootDir}/tests"              "obj/Release/"
fi

if [ "$cleanInstallCurrentOS" = true ]; then

    writeLine 
    writeLine "Cleaning ${platform} Install" "White" "Blue" $lineWidth
    writeLine 

    # Clean shared python venvs
    removeDir "${rootDir}/src/runtimes/bin/${os}/"

    # Clean module python venvs
    for dirName in "${pythonModules[@]}"
    do
        removeDir "${rootDir}/src/modules/${dirName}/bin/${os}/"
    done
    for dirName in "${pythonExternalModules[@]}"
    do
        removeDir "${externalModulesDir}/${dirName}/bin/${os}/"
    done
    for dirName in "${pythonDemoModules[@]}"
    do
        removeDir "${rootDir}/src/demos/modules/${dirName}/bin/${os}/"
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
    for dirName in "${pythonModules[@]}"
    do
        removeDir "${rootDir}/src/modules/${dirName}/bin/"
    done
    for dirName in "${pythonExternalModules[@]}"
    do
        removeDir "${externalModulesDir}/${dirName}/bin/"
    done
    for dirName in "${pythonDemoModules[@]}"
    do
        removeDir "${rootDir}/src/demos/modules/${dirName}/bin/"
    done
fi

if [ "$cleanAssets" = true ]; then

    writeLine 
    writeLine "Cleaning assets" "White" "Blue" $lineWidth
    writeLine 

    # Internal modules
    removeDir "${rootDir}/src/modules/BackgroundRemover/models"
    removeDir "${rootDir}/src/modules/Cartooniser/weights"
    removeDir "${rootDir}/src/modules/FaceProcessing/assets"
    removeDir "${rootDir}/src/modules/LlamaChat/models"
    removeDir "${rootDir}/src/modules/ObjectDetectionYOLOv5Net/assets"
    removeDir "${rootDir}/src/modules/ObjectDetectionYOLOv5Net/custom-models"
    removeDir "${rootDir}/src/modules/ObjectDetectionYOLOv5Net/LocalNugets"
    removeDir "${rootDir}/src/modules/ObjectDetectionYOLOv5-3.1/assets"
    removeDir "${rootDir}/src/modules/ObjectDetectionYOLOv5-3.1/custom-models"
    removeDir "${rootDir}/src/modules/ObjectDetectionYOLOv5-6.2/assets"
    removeDir "${rootDir}/src/modules/ObjectDetectionYOLOv5-6.2/custom-models"
    removeDir "${rootDir}/src/modules/OCR/paddleocr"
    removeDir "${rootDir}/src/modules/SceneClassifier/assets"
    removeDir "${rootDir}/src/modules/SoundClassifierTF/data"
    removeDir "${rootDir}/src/modules/Text2Image/assets"
    removeDir "${rootDir}/src/modules/TrainingObjectDetectionYOLOv5/assets"
    removeDir "${rootDir}/src/modules/TrainingObjectDetectionYOLOv5/datasets"
    removeDir "${rootDir}/src/modules/TrainingObjectDetectionYOLOv5/fiftyone"
    removeDir "${rootDir}/src/modules/TrainingObjectDetectionYOLOv5/training"
    removeDir "${rootDir}/src/modules/TrainingObjectDetectionYOLOv5/zoo"


    # Esternal modules
    removeDir "${rootDir}/src/modules/ALPR/paddleocr"
    removeDir "${rootDir}/src/modules/ALPR-RKNN/paddleocr"
    removeDir "${externalModulesDir}/ObjectDetectionCoral/assets"
    removeDir "${externalModulesDir}/ObjectDetectionCoral/edgetpu_runtime"
    removeDir "${externalModulesDir}/ObjectDetectionYoloRKNN/assets"
    removeDir "${externalModulesDir}/ObjectDetectionYoloRKNN/custom-models"

    # Demo modules
    removeDir "${rootDir}/src/demos/modules/DotNetLongProcess/assets"
    removeDir "${rootDir}/src/demos/modules/DotNetSimple/assets"
    removeDir "${rootDir}/src/demos/modules/PythonLongProcess/assets"
    removeDir "${rootDir}/src/demos/modules/PythonSimple/assets"
fi

if [ "$cleanDownloadCache" = true ]; then

    writeLine 
    writeLine "Cleaning download cache" "White" "Blue" $lineWidth
    writeLine 

    for path in ${rootDir}/src/downloads/* ; do
        if [ -d "$path" ] && [ "$path" != "${rootDir}/src/downloads/modules" ] && [ "$path" != "${rootDir}/src/downloads/models" ]; then
            removeDir "${path}"
        fi
    done

    for path in ${rootDir}/src/downloads/modules/* ; do
        if [ -d "$path" ] && [ "$path" != "${rootDir}/src/downloads/modules/readme.txt" ]; then
            removeFile "${path}"
        fi
    done
    for path in ${rootDir}/src/downloads/models/* ; do
        if [ -d "$path" ] && [ "$path" != "${rootDir}/src/downloads/models/models.json" ]; then
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