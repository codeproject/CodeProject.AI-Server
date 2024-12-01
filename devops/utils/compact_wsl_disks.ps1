# This script will search for WSL's VHDX files for and compact them

# ENSURE:
# 1. You can run scripts:
#    Set-ExecutionPolicy RemoteSigned -Scope CurrentUser
# 2. You have Hyper-V management tools:
#    Enable-WindowsOptionalFeature -Online -FeatureName Microsoft-Hyper-V-All -All -NoRestart

# Check if the script is running with administrator privileges
$IsAdmin = [bool]([System.Security.Principal.WindowsIdentity]::GetCurrent().Owner.IsWellKnown([System.Security.Principal.WellKnownSidType]::BuiltinAdministratorsSid))

if (-not $IsAdmin) {
    # Relaunch the script with administrator privileges
    $argList = "$($myinvocation.MyCommand.Path)"  # Get the script path
    Start-Process powershell -ArgumentList "-NoProfile -ExecutionPolicy Bypass -File $argList" -Verb RunAs
    exit
}

# Shutdown WSL
Write-Host "Shutting down WSL..."
wsl --shutdown
Start-Sleep -Seconds 5  # Wait a few seconds to ensure WSL is fully shut down
Write-Host "WSL shut down successfully."

# Search for all VHDX files under LOCALAPPDATA\...\Packages\...
$baseFolderPath = Join-Path -Path $env:LOCALAPPDATA -ChildPath "Packages"
$vdiskFiles = Get-ChildItem -Path $baseFolderPath -Recurse -Filter "ext4.vhdx"

# Loop through each VHDX file
foreach ($vdisk in $vdiskFiles) {

    $diskName = $vdisk.Name
    if ($vdisk.FullName -like "*Ubuntu*") {
        $diskName = "Ubuntu VHDX"
    } elseif ($vdisk.FullName -like "*Debian*") {
        $diskName = "Debian VHDX"
    }

    Write-Host "Found ${diskName} at $($vdisk.FullName)"
    
    # Ask the user if they want to compact this VHD/VHDX
    $userInput = Read-Host "Do you want to compact this disk? (Y/N)"
    
    if ($userInput -match "^[Yy]$") {
        # Compact the VHD/VHDX file to reclaim unused space
        Optimize-VHD -Path $vdisk.FullName -Mode Full
        Write-Host "VHD compacted: $($vdisk.Name)"
    } 
    # else
    # {
    #    Write-Host "Skipping VHD/VHDX: $($vdisk.Name)"
    # }
}

Write-Host "VHD/VHDX compression complete."
