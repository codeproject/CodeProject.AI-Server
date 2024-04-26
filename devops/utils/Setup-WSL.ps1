# ==============================================================================
# This script sets up WSL on an external drive E:
# Very handy if your primary drive is getting a little tight on space.
# ==============================================================================

# Find the URL of the distribution you want to install from
# https://docs.microsoft.com/en-us/windows/wsl/install-manual#downloading-distributions
$installUrl = "https://aka.ms/wslubuntu2204"

# You'll find this if you extract the archive in the install URL. Good luck.
$distroName = "Ubuntu_2204.1.7.0_x64"

# For naming folders
$installName = "Ubuntu2204"

# Substitute the drive on which you want WSL to be installed if not E:
Set-Location E:

# Create a directory for our installation and change to it, we'll call it WSL-Ubuntu2204:
New-Item WSL -Type Directory
Set-Location .\WSL

# Using the URL you found above, download the appx package:
# Invoke-WebRequest -Uri $installUrl -OutFile ${installName}.appx -UseBasicParsing
Start-BitsTransfer -Source ${installUrl} -Description ${installName} -Destination '${installName}-all.appx'

# unpack:
ren .\${installName}-all.appx .\${installName}-all.zip
Expand-Archive .\${installName}-all.zip
del .\${installName}-all.zip

Set-Location ${installName}-all

# rename to .zip so that Expand-Archive will work
ren .\${distroName}.appx .\${distroName}.zip
Expand-Archive .\${distroName}.zip
del .\${distroName}.zip

mv ${distroName} ../${installName}
Set-Location ..
rmdir ${installName}-all -Confirm:$false -Recurse -Force
Set-Location ${installName}

# Now it exists, run it. This will install Ubuntu on WSL
ubuntu.exe