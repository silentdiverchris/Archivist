# Archivist
A simple C# Net Core archiving utility that can be made as complicated as you want it to be.

Essentially I got frustrated with built-in and otherwise existing backup systems for Windows and wanted something to do regular backups and archive my media the way I want, which is essentially zipping the contents of numerous directories into individual zip files in a local archive directory, keeping one or more generations of them there, then copying some or all of them from there to numerous other places depending on various criteria and whether those volumes are mounted, keeping one or more generations on each of those.

There is no UI other than the console output, it's driven from a json configuration file and reports to the console and optionally to a text log file and/or a SQL table.

This was written to do exactly what I want from a backup/archiving system, it's not intended to be a panacea for everyone but it pretty much covers what one might want from such a thing in as much as it compresses, encrypts, copies files around and retains however many versions of the files you want.

It uses the LastWriteTime of the zip files it creates to decide whether a new archive should be taken, so won't constantly be zipping up identical sets of files.

The text below is fairly detailed but isn't as full as it could be, please feel free to get in touch or raise an issue to ask for more details, point out mistakes or report bugs, I'll update the below with any corrections, clarifications or expansions.

# Licence
Feel free to do whatever you like with the code.

# Caveats
Code and executables provided as-is. This system runs on my machines several times a day to process my own precious files and is written with caution very much in mind by somebody who is paranoid about these things.

This is created with Net Core 6.0.0 preview 6 and Visual Studio 2022 beta, I'll gradually move it along as newer versions are released. It's my intention that it remains on this 'bleeding edge' if it can be called that, but with it essentially just zipping and copying files I don't see that would make it in any way dangerous to use.

## Does it alter or delete any of my files ?

It doesn't write to or delete any of the source files it processes, it purely reads them to zip them up; with one optional exception.

If you enable the secure directory function it will delete unencrypted files in the set of secure directories, if any are defined, but only when it's specifically told to, after having checked an encrypted version exists, or that a new encryption reported success and that the newly encrypted version of the file exists.

# The archiving process

There are three main parts to the process, done in the order listed below.

## Securing directories

You can nominate a list of 'secure directories' that the system will automatically encrypt files found in, each to its own individual '.aes' file, and optionally remove the unencrypted version.

The reason for this process being that I like to keep credentials, account details etc. in little text files, screen shots etc. in various directories, decrypting them manually to view and update them, and either immediately (re-)encrypt manually or more likely, leave them for Archivist to do the next time it runs. My machine runs Archivist several times a day for different jobs, so they don't stay that way for long.

It will take each file name and append '.aes' to it to determine the encrypted file name, so 'SecretPassword.txt' will be encrypted into 'SecretPassword.txt.aes'.

It will ignore any file called 'clue.txt' in upper, lower or mixed case, I use a file of that name to store a cryptic reminder of the password I use for files in that directory.

When files are encrypted it sets the last write time to that of the source file, and uses the last write times to determine which file is most recent.

When Archivist runs it will encrypt any files that are not of the fom '\*.aes' if the unencrypted version has a later write time. It will optionally delete the unencrypted version if an encrypted version exists once it is sure the encryption happened successfully, dependign on configuration DeleteArchiveAfterEncryption.

If it finds an unencrypted file with a write time the same as, or earlier than the encrypted version it will assume the file was manually unencrypted to view it, and just delete the file, retaining the encrypted one. To restate the previous paragraph, if the unencrypted file has a later last write time it will re-encrypt it, overwriting the previous encryption.

The is done in the first step, so the files zipped up in the next step are safe from prying eyes and the files at rest on your local folder are secured again.

To nominate secure directories, add them to the GlobalSecureDirectories or SecureDirectories list in the configuration file, see below for details.

## Archiving source directories

The second part of the process is to take the list of directories in the GlobalSourceDirectories and SourceDirectories configuration and recursively zip each into a single output file in a nominated directory, known as the primary archive directory,  specified in the configuration as PrimaryArchiveDirectoryName.

