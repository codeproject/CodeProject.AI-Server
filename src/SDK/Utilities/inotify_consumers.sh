#!/bin/bash

# Get the procs sorted by the number of inotify watches
# @author Carl-Erik Kopseng
# @latest https://github.com/fatso83/dotfiles/blob/master/utils/scripts/inotify-consumers
# Discussion leading up to answer: https://unix.stackexchange.com/questions/15509/whos-consuming-my-inotify-resources
#
#
########################################## Notice ##########################################
###        Since Fall 2022 you should prefer using the following C++ version             ###
###                    https://github.com/mikesart/inotify-info                          ###
############################################################################################
#
#
# The fastest version of this script is here: https://github.com/fatso83/dotfiles/commit/inotify-consumers-v1-fastest
# Later PRs introduced significant slowdowns at the cost of better output, but it is insignificant on most machines
# See this for details: https://github.com/fatso83/dotfiles/pull/10#issuecomment-1122374716

main(){
    printf "\n%${WLEN}s  %${WLEN}s\n" "INOTIFY" "INSTANCES"
    printf "%${WLEN}s  %${WLEN}s\n" "WATCHES" "PER   "
    printf "%${WLEN}s  %${WLEN}s  %s\n" " COUNT " "PROCESS "    "PID USER         COMMAND"
    printf -- "------------------------------------------------------------\n"
    generateData
}

usage(){
    cat << EOF
Usage: $0 [--help|--limits]
    -l, --limits    Will print the current related limits and how to change them
    -h, --help      Show this help
EOF
}

limits(){
    printf "\nCurrent limits\n-------------\n"
    sysctl fs.inotify.max_user_instances fs.inotify.max_user_watches

    cat <<- EOF
Changing settings permanently
-----------------------------
echo fs.inotify.max_user_watches=524288 | sudo tee -a /etc/sysctl.conf
sudo sysctl -p # re-read config
EOF
}

generateData() {
    local -i PROC
    local -i PID
    local -i CNT
    local -i INSTANCES
    local -i TOT
    local -i TOTINSTANCES
    # read process list into cache
    local PSLIST="$(ps ax -o pid,user=WIDE-COLUMN,command $COLSTRING)"
    local INOTIFY="$(find /proc/[0-9]*/fdinfo -type f 2>/dev/null | xargs grep ^inotify 2>/dev/null)"
    local INOTIFYCNT="$(echo "$INOTIFY" | cut -d "/" -s --output-delimiter=" "  -f 3 |uniq -c | sed -e 's/:.*//')"
    # unique instances per process is denoted by number of inotify FDs
    local INOTIFYINSTANCES="$(echo "$INOTIFY" | cut -d "/" -s --output-delimiter=" "   -f 3,5 | sed -e 's/:.*//'| uniq |awk '{print $1}' |uniq -c)"
    local INOTIFYUSERINSTANCES="$(echo "$INOTIFY" | cut -d "/" -s --output-delimiter=" "   -f 3,5 | sed -e 's/:.*//' | uniq |
    	     while read PID FD; do echo $PID $FD $(grep -e "^ *${PID} " <<< "$PSLIST"|awk '{print $2}'); done | cut -d" "  -f 3 | sort | uniq -c |sort -nr)"
    set -e

    cat <<< "$INOTIFYCNT" |
        {
            while read -rs CNT PROC; do   # count watches of processes found
                echo "${PROC},${CNT},$(echo "$INOTIFYINSTANCES" | grep " ${PROC}$" |awk '{print $1}')"
            done
        } |
        grep -v ",0," |                  # remove entires without watches
        sort -n -t "," -k 2,3 -r |         # sort to begin with highest numbers
        {                                # group commands so that $TOT is visible in the printf
	    IFS=","
            while read -rs PID CNT INSTANCES; do   # show watches and corresponding process info
                printf "%$(( WLEN - 2 ))d  %$(( WLEN - 2 ))d     %s\n" "$CNT" "$INSTANCES" "$(grep -e "^ *${PID} " <<< "$PSLIST")"
                TOT=$(( TOT + CNT ))
		TOTINSTANCES=$(( TOTINSTANCES + INSTANCES))
            done
	    # These stats should be per-user as well, since inotify limits are per-user..
            printf "\n%$(( WLEN - 2 ))d  %s\n" "$TOT" "WATCHES TOTAL COUNT"
# the total across different users is somewhat meaningless, not printing for now.
#            printf "\n%$(( WLEN - 2 ))d  %s\n" "$TOTINSTANCES" "TOTAL INSTANCES COUNT"
        }
    echo ""
    echo "INotify instances per user (e.g. limits specified by fs.inotify.max_user_instances): "
    echo ""
    (
      echo "INSTANCES    USER"
      echo "-----------  ------------------"
      echo "$INOTIFYUSERINSTANCES"
    ) | column -t
    echo ""
    exit 0
}

# get terminal width
declare -i COLS=$(tput cols 2>/dev/null || echo 80)
declare -i WLEN=10
declare COLSTRING="--columns $(( COLS - WLEN ))" # get terminal width

if [ "$1" = "--limits" -o "$1" = "-l" ]; then
    limits
    # exit 0
elif [ "$1" = "--help" -o "$1" = "-h" ]; then
    usage
    exit 0
elif [ "$1" = "--clipped" -o "$1" = "-f" ]; then
    main
    exit 0
elif [ -n "$1" ]; then
    printf "\nUnknown parameter '$1'\n" >&2
    usage
    exit 1
fi

unset COLSTRING
main
