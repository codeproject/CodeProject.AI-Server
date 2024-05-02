# environment_check.py 
#
# Part of https://github.com/trtb/python_toolbox/
#
#      License: MIT
#      Author:  Alexandre Novius
#
####################################################################

import sys
import os
import argparse
from typing import List

from pip._internal.req.req_file import parse_requirements
from pip._internal.req.constructors import install_req_from_parsed_requirement
from pip._internal.network.session import PipSession


def check_is_file(filename: str) -> str:
    """ assert file path exists """
    if not os.path.isfile(filename):
        raise argparse.ArgumentTypeError("The file {} does not exist".format(filename))
    return filename


def check_requirements(requirement_file: str, debug: bool = False) -> bool:
    """
    Test whether all packages specified in requirements.txt are present in the current environment.

    :param requirement_file: requirement file path
    :param debug: if true print debug information
    :return: true if all library are present in current environment, else false
    """
    session = PipSession()
    requirement_path = os.path.abspath(requirement_file)
    requirements = parse_requirements(requirement_path, session)
    all_good = True
    for requirement in requirements:
        req_to_install = install_req_from_parsed_requirement(requirement)
        req_to_install.check_if_exists(use_user_site=False)

        if req_to_install.satisfied_by:
            res = "satisfied with {}".format(req_to_install)
        else:
            res = "not satisfied!"
            all_good = False
        if debug:
            print("Requirement: {:30s} {}".format(requirement.requirement, res))
    if debug:
        if not all_good:
            print("Some libraries are missing, you can try to run: pip install -U -r {}".format(requirement_file))
        else:
            print("All requirements are satisfied.")
    return all_good


def get_arguments(args: List) -> argparse.Namespace:
    description = "Test whether all packages specified in the requirements.txt file are present in the current " \
                  "environment."
    parser = argparse.ArgumentParser(description=description)

    parser.add_argument('requirement_file', type=lambda x: check_is_file(x),
                        help="path to the requirement file")

    parser.add_argument('-d', '--debug', action='store_true',
                        help="debug mode")

    opt = parser.parse_args(args)

    return opt


def main():
    opt = get_arguments(sys.argv[1:])
    res = run(requirement_file=opt.requirement_file, debug=opt.debug)
    sys.exit(1 - int(res))


if __name__ == "__main__":
    main()
