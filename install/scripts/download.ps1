function DownloadAndExtract($URL, $DownloadDir, $DirToSave){

    Write-Host -NoNewline "Downloading to" $DownloadDir$DirToSave"..."    # "from" $URL "..."

    try {

        # Doesn't provide progress as %
        # Invoke-WebRequest -Uri $URL -OutFile $DownloadDir$DirToSave".zip"

        Start-BitsTransfer -Source $URL -Destination $DownloadDir$DirToSave".zip"
    }
    catch {
        "An error occurred that could not be resolved."
    }

    Write-Host -NoNewline "Expanding..."
    Expand-Archive -Path $DownloadDir$DirToSave".zip" -DestinationPath $DownloadDir$DirToSave -Force
    Remove-Item -Path $DownloadDir$DirToSave".zip" -Force
    Write-Host "Done."
}

$storageUrl  = $args[0] # "https://codeproject-ai.s3.ca-central-1.amazonaws.com/sense/installer/"
$downloadDir = $args[1] # "downloads/" # relative to the current directory
$fileToGet   = $args[2] # eg packages_for_gpu.zip
$dirToSave   = $args[3] # eg packages

DownloadAndExtract -URL $storageUrl$fileToGet -DownloadDir $downloadDir -DirToSave $dirToSave