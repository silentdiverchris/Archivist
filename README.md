# Archivist
A C# Net Core archiving/backup utility.

I got frustrated with existing backup systems for Windows and wanted something to do sporadic and regular backups, and archive my media the way I want.

I previously wrote a PowerShell system that used WinRar and RoboCopy and an increasingly numerous set of text files containing the options for each specific type of archive I wanted, but it gradually become unmanageable and perhaps went beyond what PowerShell is really intended to be doing.

And so it came to pass that Archivist was born, almost certainly to the sound of celestial trumpets.

It's not ideal for non-technical users, it has no GUI and relies on considered tweaking of a potentially large .json file, but for a technical user who can be bothered to put some time into setting it up it can be a powerful and hugely adaptable backup solution.

Rather than dump a single subset of everything to one place, it can archive different sets of files in different directories to multiple places depending on inclusion and exclusion file specifications, retaining a complete history of them in one place such as a massive local or NAS volume, and just the latest X versions and/or the last Y days worth of various selections of them on any number of other fixed or removable volumes.

You can define different backup jobs, perhaps one to back up everything to everywhere on an automated schedule and/or via a shortcut, one to add new photos and movies to an archive every week, set up desktop shortcuts to fire off backups of specific, frequently changing files like code and documents to fast internal drives, or to a specific volume if it's mounted, or whatever happens to be mapped to F: at the time.

There is no UI other than the console output, it's driven from a single json configuration file 'appsettings.json' and reports to the console, plus optionally to a text log file and/or a SQL table and/or the Windows event log.

While it can theoretically be compiled for use on Linux or Mac or in a Docker container, it has only been tested or used on native Windows 10.

This was written to do exactly what I personally want from an archiving system, it's not intended to be a panacea for everyone but I hope it pretty much covers what most people might want from such a thing. Feel free to ask for a feature to be added, or feel even freer to add it yourself.

For the purposes of this document, an 'archive' is pretty much synonymous with a zip file of a nominated source directory.

It uses the LastWriteTime of the zip files it creates to decide whether a new archive should be created, checking for any files in the source to see if at least one has a later last write time than the archive file. As such, it doesn't constantly zip up identical sets of files.

The text below is fairly detailed, please feel free to get in touch or raise an issue to ask for further detail, point out mistakes or report bugs, I'll update the below with any corrections, clarifications or expansions.

