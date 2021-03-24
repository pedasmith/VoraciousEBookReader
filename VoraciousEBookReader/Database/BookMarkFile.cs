using Microsoft.Toolkit.Uwp.Helpers;
using Newtonsoft.Json;
using SimpleEpubReader.UwpClasses;
using SimpleEpubReader.UwpDialogs;
using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Windows.Storage;

// See https://github.com/Microsoft/Windows-universal-samples/tree/master/Samples/FileAccess
// for how we use the future access and most recently used (MRU)
// MRU: StorageApplicationPermissions.MostRecentlyUsedList
// Future access list: StorageApplicationPermissions.FutureAccessList
// 
namespace SimpleEpubReader.Database
{
    /// <summary>
    /// Smart class to handle bookmarks. Smarts include keeping track of the file in the 
    /// MRU list and the FutureAccess list
    /// 
    /// </summary>
    public class BookMarkFile
    {
        public enum BookMarkFileType { RecentOnly, FullFile };

        public DateTimeOffset SaveTime { get; set; } = DateTimeOffset.Now;
        public string SavedFromName { get; set; } = ThisComputerName();
        public List<BookData> BookMarkList { get; set; }


        public static async Task<StorageFolder> SetSaveFolderAsync()
        {
            // First tell people what they are about to do
            var showResult = await SimpleDialogs.HowToPickSaveFolder();
            if (showResult == SimpleDialogs.Result.Cancel) return null;

            var folder= await BookmarkFileDirectory.PickBookmarkFolderAsync();
            if (folder != null)
            {
                BookmarkFileDirectory.UpdateOrReplaceFutureAccessList(folder);
                BookmarkFileDirectory.ResetCachedFileTimes();
            }
            return folder;
        }
        /// <summary>
        /// Gets the bookmark folder, incredibly rarely by asking the user to pick a folder.
        /// </summary>
        /// <param name="forceUserPick"></param>
        /// <returns>Storage folder to use; can be null in case of horrific error</returns>




        /// <summary>
        /// Returns the number of files in the folder that are newer than recorded.
        /// </summary>
        /// <returns></returns>
        public static async Task<int> SmartReadAsync()
        {
            int retval = 0;
            var folder = await BookmarkFileDirectory.GetBookmarkFolderAsync();
            if (folder == null)
            {
                folder = await SetSaveFolderAsync();
            }
            if (folder == null) return retval;

            var fileTimes = BookmarkFileDirectory.GetCachedFileTimes();
            var bookmarkfiles = await BookmarkFileDirectory.GetAllBookmarkFilesSortedAsync(folder);
            var preferredName = ThisComputerName() + ".recent" + BookmarkFileDirectory.EXTENSION; // is a JSON file

            foreach (var bookmarkfilename in bookmarkfiles)
            {
                var modified = await BookmarkFileDirectory.GetLastModifiedDateAsync(folder, bookmarkfilename);

                var lastDateRead = DateTimeOffset.MinValue; 
                // Set to very small so if the value can't be read, the default is such that the
                // file will be read. Will fail in the corner case of the bookmark file being
                // given an absurd file time.
                if (fileTimes.ContainsKey (bookmarkfilename))
                {
                    try
                    {
                        lastDateRead = (DateTimeOffset)fileTimes[bookmarkfilename];
                    }
                    catch (Exception)
                    {
                        App.Error($"Getting last read file time for {bookmarkfilename} isn't a DateTimeOffset?");
                    }
                }
                if (modified > lastDateRead && bookmarkfilename != preferredName)
                {
                    retval++;
                    var bmf = await ReadFileAsBookMarkFileAsync(folder, bookmarkfilename);
                    if (bmf != null)
                    {
                        var nchanges = await MergeAsync(bmf);
                        fileTimes[bookmarkfilename] = modified;
                    }
                    else
                    {
                        App.Error($"Unable to read bookmark file {bookmarkfilename}");
                    }
                }
            }
            BookmarkFileDirectory.SaveCachedFileTimes(fileTimes);
            return retval;
        }

        /// <summary>
        /// Given a storage file, returns a read-in book mark file object. This is a little
        /// harder than it looks (and hence it's beeing a class.
        /// </summary>
        /// <param name="file"></param>
        /// <returns></returns>
        private static async Task<BookMarkFile> ReadFileAsBookMarkFileAsync(StorageFolder folder, string bookmarkfilename)
        {
            var str = await BookmarkFileDirectory.ReadFileAsync(folder, bookmarkfilename);

            BookMarkFile bmf = null;
            try
            {
                // Try to be a little smart just so that I get fewer exceptions. Old files start with [ because they are just
                // arrays; the new file starts with { because it's an object.
                // "old": version used for a couple of weeks early in 2020
                // "new" every other version
                if (!str.StartsWith("["))
                {
                    bmf = JsonConvert.DeserializeObject<BookMarkFile>(str);
                }
            }
            catch (Exception)
            {

            }
            if (bmf == null)
            {
                // Try to read files which are just a raw list of books.
                try
                {
                    var list = JsonConvert.DeserializeObject<List<BookData>>(str);
                    if (list != null)
                    {
                        bmf = new BookMarkFile()
                        {
                            SavedFromName = "unknown-computer",
                            SaveTime = DateTimeOffset.FromUnixTimeSeconds(24 * 60 * 60),
                            BookMarkList = list,
                        };
                    }
                }
                catch (Exception)
                {

                }
            }
            return bmf;
        }


