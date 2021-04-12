using System;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.AccessCache;
using Windows.Storage.Pickers;
using Windows.System;
using Windows.UI.Popups;

namespace SimpleEpubReader.Controls
{
    static class EBookFolder
    {
        public const string EBOOKREADERFOLDERFILETOKEN = "EBOOKREADER_FOLDER";
        /// <summary>
        /// Tells user to pick the folder.
        /// </summary>
        /// <returns></returns>
        public static async Task<StorageFolder> PickFolderAsync()
        {
            var picker = new FolderPicker()
            {
                CommitButtonText = "Pick eBook Reader folder",
                ViewMode = PickerViewMode.List,
                SuggestedStartLocation = PickerLocationId.DocumentsLibrary,
                SettingsIdentifier = EBOOKREADERFOLDERFILETOKEN,
            };
            picker.FileTypeFilter.Add("*");
            var folder = await picker.PickSingleFolderAsync();
            if (folder != null)
            {
                StorageApplicationPermissions.FutureAccessList.AddOrReplace(EBOOKREADERFOLDERFILETOKEN, folder);
            }
            return folder;
        }
        public static async Task<StorageFolder> GetFolderSilentAsync()
        {
            if (!StorageApplicationPermissions.FutureAccessList.ContainsItem(EBOOKREADERFOLDERFILETOKEN)) return null;
            try
            {
                var folder = await StorageApplicationPermissions.FutureAccessList.GetFolderAsync(EBOOKREADERFOLDERFILETOKEN);
                return folder;
            }
            catch (Exception)
            {

            }
            return null;
        }

        public static async Task LaunchExplorerAtFolderAsync()
        {
            var folder = await GetFolderSilentAsync();
            if (folder != null)
            {
                var launchOptions = new FolderLauncherOptions();
                launchOptions.DesiredRemainingView = Windows.UI.ViewManagement.ViewSizePreference.UseMore;
                await Launcher.LaunchFolderAsync(folder, launchOptions);
            }
            else
            {
                var dlg = new MessageDialog("No eBook Reader folder has been saved. The Send To eBook Reader command will set this value.");
                await dlg.ShowAsync();
            }
        }
    }
}
