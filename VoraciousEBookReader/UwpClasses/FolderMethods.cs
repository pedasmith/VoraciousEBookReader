using PCLStorage;
using PCLStorage.Exceptions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Storage; // Is in a UWP-only directory

namespace SimpleEpubReader.UwpClasses
{
    public static class FolderMethods
    {
        const string DownloadFolder = "download";

        public static string InstallationFolder
        {
            get
            {
                StorageFolder returnFolder = Windows.ApplicationModel.Package.Current.InstalledLocation;
                return returnFolder.Path;
            }
        }
        public static string LocalFolder
        {
            get
            {
                StorageFolder returnFolder = Windows.Storage.ApplicationData.Current.LocalFolder;
                return returnFolder.Path;
            }
        }

        public static string LocalCacheFolder
        {
            get
            {
                StorageFolder returnFolder = Windows.Storage.ApplicationData.Current.LocalCacheFolder;
                return returnFolder.Path;
            }
        }
        public static async Task<string> GetScreenShotsFolderAsync()
        {
            StorageFolder installationFolder = Windows.ApplicationModel.Package.Current.InstalledLocation;
            var helpScreenShot = await installationFolder.GetFolderAsync(@"Assets\HelpScreenShots");
            return helpScreenShot.Path;
        }

        public static async Task<IFolder> EnsureDownloadFolder()
        {
            try
            {
                var cache = ApplicationData.Current.LocalCacheFolder;
                var root = await FileSystem.Current.GetFolderFromPathAsync(cache.Path);
                var folder = await root.CreateFolderAsync(DownloadFolder, PCLStorage.CreationCollisionOption.OpenIfExists);
                return folder;
            }
            catch (Exception)
            {
                return null; // really?
            }
            //Original windows-only code
            //var root = ApplicationData.Current.LocalCacheFolder;
            //var folder = await root.CreateFolderAsync(DownloadFolder, CreationCollisionOption.OpenIfExists);
            //return folder;
        }

        public static async Task<IFile> GetFileAsync (IFolder folder, string filename)
        {
            try
            {
                var file = await folder.GetFileAsync(filename);
                return file;
            }
            catch (FileNotFoundException)
            {
                return null;
            }
            catch (Exception ex)
            {
                App.Error($"ERROR: GetFileAsync threw unexpected exception {ex.Message} for filename {filename}");
            }
            return null;
        }
    }
}