        /// <summary>
        /// Merges the changes from a single read-in bookmarkfile into the local database.
        /// </summary>
        /// <param name="bmf"></param>
        /// <returns></returns>
        private static async Task<int> MergeAsync(BookMarkFile bmf)
        {
            int nchanges = 0;
            // Now let's be very smart about combining this file in with the original.
            var bookdb = BookDataContext.Get();
            var currbooks = CommonQueries.BookGetAllWhichHaveUserData(bookdb);
            const int CHANGES_PER_SAVE = 1000;
            int nextDbSaveChange = CHANGES_PER_SAVE;
            foreach (var external in bmf.BookMarkList)
            {
                var book = currbooks.Find(b => b.BookId == external.BookId);
                if (book == null) book = CommonQueries.BookGet(bookdb, external.BookId);
                if (book == null)
                {
                    // Prepend the BookMarkSource so that the book is clearly labeled
                    // as being from a bookmark file (and therefore this is kind of a fake entry)
                    if (!external.BookSource.StartsWith (BookData.BookSourceBookMarkFile))
                    {
                        external.BookSource = BookData.BookSourceBookMarkFile + external.BookSource;
                    }
                    // Must set all these ids to zero so that they get re-set by EF.
                    if (external.Review != null) external.Review.Id = 0;
                    if (external.Notes != null)
                        {
                        external.Notes.Id = 0;
                        foreach (var note in external.Notes.Notes)
                        {
                            note.Id = 0;
                        }
                    }
                    if (external.NavigationData != null) external.NavigationData.Id = 0;
                    external.DownloadData = null; // on this computer, nothing has been downloaded.

                    CommonQueries.BookAdd(bookdb, external, CommonQueries.ExistHandling.IfNotExists);
                    nchanges++;
                    App.Error($"NOTE: adding external {external.BookId} name {external.Title}");
                }
                else
                {
                    // Great -- now I can merge the UserReview, Notes, and BookNavigationData.
                    int nbookchanges = 0;
                    if (external.Review != null)
                    {
                        if (book.Review == null)
                        {
                            external.Review.Id = 0; // clear it out so that EF will set to the correct value.
                            book.Review = external.Review;
                            nbookchanges++;
                        }
                        else
                        {
                            nbookchanges += book.Review.Merge(external.Review);
                        }
                    }

                    if (external.NavigationData != null)
                    {
                        if (book.NavigationData == null)
                        {
                            external.NavigationData.Id = 0; // clear it out so that EF will set to the correct value.
                            book.NavigationData = external.NavigationData;
                            nbookchanges++;
                        }
                        else
                        {
                            nbookchanges += book.NavigationData.Merge(external.NavigationData);
                        }
                    }

                    if (external.Notes != null)
                    {
                        if (book.Notes == null)
                        {
                            // Copy them all over
                            book.Notes = new BookNotes()
                            {
                                BookId = external.Notes.BookId,
                            };
                            foreach (var note in external.Notes.Notes)
                            {
                                note.Id = 0; // reset to zero to insert into the currrent book.
                                book.Notes.Notes.Add(note);
                            }
                            nbookchanges++;
                        }
                        else
                        {
                            // Add in only the changed notes. The ids will not be the same
                            nbookchanges += book.Notes.Merge(external.Notes);
                        }
                    }

                    if (nbookchanges > 0)
                    {
                        ; // hook to hang the debugger on.
                    }
                    nchanges += nbookchanges;

                    if (nchanges > nextDbSaveChange)
                    {
                        await bookdb.SaveChangesAsync();
                        nextDbSaveChange = nchanges + CHANGES_PER_SAVE;
                    }
                }
            }

            // And save at the end!
            if (nchanges > 0)
            {
                await bookdb.SaveChangesAsync();
            }

            return nchanges;
        }
        public static async Task SmartSaveAsync(BookMarkFileType saveType)
        {
            var preferredName = saveType == BookMarkFileType.RecentOnly
                ? ThisComputerName() + ".recent" + BookmarkFileDirectory.EXTENSION
                : "FullBookmarkFile" + BookmarkFileDirectory.EXTENSION
                ;
            StorageFolder folder = null;
            try
            {
                folder = await BookmarkFileDirectory.GetBookmarkFolderAsync();
                if (folder == null)
                {
                    folder = await SetSaveFolderAsync();
                }
                if (folder == null) return;

                if (folder != null)
                {
                    // Make sure the files are in sync first
                    int nread = await SmartReadAsync();
                    System.Diagnostics.Debug.WriteLine($"Smart save: smart read {nread} files");

                    var bmf = CreateBookMarkFile(saveType);
                    var str = bmf.AsFileString();
                    await BookmarkFileDirectory.WriteFileAsync(folder, preferredName, str);
                }

            }
            catch (Exception ex)
            {
                App.Error($"Unable to save file {preferredName} to folder {folder} exception {ex.Message}");
            }
        }