This uses the 'ZipFile.CreateFromDirectory' interface in Microsoft's Sytem.IO.Compression library, see https://docs.microsoft.com/en-us/dotnet/api/system.io.compression?view=net-5.0 for details.

The resulting zip files can then optionally be encrypted, creating a file with a '.aes' extension, so 'ArchivedFile.zip' is encrypted to 'ArchieFile.zip.aes'. Set EncryptOutput on the source directory to enable this. See the AESCrypt section below to set up encryption.

## Copying archives

The final part takes the lists of directories in GlobalArchiveDirectories and ArchiveDirectories defined in the configuration file and copies files from the primary archive directory to those directories depending on all the filters, inclusions and exclusions specified in the configuration ArchiveDirectories settngs.

# Global and non-global directories

When preparing to run a job, the system combines the global directory lists with the list defined for the job, and runs all of them. Typically I find I just have all mine in the global lists to be run with all jobs but there is the flexibility to have different sets for different jobs.

# Selection and filtering

Directories and files can be selected, included and excluded in various ways according to their names, the job that is running, the current time of day, whether they change frequently or not, are on slow volumes and more. See the configuration file section for full details, some main topics along these lines are mentioned below and detailed later.

## File inclusions and exclusions

See IncludeSpecifications and ExcludeSpecifications, below.

## Slow volumes

I mainly archive to external SSDs and HDDs but also to a set of MicroSD cards, which are pretty slow but cheap for the capacity, fairly indestructable and handy to carry around and store in quantity. The system can be set up to only write to these slow volumes on an overnight run, or at the end of the week, say.

See IsSlowVolume and ProcessSlowVolumes, below.

# File Versions

At two points in the process, namely when a zip archive is created and after copying it to an archive directory, the system can delete older generations of each file and keep a specific number of them. To do this, set the AddVersionSuffix and RetainVersions settings on the directory in question.

If an archive would be to file 'FileName.zip', setting AddVersionSuffix would name the first one as 'FileName-0001.zip', the next as 'FileName-0002.zip' etc. Then setting RetainVersions to 3, for example, would leave those as-is when 'FileName-0003.zip' was created, then when 'FileName-0004.zip' was created would delete 'FileName-0001.zip', retaining the last 3.

When it gets to 9999 it will fail to create the next file, hopefully I'll have fixed that by then.

Using the RetainVersions setting you can tell it to keep the last 2, 5 or however many versions you like. It will delete the ones with the lowest numbers, which would usually be the oldest if you haven't touched the files since but the file timestamps aren't a factor in the decision, it judges purely by the digits in the file name.

If it finds any file name that isn't of the form \[base file name]\[hyphen\]\[4 digits\]\[dot]\[extension] it won't touch it.

If you set the number of versions in a source directory higher than the retain number in an archive directory the system will end up copying files from source to archive and then immediately deleting them, it'd be nice to catch this and not do it but it doesn't at present.

If this is the case, the system will warn you in the console/log but will carry on and do it anyway.

If AddVersionSuffix for a directory is false, files will not be versioned and there will only ever be one version of each archive in that directory.

This versioning can start to eat up disk space of course, the system will report the space free on drives it uses to the console/log, and generate a warning if it is below 50Gb, currently.

# Performance

It could be faster, the 7-Zip library seems to be faster than the .Net compression, and a previous version of the code used RoboCopy which did the copying more quickly especially with muti-threaded copies. 

I might update it to use RoboCopy again but it's not really a priority, it wasn't all that much faster, I don't sit waiting for it to finish anyway and having complete control over what gets copied is nicer than tweaking parameters to RoboCopy.

# Full disk

If the destination disk fills up while creatign a zip or copying a file it will fail that operation and report an error but continue, so an archive of your music library might fail but the archives of smaller sets of files defined later in the job will still be attempted.

# Jobs

You can define any number of different jobs which can select different sets of directories and files. In this way you can set up daily, weekly and monthly backups, or a job to backup one specific directory every hour, or only fast volumes every 10 minutes.

# Removable volumes

