
from typing import Dict, List, Union

# Define a Json type to allow type hints to be sensible.
# See https://adamj.eu/tech/2021/06/14/python-type-hints-3-somewhat-unexpected-uses-of-typing-any-in-pythons-standard-library/
_PlainJSON = Union[
    None, bool, int, float, str, List["_PlainJSON"], Dict[str, "_PlainJSON"]
]
JSON = Union[_PlainJSON, Dict[str, "JSON"], List["JSON"]]


def shorten(text: str, max_length: int) -> str:
    if len(text) <= max_length:
        return text

    segment_length = int((max_length - 3) / 2)
    return text[0:segment_length] + '...' + text[-segment_length]



# Test availability of required packages.
import pkg_resources

# from pathlib import Path
# _REQUIREMENTS_PATH = Path(__file__).parent.with_name("requirements.txt")


def packageInstallReport(requirements_path: str) -> str:
    """
    Generates a report on the packages that are missing or have version
    conflicts, based on the supplied requirements.txt file
    """

    report: str = ""

    # Ref: https://stackoverflow.com/a/45474387/
    requirements = pkg_resources.parse_requirements(requirements_path.open())
    for requirement in requirements:
        requirement = str(requirement)
        try:
            pkg_resources.require(requirement)
        except pkg_resources.DistributionNotFound:
            report += requirement + " not found\n";
        except pkg_resources.VersionConflict:
            report += requirement + " has a version conflict\n";

    return report.strip()