        public static BookMarkFile CreateBookMarkFile(BookMarkFileType fileType)
        {
            var retval = new BookMarkFile();
            var bookdb = BookDataContext.Get();

            var list = fileType == BookMarkFileType.FullFile
                ? CommonQueries.BookGetAllWhichHaveUserData(bookdb)
                : CommonQueries.BookGetRecentWhichHaveUserData(bookdb);

            // We only save some of the BookData fields in a book mark file. 
            // Don't bother with the full file list (total waste of time), or the people list.
            var trimmedList = new List<BookData>();
            foreach (var book in list)
            {
                trimmedList.Add(CreateBookMarkBookData(book));
            }

            retval.BookMarkList = trimmedList;
            return retval;
        }

        /// <summary>
        /// A full BookData has too much data to write into the BookMark file. Trim it down
        /// by removing elements that are never changed by the user and aren't needed to
        /// merge into the other computer's databases
        /// </summary>
        /// <param name="source"></param>
        /// <returns></returns>
        private static BookData CreateBookMarkBookData(BookData source)
        {
            BookData retval = new BookData()
            { 
                BookId = source.BookId,
                Title = source.Title,
                BookSource = source.BookSource,
                BookType = source.BookType,
                Issued = source.Issued,
                Language = source.Language,
                DenormDownloadDate = source.DenormDownloadDate,
                DenormPrimaryAuthor = source.DenormPrimaryAuthor,

                // Complex structures to be copied
                NavigationData = source.NavigationData,
                Notes = source.Notes,
                Review = source.Review,
            };
            // DownloadData, Files and People are explicity kept blank
            return retval;
        }
        public string AsFileString()
        {
            var retval = JsonConvert.SerializeObject(this, Formatting.Indented);
            return retval;
        }
        public static string ThisComputerName()
        {
            string retval = null;
            retval = Dns.GetHostName(); // Seems to work and return a useful short name

            // Fix up the computer name so it doesn't contain invalid chars.
            retval = AsValidFilename(retval);
            return retval;
        }

        /// <summary>
        /// Fix up a string so that it will be a valid file name. It will be limited to 20 chars,
        /// won't have a ":", "/", "\", NUL, etc. 
        /// See https://docs.microsoft.com/en-us/windows/win32/fileio/naming-a-file
        /// </summary>
        /// <param name="str"></param>
        /// <returns></returns>
        private static string AsValidFilename(string str)
        {
            if (str == null) return "_";
            str = str.Trim();
            if (str == "") return "_";

            var sb = new StringBuilder();
            foreach (var ch in str)
            {
                if (ch < 20 || ch == 0x7F || ch == '<' || ch == '>' || ch == ':' || ch == '/' 
                    || ch == '\\' || ch == '|' || ch == '?' || ch == '*'
                    || ch == '\'' || ch == '"')
                {
                    sb.Append('_');
                }
                else
                {
                    sb.Append(ch);
                }
            }
            var retval = sb.ToString().TrimEnd ('.');
            if (retval.Length > 20) retval = retval.Substring(0, 20);

            // These are all reserved words. I'm actually grabbing a superset of reserved workds;
            // technically you can call a file consequence.txt but I disallow it.
            var upper = retval.ToUpper();
            if (upper.StartsWith ("CON") || upper.StartsWith("PRN") || upper.StartsWith("AUX")
                || upper.StartsWith("COM") || upper.StartsWith("LPT"))
            {
                var suffix = (retval.Length == 3) ? "" : retval.Substring(3);
                retval = retval.Substring(0, 3) + "_" + suffix;
            }

            return retval;
        }

        public static int TestAsValidFilename()
        {
            int nerror = 0;
            var name = ThisComputerName();
            nerror += TestAsValidFilenameOne(null, "_");
            nerror += TestAsValidFilenameOne("", "_");
            nerror += TestAsValidFilenameOne("  _   ", "_");
            nerror += TestAsValidFilenameOne("  ABC.TXT   ", "ABC.TXT");
            nerror += TestAsValidFilenameOne("abcdef", "abcdef");
            nerror += TestAsValidFilenameOne("abcdef.", "abcdef");
            nerror += TestAsValidFilenameOne("abc/cd/ef", "abc_cd_ef");
            nerror += TestAsValidFilenameOne("abc\\def\\ghi", "abc_def_ghi");
            nerror += TestAsValidFilenameOne("abc\"def", "abc_def");
            nerror += TestAsValidFilenameOne("con", "con_");
            nerror += TestAsValidFilenameOne("con:", "con__");
            nerror += TestAsValidFilenameOne("clock:", "clock_");
            nerror += TestAsValidFilenameOne("con.txt", "con_.txt");
            return nerror;
        }
        private static int TestAsValidFilenameOne(string value, string expected)
        {
            int nerror = 0;
            var actual = AsValidFilename(value);
            if (actual != expected)
            {
                App.Error($"TEST: AsValidFilename({value}) expected {expected} but got {actual}");
                nerror++;
            }
            return nerror;
        }
    }
}