My backup system involves having several external drives which I mount for various reasons, eg. one for daily backups which is almost always connected, one which I plug in just at the start of each week, one for the start of each month, and a pair of two identical large SSDs which I generally leave one of attached but alternate between them. So sometimes S:\ or Y:\ might exist, sometimes it won't.

If you mark a directory as removable with IsRemovable, the system will try to use it but if it's not there it won't be considered as an error. Any drive that is not found which is not marked as removable will be reported as an error.

This means you don't need to be too bothered about exactly which drives you plug in, it'll just archive to what it finds, but it can distinguish betweeen a drive not having been plugged in and one that should be there but isn't.

# Scheduling

There is no built in scheduler, it works just fine with Windows Scheduler and any other decent cron system that can call an executable and ideally specify a parameter. 

Just specify the the job name in the app settings file as 'RunJobName' or, better, as the first parameter to the executable.

# Logging

## Console

It will write most or all log messages to the console, with warnings in yellow, errors in red and success/completed messages in green.

Setting DebugConsole to true in the app settings file makes it write everything to the console. 

Without DebugConsole the console will get a more readable subset of just the important messages and will always let you know what it's doing right now, so it's generally best to leave this turned off for readability and dig into the text/SQL log if you need more detail.

Full logging always goes to the file and SQL logs, which includes a lot of verbose stuff about the decisions it made according to settings, file timestamps etc.

## Text file

You can nominate a directory with the LogDirectory item in app settings, this tells it where to create text log files. Log file names are in the form;

Archiver-\[JobName]-YYYYMMDDHHMMSS.log

If the log directory is not supplied, no text logging will happen. If it is supplied but doesn't exist, an error will be reported.

## SQL

I like my logs in a SQL table for easy filtering and the like. If you give it a valid SQL connection string (DefaultConnection in ConnectionStrings in app settngs) it will log to a SQL table, by default called 'Log'. 

If they don't exist, it will automatically create a 'Log' table and an 'AddToLog' stored procedure in the nominated database and write log messages to it using the stored procedure. 

If a connection string is not supplied, no SQL logging will happen, if it is supplied but the system canot connect to it, an error will be reported.

You can alter the stored procedure and log table to suit, so as to change the formatting or use another table entirely as long as you don't remove any of the original parameters of the AddLogEntry stored procedure. 

The system knows nothing about the SQL table, all it relies on is calling the AddLogEntry with three parameters, what happens internally is entirely customisable.

If you mess things up, delete them and the standard stored procedure and table will be recreated on the next run.

## Windows event log

Errors, warnings and job start and finish messages will always be written to the Windows event log.

By default, information messages will not, set boolean app settings WriteProgressToEventLog to true to write all progress messages to it too.

The program is written with Net Core so can be used on platforms other than Windows but the event log writing will currently only work on Windows.

# Code

You can read the source code for fuller descriptions and a better understanding of how it works, most functions and other declarations are decorated with top-level comments, the structure is pretty clear and names of things are nice and descriptive.

# Plain-text password alert !

To make the encryption work you need to either add the password to use to the EncryptionPassword section of the configuration file, or put it in a text file and specify the full path in the EncryptionPasswordFile setting. 

Obviously this is a plain-text password in a file of one kind or another so definitely to be done with some consideration as to how good an idea that is, especially if the configuration or password files themselves are archived by the system. 

You could end up with a nicely encrypted set of files with the password conveniently provided in plain text nearby.

# AESCrypt

It uses AESCrypt to do the encryption rather than a built-in library, the reason being that I routinely use AESCrypt's explorer extension to decrypt these files to view and alter my little credential files and want the encryption to be done by the same code I'm expecting to decrypt it with.

To enable encryption you'll need to manually install AESCrypt from https://www.aescrypt.com/. Thanks to Paketizer for this great utility and being happy for me to involve their product in my utility.

Once it's installed, add the path to the executable to the appsettings.json file as AESEncryptPath, see App settings, below.

Encryption is disabled by default, to enable it, set EncryptOutput to true in the source directories in the configuration file and/or define one or more secure directories.

If the path to the exe isn't specified then no encryption will be attempted. If the path is specified but not found, an error will be reported and obviously nothing will be encrypted.

