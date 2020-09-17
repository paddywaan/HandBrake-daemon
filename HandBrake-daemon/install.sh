#!/bin/bash
DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" >/dev/null 2>&1 && pwd )"
cd $DIR
cp HandBrake-daemon /usr/local/bin/handbrake-daemon
chmod +x /usr/local/bin/handbrake-daemon
cp appsettings.json /usr/local/bin
cp default.conf /etc/handbrake-daemon.conf
cp handbrake-daemon.service /etc/systemd/system/handbrake-daemon.service
systemctl daemon-reload