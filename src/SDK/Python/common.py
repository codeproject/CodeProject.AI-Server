
from typing import Dict, List, Union
import os

# Define a JSON type to allow type hints to be sensible.
# See https://adamj.eu/tech/2021/06/14/python-type-hints-3-somewhat-unexpected-uses-of-typing-any-in-pythons-standard-library/
_PlainJSON = Union[
    None, bool, int, float, str, List["_PlainJSON"], Dict[str, "_PlainJSON"]
]
JSON = Union[_PlainJSON, Dict[str, "JSON"], List["JSON"]]

def timedelta_format(td_object):
    """Formats a time delta value in human readable format"""

    seconds = int(td_object.total_seconds())
    periods = [
        # label, #seconds,     spacer, pluralise, 0-pad  Always show
        ('yr',   60*60*24*365, ', ',   True,      False, False),   # years
        ('mth',  60*60*24*30,  ', ',   True,      False, False),   # months
        ('d',    60*60*24,     ' ',    False,     False, False),   # days
        ('',     60*60,        ':',    False,     False, True),    # hours
        ('',     60,           ':',    False,     True,  True),    # minutes
        ('',     1,            '',     False,     True,  True)     # seconds
    ]

    result=''
    for label, period_seconds, spacer, pluralise, zero_pad, show_always in periods:
        if show_always or seconds > period_seconds:
            period_value, seconds = divmod(seconds, period_seconds)
            if pluralise and period_value != 0:
                label += 's'
            if zero_pad:
                result += f"{period_value:02}{label}{spacer}"
            else:
                result += f"{period_value}{label}{spacer}"

    return result

def get_folder_size(folder):
    # eg. print "Size: " + str(getFolderSize("."))
    total_size = os.path.getsize(folder)
    for item in os.listdir(folder):
        itempath = os.path.join(folder, item)
        if os.path.isfile(itempath):
            total_size += os.path.getsize(itempath)
        elif os.path.isdir(itempath):
            total_size += get_folder_size(itempath)
    return total_size


def shorten(text: str, max_length: int) -> str:
    """
    Shorten a line of text by excising a section from the middle
    """
    if len(text) <= max_length:
        return text

    segment_length = int((max_length - 3) / 2)
    return text[0:segment_length] + '...' + text[-segment_length]

def dump_tensors():
    """
    Use the garbage collector to list the currently resident tensors
    """
    import gc
    import torch
    for obj in gc.get_objects():
        try:
            if torch.is_tensor(obj) or (hasattr(obj, 'data') and torch.is_tensor(obj.data)):
                print(type(obj), obj.size())
        except:
            pass


# NOTE: This is now DEPRECATED. Please use `check_requirements` in utils/environment_check.py instead
def check_installed_packages(requirements_path: str = None, report_version_conflicts = True) -> str:
    """
    Generates a report on the packages that are missing or have version
    conflicts, based on the supplied requirements.txt file
    Ref: https://stackoverflow.com/a/45474387/

    example:
        requirements_path = Path(__file__).parent.with_name("requirements.txt")
        print(packageInstallReport(requirements_path))
    """

    import pkg_resources

    report: str = ""

    if not requirements_path:
        requirements_path = os.path.join(os.path.dirname(__file__), "requirements.txt")

    try:
        requirements = open(requirements_path, "r").readlines()

        # or something like...
        # from pathlib import Path
        # requirements_path = Path(__file__).with_name("requirements.txt")
        # requirements = pkg_resources.parse_requirements(requirements_path.open())

        for requirement in requirements:
            requirement = str(requirement).strip()

            if requirement.startswith('-'):
                continue
            
            index = requirement.find('#')
            if index != -1:
                requirement = requirement[:index].strip()

            try:
                pkg_resources.require(requirement)
            except pkg_resources.DistributionNotFound:
                report += requirement + " not found\n"
            except pkg_resources.VersionConflict:
                if report_version_conflicts:
                    report += requirement + " has a version conflict\n"
            except Exception as ex:
                report += requirement + f" threw an exception: {str(ex)}\n"
    except:
        report = "Unable to open a requirements file"

    if report:
        report = "ERROR: " + report
    else:
        report = "SUCCESS: All packages in requirements file are present"

    return report.strip()