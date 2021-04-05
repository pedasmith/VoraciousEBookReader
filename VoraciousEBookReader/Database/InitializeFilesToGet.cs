using Microsoft.EntityFrameworkCore;
using Microsoft.Toolkit.Uwp.Helpers;
using SharpCompress.Readers;
using SimpleEpubReader.Controls;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Net.Http;
using SimpleEpubReader.UwpDialogs;
using SimpleEpubReader.UwpClasses;
using System.ComponentModel.DataAnnotations;
using PCLStorage;

namespace SimpleEpubReader.Database
{

    class InitializeFilesToGet
    {
        /// <summary>
        /// Return TRUE if the local database doesn't exist. This will be true only for the first
        /// time the app is run.
        /// </summary>
        /// <returns></returns>
        public static bool GetMustCopyInitialDatabase()
        {
            var destinationFolder = Windows.Storage.ApplicationData.Current.LocalFolder;
            var path = destinationFolder.Path;
            var task = destinationFolder.FileExistsAsync(BookDataContext.BookDataDatabaseFilename);
            task.Wait();
            var retval = !task.Result;
            return retval;
        }

        /// <summary>
        /// Called just once when the app is first run to move the read-only zipped database into
        /// the final read/write position.
        /// </summary>
        /// <param name="progressBar"></param>
        /// <returns></returns>
        public static async Task CopyAssetDatabaseIfNeededAsync(UwpRangeConverter progressBar)
        {
            var destinationFolder = FolderMethods.LocalFolder;
            var destinationFolderFolder = await PCLStorage.FileSystem.Current.GetFolderFromPathAsync(destinationFolder);
            var destinationFolderPath = destinationFolder;
            //var bookdbfullpath = destinationFolder + @"\" + BookDataContext.BookDataDatabaseFilename;
            var exists = await destinationFolderFolder.CheckExistsAsync(BookDataContext.BookDataDatabaseFilename);

            var existingFile = await FolderMethods.GetFileAsync(destinationFolderFolder, BookDataContext.BookDataDatabaseFilename);
            bool copyFileAsNeeded = true; // CHECK: this should always be true when shipping! 
            // (It's sometimes set FALSE when debugging the asset code)
            if (exists != ExistenceCheckResult.FileExists && copyFileAsNeeded)
            {
                progressBar.Minimum = 0;
                progressBar.Maximum = 1;
                progressBar.Value = 0;

                var initialFullPath = FolderMethods.InstallationFolder + @"\" + @"Assets\InitialBookData.Zip";
                var zipfile = await PCLStorage.FileSystem.Current.GetFileFromPathAsync(initialFullPath);
                System.Diagnostics.Debug.WriteLine($"Moving initial db: to={destinationFolder}");

                // Its a zipped copy; grab the inner file and expand it out!
                // Can't just copy the file: await originalInstalledFile.CopyAsync(destinationFolder, BookDataContext.BookDataDatabaseFilename);

                using (var stream = await zipfile.OpenAsync(PCLStorage.FileAccess.Read))
                {
                    using (var reader = ReaderFactory.Open(stream))
                    {
                        while (reader.MoveToNextEntry())
                        {
                            System.Diagnostics.Debug.WriteLine($"INIT ZIPREAD: {reader.Entry.Key} size {reader.Entry.Size}");

                            progressBar.Maximum = reader.Entry.Size;
                            progressBar.Value = 0;

                            var filestream = reader.OpenEntryStream();
                            var startfile = reader.Entry.Key;
                            var finalfile = startfile;
                            if (finalfile == "InitialBookData.Db")
                            {
                                finalfile = "BookData.Db";
                            }
                            var outfile = await destinationFolderFolder.CreateFileAsync (finalfile, CreationCollisionOption.ReplaceExisting);
                            var outstream = await outfile.OpenAsync (PCLStorage.FileAccess.ReadAndWrite);
                            byte[] buffer = new byte[1_000_000]; // 1 meg at a time
                            bool keepGoing = true;
                            int totalSize = 0;
                            while (keepGoing)
                            {
                                var nbytes = await filestream.ReadAsync(buffer, 0, buffer.Length);
                                totalSize += nbytes;
                                progressBar.Value = totalSize;
                                if (nbytes == 0)
                                {
                                    keepGoing = false;
                                }
                                else
                                {
                                    outstream.Write(buffer, 0, nbytes);
                                }
                            }
                            ;
                        }
                    }
                }
            }
        }


