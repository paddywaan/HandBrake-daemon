#!/bin/bash
mountdir=/mnt/handbrake
conf=default.conf
settings="$mountdir"/appsettings.json
if [[ ! -f /etc/"$conf" && ! -f /mnt/"$mountdir" ]]; then
        cp /app/default.conf /etc/handbrake-daemon.conf
        ln -s /etc/handbrake-daemon.conf /mnt/handbrake/handbrake-daemon.conf
fi
if [[ ! -f "$settings" ]]; then
        cp /app/appsettings.json "$mountdir"/appsettings.json
        rm /app/appsettings.json
        ln -s "$mountdir"/appsettings.json /app/appsettings.json
fi
dotnet handbrake-daemon.dll