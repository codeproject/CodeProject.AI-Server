import os

def cleanDir(dir: str) -> None:
    # make sure the detected directory exists
    if not os.path.exists(dir):
        os.mkdir(dir)

    # delete all the files in the output directory
    filelist = os.listdir(dir)
    for filename in filelist:
        try:
            filepath = os.path.join(dir, filename)
            os.remove(filepath)
        except:
            pass