        public async Task CreateDatabaseAsync(BookDataContext bookdb)
        {
            var n = await RdfReader.ReadZipTarRdfFile(bookdb);
            var folderPath = FolderMethods.InstallationFolder;
            var folder = await PCLStorage.FileSystem.Current.GetFolderFromPathAsync(folderPath);
            var preinstallFolder = await folder.GetFolderAsync(@"Assets\PreinstalledBooks");

            //StorageFolder installationFolder = Windows.ApplicationModel.Package.Current.InstalledLocation;
            //var preinstallFolder = await installationFolder.GetFolderAsync(@"Assets\PreinstalledBooks");
            await MainPage.MarkAllDownloadedFiles(bookdb, preinstallFolder);
        }

        /// <summary>
        /// Given the InitialBookIds from Gutenberg, download each one into the Assets/PreinstalledBooks folder. Does a pause between 
        /// each download to be nice, so it's a slow process. As of 2021-04-04, this is reduced to just 5 books.
        /// </summary>
        /// <param name="bookdb"></param>
        /// <returns></returns>

        public async Task<int> DownloadBooksAsync(BookDataContext bookdb)
        {
            int n = 0;
            var hc = new HttpClient();

            var rootPath = FolderMethods.LocalCacheFolder;
            var root = await PCLStorage.FileSystem.Current.GetFolderFromPathAsync(rootPath);
            var folder = await root.CreateFolderAsync("initial_books", CreationCollisionOption.OpenIfExists);

            IQueryable<BookData> includeList = bookdb.Books
                .Include(b => b.People)
                .Include(b => b.Files)
                .AsQueryable();
            var list = includeList.ToList();
            foreach (var bookId in InitialBookIds)
            {
                var book = list.Where(b => b.BookId == bookId).FirstOrDefault();
                if (book == null)
                {
                    App.Error($"Initialize Error: Unable to get entry for {bookId}");
                    continue;
                }
                var bestVersion = FilenameAndFormatData.GetProcessedFileList(book.Files.ToList()).FirstOrDefault();
                if (bestVersion == null)
                {
                    App.Error($"Initialize Error: Unable to get file entry for {bookId}");
                    continue;
                }
                var fileName = bestVersion.FileName;
                if (fileName.StartsWith("https:") && fileName.Contains("gutenberg.org"))
                {
                    // See the fail in BookCard.xaml.cs for details.
                    fileName = fileName.Replace("https://", "http://");
                }
                Uri uri = null;
                var status = Uri.TryCreate(fileName, UriKind.Absolute, out uri);
                if (!status)
                {
                    App.Error($"Initialize Error: Internal error: {fileName} is invalid");
                    continue;
                }
                var (outfilename, ext) = FileWizards.UriWizard.GetUriFilename(uri);
                var preferredFilename = book.GetBestTitleForFilename() + ext;

                var existsFile = await FolderMethods.GetFileAsync(folder, preferredFilename);
                if (existsFile != null)
                {
                    App.Error($"Initialize Error: already have {preferredFilename} for {uri}");
                    continue;
                }
                IFile outfile;
                try
                {
                    outfile = await folder.CreateFileAsync(preferredFilename, CreationCollisionOption.FailIfExists);
                }
                catch (Exception ex)
                {
                    App.Error($"Initialize Error: Unable to make filename for {preferredFilename} because {ex.Message}");
                    continue;
                }


                System.Diagnostics.Debug.WriteLine($"NOTE: will download {preferredFilename} from {uri}");

                try
                {
                    var responseMessage = await hc.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead);
                    if (!responseMessage.IsSuccessStatusCode)
                    {
                        App.Error($"Initialize Error: Download error for {uri}  because {responseMessage.ReasonPhrase}");
                        continue;

                    }
                    var buffer = await responseMessage.Content.ReadAsByteArrayAsync(); //.ReadAsBufferAsync(); // gets entire buffer at once.

                    if (buffer.Length > 2000)
                    {
                        await FileMethods.WriteBytesAsync(outfile, buffer);
                        // // // TODO: mark in the database! CommonQueries.DownloadedBookEnsureFileMarkedAsDownloaded(book.BookId, folder.Path, outfile.Name);
                    }
                    else
                    {
                        App.Error($"Initialize Error: Download file error for {uri} -- too small");
                        continue;
                    }
                }
                catch (Exception ex)
                {
                    App.Error($"Initialize Error: Exception on downloading {uri} -- {ex.Message}");
                    continue;
                }

                n++;

                await Task.Delay(5_000); // pause betweeen downloads to be nice.
            }

