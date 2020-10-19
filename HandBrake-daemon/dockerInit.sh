#!/bin/bash
mountdir=/mnt/handbrake
settings="$mountdir"/appsettings.json
echo Initializing docker container
if [[ ! -f /etc/handbrake-daemon.conf ]]; then #This is first run.
    echo Initialized watcher
    if [[ ! -f "$mountdir"/handbrake-daemon.conf ]]; then #Init was not called before, therefore we use blank config.
        cp default.conf "$mountdir"/handbrake-daemon.conf
        sed -i -e 's/media/handbrake/g' -e 's/transcoding\///g' "$mountdir"/handbrake-daemon.conf
        mkdir "$mountdir"/sourceDirectory "$mountdir"/completedTranscodes "$mountdir"/originalMedia
    fi
fi
if [[ ! -f "$settings" ]]; then
    echo Initialized appsettings
    cp appsettings.json "$settings"
fi
cp "$mountdir"/handbrake-daemon.conf /etc/handbrake-daemon.conf #Move the hostfile to take effect inside the container.
cp "$settings" appsettings.json #Move the hostfile to take effect inside the container.
echo "Applied users mounted configurations."
#dotnet handbrake-daemon.dll