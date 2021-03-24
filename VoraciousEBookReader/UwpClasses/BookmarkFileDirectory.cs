using Microsoft.Toolkit.Uwp.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.AccessCache;
using Windows.Storage.Pickers;
using Windows.Storage.Search;
using Windows.Storage.Streams;
using Windows.UI.WebUI;
using Windows.UI.Xaml.Controls.Maps;

/// <summary>
/// WARNING: these had used Folder, then switched to strings. But those don't actually work when the storage folder
/// doesn't natively have permissions!
/// </summary>

namespace SimpleEpubReader.UwpClasses
{
    class BookmarkFileDirectory
    {
        public const string BOOKMARKFILETOKEN = "BookMarkFile.V3"; //V1 & V2 was used for less than a month
        public const string EXTENSION = ".bookmark";
        const string CachedFileTimeName = "BOOKMARK_READ_TIMES";
        public static async Task<string> GetBookmarkFolderPathAsync()
        {
            StorageFolder folder = null;
            try
            {
                if (StorageApplicationPermissions.FutureAccessList.ContainsItem(BookmarkFileDirectory.BOOKMARKFILETOKEN))
                {
                    folder = await StorageApplicationPermissions.FutureAccessList.GetFolderAsync(BookmarkFileDirectory.BOOKMARKFILETOKEN);
                }
                else
                {
                    folder = ApplicationData.Current.LocalFolder;
                }
            }
            catch (Exception)
            {
                folder = null;
            }
            return folder?.Path;
        }
        public static async Task<StorageFolder> GetBookmarkFolderAsync()
        {
            StorageFolder folder = null;
            try
            {
                if (StorageApplicationPermissions.FutureAccessList.ContainsItem(BookmarkFileDirectory.BOOKMARKFILETOKEN))
                {
                    folder = await StorageApplicationPermissions.FutureAccessList.GetFolderAsync(BookmarkFileDirectory.BOOKMARKFILETOKEN);
                }
                else
                {
                    folder = ApplicationData.Current.LocalFolder;
                }
            }
            catch (Exception)
            {
                folder = null;
            }
            return folder;
        }
        /// <summary>
        /// Gets a list of when each different bookmark file was read in last. This lets us
        /// efficiently keep all bookmark files up to date without having to constantly
        /// reread all of them. See also the Reset and the Save items
        /// </summary>
        /// <returns></returns>

        public static Dictionary<string,DateTimeOffset> GetCachedFileTimes()
        {
            var retval = new Dictionary<string, DateTimeOffset>();
            ApplicationDataCompositeValue cachedFileTimes = null;

            var localSettings = ApplicationData.Current.LocalSettings;
            if (localSettings.Values.ContainsKey(CachedFileTimeName))
            {
                try
                {
                    cachedFileTimes = (ApplicationDataCompositeValue)localSettings.Values[CachedFileTimeName];
                }
                catch (Exception)
                {
                    App.Error($"Getting last read file time cache for all files isn't a Composite value?");
                }
            }

            // On error, just completely reset the file times cache.
            if (cachedFileTimes == null)
            {
                cachedFileTimes = new ApplicationDataCompositeValue();
                localSettings.Values[CachedFileTimeName] = cachedFileTimes;
            }
            else
            {
                foreach (var (name,value) in cachedFileTimes)
                {
                    try
                    {
                        retval[name] = (DateTimeOffset)value;
                    }
                    catch(Exception ex)
                    {
                        App.Error($"Getting last read file time cache for all files isn't a DateTimeOffset value for {name}=={value} exception {ex.Message}?");
                    }
                }
            }
            return retval;
        }

        public static async Task<DateTimeOffset> GetLastModifiedDateAsync(StorageFolder folder, string filename)
        {
            var bookmarkfile = await folder.GetFileAsync(filename); //  StorageFile.GetFileFromPathAsync(folder + @"\" + filename);
            var properties = await bookmarkfile.GetBasicPropertiesAsync();
            var modified = properties.DateModified;
            return modified;
        }

