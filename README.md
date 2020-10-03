# HandBrake-daemon

HandBrake-daemon is a cross platform (Windows, Linux) directory watcher service for [HandBrakeCLI](). Multiple watchers can be specified, which will queue and process media.

I am not a professional and this is my first attempt at maintaining a project, writing a service, and writing software for Linux, however I will do my best to resolve [issues]() when they are reported. PR's, suggestions and comments are welcome.

***
### Change log

**v1.0.0** - Release.
* Watch configurations can be added via the config file.
* Supports multiple watches.
* Watches support a `source`, `destination`, and `origin` settings.
* Watches can be individually configured to use different HandBrake encoding profiles via the `profilePath` setting.
* Watches can be configured to place the output media inside of a nested, episode directory structure. i.e. `/DestinationDirectory/TitleName/Season #/output.mp4`
* Watcher service scans the watched directories on start, and continues to watch the locations for new, and removed files.
* Subtitles matching the source name (and/or extending the name with a language, or contained within a subdirectory named `subs`), will be embedded inside the output media.
***
### Prerequisites
[HandBrake-CLI](https://handbrake.fr/downloads2.php) must be added to $PATH (Windows platforms require handbrake to be added under system rather than user level environment variables): [Linux](https://opensource.com/article/17/6/set-path-linux), [Windows 10](https://www.architectryan.com/2018/03/17/add-to-the-path-on-windows-10/).

Afterwards, you must either restart (windows) or `source ~/.*rc`, depending on which shell you use.

***
#### Linux Installation
[Download]() the zip and extract, then run the install.sh after verifying its contents. The install script will move the necessary config files to their appropriate locations.

Please double check that `/etc/systemd/system/handbrake-daemon.service` is to your liking. You probably want to set the `WorkingDirectory`, `User`, and `Group` for the service to match your file system. If you change the WD, make sure to place the appsettings.json inside the new WD.
If you have multiple, different locations under different users, please add them to a group which the service daemon runs under.

After you have configured the systemd unit, please reload the daemon with: `sudo systemctl daemon-reload` and proceed with configuration.

#### Windows Installation
[Download]() and extract the zip to the desired location, then run install.cmd as administrator to register the binary as a system service.
***
### Configuration
Linux platforms store the configuration in `/etc/handbrake-daemon.conf`
Windows platforms store the configuration in the installation/extracted directory.

**At least a single watcher must be defined in order for the service to run.**
**Please make sure that you create all referenced directories and profiles with appropriate permissions before running the service**

The source directory is used as the directory to watch for new media to process, and upon completion the output will be placed inside the destination directory.
If an *origin* directory is specified, the source file will be moved to the *Origin*al Media directory.
If none is specified, the source will be deleted rather than moved.
Watchers will only queue file extensions which are defined in the watch config.
Transcode settings are taken from the profile .json, which can be created when running HandBrake's UI on a desktop platform, for ease of configuration. Alternatively
The *isShow* boolean can be set to true in order to nest the output media inside subdirectories to aid organization of seasonal content.

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