            return n;
        }


        /// <summary>
        /// List of books in the Gutenberg Top 100, minus a few.
        /// </summary>
        public string[] InitialBookIds =
        {
            "ebooks/11",
            "ebooks/23",
            "ebooks/863",
            "ebooks/1661",
            "ebooks/2680",
        };

        /// <summary>
        /// Original long List of books in the Gutenberg Top 100, minus a few.
        /// </summary>
        public string[] InitialBookIdsTop100 =
        {
            "ebooks/100",
            "ebooks/1064",
            "ebooks/1080",
            "ebooks/11",
            "ebooks/113",
            "ebooks/1184",
            "ebooks/120",
            "ebooks/1228",
            "ebooks/1232",
            "ebooks/1250",
            "ebooks/1260",
            "ebooks/1322",
            "ebooks/1342",
            "ebooks/135",
            "ebooks/1399",
            "ebooks/140",
            "ebooks/1400",
            "ebooks/14838",
            "ebooks/1497",
            "ebooks/158",
            "ebooks/16",
            "ebooks/160",
            "ebooks/161",
            "ebooks/16328",
            "ebooks/1635",
            "ebooks/1661",
            "ebooks/1727",
            "ebooks/17396",
            "ebooks/174",
            "ebooks/1952",
            "ebooks/19942",
            "ebooks/1998",
            "ebooks/203",
            "ebooks/205",
            "ebooks/2097",
            "ebooks/215",
            "ebooks/219",
            "ebooks/23",
            "ebooks/236",
            "ebooks/23700",
            "ebooks/244",
            "ebooks/2500",
            "ebooks/25344",
            "ebooks/2542",
            "ebooks/25525",
            "ebooks/2554",
            "ebooks/2591",
            "ebooks/2600",
            "ebooks/2680",
            "ebooks/2701",
            // "ebooks/27827", sightly racy
            "ebooks/28054",
            "ebooks/2814",
            "ebooks/2852",
            "ebooks/28860",
            "ebooks/3207",
            "ebooks/3296",
            "ebooks/345",
            "ebooks/35",
            "ebooks/36",
            "ebooks/3600",
            "ebooks/375",
            "ebooks/376",
            "ebooks/3825",
            "ebooks/4077",
            "ebooks/408",
            "ebooks/42108",
            "ebooks/42324",
            "ebooks/42686",
            "ebooks/43",
            "ebooks/4300",
            "ebooks/4363",
            "ebooks/43936",
            "ebooks/45",
            "ebooks/46",
            "ebooks/514",
            "ebooks/5200",
            "ebooks/521",
            "ebooks/52521",
            "ebooks/55",
            // "ebooks/5740", is only available as a PDF
            "ebooks/58585",
            // "ebooks/58975", this is just an index
            "ebooks/6130",
            "ebooks/730",
            "ebooks/74",
            "ebooks/76",
            "ebooks/766",
            "ebooks/768",
            "ebooks/829",
            "ebooks/84",
            "ebooks/844",
            "ebooks/863",
            // "ebooks/8800", an index file (dante?)
            "ebooks/98",
            "ebooks/996",
        };
    }
}
