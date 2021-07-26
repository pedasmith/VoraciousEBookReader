using System;
using System.Threading.Tasks;
using System.Xml.XPath;
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
        public class GetFolderResult
        {
            public enum Reason {  Ok, NotSet, NoSuchFolder };
            public Reason GetResult { get; set; } = Reason.Ok;
            public StorageFolder Folder { get; set; } = null;
            public static GetFolderResult CreateFromFolder(StorageFolder folder)
            {
                return new GetFolderResult() { Folder = folder, GetResult = Reason.Ok, };
            }
            public static GetFolderResult CreateFromError(Reason result)
            {
                return new GetFolderResult() { Folder = null, GetResult = result, };
            }
        }
        public static async Task<GetFolderResult> GetFolderSilentAsync()
        {
            if (!StorageApplicationPermissions.FutureAccessList.ContainsItem(EBOOKREADERFOLDERFILETOKEN)) return GetFolderResult.CreateFromError (GetFolderResult.Reason.NotSet);
            try
            {
                var folder = await StorageApplicationPermissions.FutureAccessList.GetFolderAsync(EBOOKREADERFOLDERFILETOKEN);
                return GetFolderResult.CreateFromFolder(folder);
            }
            catch (Exception)
            {

            }
            return GetFolderResult.CreateFromError(GetFolderResult.Reason.NoSuchFolder);
        }

        public static async Task LaunchExplorerAtFolderAsync()
        {
            var result = await GetFolderSilentAsync();
            switch (result.GetResult)
            {
                case GetFolderResult.Reason.NoSuchFolder:
                    {
                        var dlg = new MessageDialog("the eBook Reader folder cannot be reached. Is the eBook connected?");
                        await dlg.ShowAsync();
                    }
                    break;
                case GetFolderResult.Reason.NotSet:
                    {
                        var dlg = new MessageDialog("No eBook Reader folder has been saved. The Send To eBook Reader command will set this value.");
                        await dlg.ShowAsync();
                    }
                    break;
                case GetFolderResult.Reason.Ok:
                    var folder = result.Folder;
                        var launchOptions = new FolderLauncherOptions();
                        launchOptions.DesiredRemainingView = Windows.UI.ViewManagement.ViewSizePreference.UseMore;
                        await Launcher.LaunchFolderAsync(folder, launchOptions);
                    break;
            }
        }
    }
}