# Parameters
There is only one parameter to the executable Archivist.exe, which is optional. It defines the name of the job to run eg. 'DailyBackup' or 'BackupMusic'. Job names cannot contain spaces. 

If no parameter is supplied, the system will run the job named in the app settings 'RunJobName'.

# App settings

Standard json configuration file, sample below;

## Sample app settings.json

```json
{
  "RunJobName": "FullBackup",
  "ConfigurationFile": "C:\\Dev\\Archivist\\Configuration.json",
  "LogDirectory": "C:\\Dev\\Archivist\\Log",
  "AESEncryptPath": "C:\\Program Files\\AESCrypt_console_v310_x64\\aescrypt.exe",
  "DebugConsole": "true",
  "WriteProgressToEventLog": "false",

  "ConnectionStrings": {
    "DefaultConnection": "Data Source=ServerNameOrAddress;Initial Catalog=Archivist;Integrated Security=true"
  }
}
```

# Configuration file

This is a json file defining the archiving jobs that exist and what they will do. Set the name of your configuration file in app settings ConfigurationFile. If the file does not exist, a default one will be created but obviously the default one won't know your directory names so won't work as-is.

The first run of the program will create the file if it doesn't exist, then report a series of errors telling you that the paths in there don't exist and bomb out without doing anything, you can then edit the file and set it up from there.

## Running the code in debug mode

Please note if you download the code and run in debug mode from the source, the configuration file will be overwritten by the custom one defined in ConfigurationHelpers whether it exists or not, and the default one will also be created as the same file name but as '.json.json', see the ConfigurationHelpers class. 

This is to make development smoother, best not run from the code unless you're playing with our own adaptation of it, and have updated the code in ConfigurationHelpers to your actual backup requirements.

## JobSpecifications

Here you define one of these for each job, eg. DailyBackup, WeeklyBackup, BackupPhotos etc.

|Setting|Description|
|-----|-----|
|Name|The name of the job to run, give this as the first parameter when calling Archiver.exe or specify it in the app settings RunJobName, the parameter will override the appsettings name. Job name cannot contain spaces.|
|ProcessTestOnly|For development only really, you can mark directories as ForTesting, and setting this to true will only process those.|
|ProcessSlowVolumes|You can mark directories as IsSlowVolume, setting this to true will make it process those, if false, it'll not process slow volumes.|
|ArchiveFairlyStatic|You can set directories as IsFairlyStatic and setting this allows you to tell a job whether to process it or not, it's intended to allow you to make a backup that only processes directories containing files that change frequently, eg source code or documents, as opposed to those that don't, eg movies or photos or whatever you don't add to much.|
|PrimaryArchiveDirectoryName|Here is where the initial zip files of each source directory are created, as such it should ideally be fairly fast and large, and on a different drive to where the source files are. Files are copied from here to the archive directories.|
|EncryptionPassword|The password to be used for encryption, see the 'Plain-text password alert !' section above. This value is overwritten if a value is provided in EncryptionPasswordFile, this will also generate a warning.|
|EncryptionPasswordFile|Loads the encryption password from this file, overwrites any value specified in EncryptionPassword. The password should be the only thing in the file.|
|SourceDirectories|These are the directories containing the files you want to zip to the primary archive directory, you can add any number, see 'Source Directories' below.|
|ArchiveDirectories|These are the directories that files will be copied to from the primary archive directory, see 'Archive Directories' below.|
|SecureDirectories|These are the directories which the system will encrypt files in place, see 'Secure Directories' below.|

## Directory settings for source and archive directories

These apply to both types of directory.

