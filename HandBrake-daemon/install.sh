#!/bin/bash

repository="paddywaan/HandBrake-daemon"
filename="handbrake-daemon.zip"

# ---------------------------------------------------------------

function upgrade () {

	printf "\nThis will stop the daemon using systemctl and replace the daemon binary.\n\n"
	read -p "  Continue upgrade? (y/n)? " choice
	case "$choice" in
	  y|Y ) ;;
	  n|N ) exit ;;
	  * ) echo "Invalid input - abort." && exit;;
	esac
	
	printf "  - Stopping daemon... "
	systemctl stop handbrake-daemon.service
	[ "$?" -ne "0" ] && printf "\n" && exit
	printf "done\n"
	
	printf "  - Replacing binary..."
	cp -v $tmp_dir/HandBrake-daemon /usr/local/bin/handbrake-daemon
	[ "$?" -ne "0" ] && printf "\n" && exit
	
	printf "\nReplacement complete.\n\n"
	
	read -p "  Restart daemon? (y/n)? " choice
	case "$choice" in
	  y|Y ) systemctl start handbrake-daemon.service;;
	  n|N ) ;;
	  * ) echo "Invalid input - abort." && exit;;
	esac
	printf "\n  Daemon status is now:\n"
	systemctl status --no-pager handbrake-daemon.service | head -n3 | tail -n2
	printf "\n"
}

function install () {
	printf "\n - Copying necessary files...\n"
	printf "   - HandBrake binary: "
	cp -v HandBrake-daemon /usr/local/bin/handbrake-daemon
	[ "$?" -ne "0" ] && printf "\n   ERROR: Are you trying to update? Use ./install.sh --update\n" && exit
	
	printf "   - Appsettings.json: "
	cp -v appsettings.json /usr/local/bin
	[ "$?" -ne "0" ] && printf "\n" && exit
	
	printf "   - Default configuration: "
	cp -v default.conf /etc/handbrake-daemon.conf
	[ "$?" -ne "0" ] && printf "\n" && exit
	
	printf "   - Systemd service template: "
	cp -v handbrake-daemon.service /etc/systemd/system/handbrake-daemon.service
	[ "$?" -ne "0" ] && printf "\n" && exit
	
	printf " - Setting executable flag on /usr/local/bin/handbrake-daemon: "
	chmod -v +x /usr/local/bin/handbrake-daemon
	
	printf " - Checking systemd unit: "
	systemctl daemon-reload
	systemctl status --no-pager handbrake-daemon.service | grep Loaded | cut -d: -f2- | sed 's/^[[:space:]]*//' 
	[ "$?" -ne "0" ] && printf "\n" && exit
	
	printf "\n\n!!! YOU'RE NOT DONE YET!\n"
	printf "!!!\n"
	printf "!!! Make sure to read the Configuration section of the README.MD in order to specify watchers and\n"
	printf "!!! create the respective directories. Otherwise the service is going to fail.\n"
	printf "!!!\n"
	printf "!!! Please modify the following files to your liking:\n"
	printf "!!!   Watcher configuration file: sudo vim /etc/handbrake-daemon.conf\n"
	printf "!!!   Systemd unit file: sudo vim /etc/systemd/system/handbrake-daemon.service\n"
	printf "!!!\n"
	printf "!!! After you're done with everything start the daemon using:\n"
	printf "!!!   systemctl start handbrake-daemon.service\n"
	printf "!!!\n"
	printf "!!! If you wish to enable the service on boot:\n"
	printf "!!!   systemctl enable handbrake-daemon.service\n\n"
	printf "   Enjoy!\n\n"
	exit 0
}
echo '
██   ██  █████  ███    ██ ██████  ██████  ██████   █████  ██   ██ ███████     ██████   █████  ███████ ███    ███  ██████  ███    ██ 
██   ██ ██   ██ ████   ██ ██   ██ ██   ██ ██   ██ ██   ██ ██  ██  ██          ██   ██ ██   ██ ██      ████  ████ ██    ██ ████   ██ 
███████ ███████ ██ ██  ██ ██   ██ ██████  ██████  ███████ █████   █████ █████ ██   ██ ███████ █████   ██ ████ ██ ██    ██ ██ ██  ██ 
██   ██ ██   ██ ██  ██ ██ ██   ██ ██   ██ ██   ██ ██   ██ ██  ██  ██          ██   ██ ██   ██ ██      ██  ██  ██ ██    ██ ██  ██ ██ 
██   ██ ██   ██ ██   ████ ██████  ██████  ██   ██ ██   ██ ██   ██ ███████     ██████  ██   ██ ███████ ██      ██  ██████  ██   ████ 
'
printf " - Verifying existence of HandBrakeCLI... "
if ! command -v HandBrakeCLI &> /dev/null
then
	printf "FAILED"
	printf "\n\n   ERROR: HandBrakeCLI either not installed or not found.\n\n"
	exit
else
	printf "Found "
	command -v HandBrakeCLI
fi

printf " - Verifying existence of sed... "
if ! command -v sed &> /dev/null
then
	printf "FAILED"
	printf "\n\n   ERROR: sed either not installed or not found.\n\n"
	exit
else
	printf "Found "
	command -v sed 
fi

printf " - Verifying existance of unzip... "

if ! command -v unzip &> /dev/null
then
	printf "FAILED"
	printf "\n\n   ERROR: sed either not installed or not found.\n\n"
	exit
else
	printf "Found "
	command -v unzip 
fi

printf " - Creating temporary directory for installation... "
tmp_dir=$(mktemp -d -t XXXXXXXXXX)
[ "$?" -ne "0" ] && printf "Error: Unable to create temporary directory\n" && exit
echo $tmp_dir
cd $tmp_dir

printf " - Determining latest release version... Found: "
release=`curl --silent https://api.github.com/repos/$repository/releases/latest | grep -Po '"tag_name": "\K.*?(?=")'`
[ "$?" -ne "0" ] && printf "\n\n  FAILED - Unable to retrieve latest release version\n" && exit
echo $release

printf " - Downloading latest release version... "
curl -L -sS https://github.com/$repository/releases/download/$release/$filename --output $filename
[ "$?" -ne "0" ] && printf "Error: Unable to download latest release version\n" && exit
printf "Total bytes received: "
stat -c "%s" $filename

printf " - Extracting "
unzip  $filename
[ "$?" -ne "0" ] && printf "Error: Unable to extract archive\n" && exit 


printf " - Checking root permissions... "
if [ `whoami` != root ]
then
	printf "FAILED"
	printf "\n\n   ERROR: Please run installer as root or using sudo\n\n"
	exit
else 
	printf "Current user: "
	whoami
fi

printf " - Checking for existing installation... "
daemon="/usr/local/bin/handbrake-daemon"
if not test -f "$daemon"; then
        printf "Not found - fresh install\n"
	install
else
        printf "Found "
        /bin/ls $daemon
	printf "\n   Reinstalling will overwrite existing configuration files!\n"
	printf "   Upgrading will only replace the binary. No data will be lost.\n\n"
	printf "   What would you like to do?\n\n"
	read -p "  (U)pgrade/(I)nstall? " choice
	case "$choice" in
	  u|U ) upgrade;;
	  i|I ) install ;;
	  * ) echo "Invalid input - abort." && exit;;
	esac
fi
exit
