Archivist readme
================

There is no friendly interface to set things up, if you are not comfortable with editing JSON files then
this program is probably not for you, sorry about that.

The program will not work after being installed, you first need to tell it which folders you want it to 
archived, where to put the archives and optionally, where to copy them to from there.

The program behaves according to settings in 2 files, both can be found in the directory you installed the 
software in, which may be 'C:\Program Files (x86)\Archivist', but you might have told the installer to 
set up the program somewhere else.

appsettings.json
----------------
This defines the basic settigs that allow the program to run, for the dsimplest setup, you will not need 
to alter this initially. 

To tell it to log to text files in a folder, put the full path to the folder into the 'LogDirectory' 
setting, it will try to create the directory if it does not exist.

configuration.json
------------------
This is the file that defines the jobs that you can run, you will definitely need to customise this one.

See documentation at https://github.com/silentdiverchris/Archivist#readme for full details.