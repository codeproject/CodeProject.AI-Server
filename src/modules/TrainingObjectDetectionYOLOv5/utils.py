from enum import Enum

# Enums ------------------------------------------------------------------------

# Actions are the actions that can be executed for the long running background
# tasks.
class Actions(Enum):
    Idle             = 0 # The module has restarted and nothing is happening.
    InvalidCommand   = 1 # an invalid Action was requested.
    TrainModel       = 2 # Training a model
    ResumeTrainModel = 3 # Resuming training a model
    CreateDataset    = 4 # Create a dataset

# ActionStates are the states that the background tasks can be in.
class ActionStates(Enum):
    Idle         = 0     # Nothing is happening
    Initializing = 1     # the Action is Initializing
    Running      = 2     # the Action is Running
    Completed    = 3     # the Action successfully completed
    Cancelling   = 4     # a request to cancel the Action was received
    Cancelled    = 5     # the Action was Cancelled
    Failed       = 6     # the Action Failed due to an Error


# A simple progress handler ----------------------------------------------------

class ProgressHandler:
    def __init__(self):
        self.progress_max   = 100
        self.progress_value = 0

    @property
    def max(self):
        return self.progress_max

    @max.setter
    def max(self, max_value:int) -> None:
        self.progress_max = max(1, max_value)

    @property
    def value(self) -> int:
        return self.progress_value

    @value.setter
    def value(self, val: int) -> None:
        self.progress_value = max(0, min(val, self.progress_max))

    @property
    def percent_done(self) -> float:
        return self.progress_value * 100 / self.progress_max # progress_max is always >= 1

class InitializationError(Exception):
    def __init__(self, message):
        super().__init__(message)

