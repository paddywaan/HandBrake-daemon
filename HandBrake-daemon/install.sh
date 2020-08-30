#!/bin/bash
cp HandBrake-daemon /usr/local/bin
cp appsettings.json /usr/local/bin
cp default.conf /etc/HandBrake-daemon.conf
cp handbrake-daemon.service /etc/systemd/system/handbrake-daemon.service
systemctl daemon-reload