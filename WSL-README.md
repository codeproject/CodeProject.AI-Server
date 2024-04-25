# Notes on developing under WSL

This repository can be run under Windows and WSL at the same time. The setup.bat
and setup.sh scripts will install runtimes and virtual environments into specific
locations for the given OS. Virtual environments, for example, will live under
the /bin/windows folder for a module in Windows, and /bin/linux when under Linux.
This allows the same code and models to be worked on and tested under both 
Operating Systems.

Note that this means you will need VS Code installed in Windows and in the WSL
hosted Ubuntu (should you choose to use Ubuntu in WSL). This further requires
that each instance of VS Code has the necessary extensions installed. The
profile sync mechanism in VS Code makes this seamless.

## VSCode integrated terminals won't load .bashrc, and so $PATH may not be accurate

If you are using VSCode in WSL, the integrated terminal doesn't load the login 
stuff. This means your .bashrc isn't loaded.

If you've installed a CUDA toolkit which has set paths in the `PATH` variable 
and updated the .bashrc file, the `PATH` variable won't have the CUDA paths in 
it, meaning you won't get access to all the CUDA tools (typically just 
`nvidia-smi`, not `nvcc`).

If the install scripts can only see `nvidia-smi` and not `nvcc` then those 
scripts will potentially see an incorrect version of CUDA. `nvidia-smi` reports
the maximum CUDA version the drivers can support, whereas `nvcc` reports the 
actual CUDA version installed and accessible.

Getting the wrong version of CUDA means potentially the wrong version of Python 
libraries such as PyTorch will be installed, leading to instability or failure.

Install CodeProject.AI on a CUDA under WSL using a terminal window, not the 
integrated VSCode terminal

## Getting a VSCode Integrated terminal to load login profiles under WSL

To get the login profile loaded add this to your settings.json file

```json
"terminal.integrated.shellArgs.windows": ["-l"],
"terminal.integrated.shellArgs.linux": ["-l"],
"terminal.integrated.shellArgs.osx": ["-l"],
```
The settings files are typically located in

| OS      | settings location                                             |
|---------|---------------------------------------------------------------|
| Windows | `C:\Users\<username>\AppData\Roaming\Code\User\settings.json` |
| Linux   | `$HOME/.config/Code/User/settings.json`                       |
| macOS   | `$HOME/Library/Application\ Support/Code/User/settings.json`  |

You can also set your default profiles to achieve the same result (replace `osx`
with `Windows` and `Linux` for other OS's) 

```json
"terminal.integrated.profiles.osx": {
  "bash": {
    "path": "bash",
    "args": ["-l"]
  }
} 
```

See this [Stackoverflow discussion](https://stackoverflow.com/questions/51820921/vscode-integrated-terminal-doesnt-load-bashrc-or-bash-profile) for more information.

## Speed considerations

To share the same files and code between WSL (Ubuntu) and Windows, you need to
have one environment point to files in another. Often this would mean that the
file system lives in Windows, and the WSL instance accesses the Windows hosted
files through the magic of WSL. 

Crossing the OS boundary will result in poor disk performance for the WSL
instance. Having a WSL instance of VS Code work on its own copy of this repo,
separate from the Windows instance, speeds disk access (eg PIP installs)
dramatically.

If you want speed, have a separate installation of this project in WSL.

## Space considerations

If you choose to run separate copies of the code in WSL and Windows then it means
you are doubling up on the code, libraries, tools, and compiled executables. If
you have limited disk space this can be an issue.

If you are low on space, use a shared installation of this project for WSL and
Windows.

### To free up space

To free up space you can use the clean.bat/clean.sh scripts under
/src/SDK/Utilities.

For Windows
```cmd 
cd src\SDK\Utilities
clean all
```
For Linux/macOS
```cmd 
cd src/SDK/Utilities
bash clean.sh all
```

Run `clean` without a parameter to see the options for fine-tuning what gets
cleaned.

To actually realise the freed up space in WSL you will need to compact the VHD
in which your WSL instance resides.

In a Windows terminal:

```cmd 
wsl --shutdown
diskpart
```

This will shutdown WSL and open a disk partition session in a new window. Locate
the VHD file for WSL by heading to `%LOCALAPPDATA%\Packages` and looking for a 
folder similar to `CanonicalGroupLimited.Ubuntu_79rhkp1fndgsc\LocalState` that
contains a file `ext4.vhd`.

Within the session, enter the following (adjusting the `ext4.vhd` location as needed)

```text
select vdisk file="%LOCALAPPDATA%\Packages\CanonicalGroupLimited.Ubuntu_79rhkp1fndgsc\LocalState\ext4.vhdx"
attach vdisk readonly
compact vdisk
detach vdisk
exit
```

Your WSL virtual hard drive should be smaller and the space that was used 
reclaimed by Windows.