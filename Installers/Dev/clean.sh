# CodeProject.AI Server and Analysis modules: Cleans debris, properly, for clean build
#
# Usage:
#   bash clean_for_build.sh
#
# We assume we're in the /src directory

# import the utilities
source $(dirname "$0")/utils.sh

function cleanSubDirs() {
	
    local basePath=$1
    local dirPattern=$2
    local excludeDirPattern=$3

    pushd "${basePath}"  >/dev/null
    if [ $? -ne 0 ]; then
        writeLine "Can't navigate to $(pwd)/${basePath}" $color_error
        popd  >/dev/null
        return
    fi

    if [ "$doDebug" == "true" ]; then
        if [ "${excludeDirPattern}" == "" ]; then
            writeLine "Removing folders in $(pwd) that match ${dirPattern}" $color_info
        else
            writeLine "Removing folders in $(pwd) that match ${dirPattern} except ${excludeDirPattern}" $color_info
        fi
    fi

    popd >/dev/null

    # Loop through all subdirs recursively
    # echo "** In ${basePath} searching for ${dirPattern} and ignoring ${excludeDirPattern}*" 

    IFS=$'\n'; set -f

    for dirName in $(find "$basePath" -type d -name "${dirPattern}" ); do    

        dirMatched="true"
        if [ "${excludeDirPattern}" != "" ]; then
            if [[ $dirName =~ ${excludeDirPattern} ]]; then
                dirMatched="false"
            fi
        fi

        if [ "$dirMatched" == "true" ]; then

            if [ "$doDebug" == "true" ]; then
                writeLine "Marked for removal: ${dirName}" $color_error
            else
                rm -r -f -d "$dirName";
            
                if [ -d "$dirName" ]; then    
                    writeLine "Unable to remove ${dirName}"  $color_error
                else
                    writeLine "Removed ${dirName}" $color_success
                fi
            fi
        else
            if [ "$doDebug" == "true" ]; then
                writeLine "Not deleting ${dirName}" $color_success
            fi
        fi

    done

    unset IFS; set +f
}

function cleanFiles() {
	
    local basePath=$1
    local excludeFilePattern=$2

    pushd "${basePath}"  >/dev/null

    if [ $? -ne 0 ]; then
        writeLine "Can't navigate to $(pwd)/${basePath}" $color_error
        popd  >/dev/null
        return
    fi

    if [ "$doDebug" == "true" ]; then
        if [ "${excludeFilePattern}" == "" ]; then
            writeLine "Removing files in $(pwd)" $color_warn
        else
            writeLine "Removing files in $(pwd) except ${excludeFilePattern}" $color_warn
        fi
    fi

    IFS=$'\n'; set -f

    for fileName in $(find . -type f ); do    

        fileMatched="true"
        if [ "${excludeFilePattern}" != "" ]; then
            if [[ $fileName == ${excludeFilePattern} ]]; then
                fileMatched="false"
            fi
        fi

        if [ "$fileMatched" == "true" ]; then

            if [ "$doDebug" == "true" ]; then
                writeLine "Marked for removal: ${fileName}" $color_error
            else
                rm -f "$fileName";
            
                if [ -f "$fileName" ]; then    
                    writeLine "Removed ${fileName}" $color_success
                else
                    writeLine "Unable to remove ${fileName}" $color_error
                fi
            fi
        else
            if [ "$doDebug" == "true" ]; then
                writeLine "Not deleting ${fileName}" $color_success
            fi
        fi

    done

    unset IFS; set +f

    popd >/dev/null
}

clear
useColor="true"
doDebug="false"


# Platform can define where things are located
if [[ $OSTYPE == 'darwin'* ]]; then
    platform='macos'
else
    platform='linux'
fi


if [ "$1" == "" ]; then
    writeLine 'Solution Cleaner' 'White'
    writeLine
    writeLine  'clean.sh [build : install : installall : all]'
    writeLine  
    writeLine  '  build      - cleans build output (bin / obj)'
    writeLine  '  assets     - removes assets to force re-copy of downloads'
    writeLine  '  downloads  - removes downloads to force re-download'
    writeLine  '  install    - removes current OS installation stuff (PIPs, downloads etc)'
    writeLine  '  installall - removes installation stuff for all platforms'
    writeLine  '  all        - removes build and installation stuff for all platforms'
    
    if [ "%platform%" == "macos" ]; then
        writeLine
        writeLine  '  tools      - removes xcode GCC tools'
        writeLine  '  runtimes   - removes installed runtimes (Python only)'
    fi
    
    writeLine
    exit