        public static async Task<List<string>> GetAllBookmarkFilesSortedAsync(StorageFolder folder)
        {
            var retval = new List<string>();
            // Kind of a lot of work just to get files that are sorted correctly.
            //var folder = await StorageFolder.GetFolderFromPathAsync(folderPath);
            var listOptions = new QueryOptions(CommonFileQuery.DefaultQuery, new List<string>() { EXTENSION });
            listOptions.SortOrder.Clear();
            var se = new SortEntry()
            {
                PropertyName = "System.DateModified",
                AscendingOrder = true,
            };
            listOptions.SortOrder.Add(se);
            var queryResult = folder.CreateFileQueryWithOptions(listOptions);
            var bookmarkfiles = await queryResult.GetFilesAsync();
            foreach (var bookmarkfile in bookmarkfiles)
            {
                retval.Add(bookmarkfile.Name);
            }
            return retval;
        }

        public static async Task<StorageFolder> PickBookmarkFolderAsync()
        {
            StorageFolder folder = null;
            var picker = new FolderPicker()
            {
                CommitButtonText = "Set Bookmark Save Location",
                ViewMode = PickerViewMode.List,
                SuggestedStartLocation = PickerLocationId.DocumentsLibrary,
                SettingsIdentifier = BookmarkFileDirectory.BOOKMARKFILETOKEN,
            };
            picker.FileTypeFilter.Add("*");
            folder = await picker.PickSingleFolderAsync();
            return folder;
        }

        public static void ResetCachedFileTimes()
        {
            ApplicationDataCompositeValue cachedFileTimes = null;

            var localSettings = ApplicationData.Current.LocalSettings;
            if (localSettings.Values.ContainsKey(CachedFileTimeName))
            {
                try
                {
                    cachedFileTimes = (ApplicationDataCompositeValue)localSettings.Values[CachedFileTimeName];
                    cachedFileTimes.Clear();
                    localSettings.Values[CachedFileTimeName] = cachedFileTimes;
                }
                catch (Exception)
                {
                }
            }
        }
        public static void SaveCachedFileTimes(Dictionary<string, DateTimeOffset> cachedFileTimes)
        {
            var localSettings = ApplicationData.Current.LocalSettings;
            var stored = localSettings.Values.ContainsKey(CachedFileTimeName) 
                ? localSettings.Values[CachedFileTimeName] as ApplicationDataCompositeValue
                : null;
            if (stored == null)
            {
                stored = new ApplicationDataCompositeValue();
            }
            foreach (var (name, value) in cachedFileTimes)
            {
                stored[name] = value;
            }
            localSettings.Values[CachedFileTimeName] = stored;
        }

        public static void UpdateOrReplaceFutureAccessList(StorageFolder folder)
        {
           // StorageFolder folder = await StorageFolder.GetFolderFromPathAsync(folderPath);
            StorageApplicationPermissions.FutureAccessList.AddOrReplace(BookmarkFileDirectory.BOOKMARKFILETOKEN, folder);
        }

        public static async Task<String> ReadFileAsync(StorageFolder folder, string filename)
        {
            //var fullpath = folder + @"\" + filename;
            var str = await folder.ReadTextFromFileAsync(filename); // PathIO.ReadTextAsync (FileIO.ReadTextAsync (PathIO.ReadTextAsync(fullpath, Windows.Storage.Streams.UnicodeEncoding.Utf8);
            return str;
        }
        public static async Task WriteFileAsync(StorageFolder folder, string filename, string text)
        {
            //var fullpath = folder + @"\" + filename;
            var file = await folder.CreateFileAsync(filename, CreationCollisionOption.ReplaceExisting);
            using (var stream = await file.OpenAsync(FileAccessMode.ReadWrite))
            {
                var dw = new DataWriter(stream);
                dw.WriteString(text);
                await dw.StoreAsync();
                await dw.FlushAsync();
            }
            //await folder.WriteTextToFileAsync(filename, text, CreationCollisionOption.ReplaceExisting); //  PathIO.WriteTextAsync(fullpath, text, Windows.Storage.Streams.UnicodeEncoding.Utf8);
        }
    }
}
