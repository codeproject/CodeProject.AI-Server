# This script will search for WSL's VHDX files for and compact them

# ENSURE:
# 1. You can run scripts:
#    Set-ExecutionPolicy RemoteSigned -Scope CurrentUser
# 2. You have Hyper-V management tools:
#    Enable-WindowsOptionalFeature -Online -FeatureName Microsoft-Hyper-V-All -All -NoRestart


# Define the base folder path using the LOCALAPPDATA environment variable
$baseFolderPath = Join-Path -Path $env:LOCALAPPDATA -ChildPath "Packages"

# Search for all VHDX files matching the pattern
$vdiskFiles = Get-ChildItem -Path $baseFolderPath -Recurse -Filter "ext4.vhdx"

Write-Host "Shutting down WSL..."
wsl --shutdown
Start-Sleep -Seconds 5  # Wait a few seconds to ensure WSL is fully shut down
Write-Host "WSL shut down successfully."

# Loop through each VHDX file found
foreach ($vdisk in $vdiskFiles) {
    Write-Host "Processing VHD/VHDX: $($vdisk.FullName)"

    # Mount the VHD/VHDX file
    Mount-VHD -Path $vdisk.FullName -PassThru | Out-Null
    Write-Host "VHD mounted: $($vdisk.Name)"

    # Compact the VHD/VHDX file to reclaim unused space
    Optimize-VHD -Path $vdisk.FullName -Mode Full
    Write-Host "VHD compacted: $($vdisk.Name)"

    # Dismount the VHD
    Dismount-VHD -Path $vdisk.FullName
    Write-Host "VHD dismounted: $($vdisk.Name)"
}

Write-Host "VHD/VHDX compression complete."
