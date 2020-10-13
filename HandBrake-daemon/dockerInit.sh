#!/bin/bash
mountdir=/mnt/handbrake
settings="$mountdir"/appsettings.json
echo Initializing docker container
if [[ ! -f /etc/"$conf" ]]; then #This is first run.
    echo Initialized watcher
    if [[ ! -f "$mountdir"/handbrake-daemon.conf ]]; then #Init was not called before, therefore we use blank config.
        cp default.conf "$mountdir"/handbrake-daemon.conf
    fi
fi
cp "$mountdir"/handbrake-daemon.conf /etc/handbrake-daemon.conf #Move the hostfile to take effect inside the container.
if [[ ! -f "$settings" ]]; then
    echo Initialized appsettings
    cp appsettings.json "$settings"
fi
cp "$settings" appsettings.json #Move the hostfile to take effect inside the container.
dotnet handbrake-daemon.dll