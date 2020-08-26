# HandBrake-daemon

HandBrake-daemon is a cross platform(Win, Linux) directory watcher service for HandBrakeCLI. Multiple watchers can be specified, which will queue and process media in alphabetical/date added ordering.
Subtitles are automatically embedded into the output container, and nested directories can be automatically created for shows/episodic content.

I am not a profressional and this is my first attempt at maintaining a project, writing a service, and writing software for Linux, however I will do my best to resolve issues when they are reported. PR's, suggestions and comments are welcome.

### Installation


### Configuration
Linux platforms store the configuration in /etc/HandBrake-daemon.conf
Windows platforms store the configuration in the installation directory.

At least a single watcher must be defined in order for the service to run.

A template is provided by default:

    ;Section titles are for the users benefit, and have no impact on the daemons behaviour.
    ;Origin, Extentions, and isShow are non mandatory parameters, which will use defaults if not specified.
    ;The file used for the profile must use the same name as the handbrake profile. For example, if the handbrake profile is called "H265-24RF-Fast", the filename must be "H265-24RF-Fast.json"
    ;The isShow parameter is used to nest subdirectories inside the destination directory, naming them in accordance with the stucture of a show.

    ;[WatcherTemplate]
    ;source=/mnt/media/transcoding/sourceDirectory
    ;destination=/mnt/media/transcoding/completedTranscodes
    ;origin=/mnt/media/transcoding/originalMedia
    ;profilePath=/mnt/media/myProfile.json
    ;extentions=mp4,avi,mkv
    ;isShow=false

The source directory is used as the directory to watch for new media to process, and will be placed inside the destination directory upon completion. If an origin directory is specified, the source file will be moved to the Original Medial directory. If none is specified, the source will be deleted rather than moved.