|Setting|Description|
|-----|-----|
|Priority|Process directories in this order, lowest number first (then by directory name alpha).|
|EnabledAtHour|Only process this directory after this hour starts, zero disables.|
|DisabledAtHour|Only process this directory after this hour starts, zero disables.|
|IsForTesting|Marks this directory as one to process when the backup type has ProcessTestOnly set, this is just used for developing the code.|
|Description|A human description of what this directory is, or what drive it's on - eg '128gb USB Key' or 'Where my photos are', this doesn't make anything work or break anything if left undefined, it's just a reminder for the human.|
|IsEnabled|Whether to process this directory in any way. If it is false, this directory will be ignored.|
|IsRemovable|Whether this is on a volume that is removeable, mainly determining whether it should be considered an error if the volume it's on can't be found. If it can't be found it won't even be considered as a warning if this is true.|
|IsSlowVolume|Whether this is a slow volume, used in conjunction with config WriteToSlowVolumes so backup jobs that only read from and write to fast drives can be set up by setting job setting ProcessSlowVolumes to false.|
|RetainVersions|If a file has a version suffix (created by setting source directory setting AddVersionSuffix to true) we will retain this many of them in this directory, zero means we keep all versions, which will eventually fill the volume. One gotcha is that if you set this lower on an archive directory than on the source directory the system will keep copying over older versions and then deleting them, if the system finds this on startup it will log a warning but cheerfully copy and delete as instructed.|
|DirectoryPath|The full path of this directory.|
|IncludeSpecifications|A list of file specs, eg '\*.txt', 'thing.\*', abc???de.jpg' etc, only process files matching these, an empty list includes all files.|
|ExcludeSpecifications|A list of file specs, eg '\*.txt', 'thing.\*', abc???de.jpg' etc, ignore files matching these, an empty list doesn't exclude any files.|
|SynchoniseFileTimestamps|Set the creation and last write time of any files created to the same as the source.|

## Source directories

Here you define the set of directories you want zipping up into files in the primary archive directory.

|Setting|Description|
|-----|-----|
|MinutesOldThreshold|Only process this directory if the latest file was updated or created this number of minutes ago, i.e. let files get this stale before archiving, prevents repeatedly archiving a folder if files are updated often and the archiver runs frequently.|
|CheckTaskNameIsNotRunning|Don't process this source if a task with this name is running, eg. I use Thunderbird for email which holds on to the files email are stored in, so I have this set to 'Thunderbird' for that source directory. Any running task found with this string in the name will prevent this directory being processed and generate a warning.|
|IsFairlyStatic|Indicates whether this source is something that changes a lot, eg source code as opposed to sets of files that are occasionally added to but not often changed like movies and photos. This doesn't stop it being archived, but means you can set up archive jobs to choose whether to process this source based on the job ArchiveFairlyStatic setting.|
|CompressionLevel|What type of compression to use on creating zip files, see see Microsoft System.IO.Compression docs at https://docs.microsoft.com/en-us/dotnet/api/system.io.compression.zipfile.createfromdirectory?view=net-5.0 for full details, options are Optimal = 0, Fastest = 1, NoCompression = 2, SmallestSize = 3.|
|ReplaceExisting|Overwrite any output files with the same name as one being created.|
|EncryptOutput|Encrypt the output file after zipping, uses AESEncrypt at the moment, see the AESCrypt section above for setup instructions. You need to install it manually and put the path to the exe in the AESEncryptPath setting in appsettings.json. The reason it doesn't use built-in .Net encryption is because I use the AESEncrypt Explorer extension so want to encrypt files with the same code.|
|DeleteArchiveAfterEncryption|Delete the unencrypted zip archive after successful encryption.|
|OutputFileName|The name of the zipped output file (no path), if not specified it uses the path to generate the name so directory 'C:\AbC\DeF\GhI' will be archived to 'AbC-DeF-GhI.zip'. For ease of use and clarity it's best to default this unless you really want to set the name to something else.|
|AddVersionSuffix|Adds a suffix to the file name of the form '-nnnn' before the extension, each new file adds 1 to the number. So archiving 'C:\Blah' with the default OutputFileName setting results in files 'Blah-0001.zip', 'Blah-0002.zip' etc. This works alongside RetainVersions to limit the number of these which it keeps.|
|MinutesOldThreshold|Set this to more than zero to have the system ignore new and altered files until they are this old. This allows you to stop the system making many archives of files that change frequently if you run archiver often. For example, you might set a directory to 60 so it only archives files in there that were created or changed over an hour ago.|

