
from cgitb import text
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
