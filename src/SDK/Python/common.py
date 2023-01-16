
from typing import Dict, List, Union

# Define a Json type to allow type hints to be sensible.
# See https://adamj.eu/tech/2021/06/14/python-type-hints-3-somewhat-unexpected-uses-of-typing-any-in-pythons-standard-library/
_PlainJSON = Union[
    None, bool, int, float, str, List["_PlainJSON"], Dict[str, "_PlainJSON"]
]
JSON = Union[_PlainJSON, Dict[str, "JSON"], List["JSON"]]


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

# Test availability of required packages.

# requirements_path = Path(__file__).parent.with_name("requirements.txt")
# print(packageInstallReport(requirements_path))

def packageInstallReport(requirements_path: str = None) -> str:
    """
    Generates a report on the packages that are missing or have version
    conflicts, based on the supplied requirements.txt file
    # Ref: https://stackoverflow.com/a/45474387/
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
            requirement = str(requirement)
            try:
                pkg_resources.require(requirement)
            except pkg_resources.DistributionNotFound:
                report += requirement + " not found\n";
            except pkg_resources.VersionConflict:
                report += requirement + " has a version conflict\n";
    except:
        report = "Unable to open a requirements file"

    return report.strip()