## Archive directories

Settings that just apply to archive directories.

Err, none, archive directories just have the shared settings, above.

## Configuration.json sample

The sample below is created by serialising a populated class, so has all settings in it. A real one doesn't need to be anywhere this big as most values are the defaults, but good to have in full here to show all the settings that can be specified.

This is similar to the default file which will be created if the one in appsettings.json does not exist, the paths in it are intentionally unlikely to exist so the program will report them as errors and stop without doing anything, you will need to adjust it to suit the setup you want.

```json
"JobSpecifications": [
    {
      "Name": "QuickBackup",
      "WriteToConsole": true,
      "PauseBeforeExit": true,
      "ProcessTestOnly": false,
      "ProcessSlowVolumes": false,
      "ArchiveFairlyStatic": false,
      "PrimaryArchiveDirectoryName": "M:\\PrimaryArchiveFolderName",
      "EncryptionPassword": "passwordinplaintext-scary",
      "SourceDirectories": [],
      "ArchiveDirectories": [],
      "SecureDirectories": []
    },
    {
      "Name": "TestBackup",
      "WriteToConsole": true,
      "PauseBeforeExit": true,
      "ProcessTestOnly": true,
      "ProcessSlowVolumes": false,
      "ArchiveFairlyStatic": false,
      "PrimaryArchiveDirectoryName": "M:\\PrimaryArchiveFolderName",
      "EncryptionPassword": null,
      "EncryptionPasswordFile": "C:\\Dev\\Archivist\\EncryptionPassword.txt"
      "SourceDirectories": [],
      "ArchiveDirectories": [],
      "SecureDirectories": []
    }
  ],
  "GlobalSourceDirectories": [
    {
      "MinutesOldThreshold": 0,
      "CheckTaskNameIsNotRunning": null,
      "IsFairlyStatic": false,
      "CompressionLevel": 0,
      "ReplaceExisting": true,
      "EncryptOutput": false,
      "DeleteArchiveAfterEncryption": true,
      "AddVersionSuffix": true,
      "OutputFileName": null,
      "Priority": 99,
      "EnabledAtHour": 0,
      "DisabledAtHour": 0,
      "IsForTesting": false,
      "Description": null,
      "IsEnabled": true,
      "IsRemovable": false,
      "IsSlowVolume": false,
      "RetainVersions": 2,
      "DirectoryPath": "C:\\ProbablyDoesntExist",
      "IncludeSpecifications": null,
      "ExcludeSpecifications": null,
      "SynchoniseFileTimestamps": false
    },
    {
      "MinutesOldThreshold": 0,
      "CheckTaskNameIsNotRunning": null,
      "IsFairlyStatic": false,
      "CompressionLevel": 0,
      "ReplaceExisting": true,
      "EncryptOutput": true,
      "DeleteArchiveAfterEncryption": true,
      "AddVersionSuffix": true,
      "OutputFileName": null,
      "Priority": 3,
      "EnabledAtHour": 0,
      "DisabledAtHour": 0,
      "IsForTesting": false,
      "Description": null,
      "IsEnabled": true,
      "IsRemovable": false,
      "IsSlowVolume": false,
      "RetainVersions": 2,
      "DirectoryPath": "C:\\ProbablyDoesntExistEither",
      "IncludeSpecifications": null,
      "ExcludeSpecifications": null,
      "SynchoniseFileTimestamps": false
    },
    {
      "MinutesOldThreshold": 60,
      "CheckTaskNameIsNotRunning": null,
      "IsFairlyStatic": false,
      "CompressionLevel": 1,
      "ReplaceExisting": true,
      "EncryptOutput": false,
      "DeleteArchiveAfterEncryption": false,
      "AddVersionSuffix": true,
      "OutputFileName": null,
      "Priority": 99,
      "EnabledAtHour": 0,
      "DisabledAtHour": 0,
      "IsForTesting": true,
      "Description": null,
      "IsEnabled": true,
      "IsRemovable": false,
      "IsSlowVolume": false,
      "RetainVersions": 2,
      "DirectoryPath": "D:\\Temp",
      "IncludeSpecifications": null,
      "ExcludeSpecifications": null,
      "SynchoniseFileTimestamps": false
    },
    {
      "MinutesOldThreshold": 0,
      "CheckTaskNameIsNotRunning": null,
      "IsFairlyStatic": true,
      "CompressionLevel": 2,
      "ReplaceExisting": true,
      "EncryptOutput": false,
      "DeleteArchiveAfterEncryption": false,
      "AddVersionSuffix": false,
      "OutputFileName": null,
      "Priority": 99,
      "EnabledAtHour": 0,
      "DisabledAtHour": 0,
      "IsForTesting": false,
      "Description": null,
      "IsEnabled": true,
      "IsRemovable": false,
      "IsSlowVolume": false,
      "RetainVersions": 0,
      "DirectoryPath": "M:\\Media\\Movies",
      "IncludeSpecifications": null,
      "ExcludeSpecifications": null,
      "SynchoniseFileTimestamps": false
    }
  ],
  "GlobalArchiveDirectories": [
    {
      "Priority": 3,
      "EnabledAtHour": 0,
      "DisabledAtHour": 0,
      "IsForTesting": false,
      "Description": "A very slow but big and cheap MicroSD card",
      "IsEnabled": true,
      "IsRemovable": true,
      "IsSlowVolume": true,
      "RetainVersions": 2,
      "DirectoryPath": "S:\\ArchivedFilesBlah",
      "IncludeSpecifications": [
        "*.zip"
      ],
      "ExcludeSpecifications": [
        "Media-*.*",
        "Temp*.*",
        "Incoming*.*"
      ],
      "SynchoniseFileTimestamps": true
    },
    {
      "Priority": 1,
      "EnabledAtHour": 0,
      "DisabledAtHour": 0,
      "IsForTesting": true,
      "Description": "External SSD set, 2 x 476GB, connected alternately on demand",
      "IsEnabled": true,
      "IsRemovable": true,
      "IsSlowVolume": true,
      "RetainVersions": 2,
      "DirectoryPath": "Y:\\ArchivedFilesAgain",
      "IncludeSpecifications": [
        "*.zip"
      ],
      "ExcludeSpecifications": [
        "Media-*.*",
        "Temp*.*",
        "Incoming*.*"
      ],
      "SynchoniseFileTimestamps": true
    },
    {
      "Priority": 2,
      "EnabledAtHour": 0,
      "DisabledAtHour": 0,
      "IsForTesting": true,
      "Description": "External massive HDD, connected almost all the time",
      "IsEnabled": true,
      "IsRemovable": true,
      "IsSlowVolume": true,
      "RetainVersions": 4,
      "DirectoryPath": "Z:\\Archive",
      "IncludeSpecifications": [
        "*.zip"
      ],
      "ExcludeSpecifications": [],
      "SynchoniseFileTimestamps": true
    }
  ],
  "GlobalSecureDirectories": [
    {
      "DeleteSourceAfterEncrypt": false,
      "Priority": 99,
      "EnabledAtHour": 0,
      "DisabledAtHour": 0,
      "IsForTesting": false,
      "Description": null,
      "IsEnabled": true,
      "IsRemovable": false,
      "IsSlowVolume": false,
      "RetainVersions": 0,
      "DirectoryPath": "C:\\Something\\Secure",
      "IncludeSpecifications": null,
      "ExcludeSpecifications": null,
      "SynchoniseFileTimestamps": true
    },
    {
      "DeleteSourceAfterEncrypt": false,
      "Priority": 99,
      "EnabledAtHour": 0,
      "DisabledAtHour": 0,
      "IsForTesting": false,
      "Description": null,
      "IsEnabled": true,
      "IsRemovable": false,
      "IsSlowVolume": false,
      "RetainVersions": 0,
      "DirectoryPath": "C:\\SomethingElse\\AlsoSecure",
      "IncludeSpecifications": null,
      "ExcludeSpecifications": null,
      "SynchoniseFileTimestamps": true
    }
  ]
}
```

