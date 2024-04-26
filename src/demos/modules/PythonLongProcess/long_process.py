import time

cancelled = False

def a_long_process(callback):

    result    = ""
    step      = 0
    cancelled = False

    for i in range(1, 11):
       if cancelled: break

       time.sleep(1)
       step   = 1 if not step else step + 1
       result = str(step) if not result else f"{result} {step}"
       callback(result, step)


def cancel_process():
    global cancelled
    cancelled = True