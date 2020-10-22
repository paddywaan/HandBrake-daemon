# HandBrake-daemon


HandBrake-daemon is a cross platform (Windows, Linux) directory watcher service for [HandBrakeCLI](https://handbrake.fr/downloads2.php). Multiple watchers can be specified, which will queue and process media.

I am not a professional and this is my first attempt at maintaining a project, writing a service, and writing software for Linux, however I will do my best to resolve [issues](https://github.com/paddywaan/HandBrake-daemon/issues) when they are reported. PR's, suggestions and comments are welcome.

***
### Change log

**v1.1.0**
* Added docker support and build process.
* Added ability to specify default profiles(by name) in the profilePath configuration.
* Added configurations to define pre/post-scripts to execute for each transcoded item.

**v1.0.0** - Release.
* Watch configurations can be added via the config file.
* Supports multiple watches.
* Watches support a `source`, `destination`, and `origin` settings.
* Watches can be individually customised to use different HandBrake encoding profiles via the `profilePath` setting.
* Watches can be configured to place the output media inside of a nested, episode directory structure. i.e. `/DestinationDirectory/TitleName/Season #/output.mp4`
* Watcher service scans the watched directories on start, and continues to watch the locations for new, and removed files.
* Subtitles matching the source name (and/or extending the name with a language, or contained within a subdirectory named `subs`), will be embedded inside the output media.
***
### Prerequisites
[HandBrake-CLI](https://handbrake.fr/downloads2.php) must be added to $PATH (Windows platforms require handbrake to be added under system rather than user level environment variables): [Linux](https://opensource.com/article/17/6/set-path-linux), [Windows 10](https://www.architectryan.com/2018/03/17/add-to-the-path-on-windows-10/).

Afterwards, you must either restart (windows) or `source ~/.*rc`, depending on which shell you use.

***
### Linux Installation & Upgrade
##### Automatic method

  * Verify the contents of [install.sh](https://raw.githubusercontent.com/paddywaan/HandBrake-daemon/master/HandBrake-daemon/install.sh)
  * Run the follwing command in a terminal emulator of your liking:
```
sudo bash -c "$(curl -s -L https://raw.githubusercontent.com/paddywaan/HandBrake-daemon/master/HandBrake-daemon/install.sh)"
```
##### Manual method
[Download](https://github.com/paddywaan/HandBrake-daemon/releases/latest) the zip and extract, then run the install.sh after verifying its contents. The install script will move the necessary config files to their appropriate locations.


##### Linux Automatic Removal / Uninstall

  * Verify the contents of [uninstall.sh](https://raw.githubusercontent.com/paddywaan/HandBrake-daemon/master/HandBrake-daemon/uninstall.sh)
  * Run the follwing command in a terminal emulator of your liking:
```
sudo bash -c "$(curl -s -L https://raw.githubusercontent.com/paddywaan/HandBrake-daemon/master/HandBrake-daemon/uninstall.sh)"
```

### Windows Installation
[Download](https://github.com/paddywaan/HandBrake-daemon/releases/latest) and extract the zip to the desired location, then run install.cmd as administrator to register the binary as a system service. You may now manipulate the service via `services.msc`


### Docker Installation

The docker image hes been setup to automatically create some directories and apply configurations, so you may leave the guest mountpoint `/mnt/handbrake` untouched if you do not have specific mounting requirements. Set the host location to be mounted and ensure the host directory exists, then run the following:
```
docker pull paddywaan/handbrakedaemon
sudo docker run -d -it --name handbrakedaemon -v /Host/Mount/Location:/mnt/handbrake paddywaan/handbrakedaemon handbrake-daemon
```

Nagivate to `/Host/Mount/Location` and two files, `appsettings.json` and `handbrake-daemon.conf` will have been created, alongside 3 directories. The config has been setup to automatically provide a working configuration with preset directory structure.


Restart the docker container using `sudo docker restart handbrakedaemon`

**CAUTION:** Moving files from an NTFS host will not trigger INotify on the guest system. The guest system must have the files moved or created within the context of the guest, or an ext4FS in order that INotify is triggered (This is what does the legwork for active directory watching).

---

### Configuration
Linux platforms store the configuration in `/etc/handbrake-daemon.conf`
Windows platforms store the configuration in the installation/extracted directory.

**At least a single watcher must be defined in order for the service to run.**
**Please make sure that you create all referenced directories and profiles with appropriate permissions before running the service**

* Section titles are for the users benefit, and have no impact on the daemons behavior, other than specifying a new watch directory/config.
* The **source directory** is used as the directory to watch for new media to process, and upon completion the output will be placed inside the **destination directory**.
* If an **origin** directory is specified, the source file will be moved to the Original Media directory. If none is specified, the source will be deleted rather than moved.
* Watchers will only queue file **extensions** which are defined in the watch config. If none are specified, they default to `mp4,mkv,avi`
* Transcode settings are taken from the **profilePath**, which references a .json file containing a custom preset which may be exported from HandBrake's desktop UI, or if the built-in profiles are satisfactory, you may use `HandBrakeCLI -z` to list all available profile titles. Simply replace the profile path with the title, inclusive of spaces, no escape sequences are required.
* The **isShow** boolean can be set to true in order to nest the output media inside subdirectories to aid organization of seasonal content.
* The preScript path can be set to run a script prior to the transcode process. The arguments: `SourcePath DestinationPath OriginPath and (Boolean)isShow` are passed to the invoked script. The transcode process for this item will be skipped the script was not successful.
* The postScript path can be set to run a script after the transcoding process. The arguments: `DestinationPath OriginPath and (Boolean)isShow` are passed to the invoked script. The script will not run if the transcode was not successful.

Finally, you may start the service for the first time:

Windows: `sc start HandBrake-daemon`

Linux: `sudo systemctl start handbrake-daemon.service`

When you are happy with the configuration, you can enable the daemon on boot via: `sudo systemctl enable handbrake-daemon.service`
***
### Logging

**Linux:** Logging verbosity can be changed via setting the *default* level in appsettings.json: `Debug, Information, Warning, Error, Critical`.
By default, the level is set to Information, and the *.json* is stored alongside the binary in `/usr/local/bin/` unless an alternative *WorkingDirectory* is specified inside the `.service` unit.
The logs are stored at `/var/log/handbrake-daemon.log` by default, and an updated progress% on the current encode can be seen via: `tail -f /var/log/handbrake-daemon.log`.

**Windows:** Logging verbosity can be changed via setting the default level for the `EventLog` section in appsettings.json: `Debug, Information, Warning, Error, Critical`. By default the level is set to Information, and the logs are stored inside EventViewer: *Application*, under the source: *HandBrake-daemon*