fi

cleanAssets='false'
cleanDownloads='false'
cleanBuild='false'
cleanInstallLocal='false'
cleanInstallAll='false'
cleanAll='false'
cleanTools='false'
cleanRuntimes='false'

if [ "$1" == "assets" ]; then     cleanAssets='true'; fi
if [ "$1" == "downloads" ]; then  cleanDownloads='true'; fi
if [ "$1" == "build" ]; then      cleanBuild='true'; fi
if [ "$1" == "install" ]; then    cleanInstallLocal='true'; fi
if [ "$1" == "installall" ]; then cleanInstallAll='true'; fi
if [ "$1" == "all" ]; then        cleanAll='true'; fi

# not covered by "all"
if [ "$1" == "tools" ]; then      cleanTools='true'; fi
if [ "$1" == "runtimes" ]; then   cleanRuntimes='true'; fi

if [ "$cleanAll" == 'true' ]; then
    cleanInstallAll='true'
    cleanBuild='true'
    cleanAssets='true'
    cleanDownloads='true'
fi

if [ "$cleanInstallLocal" == 'true' ] || [ "$cleanInstallAll" == 'true' ]; then
    cleanAssets='true'
fi

if [ "$cleanBuild" == "true" ]; then
    
    writeLine 
    writeLine "Cleaning Build                                                      " "White" "Blue"
    writeLine 

    cleanSubDirs "../../src" "bin" "AnalysisLayer/bin"
    cleanSubDirs "../../src" "obj" "ObjectDetection"
    cleanSubDirs "../windows" "bin"
    cleanSubDirs "../windows" "obj"
    cleanSubDirs "../../demos" "bin"
    cleanSubDirs "../../demos" "obj" "Objects"
    cleanSubDirs "../../tests" "bin"
    cleanSubDirs "../../tests" "obj"
fi

if [ "$cleanInstallLocal" == "true" ]; then

    writeLine 
    writeLine "Cleaning ${platform} Install                                            " "White" "Blue"
    writeLine 

    cleanSubDirs "../../src/AnalysisLayer/bin" "${platform}"
    cleanSubDirs "../../src/AnalysisLayer/BackgroundRemover" "models"
    cleanSubDirs "../../src/AnalysisLayer/ObjectDetectionNet" "assets"
    cleanSubDirs "../../src/AnalysisLayer/ObjectDetectionYolo" "assets"
    cleanSubDirs "../../src/AnalysisLayer/Vision" "assets"
    cleanSubDirs "../../src/AnalysisLayer/Vision" "datastore"
    cleanSubDirs "../../src/AnalysisLayer/Vision" "tempstore"
fi

if [ "$cleanInstallAll" == "true" ]; then

    writeLine 
    writeLine "Cleaning install for all platforms                                  " "White" "Blue"
    writeLine 

    cleanSubDirs "../../src/AnalysisLayer" "bin"
fi

if [ "$cleanAssets" == "true" ]; then

    writeLine 
    writeLine "Cleaning assets                                                     " "White" "Blue"
    writeLine 

    cleanSubDirs "../../src/AnalysisLayer/BackgroundRemover"   "models"
    cleanSubDirs "../../src/AnalysisLayer/ObjectDetectionNet"  "assets"
    cleanSubDirs "../../src/AnalysisLayer/ObjectDetectionYolo" "assets"
    cleanSubDirs "../../src/AnalysisLayer/Vision"              "assets"
fi

if [ "$cleanDownloads" == "true" ]; then

    writeLine 
    writeLine "Cleaning downloads                                                  " "White" "Blue"
    writeLine 

    cleanFiles "../downloads" "*.zip"   # keep original downloads
fi

if [ "$platform" == "macos" ] || [ "$platform" == "macos-arm" ]; then

    if [ "$cleanTools" == "true" ]; then
        writeLine 
        writeLine "Removing xcode command line tools                                " "White" "Blue"
        writeLine 

        cleanSubDirs "/Library/Developer" "CommandLineTools"
    fi
    
    if [ "$cleanRuntimes" == "true" ]; then
        writeLine 
        writeLine "Removing Python 3.8 and 3.9                                      " "White" "Blue"
        writeLine 

        arch -x86_64 /usr/local/bin/brew uninstall python@3.8
        arch -x86_64 /usr/local/bin/brew uninstall python@3.9
    fi    
fi