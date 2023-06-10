using System;
using System.IO;
using System.Linq;
using System.Text.Json.Serialization;

namespace Archivist.Classes
{
    /// <summary>
    /// Properties that relate specifically to a directory
    /// </summary>
    public class BaseDirectory
    {
        private bool _pathInitialised = false;
        private string? _directoryPath = null;

        /// <summary>
        /// If non-null, find a drive with this label and combine with the DirectoryPath 
        /// to determine the directory to be used. Allows for removable drives that get 
        /// mounted with different letters to be identified.
        /// If the DirectoryPath has a drive designation, eg 'D:\Archive', this property 
        /// is ignored (specifically characters 2 and 3 are ':\'), so to use this function, set
        /// this to, say, '4TB External HDD' and the DirectoryPath to 'Archive'
        /// </summary>
        public string? VolumeLabel { get; set; }

        /// <summary>
        /// The path of this directory, some directories are identified by a path name 
        /// and volume label rather than drive letter, so this sets up the drive letter 
        /// on first use where necessary
        /// </summary>
        /// 
        public string? DirectoryPath
        {
            get
            {
                if (!_pathInitialised)
                {
                    // Set this first or the VerifyVolume call will recurse
                    _pathInitialised = true;

                    if (VolumeLabel is not null)
                    {
                        VerifyVolume();
                    }
                }

                return _directoryPath;
            }
            set
            {
                _directoryPath = value;
            }
        }

        /// <summary>
        /// A human description of what this directory is, or what drive it's 
        /// on - eg '128gb USB Key' or 'Where my photos are', this doesn't make anything work or
        /// break anything if left undefined, it's just a reminder for the human.
        /// </summary>
        public string? Description { get; set; }

        /// <summary>
        /// Whether to process this directory in any way
        /// </summary>
        public bool IsEnabled { get; set; } = true;

        /// <summary>
        /// Whether this is on a volume that is removeable, mainly determining whether it should 
        /// be considered an error if the volume it's on can't be found. If it can't be found it
        /// won't even be considered as a warning if this is true.
        /// </summary>
        public bool IsRemovable { get; set; } = false;

        /// <summary>
        /// Process directories in this order, lowest number first (then by directory name alpha)
        /// </summary>
        public short Priority { get; set; } = 99;

        /// <summary>
        /// Only process this directory after this hour starts, zero disables
        /// </summary>
        public short EnabledAtHour { get; set; } = 0;

        /// <summary>
        /// Only process this directory before this hour starts, zero disables
        /// </summary>
        public short DisabledAtHour { get; set; } = 0;

        /// <summary>
        /// Whether this is a slow volume, used in conjunction with config WriteToSlowVolumes so
        /// backup jobs that only read from and write to fast drives can be set up by setting job 
        /// setting ProcessSlowVolumes to false.
        /// </summary>
        public bool IsSlowVolume { get; set; } = false;

        /// <summary>
        /// Whether this directory was found to exist and be readable
        /// </summary>
        [JsonIgnore]
        public bool IsAvailable
        {
            get
            {
                return Directory.Exists(DirectoryPath);
            }
        }

        public void VerifyVolume()
        {
            if (!string.IsNullOrEmpty(VolumeLabel))
            {
                if (DirectoryPath!.Contains(@":\") == false)
                {
                    // Network volume, find the drive letter

                    var drive = DriveInfo.GetDrives().SingleOrDefault(_ => _.VolumeLabel == VolumeLabel);

                    if (drive != null)
                    {
                        DirectoryPath = Path.Join(drive.Name, DirectoryPath);
                    }
                    else
                    {
                        if (IsRemovable)
                        {
                            // That's fine, it's just not mounted/mapped right now
                        }
                        else
                        {
                            throw new Exception($"IdentifyVolume cannot find volume '{VolumeLabel}'");
                        }                        
                    }
                }
                //else
                //{
                //    throw new Exception($"IdentifyVolume found VolumeLabel '{VolumeLabel}' but DirectoryPath '{DirectoryPath}' has a nominated drive");
                //}
            }
        }

        public bool IsToBeProcessed(Job jobSpec)
        {
            if (!IsEnabled)
                return false;

            if (IsRemovable && !IsAvailable)
                return false;

            if (IsSlowVolume && jobSpec.ProcessSlowVolumes == false)
                return false;

            if (EnabledAtHour != 0 || DisabledAtHour != 0)
            {
                var currentHour = DateTime.Now.Hour;

                if (EnabledAtHour != 0 && currentHour < EnabledAtHour)
                    return false;

                if (DisabledAtHour != 0 && currentHour >= DisabledAtHour)
                    return false;
            }

            return true;
        }
    }
}