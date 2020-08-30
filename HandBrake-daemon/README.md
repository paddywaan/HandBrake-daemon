# HandBrake-daemon

HandBrake-daemon is a cross platform(Windows, Linux) directory watcher service for HandBrakeCLI. Multiple watchers can be specified, which will queue and process media in alphanumerical/date added ordering.
Subtitles are automatically embedded into the output container, and nested directories can be automatically created for shows/episodic content.

I am not a profressional and this is my first attempt at maintaining a project, writing a service, and writing software for Linux, however I will do my best to resolve issues when they are reported. PR's, suggestions and comments are welcome.


### Prerequisites
[HandBrake-CLI](https://handbrake.fr/downloads2.php) must be added to $PATH: [Linux](https://opensource.com/article/17/6/set-path-linux), [Windows 10](https://www.architectryan.com/2018/03/17/add-to-the-path-on-windows-10/).

Afterwards, you must either restart (windows) or `source ~/.*rc`, depending on which shell you use.

***
#### Linux Installation
[Download]() the zip and extract, then run the install.sh after verifying its contents. The install script will move the necessary config files to their appropriate locations.

Please double check that /etc/systemd/system/handbrake-daemon.service is to your liking. You probably want to set the `WorkingDirectory`, `User`, and `Group` for the service to match your filesystem.
If you have multiple, different locations under different users, please add them to a group which the service daemon runs under.

After you have configured the systemd unit, please reload the daemon with: `sudo systemctl daemon-reload` and proceed with configuration.

#### Windows Installation
[Download]() and extract the zip to the desired location, then open cmd as Adminstrator:

    sc.exe create HandBrake-daemon binPath="PATHTOEXECUTABLE"

### Configuration
Linux platforms store the configuration in /etc/HandBrake-daemon.conf
Windows platforms store the configuration in the installation/extracted directory.

At least a single watcher must be defined in order for the service to run.

The source directory is used as the directory to watch for new media to process, and will be placed inside the destination directory upon completion.
If an origin directory is specified, the source file will be moved to the Original Medial directory.
If none is specified, the source will be deleted rather than moved.

Finally, you may start the service for the first time:

Windows: `sc start HandBrake-daemon`

Linux: `sudo systemctl start handbrake-daemon.service`

When you are happy with the configuration, you can enable the daemon on boot via: `sudo systemctl enable handbrake-daemon.service`