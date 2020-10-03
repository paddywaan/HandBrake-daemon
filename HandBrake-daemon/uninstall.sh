#!/bin/bash
printf "\n - Checking root permissions... "
if [ `whoami` != root ]
then
        printf "FAILED"
        printf "\n\n   ERROR: Please run as root or using sudo\n\n"
        exit
else
        printf "Current user: "
        whoami
fi
printf " - Checking for existing installation... "
daemon="/usr/local/bin/handbrake-daemon"
if not test -f "$daemon"; then
	printf "FAILED\n\n"	
	read -p "  Daemon does not seem to installed, continue anyway? (y/n) " choice
	case "$choice" in
	  y|Y ) printf "\n\n";;
	  n|N ) exit ;;
	  * ) echo "Invalid input - abort." && exit;;
	esac
else
	printf "Found "
	/bin/ls $daemon
	printf "\n\n"
fi


printf "This is going to disable the Daemon and PERMANENTLY REMOVE the following files from your system:\n"
printf "  /usr/local/bin/handbrake-daemon\n"
printf "  /usr/local/bin/appsettings.json\n"
printf "  /etc/handbrake-daemon.conf\n"
printf "  /etc/systemd/system/handbrake-daemon.service\n\n"

printf "The following files / folders will NOT be automatically removed, manual cleanup required:\n"
printf "  " && cat /etc/handbrake-daemon.conf | grep 'source=' | cut -d= -f2
printf "  " && cat /etc/handbrake-daemon.conf | grep 'destination=' | cut -d= -f2
printf "  " && cat /etc/handbrake-daemon.conf | grep 'origin=' | cut -d= -f2
printf "  " && cat /etc/handbrake-daemon.conf | grep 'profilePath=' | cut -d= -f2
printf "\nHandBrakeCLI is also NOT going to be removed. Please use your respective package manager accordingly.\n\n"


read -p "  Continue removal? (y/n)? " choice
case "$choice" in 
  y|Y ) ;;
  n|N ) exit ;;
  * ) echo "Invalid input - abort." && exit;;
esac

printf "\nStopping handbrake-daemon..."
printf "\nDisabling handbrake-Daemon...\n"
systemctl stop handbrake-daemon.service
systemctl disable handbrake-daemon.service
printf "Deleting files...\n"
rm -v /usr/local/bin/handbrake-daemon
rm -v /usr/local/bin/appsettings.json
rm -v /etc/handbrake-daemon.conf
rm -v /etc/systemd/system/handbrake-daemon.service

printf "\n  Cleanup complete\n\n"

printf "Main files have been cleaned up. Please proceed cleaning up the watchers files / directories manually.\n\n"
printf "Have a nice day.\n\n"