# Licence
The code is licensed under [The MIT Licence](https://opensource.org/licenses/mit-license.php); which essentially means feel free to do whatever you like with it, but any horrific consequences are not my fault.

# Installation
There is no installer package, currently you need to download the code and build it locally. 

I'll generate a proper release/installer soon when it's stabilised a bit.

It was created with Net Core 6.0.0 preview 6 and Visual Studio 2022 beta, so you'll definitely need at least the former, and will no doubt get a raft of downgrade issues if you don't use the latter.

# Caveats
Code and executables provided as-is. This system runs on my machines several times a day to process my own precious files and is written with caution very much in mind by somebody who is paranoid about these things.

This is created with Net Core 6.0.0 preview 6 and Visual Studio 2022 beta, I'll gradually move it along as newer versions are released. It's my intention that it remains on this 'bleeding edge' if it can be called that as this mini-project is partially about me trying out the new releases as they become available, but with it essentially just zipping and copying files I don't see that would make it in any way dangerous to use.

# Key question - does it alter or delete any of my files ?

No, it doesn't write to or delete any of the source files it processes, nor does it add any files to the source directories. It doesn't even set archive flags, though that's an idea for a possible enhancement now I come to think of it. 

It purely reads the source directories to zip them up; with one optional exception, below.

If you enable the 'secure directories' function by defining some, it will delete unencrypted files in the set of secure directories, only when it's specifically told to with the DeleteSourceAfterEncrypt setting (which defaults to false), only after having checked an encrypted version already exists, or that a new encryption reported success and that the newly encrypted version of the file exists.

# The archiving process

The diagram below shows a simple layout, essentially source directories are zipped to files in the primary archive directory, then copied to other archive directories.

Note that in the example, all code and document archives are copied to one drive, the latest versions of all archives are copied to a second drive, and everything is copied to a third; you can set it up to spread the files around as you like. 

It could be configured to keep the last month of updates to your code in one archive directory and also keep the complete history of it on a different one, keep the last 10 versions of your documents plus all versions for the last 90 days elsewhere, and keep the 3 most recent versions of everything somewhere else, copy the last month of all archives to a removable drive whenever it is mounted, and just the most recent code and documents to a different removable drive if it is plugged in.

You can set it up to be as simple or as excessive as you see fit, and then continually tinker with the configuration as your level of paranoia varies, more volumes become available to fill up and ever more convoluted backup strategies occur to you.

<img alt="Source and archive directories" title="Source and archive directories" src="https://github.com/silentdiverchris/Archivist/raw/master/Diagrams/Directories.png">

There are three main parts to the process, done in the order listed below.

## Securing directories

You can nominate a list of 'secure directories' that the system will automatically encrypt files found in, each to its own individual '.aes' file, and optionally remove the unencrypted version.

The reason for this process being that I like to keep credentials, account details and other secret stuff in little text files, screen shots and the like in various directories, decrypting them manually to view and update them, and either immediately (re-)encrypt manually or more likely, leave them for Archivist to process. 

My main development PC runs various Archivist jobs several times a day so nothing stays unsecured for long and will always be secured before any archiving is done so I know no sensitive data is in plain text in any of my backups.

It will take each file name and append '.aes' to it to determine the encrypted file name, so 'SecretPassword.txt' will be encrypted into 'SecretPassword.txt.aes'.

It will ignore any file called 'clue.txt' in upper, lower or mixed case, I use a file of that name to store a cryptic reminder of the password I use for files in that directory.

When files are encrypted it sets the last write time to that of the source file, and uses the last write times to determine which file is most recent.

When Archivist runs it will encrypt any files in secure directories that are not of the form '\*.aes' if the unencrypted version has a later write time. 

It will then optionally delete the unencrypted version if an encrypted version exists once it is sure the encryption happened successfully, configuration setting DeleteArchiveAfterEncryption controls whether it deletes of the unencrypted files.

This setting defaults to false, which isn't the recommended value but works on the basis of "Don't delete stuff unless specifically told to" and allows a new user to verify that it's working how it should before trusting it.

If DeleteArchiveAfterEncryption is set to true, and it finds an unencrypted file with a write time the same as, or earlier than the encrypted version it will assume the file was manually unencrypted to view it, and just delete the file, retaining the encrypted one. 

If the unencrypted file has a later last write time it will re-encrypt it, overwriting the previous encryption and optionally deleting the unencrypted version.

To nominate secure directories, add them to the GlobalSecureDirectories or SecureDirectories list in the [application settings](#AppSettings) file.

## Archiving source directories

The second part of the process is to take the list of directories in the GlobalSourceDirectories and SourceDirectories configuration and recursively zip each into a single output file in a nominated directory, known as the primary archive directory,  specified in the configuration as PrimaryArchiveDirectoryName.

This uses the 'ZipFile.CreateFromDirectory' interface in Microsoft's Sytem.IO.Compression library, see the [Microsoft documentation](https://docs.microsoft.com/en-us/dotnet/api/system.io.compression?view=net-5.0) for details.

The resulting zip files can then optionally be encrypted, creating a file with a '.aes' extension, so 'ArchivedFile.zip' is encrypted to 'ArchivedFile.zip.aes'. Set the EncryptOutput configuration setting on the source directory to enable this. 

See the [AESCrypt](#AESCrypt) section for full details on setting up encryption.

## Copying archives

The final part takes the lists of directories in GlobalArchiveDirectories and ArchiveDirectories defined in the configuration file and copies files from the primary archive directory to those directories depending on all the filters, inclusions and exclusions specified in the configuration ArchiveDirectories settings.

# Screenshots

## Sample console output

A typical console output is below. 

First it shows the job name, configuration file name, log file name and the SQL destination for logs.

Next it shows the securing of two text files, encrypting them both and deleting the source files. 

It then goes through most source directories for archiving but finds nothing has changed in most of them so skips those but does find 3 folders worth zipping up to the primary archive directory. 

Then it copies several files to an archive directory on another drive, the ones it just created plus a few more, I had deleted a few to give it more work to do for the screenshot.

<img alt="Half way through an archive" title="Half way through an archive" src="https://github.com/silentdiverchris/Archivist/raw/master/Screenshots/ExampleConsole1.png">

Note that deleting an old archive version was reported as a warning but it probably shouldn't be, it's entirely expected behaviour so I'll make it an unremarkable info type log entry at some point but I wanted to know about deletions as I've just changed the code for the new RetainDaysOld setting so want to keep an eye on it.

The rest of the console output after the archive had completed follows;

<img alt="Completed archive" title="Completed archive" src="https://github.com/silentdiverchris/Archivist/raw/master/Screenshots/ExampleConsole2.png">

It is waiting for a key-press before closing the console window because job configuration setting PauseBeforeExit is telling it to. 

# Global and non-global directories

When preparing to run a job, the system combines the global directory lists with the list defined for the job, and runs all of them. Typically I find I just have all mine in the global lists to be run with all jobs but there is the flexibility to have different sets for different jobs.

# Selection and filtering

Directories and files can be selected, included and excluded in various ways according to their names, the job that is running, the current time of day, whether they change frequently or not, are on slow volumes and more. See the configuration file section for full details, some main topics along these lines are mentioned below and detailed later.

## File inclusions and exclusions

Include or exclude files to be copied to archives depending on a set of file specifications e.g. 'Media*.zip'.

See IncludeSpecifications and ExcludeSpecifications in the [application settings](#AppSettings) section below.

## Slow volumes

I mainly archive to external SSDs and HDDs but also to a set of MicroSD cards, which are rather slow to write to but cheap for the capacity and fairly indestructible. The system can be set up to only write to these slow volumes on an overnight run, or at the end of the week, say.

See IsSlowVolume and ProcessSlowVolumes in the [application settings](#AppSettings) section below.

# File timestamps

When an archive is copied out to archive directories, the LastWriteTime is set to the same value as the source file.

The system does not use file creation time to make any decisions, the last write time is the one it sets and uses to make decisions.

# File versions

An archive of folder 'Development' would normally create file 'Development.zip', setting AddVersionSuffix would name the first one as 'Development-0001.zip', the next as 'Development-0002.zip' and so on. 

Setting RetainVersions to 3, for example, would leave those as-is when 'Development-0003.zip' was created, then when 'Development-0004.zip' was created would delete 'Development-0001.zip', retaining the last 3.

When it gets to 9900 it will start generating warnings, and at 9999 it will generate an error and not create the next version of that file. Currently you need to renumber the files, e.g. back to 0001, 0002 etc. to get it working again. 

The system stores no internal record of what it calls files, it goes from what it finds at the time.

Using the RetainVersions setting you can tell it to keep the last 2, 5 or however many versions you like. It will delete the ones with the lowest numbers, which would usually be the oldest if you haven't touched the files since but the file timestamps aren't a factor in the decision, it judges purely by the digits in the file names.

If it finds any file name that isn't of the form \[base file name]\[hyphen\]\[4 digits\]\[dot]\[extension] it won't touch it. Nor will it touch any file that has a \[base file name] that it isn't actively writing at the time.

If AddVersionSuffix for a directory is false, files will not be versioned and there will only ever be one version of each archive in that directory.

This versioning can start to eat up disk space of course, the system will report the space free on drives it uses to the console/log, and generate a warning if it is below 50Gb, currently.

This behaviour can be limited by specifying RetainDaysOld, which will make sure no files are deleted if they were last written to less than that many days ago, regardless of the number of versions, see below for more details.

# Deleting old versions

At two points in the process, namely when a zip archive is created and after copying it to an archive directory, the system can delete older generations of each file and so keep a specific number of them. 

To do this, set the AddVersionSuffix, RetainVersions and RetainDaysOld settings on the directory in question.

The RetainVersions setting defines how many versions will be kept, but is limited by the RetainDaysOld setting, which ensures that no file is deleted if it was last written to less than that number of days ago, regardless of the RetainVersions setting.

The RetainDaysOld setting will not cause older files to be deleted, it only prevents younger files being deleted.

Setting AddVersionSuffix to true, and both RetainVersions and RetainDaysOld to zero will just keep adding new versions and never delete anything.

In this way, you can for example retain the last 3 versions and any versions up to 30 days old in the primary archive directory while retaining every copy of these files up to a year old in another archive directory, all files forever in another archive directory and just the very latest of each file in yet another archive directory.

The process to review files for deletion only happens after an archive file is created or copied, so if a source directory is not changed, and so no new archive of it is created, no old archives of it will be deleted.

# Performance

It could be faster, the 7-Zip library seems to be faster than the .Net compression, and a previous version of the code used RoboCopy, which did the copying more quickly especially with muti-threaded copies. 

I might update it to use RoboCopy again but it's not really a priority, it wasn't all that much faster, I don't sit waiting for it to finish anyway and; stable and dependable (and fantastic) as RoboCopy is, it's nice not to have a call out to another external executable.

# Full disk

If the destination disk fills up while creating a zip or copying a file it will fail that operation and report an error but continue, so an archive of your music library might fail but the archives of smaller sets of files defined later in the job will still be attempted.

# Jobs

You can define any number of different jobs which can select different sets of directories and files. In this way you can set up daily, weekly and monthly backups, or a job to backup one specific directory every hour, or only fast volumes every 10 minutes.

# Removable volumes

My backup system involves having several external drives which I mount for various reasons, e.g. one for daily backups which is almost always connected, one which I plug in just at the start of each week, one for the start of each month, and a pair of two identical large SSDs which I generally leave one of attached but alternate between them. So sometimes S:\ or Y:\ might exist, sometimes it won't.

If you don't want to rely on mounted drives always having the same drive letter you can identify directories by volume label rather than drive letter by setting the the VolumeLabel configuration and not providing a drive in the DirectoryPath, see these items in the [application settings](#AppSettings) section below.

If you mark a directory as removable with IsRemovable, the system will try to use it but if it's not there it won't be considered as an error. Any drive that is not found which is not marked as removable will be reported as an error.

This means you don't need to be too bothered about exactly which drives you plug in, it'll just archive to what it finds, but this setting allows it to distinguish between an external disk not having been plugged in and a internal one that should be available but isn't.

# Scheduling

There is no built in scheduler, it works just fine with Windows Scheduler and any other decent cron system that can call an executable and ideally specify a parameter. 

Just specify the the job name in the application settings file as 'RunJobName' or, better, as the first parameter to the executable.

Below is a screenshot of Windows Scheduler setting up the call and job name parameter.

<img alt="Windows Scheduler" title="Windows Scheduler" src="https://github.com/silentdiverchris/Archivist/raw/master/Screenshots/SchedulerExample1.png">

# Logging

## Console

It will write most or all log messages to the console, with warnings in yellow, errors in red and success/completed messages in green.

Setting DebugConsole to true in the application settings file makes it write everything to the console. 

Without DebugConsole the console will get a more readable subset of just the important messages and will always let you know what it's doing right now, so it's generally best to leave this turned off for readability and dig into the text/SQL log if you need more detail.

Full logging always goes to the file and SQL logs, which includes a lot of verbose stuff about the decisions it made according to settings, file timestamps et..

## Text file

You can nominate a directory with the LogDirectoryPath item in application settings, this tells it where to create text log files. Log file names are in the form;

Archiver-\[JobName]-YYYYMMDDHHMMSS.log

If a log directory path is not supplied, no text logging will happen. 

If a full path is supplied but doesn't exist, the program will try to create it, and an error will be reported if it fails to.

If a partial path is supplied, i.e. one without any directory separator such as 'Log', it will try to create it in the same directory as the executable, which may require the program to be run with raised privileges.

## SQL logging

I like my logs in a SQL table for easy filtering and having it all in one place. If you give it a valid SQL connection string it will try to call stored procedure 'AddToLog' in that database to write log messages.

If the stored procedure doesn't exist, it will automatically attempt to create a 'Log' table and the 'AddToLog' stored procedure in the nominated database (see script below) and will call the stored procedure to write log messages.

If a connection string is not supplied, no SQL logging will happen, which seems fair. 

If one is supplied but the program cannot connect to it, an error will be reported.

You can alter the stored procedure and log table as you wish, the system knows nothing about the  table, it just attempts to call AddLogEntry with three parameters, what happens internally is entirely customisable.

If you want to revert to the default entities, delete the table and stored procedure and they will be recreated on the next run.

## SQL entity creation script

The script used to create the entities is below, it's a vanilla SSMS 'Create Script' output, no funny business, apologies for unpleasant formatting.

```sql
-- Straight script generation from SQL

/****** Object:  Table [dbo].[Log]    Script Date: 04/08/2021 12:28:53 ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[Log]') AND type in (N'U'))
BEGIN
CREATE TABLE [dbo].[Log](
	[Id] [int] IDENTITY(1,1) NOT NULL,
	[CreatedUTC] [datetime2](3) NOT NULL,
	[LogText] [varchar](8000) NOT NULL,
	[LogSeverity] [tinyint] NOT NULL,
	[FunctionName] [varchar](100) NULL,
 CONSTRAINT [PK_Log] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
END
GO
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[DF_Log_CreatedUTC]') AND type = 'D')
BEGIN
ALTER TABLE [dbo].[Log] ADD  CONSTRAINT [DF_Log_CreatedUTC]  DEFAULT (getutcdate()) FOR [CreatedUTC]
END
GO
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[DF_Log_LogSeverity]') AND type = 'D')
BEGIN
ALTER TABLE [dbo].[Log] ADD  CONSTRAINT [DF_Log_LogSeverity]  DEFAULT ((1)) FOR [LogSeverity]
END
GO
/****** Object:  StoredProcedure [dbo].[AddLogEntry]    Script Date: 04/08/2021 12:28:54 ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[AddLogEntry]') AND type in (N'P', N'PC'))
BEGIN
EXEC dbo.sp_executesql @statement = N'CREATE PROCEDURE [dbo].[AddLogEntry] AS' 
END
GO
ALTER PROCEDURE [dbo].[AddLogEntry]
	@FunctionName				varchar(100) = NULL,
	@LogText					varchar(8000),
	@LogSeverity				tinyint = 1
AS
BEGIN

	Insert Into [Log] (FunctionName, LogText, LogSeverity)
	Values (@FunctionName, @LogText, @LogSeverity)

END
GO

```

## Windows event log

Errors, warnings and job start and finish messages will always be written to the Windows event log.

By default, information messages will not be sent to the event log, set application settings VerboseEventLog to true to write all progress messages to it.

The program is written with Net Core so can be used on platforms other than Windows but the event log writing will currently only work on Windows.

## Console

If job setting WriteToConsole is true, it will write a pretty full account of what it is doing to the console, setting VerboseConsole to true sends a good deal more to the console.

If PauseBeforeExit is true or if any errors are detected, it will ask for a key to be pressed before closing the console at the end of a run.

# Code

You can read the source code for fuller descriptions and a better understanding of how it works, most functions and other declarations are decorated with top-level comments, the structure is pretty clear and names of things are nice and descriptive.

# Plain-text password alert !

To make the encryption work you need to either supply a password to the EncryptionPassword job setting or put it in a text file and specify the full path in the EncryptionPasswordFile setting. 

Obviously this is a plain-text password in a file of one kind or another so should be done with some consideration as to how good an idea that is, especially if the configuration or password files themselves are archived by the system. 

You could end up with a nicely encrypted set of files with the password conveniently provided in plain text nearby.

An option to allow the user to type it into the console at job startup would be a nice enhancement.

<a name="AESCrypt"></a>
# AESCrypt

It uses AESCrypt to do the encryption rather than a built-in library, the reason being that I routinely use AESCrypt's explorer extension to decrypt these files to view and alter my little credential files and want the encryption to be done by the same code I'm expecting to decrypt it with.

To enable encryption you'll need to manually install AESCrypt from https://www.aescrypt.com/. Thanks to Paketizer for this great utility and being happy for me to involve their product in my utility.

Once it's installed, add the path to the executable to the appsettings.json file as AESEncryptPath, see the [application settings](#AppSettings) section below.

Encryption is disabled by default, to enable it, set EncryptOutput to true in the source directories in the configuration file and/or define one or more secure directories.

If the path to the executable isn't specified then no encryption will be attempted. If the path is specified but not found, an error will be reported and obviously nothing will be encrypted.

# Parameters
There is only one parameter to the executable Archivist.exe, which is optional. It defines the name of the job to run e.g. 'DailyBackup' or 'BackupMusic'. Job names cannot contain spaces. 

If no parameter is supplied, the system will run the job named in the application settings 'DefaultJobName'.

<a name="AppSettings"></a>
# Application settings

This is a json file defining the basic setup for the program plus the archiving jobs that exist and what they will do. It must be in the same directory as the Archivist executable file.

If the file does not exist, which will be the case on initial installation, a default one will be created but the default one won't know your directory names or what you want it to do, so won't work as-is.

The first run of the program with the default settings file will report a series of errors to the console telling you that the paths in there don't exist and any other problems it finds and then terminate without doing anything, you can then edit the file and set it up from there.

If you want to get a full example file at any time, rename or delete the existing one and the deafult file will be created on the next run.

## Default appsettings.json file

The default settings file will look something like the example below.

It will need to be altered to reflect your directory names and archiving preferences. 

You may well end up deleting most of it, but it seems a good idea to give a full example with all the defaults explicitly shown, to illustrate what options are available... and save me having to manually edit them all out.

The first few lines are basic setup details, then it goes into describing two example jobs and the directories that they will process.

```json
{
  "DefaultJobName": "ExampleJob1",
  "LogDirectoryPath": "Log",
  "AESEncryptPath": "",
  "SqlConnectionString": "",
  "VerboseConsole": false,
  "VerboseEventLog": false,
  "Jobs": [
    {
      "Name": "ExampleJob1",
      "Description": "An example of a job specification, you will need to edit this to point it at a primary archive directory, and any other changes you want to make.",
      "AutoViewLogFile": false,
      "WriteToConsole": true,
      "PauseBeforeExit": true,
      "ProcessTestOnly": true,
      "ProcessSlowVolumes": false,
      "ArchiveFairlyStatic": false,
      "PrimaryArchiveDirectoryName": "M:\\PrimaryArchiveDirectoryName",
      "EncryptionPassword": null,
      "EncryptionPasswordFile": null,
      "SourceDirectories": [],
      "ArchiveDirectories": [],
      "SecureDirectories": []
    },
    {
      "Name": "ExampleJob2",
      "Description": "Another example of a job specification",
      "AutoViewLogFile": false,
      "WriteToConsole": true,
      "PauseBeforeExit": true,
      "ProcessTestOnly": false,
      "ProcessSlowVolumes": false,
      "ArchiveFairlyStatic": false,
      "PrimaryArchiveDirectoryName": "M:\\PrimaryArchiveDirectoryName",
      "EncryptionPassword": null,
      "EncryptionPasswordFile": "C:\\InvalidDirectoryName\\PasswordInTextFile.txt",
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
      "RetainVersions": 2,
      "RetainDaysOld": 7,
      "IncludeSpecifications": null,
      "ExcludeSpecifications": null,
      "VolumeLabel": null,
      "DirectoryPath": "C:\\ProbablyDoesntExist",
      "SynchoniseFileTimestamps": true,
      "DeleteSourceAfterEncrypt": false,
      "Description": null,
      "IsEnabled": true,
      "IsRemovable": false,
      "Priority": 99,
      "EnabledAtHour": 0,
      "DisabledAtHour": 0,
      "IsForTesting": false,
      "IsSlowVolume": false
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
      "RetainVersions": 2,
      "RetainDaysOld": 7,
      "IncludeSpecifications": null,
      "ExcludeSpecifications": null,
      "VolumeLabel": null,
      "DirectoryPath": "C:\\ProbablyDoesntExistEither",
      "SynchoniseFileTimestamps": true,
      "DeleteSourceAfterEncrypt": false,
      "Description": null,
      "IsEnabled": true,
      "IsRemovable": false,
      "Priority": 3,
      "EnabledAtHour": 0,
      "DisabledAtHour": 0,
      "IsForTesting": false,
      "IsSlowVolume": false
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
      "RetainVersions": 2,
      "RetainDaysOld": 7,
      "IncludeSpecifications": null,
      "ExcludeSpecifications": null,
      "VolumeLabel": null,
      "DirectoryPath": "D:\\Temp",
      "SynchoniseFileTimestamps": true,
      "DeleteSourceAfterEncrypt": false,
      "Description": null,
      "IsEnabled": true,
      "IsRemovable": false,
      "Priority": 99,
      "EnabledAtHour": 0,
      "DisabledAtHour": 0,
      "IsForTesting": true,
      "IsSlowVolume": false
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
      "RetainVersions": 1,
      "RetainDaysOld": 7,
      "IncludeSpecifications": null,
      "ExcludeSpecifications": null,
      "VolumeLabel": null,
      "DirectoryPath": "M:\\Media\\Movies",
      "SynchoniseFileTimestamps": true,
      "DeleteSourceAfterEncrypt": false,
      "Description": null,
      "IsEnabled": true,
      "IsRemovable": false,
      "Priority": 99,
      "EnabledAtHour": 0,
      "DisabledAtHour": 0,
      "IsForTesting": false,
      "IsSlowVolume": false
    }
  ],
  "GlobalArchiveDirectories": [
    {
      "RetainVersions": 2,
      "RetainDaysOld": 90,
      "IncludeSpecifications": [
        "*.zip"
      ],
      "ExcludeSpecifications": [
        "Media-*.*",
        "Temp*.*",
        "Incoming*.*"
      ],
      "VolumeLabel": "BigMicroSD-01",
      "DirectoryPath": "ArchivedFiles",
      "SynchoniseFileTimestamps": true,
      "DeleteSourceAfterEncrypt": false,
      "Description": "A very slow but big and cheap MicroSD card",
      "IsEnabled": true,
      "IsRemovable": true,
      "Priority": 3,
      "EnabledAtHour": 0,
      "DisabledAtHour": 0,
      "IsForTesting": false,
      "IsSlowVolume": true
    },
    {
      "RetainVersions": 5,
      "RetainDaysOld": 90,
      "IncludeSpecifications": [
        "*.zip"
      ],
      "ExcludeSpecifications": [
        "Media-*.*",
        "Temp*.*",
        "Incoming*.*"
      ],
      "VolumeLabel": "ExternalSSD-01",
      "DirectoryPath": "ArchivedFileDirectoryName",
      "SynchoniseFileTimestamps": true,
      "DeleteSourceAfterEncrypt": false,
      "Description": "External SSD connected on demand",
      "IsEnabled": true,
      "IsRemovable": true,
      "Priority": 1,
      "EnabledAtHour": 0,
      "DisabledAtHour": 0,
      "IsForTesting": true,
      "IsSlowVolume": true
    },
    {
      "RetainVersions": 1,
      "RetainDaysOld": 7,
      "IncludeSpecifications": [
        "Media*.*"
      ],
      "ExcludeSpecifications": [],
      "VolumeLabel": "ExternalHDD-01",
      "DirectoryPath": "ArchivedFileDirectoryName",
      "SynchoniseFileTimestamps": true,
      "DeleteSourceAfterEncrypt": false,
      "Description": "External HDD connected on demand just for latest versions of all media",
      "IsEnabled": true,
      "IsRemovable": true,
      "Priority": 1,
      "EnabledAtHour": 0,
      "DisabledAtHour": 0,
      "IsForTesting": true,
      "IsSlowVolume": true
    },
    {
      "RetainVersions": 10,
      "RetainDaysOld": 365,
      "IncludeSpecifications": [
        "*.zip"
      ],
      "ExcludeSpecifications": [],
      "VolumeLabel": null,
      "DirectoryPath": "Z:\\Archive",
      "SynchoniseFileTimestamps": true,
      "DeleteSourceAfterEncrypt": false,
      "Description": "Internal massive HDD",
      "IsEnabled": true,
      "IsRemovable": true,
      "Priority": 2,
      "EnabledAtHour": 0,
      "DisabledAtHour": 0,
      "IsForTesting": true,
      "IsSlowVolume": true
    }
  ],
  "GlobalSecureDirectories": [
    {
      "VolumeLabel": null,
      "DirectoryPath": "C:\\Something\\Secure",
      "SynchoniseFileTimestamps": true,
      "DeleteSourceAfterEncrypt": false,
      "Description": null,
      "IsEnabled": true,
      "IsRemovable": false,
      "Priority": 99,
      "EnabledAtHour": 0,
      "DisabledAtHour": 0,
      "IsForTesting": false,
      "IsSlowVolume": false
    },
    {
      "VolumeLabel": null,
      "DirectoryPath": "C:\\SomethingElse\\AlsoSecure",
      "SynchoniseFileTimestamps": true,
      "DeleteSourceAfterEncrypt": false,
      "Description": null,
      "IsEnabled": true,
      "IsRemovable": false,
      "Priority": 99,
      "EnabledAtHour": 0,
      "DisabledAtHour": 0,
      "IsForTesting": false,
      "IsSlowVolume": false
    }
  ]
}
```

## Minimal appsettings.json file
This is a bare bones configuration to just archive one directory out to one backup drive, most settings are defaulted.

```json
{
  "DefaultJobName": "SimpleJob",
  "LogDirectoryPath": "Log",
  "Jobs": [
    {
      "Name": "SimpleJob",
      "Description": "An example of a simple job specification",
      "WriteToConsole": true,
      "PauseBeforeExit": true,
      "PrimaryArchiveDirectoryName": "M:\\PrimaryArchiveDirectoryName",
      "SourceDirectories": [ 
         {
		  "ReplaceExisting": true,
		  "DeleteArchiveAfterEncryption": true,
		  "AddVersionSuffix": true,
		  "RetainVersions": 2,
		  "RetainDaysOld": 7,
		  "DirectoryPath": "C:\\AllMyStuff",
		  "SynchoniseFileTimestamps": true,
		  "IsEnabled": true
		}
	  ],
      "ArchiveDirectories": [
	  {
		  "RetainVersions": 2,
		  "RetainDaysOld": 365,
		  "IncludeSpecifications": [
				"*.*"
		],
      "ExcludeSpecifications": [ ],
      "VolumeLabel": "BackupDrive-01",
      "DirectoryPath": "ArchivedFiles",
      "SynchoniseFileTimestamps": true,
      "DeleteSourceAfterEncrypt": false,
      "Description": "My only backup drive",
      "IsEnabled": true,
      "Priority": 3,
      "EnabledAtHour": 0,
      "DisabledAtHour": 0
    }
	]
  ]
}
```

## Running the code in debug mode

Please note if you download the code and run in debug mode from the source, the configuration file will be overwritten by the custom one defined in ConfigurationHelpers whether it exists or not, and the default one will also be created as the same file name but as '.json.json', see the ConfigurationHelpers class. 

This is to make development smoother, best not run from the code unless you're playing with our own adaptation of it, and have updated the code in ConfigurationHelpers to your actual backup requirements.

## Jobs

In this section of the settings you can, well, must define at least one job, for example 'DailyBackup', 'WeeklyBackup', 'ArchivePhotos' or the like.

|Setting|Description|
|-----|-----|
|Name|The name of the job to run, give this as the first parameter when calling Archiver.exe or specify it in the application settings RunJobName, the parameter will override the application settings. Job name cannot contain spaces.|
|Description|A text description of what this job does, not validated, just for human consumption.|
|ProcessTestOnly|For development only really, you can mark directories as ForTesting, and setting this to true will only process those.|
|ProcessSlowVolumes|You can mark directories as IsSlowVolume, setting this to true will make it process those, if false, it'll not process slow volumes.|
|ArchiveFairlyStatic|You can set directories as IsFairlyStatic and setting this allows you to tell a job whether to process it or not, it's intended to allow you to make a backup that only processes directories containing files that change or are added frequently, such as source code or documents, as opposed to those that don't, like a movie library.|
|PrimaryArchiveDirectoryName|Here is where the initial zip files of each source directory are created, as such it should ideally be fairly fast and large, and on a different drive to where the source files are.<br><br>Files are copied from here to the archive directories.|
|EncryptionPassword|The password to be used for encryption, see the 'Plain-text password alert !' section above.<br><br>This value is overwritten if a value is provided in EncryptionPasswordFile, this will also generate a warning.|
|EncryptionPasswordFile|Loads the encryption password from this file, overwrites any value specified in EncryptionPassword. The password should be the only thing in the file.|
|SourceDirectories|These are the directories containing the files you want to zip to the primary archive directory, you can add any number, see 'Source Directories' below.|
|ArchiveDirectories|These are the directories that files will be copied to from the primary archive directory, see 'Archive Directories' below.|
|SecureDirectories|These are the directories which the system will encrypt files in place, see 'Secure Directories' below.|

## Directory settings for source and archive directories

These settings apply to both archive and source directories.

|Setting|Description|
|-----|-----|
|Priority|Process directories in this order, lowest number first (then by directory name alpha).|
|EnabledAtHour|Only process this directory after this hour starts, zero disables.|
|DisabledAtHour|Only process this directory after this hour starts, zero disables.|
|IsForTesting|Marks this directory as one to process when the backup type has ProcessTestOnly set, this is just used for developing the code.|
|Description|A human description of what this directory is, or what drive it's on - e.g... '128gb USB Key' or 'Where my photos are', this doesn't make anything work or break anything if left undefined, it's just a reminder for the human.|
|IsEnabled|Whether to process this directory in any way. If it is false, this directory will be ignored.|
|IsRemovable|Whether this is on a volume that is removable, mainly determining whether it should be considered an error if the volume it's on can't be found. If it can't be found it won't even be considered as a warning if this is true.|
|IsSlowVolume|Whether this is a slow volume, used in conjunction with configuration WriteToSlowVolumes so backup jobs that only read from and write to fast drives can be set up by setting job setting ProcessSlowVolumes to false.|
|RetainVersions|If a file has a version suffix (created by setting source directory setting AddVersionSuffix to true) we will retain this many of them in this directory, zero means we keep all versions, which will eventually fill the volume.<br><br>Something to be aware of is that if you set this lower on an archive directory than on the source directory the system will keep copying over older versions and then deleting them, if the system finds this on startup it will log a warning but cheerfully copy and delete as instructed.<br><br>If RetainVersions is set to zero, no archives will ever be deleted.|
|RetainDaysOld|Specifies the minimum age at which an archive file can be deleted, regardless of any version numbering. The age is determined by the last write time, not the creation time.<br><br>Zero disables this function and any non-zero value has a minimum of 7 days. This ensures that however many versions of the archive are created, it will not delete any file that is younger than this number of days.<br><br>This does not cause files to be deleted after that number of days, it just stops younger files being deleted.<br><br>If the RetainVersions setting is set to zero, this setting will have no effect and no archives will ever be deleted.|
|VolumeLabel|Allows the directory to be identified by the volume label rather than a drive designation, for removable drives which aren't always F:\ or whatever.<br><br>Set this to a valid volume label and ensure the DirectoryPath has no drive designation.<br><br>For example, VolumeLabel '1TB HDD' and DirectoryPath 'Archive' will map to 'F:\Archive' when that volume is mounted as drive 'F' and 'E:\Archive' when it is mounted as 'E'.|
|DirectoryPath|The path of this directory, either the full path e.g. 'H:\Archive', or just 'Archive' if a valid VolumeLabel is supplied.|
|IncludeSpecifications|A list of file specifications, e.g... '\*.txt', 'thing.\*', abc???de.jpg' etc., only process files matching these, an empty list includes all files.|
|ExcludeSpecifications|A list of file specifications, e.g... '\*.txt', 'thing.\*', abc???de.jpg' etc., ignore files matching these, an empty list doesn't exclude any files.|
|SynchoniseFileTimestamps|Set the creation and last write time of any files created to the same as the source.|

## Source directories

These settings apply only to source directories.

Here you define the set of directories you want zipping up into files in the primary archive directory.

|Setting|Description|
|-----|-----|
|MinutesOldThreshold|Only process this directory if the latest file was updated or created this number of minutes ago, i.e. let files get this stale before archiving, prevents repeatedly archiving a folder if files are updated often and the archiver runs frequently.|
|CheckTaskNameIsNotRunning|Don't process this source if a task with this name is running, e.g. I use Thunderbird for email which holds on to the files email are stored in, so I have this set to 'Thunderbird' for that source directory.<br><br>Any running task found with this string in the name will prevent this directory being processed and generate a warning.|
|IsFairlyStatic|Indicates whether this source is something that changes a lot, e.g... source code as opposed to sets of files that are occasionally added to but not often changed like movies and photos.<br><br>This doesn't stop it being archived, but means you can set up archive jobs to choose whether to process this source based on the job ArchiveFairlyStatic setting.|
|CompressionLevel|What type of compression to use on creating zip files, options are;<br>0: Optimal<br>1: Fastest<br>2: NoCompression<br>3: SmallestSize<br>For full details see the [Microsoft documentation](https://docs.microsoft.com/en-us/dotnet/api/system.io.compression.zipfile.createfromdirectory?view=net-5.0).|
|ReplaceExisting|Overwrite any output files with the same name as one being created.|
|EncryptOutput|Encrypt the output file after zipping, uses AESEncrypt at the moment, see the [AESCrypt](#AESCrypt) section above for setup instructions.<br><br>You need to install it manually and put the path to the exe in the AESEncryptPath setting in appsettings.json.<br><br>The reason it doesn't use built-in .Net encryption is because I use the AESEncrypt Explorer extension so want to encrypt files with the same code.|
|DeleteArchiveAfterEncryption|Delete the unencrypted zip archive after successful encryption.|
|OutputFileName|The name of the zipped output file (no path), if not specified it uses the path to generate the name so directory 'C:\AbC\DeF\GhI' will be archived to 'AbC-DeF-GhI.zip'.<br><br>For ease of use and clarity it's best to default this unless you really want to set the name to something else.|
|AddVersionSuffix|Adds a suffix to the file name of the form '-nnnn' before the extension, each new file adds 1 to the number. Archiving 'C:\Blah' with the default OutputFileName setting results in files 'Blah-0001.zip', 'Blah-0002.zip' etc..<br><br>This works alongside RetainVersions and RetainDaysOld to limit the number of these which it keeps.|
|MinutesOldThreshold|Set this to more than zero to have the system ignore new and altered files until they are this old. This allows you to stop the system making many archives of files that change frequently if you run archiver often.<br><br>For example, you might set a directory to 60 so it only archives files in there that were created or changed over an hour ago.|

## Archive directories

Settings that only apply to archive directories.

None, archive directories just have the shared settings, above.