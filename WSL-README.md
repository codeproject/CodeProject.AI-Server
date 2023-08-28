# Notes on developing under WSL

This repository can be run under Windows or WSL at the same time. The setup.bat
and setup.sh scripts will install runtimes and virtual environments into specific
locations for the given OS. Virtual environments, for example, will live under
the /bin/windows folder for a module in Windows, and /bin/linux when under Linux.
This allows the same code and models to be worked on and tested under both 
Operating Systems.

Note that this means you will need VS Code installed in Windows and in the WSL
hosted Ubuntu (should you choose to use Ubuntu in WSL). This further requires
that each instance of VS Code has the necessary extensions installed. The
profile sync mechanism in VS Code makes this seamless.

## Speed considerations

To share the same files and code between WSL (Ubuntu) and Windows, you need to
have one environment point to files in another. Often this would mean that the
file system lives in Windows, and the WSL instance accesses the Windows hosted
files through the magic of WSL. 

Crossing the OS boundary will result in poor disk performance for the WSL
instance. Having a WSL instance of VS Code work on its own copy of this repo,
separate from the Windows instance, speeds disk access (eg PIP installs)
dramatically.

## Space considerations

If you choose to run separate copies of the code in WSL and Windows then it means
you are doubling up on the code, libraries, tools, and compiled executables. If
you have limited disk space this can be an issue.

### To free up space

To free up space you can use the clean.bat/clean.sh scripts under /src/SDK/Scripts.

To actually realise the freed up space in WSL you will need to compact the VHD
in which your WSL instance resides.

In a Windows terminal:

```cmd 
wsl --shutdown
diskpart
```

This will shutdown WSL and open a disk partition session in a new window. Locate
the VHD file for WSL by heading to `%LOCALAPPDATA%\Packages` and looking for a 
folder similar to `CanonicalGroupLimited.Ubuntu_79rhkp1fndgsc` that contains a 
file `ext4.vhd`.

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