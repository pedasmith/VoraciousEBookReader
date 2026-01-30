using Microsoft.Toolkit.Uwp.UI.Controls;
using PCLStorage;
using SimpleEpubReader.Controls;
using SimpleEpubReader.Database;
using SimpleEpubReader.FileWizards;
using SimpleEpubReader.UwpClasses;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Windows.ApplicationModel.Activation;
using Windows.ApplicationModel.DataTransfer;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.Storage; // hard to replace because of file permissions
using SimpleEpubReader.EbookReader;
using EpubSharp;
using Windows.Storage.Pickers;
using System.Net.Http;
using System.Threading;
using Windows.Storage.Streams;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.UI.Xaml.Media;
using Windows.UI.Core;
using Windows.Foundation;

// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace SimpleEpubReader
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page, Navigator.ISetAppColors, SimpleBookHandler, ISetImages
    {
        ProjectRomeActivity RomeActivity = new ProjectRomeActivity();
        public MainPage()
        {
            Logger.Log("MainPage:Constructor: called");
            //SLOW: Inititalizing components takes 2020 milliseconds
            this.InitializeComponent();

            Logger.LogBlock = uiLog;
            Logger.Log("MainPage:Constructor: called and components initialized");
            Nav.AddNavigateTo(Navigator.NavigateControlId.ChapterDisplay, uiChapterControl);
            Nav.AddNavigateTo(Navigator.NavigateControlId.MainReader, uiReaderControl);
            Nav.AddNavigateTo(Navigator.NavigateControlId.ProjectRome, RomeActivity);
            // Will be added automatically as needed: Nav.AddSelectTo(Navigator.NavigateControlId.WebSearchDisplay, uiWebSearchControl);
            // SimpleBookHandler is just for notes, not really "books" per se.
            Nav.AddSimpleBookHandler(Navigator.NavigateControlId.MainPage, this);
            Nav.AddSimpleBookHandler(Navigator.NavigateControlId.NoteListDisplay, uiNoteList);
            Nav.AddSimpleBookHandler(Navigator.NavigateControlId.BookSearchDisplay, uiBookSearchControl);
            Nav.AddSimpleBookHandler(Navigator.NavigateControlId.ProjectRome, RomeActivity);
            Nav.AddSetAppColor(Navigator.NavigateControlId.MainPage, this);
            Nav.AddSetAppColor(Navigator.NavigateControlId.MainReader, uiReaderControl);

            Nav.MainBookHandler = uiReaderControl;
            uiReaderControl.SetChapters = uiChapterControl;
            uiReaderControl.SetImages = uiImageControl;
            uiReaderControl.SetImages2 = RomeActivity;
            uiReaderControl.SetImages3 = this;

            uiNoteList.ParentCommandBar = uiSecondDisplayCommandBar;
            uiWebSearchControl.ParentCommandBar = uiSecondDisplayCommandBar;
            uiBookSearchControl.ParentCommandBar = uiSecondDisplayCommandBar;

            // Customization has to happen after the MainBookHandler is set up.
            var userCustomization = (App.Current as App).Customization;
            userCustomization.Initialize();

            this.Loaded += MainPage_Loaded;


            // Update the title bar. Reset back to the original default colors.
            // Need to do this because I horked everything :-)
#if NEVER_EVER_DEFINED
            if (false)
            {
                var uis = new UISettings();
                var titleBar = ApplicationView.GetForCurrentView().TitleBar;
                var dict = App.Current.Resources;
                titleBar.BackgroundColor = uis.GetColorValue(UIColorType.Background);
                titleBar.ForegroundColor = uis.GetColorValue(UIColorType.Foreground);
                titleBar.ButtonBackgroundColor = uis.GetColorValue(UIColorType.Background);
                titleBar.ButtonForegroundColor = (Color)Resources["SystemAccentColor"];
                    //uis.GetColorValue(UIColorType.Foreground);

            }
#endif

            Window.Current.CoreWindow.KeyDown += CoreWindow_KeyDown;
            Window.Current.CoreWindow.KeyUp += CoreWindow_KeyUp;

            // COLORTHEME: no colors 
            // // // this.ActualThemeChanged += MainPage_ActualThemeChanged;
            // // // UISettings = new UISettings();
            // // // UISettings.ColorValuesChanged += UISettings_ColorValuesChanged;
            Logger.Log("MainPage:Constructor: returning");
        }


        Navigator Nav = Navigator.Get();

        private async void MainPage_Loaded(object sender, RoutedEventArgs e)
        {
            Logger.Log("MainPage:Loaded:called");
            var bookdb = BookDataContext.Get();
            Logger.Log("MainPage:Loaded:about to migrate");
            CommonQueries.BookDoMigrate(bookdb);
            Logger.Log("MainPage:Loaded:done migration");

            // Reset to the tab the user had originally picked.
            SelectUserTab();
            var (id, _) = MainEpubReader.GetCurrReadingBook(); // _ is pos

            if (App.SavedActivatedBookData != null)
            {
                var nav = Navigator.Get();
                nav.DisplayBook(Navigator.NavigateControlId.InitializationCode, App.SavedActivatedBookData, App.SavedActivatedBookLocation);
            }
            else if (id != null)
            {
                var book = CommonQueries.BookGet(bookdb, id);
                if (book != null)
                {
                    var nav = Navigator.Get();
                    nav.DisplayBook(Navigator.NavigateControlId.InitializationCode, book);
                }
            }
            if (WarmUpDataBase == null)
            {
                WarmUpDataBase = CommonQueries.FirstSearchToWarmUpDatabase();
            }

#if DEBUG
            // There are only shown in debug mode!
            uiLogTab.Visibility = Visibility.Visible;
            uiDebugMenu.Visibility = Visibility.Visible;
#endif

            await BookMarkFile.SmartReadAsync();

            Logger.Log("MainPage:Loaded:returning");
        }
        Task WarmUpDataBase = null;

        private void OnClearLog(object sender, RoutedEventArgs e)
        {
            Logger.Clear();
        }

        const string CURR_P1_TAB = "CurrTabId";

        private void OnTabSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.AddedItems.Count < 1) return;
            if (!(e.AddedItems[0] is Microsoft.Toolkit.Uwp.UI.Controls.TabViewItem newtab))
            {
                return;
            }
            var content = newtab.Content;

            // Save the tab settings
            var localSettings = Windows.Storage.ApplicationData.Current.LocalSettings;
            localSettings.Values[CURR_P1_TAB] = newtab.Tag as string;

            if (content is WebSearch ws)
            {
                Nav.AddSelectTo(Navigator.NavigateControlId.WebSearchDisplay, ws);
            }
            else
            {
                Nav.RemoveSelectTo(Navigator.NavigateControlId.WebSearchDisplay);
            }
            ;
        }

        public void SelectUserTab()
        {
            var localSettings = Windows.Storage.ApplicationData.Current.LocalSettings;
            var tabTag = localSettings.Values[CURR_P1_TAB] as string;

            if (string.IsNullOrEmpty(tabTag)) return;

            foreach (var tabObject in uiTabSet.Items)
            {
                if (!(tabObject is TabViewItem tab)) continue;
                var tag = tab.Tag as string;
                if (string.IsNullOrEmpty(tag)) continue;
                if (tag == tabTag)
                {
                    uiTabSet.SelectedItem = tab;
                }
            }
        }
#if NEVER_EVER_DEFINED
        private async void OnRebuildDatabase(object sender, RoutedEventArgs e)
        {
            var path = Windows.Storage.ApplicationData.Current.LocalFolder.Path;
            System.Diagnostics.Debug.WriteLine($"INFO: local files are in {path}");
            var bookdb = BookDataContext.Get();
            int nbook = await RdfReader.ReadDirAsync(bookdb);
            if (nbook < 5)
            {
                App.Error($"Error when rebuilding database; only got {nbook} for {path}");
            }
            // Will clear out the old db and save as needed.
        }
#endif

        private static async Task<BookData> InsertFileIntoDatabase(BookDataContext bookdb, IFolder folder, string filename, bool getFullData = false)
        {
            BookData bookData = null;
            if (bookdb == null) bookdb = BookDataContext.Get();
            string fullfname = $"{folder.Path}\\{filename}";
            var wd = new WizardData() { FilePath = fullfname, FileName = filename };
            try
            {
                wd = await GutenbergFileWizard.GetDataAsync(wd, getFullData);
                if (string.IsNullOrEmpty(wd.BookId))
                {
                    // 2024-11-15
                    // Can happen for e.g., https://github.com/asido/EpubSharp/issues/12 file2 where the
                    // book Id in the Opf is blank. Replace with the author + title + date and hope for the best?
                    var generated_id = $"GENERATE_12_B_{wd.BD.BestAuthorDefaultIsNull}_{wd.BD.Title}";
                    wd.BookId = generated_id;
                    wd.BD.BookId = generated_id;
                }
                if (!string.IsNullOrEmpty(wd.BookId))
                {
                    bookData = CommonQueries.BookGet(bookdb, wd.BookId);

                    //TODO: when I drop a book that's been added to the database because it was
                    // in a bookmark file (which should happen reasonably often!)
                    // then the bookData here is non-null, but also not really filled in well.
                    if (bookData == null || bookData.BookSource.StartsWith(BookData.BookSourceBookMarkFile))
                    {
                        if (wd.BD != null)
                        {
                            // Gotcha! Add to the main book database!
                            if (!string.IsNullOrEmpty(wd.BD.BookId))
                            {
                                wd.BD.Files.Add(new FilenameAndFormatData()
                                {
                                    FileName = filename,
                                    BookId = wd.BookId,
                                    MimeType = "application/epub+zip"
                                });
                                wd.BD.BookSource = BookData.BookSourceUser;

                                // Add in possible data from the bookData set by the user
                                if (bookData != null)
                                {
                                    wd.BD.NavigationData = bookData.NavigationData;
                                    wd.BD.Review = bookData.Review;
                                    wd.BD.Notes = bookData.Notes;

                                    //TODO: now get this book into the database.
                                    // via some kind of merge??
                                }
                                else
                                {
                                    CommonQueries.BookAdd(bookdb, wd.BD, CommonQueries.ExistHandling.IfNotExists);
                                    CommonQueries.BookSaveChanges(bookdb);
                                    bookData = wd.BD;
                                }
                            }
                            else
                            {
                                App.Error($"ERROR: {filename} ({wd.BookId}) was unable to wizard read and get ID");
                            }
                        }
                        else
                        {
                            App.Error($"ERROR: {filename} ({wd.BookId}) was unable to wizard read");
                        }
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"{filename}({wd.BookId}) is {bookData.GetBestTitleForFilename()}");
                    }
                    var fullpath = folder.Path;
                    if (fullpath.Contains(@"AppX\Assets\PreinstalledBooks"))
                    {
                        // Whoops. The initial database might incorrectly have a developer path hard-coded.
                        // Replace with correct location.
                        fullpath = $"PreinstalledBooks:";
                    }
                    CommonQueries.DownloadedBookEnsureFileMarkedAsDownloaded(bookdb, wd.BookId, fullpath, filename);

                }
                else
                {
                    App.Error($"{filename} with id {wd.BookId} is not a known type of e-book");
                }
            }
            catch (Exception)
            {
                App.Error($"{filename} is not a readable e-book");
            }
            return bookData;
        }

        /// <summary>
        /// Given a folder, finds all of the .EPUB files and makes sure they are in the 
        /// download database.
        /// </summary>
        /// <param name="folder"></param>
        /// <param name="getFullData"></param>
        /// <returns></returns>
        public static async Task MarkAllDownloadedFiles(BookDataContext bookdb, IFolder folder, bool getFullData = false)
        {
            var files = await folder.GetFilesAsync();
            foreach (var file in files)
            {
                var filename = file.Name;
                if (filename.ToLower().EndsWith(".epub"))
                {
                    await InsertFileIntoDatabase(bookdb, folder, filename, getFullData);
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"ERROR: MarkAllDownloadedFiles: {filename} is not an e-book");
                }
            }
        }

        private async void OnReadDownloadedFiles(object sender, RoutedEventArgs e)
        {
            BookDataContext bookdb = BookDataContext.Get();

            // Done in the OnCreateDatabase --> CreateDatabaseAsync methods
            //StorageFolder installationFolder = Windows.ApplicationModel.Package.Current.InstalledLocation;
            //var preinstallFolder = await installationFolder.GetFolderAsync(@"Assets\PreinstalledBooks");
            //await MarkAllDownloadedFiles(bookdb, preinstallFolder);

            var downloadFolder = await FolderMethods.EnsureDownloadFolder();
            await MarkAllDownloadedFiles(bookdb, downloadFolder, true);
        }

        //private async void OnReadRDF(object sender, RoutedEventArgs e)
        //{
        //    BookDataContext bookdb = BookDataContext.Get();
        //    await RdfReader.ReadZipTapRdfFile(bookdb);
        //}

        private void OnDragFileOver(object sender, DragEventArgs e)
        {
            e.AcceptedOperation = Windows.ApplicationModel.DataTransfer.DataPackageOperation.Copy;
        }

        public async Task DoFilesActivated(FileActivatedEventArgs args)
        {
            var items = args.Files;
            var bookdb = BookDataContext.Get();
            await DoOpenFilesAsync(bookdb, items);
        }

        private async void OnDropFile(object sender, DragEventArgs e)
        {
            if (e.DataView.Contains(StandardDataFormats.StorageItems))
            {
                await FolderMethods.EnsureDownloadFolder();
                var items = await e.DataView.GetStorageItemsAsync();
                var bookdb = BookDataContext.Get();
                await DoOpenFilesAsync(bookdb, items);
            }
        }

        private async Task DoOpenFilesAsync(BookDataContext bookdb, IReadOnlyList<IStorageItem> items)
        {
            var downloadFolderFolder = await FolderMethods.EnsureDownloadFolder();
            var downloadFolder = await StorageFolder.GetFolderFromPathAsync(downloadFolderFolder.Path);
            bool isFirst = true;
            foreach (var item in items)
            {
                var storageFile = item as StorageFile;
                if (storageFile.Name.ToLower().EndsWith(".epub"))
                {
                    var newfile = await storageFile.CopyAsync(downloadFolder, storageFile.Name, Windows.Storage.NameCollisionOption.ReplaceExisting);
                    if (newfile == null)
                    {
                        App.Error($"ERROR: unable to copy file....");
                        ;
                    }
                    else
                    {
                        var bookdata = await InsertFileIntoDatabase(bookdb, downloadFolderFolder, storageFile.Name, true);
                        if (bookdata != null && isFirst)
                        {
                            // Only display the first book
                            var nav = Navigator.Get();
                            nav.DisplayBook(Navigator.NavigateControlId.DroppedFile, bookdata);
                            uiTabSet.SelectedItem = uiChapterTab;
                            isFirst = false;
                        }
                    }
                }
            }
        }

        private async void OnSaveUserJson(object sender, RoutedEventArgs e)
        {
            await BookMarkFile.SmartSaveAsync(BookMarkFile.BookMarkFileType.RecentOnly);
        }
        private async void OnSaveUserAllJson(object sender, RoutedEventArgs e)
        {
            await BookMarkFile.SmartSaveAsync(BookMarkFile.BookMarkFileType.FullFile);
        }


        private async void OnRestoreUserJson(object sender, RoutedEventArgs e)
        {
            await BookMarkFile.SmartReadAsync();
        }
        private async void OnSetUserJsonFolder(object sender, RoutedEventArgs e)
        {
            await BookMarkFile.SetSaveFolderAsync();
        }

        private void OnFixupDatabase(object sender, RoutedEventArgs e)
        {
            BookDataContext.ResetSingleton("InitialBookData.Db");
            var bookdb = BookDataContext.Get();
            var books = bookdb.Books.ToList();
            int nfix = 0;
            foreach (var book in books)
            {
                if (book.LCC == null) { nfix++; book.LCC = ""; }
                if (book.LCCN == null) { nfix++; book.LCCN = ""; }
                if (book.LCSH == null) { nfix++; book.LCSH = ""; }
            }
            ;
            bookdb.SaveChanges();
            BookDataContext.ResetSingleton(null);
        }

        private async void OnSetFonts(object sender, RoutedEventArgs e)
        {
            var pref = new CustomizeControl();
            var cd = new ContentDialog()
            {
                Title = "Customize Fonts and Sizes",
                Content = pref,
                PrimaryButtonText = "OK",
            };
            await cd.ShowAsync();
        }


        #region Color Handlers

        // COLORTHEME: as of 2020-05-20, don't change colors. It turns out that color
        // changes are much more painful than anticipated.
#if NEVER_EVER_DEFINED
        private async void UISettings_ColorValuesChanged(UISettings sender, object args)
        {
            await this.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
            {
                var bg = UISettings.GetColorValue(UIColorType.Background);
                var fg = UISettings.GetColorValue(UIColorType.Foreground);
                Logger.Log($"NOTE: ColorValue has changed bg={bg} fg={fg}");
                var nav = Navigator.Get();
                nav.SetAppColors(bg, fg);
            });
        }

        UISettings UISettings = null;

        private void MainPage_ActualThemeChanged(FrameworkElement sender, object args)
        {
            Logger.Log($"NOTE: Theme has changed {Application.Current.RequestedTheme.ToString()}");
        }
#endif
        public void SetAppColors(Color bg, Color fg)
        {
            return; // Seriously, don't set colors.
#if NEVER_EVER_DEFINED
            //NOTE: is this correct? And the answer is: no. Setting the theme like this isn't a "thing"
            var bgb = new SolidColorBrush(bg);
            var fgb = new SolidColorBrush(fg);

            var tb = ApplicationView.GetForCurrentView().TitleBar;
            tb.ForegroundColor = fg;
            tb.BackgroundColor = bg;
            tb.ButtonBackgroundColor = bg;
            tb.ButtonForegroundColor = fg;

            SetColorByName("TargetBackground", bg);
            SetColorByName("SystemColorButtonFaceColor", bg);
            SetColorByName("SystemColorWindowColor", bg);
            SetColorByName("SystemBaseHighColor", bg);
            SetColorByName("SystemChromeMediumLowColor", bg);
            SetColorByName("SystemChromeMediumColor", bg);
            SetColorByName("SystemBaseMediumLowColor", bg);
            SetColorByName("SystemBaseLowColor", bg);
            SetColorByName("", bg);
            SetColorByName("", bg);
            SetColorByName("", bg);
            SetColorByName("", bg);
            SetColorByName("", bg);
            SetColorByName("", bg);
            SetColorByName("", bg);
            SetColorByName("", bg);


            SetColorByName("TargetBorderBrush", fg);
            SetColorByName("SystemColorButtonTextColor", fg);
            SetColorByName("SystemAccentColor", fg);
            SetColorByName("SystemAltMediumColor", fg);
            SetColorByName("SystemColorHighlightColor", fg);
            SetColorByName("SystemColorHighlightTextColor", fg);
            SetColorByName("SystemChromeMediumColor", fg);
            SetColorByName("SystemListLowColor", fg);
            SetColorByName("SystemListMediumColor", fg);
            SetColorByName("", fg);
            SetColorByName("", fg);
            SetColorByName("", fg);
            SetColorByName("", fg);
            SetColorByName("", fg);
            SetColorByName("", fg);
            SetColorByName("", fg);
            SetColorByName("", fg);
#endif
        }

#if NEVER_EVER_DEFINED
        private void SetColorByName (string name, Color color)
        {
            var dict = App.Current.Resources;

            object resource = null;
            var result = dict.TryGetValue (name, out resource);
            if (result)
            {
                var asscb = resource as SolidColorBrush;
                if (asscb != null)
                {
                    asscb.Color = color;
                }
                if (resource is Color)
                {
                    dict[name] = color;
                }
            }
        }
#endif


        #endregion

        private async void OnShowLocation(object sender, RoutedEventArgs e)
        {
            var txt = $"Path is {FolderMethods.LocalFolder}";
            var dlg = new ContentDialog()
            {
                Content = new TextBlock() { Text = txt, TextWrapping = TextWrapping.Wrap, IsTextSelectionEnabled = true },
                Title = "Current location",
                PrimaryButtonText = "OK",
                SecondaryButtonText = "Copy",
            };
            var button = await dlg.ShowAsync();
            switch (button)
            {
                case ContentDialogResult.Secondary:
                    var dp = new DataPackage();
                    dp.Properties.Title = "Local Folder";
                    dp.SetText(FolderMethods.LocalFolder);
                    dp.RequestedOperation = DataPackageOperation.Copy;
                    Clipboard.SetContent(dp);
                    break;
            }
        }
        private async void OnCopyOtherInstall(object sender, RoutedEventArgs e)
        {
            var root = ApplicationData.Current.LocalCacheFolder;
            var destFolder = await root.CreateFolderAsync(FolderMethods.DownloadFolder, Windows.Storage.CreationCollisionOption.OpenIfExists);

            var picker = new FileOpenPicker();
            picker.ViewMode = PickerViewMode.List;
            picker.SuggestedStartLocation = PickerLocationId.Downloads;
            picker.FileTypeFilter.Add(".epub");
            picker.FileTypeFilter.Add(".epub3");
            picker.FileTypeFilter.Add(".txt");
            var list = await picker.PickMultipleFilesAsync();
            foreach (var file in list)
            {
                // TODO: works, but badly?
                await file.CopyAsync(destFolder, file.Name, Windows.Storage.NameCollisionOption.ReplaceExisting);
            }
        }

        /// <summary>
        /// Dwonloads the correct set (5 as of 2021-04-04) of books to pre-populate the app with.
        /// Would be nice to sometime include popular books as a category :-)
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void OnDownloadInitialFiles(object sender, RoutedEventArgs e)
        {
            var bookdb = BookDataContext.Get();
            var iftg = new InitializeFilesToGet();
            await iftg.DownloadBooksAsync(bookdb);
        }

        private async void OnCreateDatabase(object sender, RoutedEventArgs e)
        {
            BookDataContext.ResetSingleton("InitialBookData.Db");
            var bookdb = BookDataContext.Get();
            CommonQueries.BookDoMigrate(bookdb); // might not exist at all; Migrate is the way to force creation of tables.

            var iftg = new InitializeFilesToGet();
            await iftg.CreateDatabaseAsync(bookdb);
            BookDataContext.ResetSingleton(null); // reset database
        }

        /// <summary>
        ///  Download the latest Gutenberg catalog
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void OnDownloadLatestCatalog(object sender, RoutedEventArgs e)
        {
            var uri = GutenbergDownloadControl.CurrentGutenbergCatalogLocation; // http://www.gutenberg.org/cache/epub/feeds/rdf-files.tar.zip"
            var picker = new FileSavePicker()
            {
                SuggestedStartLocation = PickerLocationId.DocumentsLibrary,
                SuggestedFileName = "rdf-files.tar.zip",
                SettingsIdentifier = "NewGutengbergFile",
            };
            picker.FileTypeChoices.Add("Plain Text", new List<string>() { ".zip" });
            var filepick = await picker.PickSaveFileAsync();
            if (filepick == null) return;

            int totalRead = 0;
            System.IO.Stream stream = null;
            var hc = new HttpClient();
            var cts = new CancellationTokenSource();
            var ct = cts.Token;
            var copyError = false;
            using (var outstream = await filepick.OpenAsync(FileAccessMode.ReadWrite))
            {
                HttpResponseMessage result = null;
                const uint mbufferSize = 1024 * 1024;
                var mbuffer = new byte[mbufferSize];
                try
                {
                    result = await hc.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, ct);
                    stream = await result.Content.ReadAsStreamAsync();
                    // Just having the stream means nothing; we need to read from the stream. That's
                    // where the incoming bytes will actually be read
                }
                catch (Exception)
                {
                    copyError = true;
                }

                try
                {
                    bool keepGoing = !ct.IsCancellationRequested && !copyError;
                    int tempTotalRead = 0;

                    var startdlg = new ContentDialog()
                    {
                        Content = new TextBlock()
                        {
                            Text = $"Starting to download file now {uri.OriginalString}",
                            TextWrapping = TextWrapping.Wrap,
                            IsTextSelectionEnabled = true
                        },
                        Title = "Starting catalog download",
                        PrimaryButtonText = "OK",
                    };
                    await startdlg.ShowAsync();


                    while (keepGoing)
                    {
                        var nread = await stream.ReadAsync(mbuffer, 0, mbuffer.Length);
                        totalRead += nread;
                        tempTotalRead += nread;
                        if (nread == 0) // When we get no bytes, the stream is done.
                        {
                            keepGoing = false;
                        }
                        else
                        {
                            // Write out the buffer, but do it correctly.
                            //IBuffer buffer = Windows.Security.Cryptography.CryptographicBuffer.CreateFromByteArray(mbuffer);
                            var buffer = mbuffer.AsBuffer(0, nread); // SO MANY CONVERSIONS.
                            await outstream.WriteAsync(buffer);
                        }
                        if (tempTotalRead > 1_000_000) // pause a little bit every million bytes. Otherwise the UI is impossible...
                        {
                            Logger.Log($"Download catalog: got {totalRead} bytes");
                            tempTotalRead = 0;
                        }
                        if (ct.IsCancellationRequested)
                        {
                            keepGoing = false;
                            copyError = true;
                        }
                    }
                }
                catch (Exception)
                {
                    ; // all of the actual data-reading exceptions.
                    copyError = true;
                }

                var donedlg = new ContentDialog()
                {
                    Content = new TextBlock()
                    {
                        Text = copyError ? "ERROR! " : "Download complete:" + $"size is {totalRead}",
                        TextWrapping = TextWrapping.Wrap,
                        IsTextSelectionEnabled = true
                    },
                    Title = "Catalog download complete",
                    PrimaryButtonText = "OK",
                };
                await donedlg.ShowAsync();


            }
        }



        public Task DisplayBook(BookData book, BookLocation location)
        {
            // The primary purpose of the DisplayBook to to turn off the help display!
            uiReaderControl.Visibility = Visibility.Visible;
            uiHelpControl.Visibility = Visibility.Collapsed;
            return Task.Delay(0);
        }



        private async void OnUpdateGutenberg(object sender, RoutedEventArgs e)
        {
            var dl = new GutenbergDownloadControl();
            var cd = new ContentDialog()
            {
                Title = "Update Gutenberg Catalog",
                Content = dl,
                //Buttons are handled inside the dialog: PrimaryButtonText = "OK",
            };
            dl.DialogParent = cd;
            await cd.ShowAsync();
        }
        private Visibility ToggleVisibility(Visibility value)
        {
            switch (value)
            {
                case Visibility.Collapsed: return Visibility.Visible;
                default: return Visibility.Collapsed;
            }
        }

        enum HelpType { Classic, eBookReader };
        HelpType CurrHelpType = HelpType.Classic;

        private async void OnHelpToggle(object sender, RoutedEventArgs e)
        {
            uiHelpControl.Visibility = ToggleVisibility(uiHelpControl.Visibility);
            uiReaderControl.Visibility = ToggleVisibility(uiReaderControl.Visibility);

            if (uiHelpControl.Visibility == Visibility.Visible && CurrHelpType != HelpType.Classic)
            {
                // Switch to classic mode
                await uiHelpControl.SetupHelpImages();
            }
        }


        /// <summary>
        /// Used to set the number of images displayed in the little image tab
        /// </summary>
        /// <param name="images"></param>
        /// <returns></returns>
        public async Task SetImagesAsync(ICollection<EpubByteFile> images)
        {
            // Find the image tab
            TabViewItem foundTab = null;
            foreach (var tabItem in uiTabSet.Items)
            {
                var tab = tabItem as TabViewItem;
                if (tab == null) continue;
                if ((tab.Tag as string) == "tagImages")
                {
                    foundTab = tab;
                }
            }
            if (foundTab == null) return;

            // Update the tab...
            string header = "Images";
            int n = images.Count;
            switch (images.Count)
            {
                case 0: header = $"No images"; break;
                case 1: header = $"Images ①"; break;
                case 2: header = $"Images ②"; break;
                case 3: header = $"Images ③"; break;
                case 4: header = $"Images ④"; break;
                case 5: header = $"Images ⑤"; break;
                case 6: header = $"Images ⑥"; break;
                case 7: header = $"Images ⑦"; break;
                case 8: header = $"Images ⑧"; break;
                case 9: header = $"Images ⑨"; break;
                case 10:
                case 11:
                case 12:
                case 13:
                case 14:
                case 15:
                case 16:
                case 17:
                case 18:
                case 19:
                case 20:
                    header = $"Images " + (char)(0x2469 + n - 10); break;
                case 21:
                case 22:
                case 23:
                case 24:
                case 25:
                case 26:
                case 27:
                case 28:
                case 29:
                case 30:
                case 31:
                case 32:
                case 33:
                case 34:
                case 35:
                    header = $"Images " + (char)(0x3251 + n - 21); break;
                case 36:
                case 37:
                case 38:
                case 39:
                case 40:
                case 41:
                case 42:
                case 43:
                case 44:
                case 45:
                case 46:
                case 47:
                case 48:
                case 49:
                case 50:
                    header = $"Images " + (char)(0x32B1 + n - 36); break;
                default: header = $"Image ({images.Count})"; break;
            }
            foundTab.Header = header;
            if (header == null) await Task.Delay(0); // make the compiler be quiet about being async since I have to return a Task
        }
        /// <summary>
        /// Pop up dialog to let user pick what books to download to e-Book Reader
        /// </summary>

        private async void OnEbookReaderSendTo(object sender, RoutedEventArgs e)
        {
            // Dismiss the menu
            var popups = VisualTreeHelper.GetOpenPopups(Window.Current);
            foreach (var popup in popups)
            {
                popup.IsOpen = false;
            }
            var content = new EBookReaderPickAndSend();
            var dlg = new ContentDialog()
            {
                Title = "Download book to an eBook Reader",
                Content = content,
            };
            await dlg.ShowAsync();
        }

        /// <summary>
        /// TODO: Open the folder that is the E-Book Reader
        /// </summary>
        private async void OnEbookReaderOpenFolder(object sender, RoutedEventArgs e)
        {
            await EBookFolder.LaunchExplorerAtFolderAsync();
        }


        /// <summary>
        /// TODO: Mark all eBookReader books as Read
        /// </summary>
        private async void OnEbookReaderMark(object sender, RoutedEventArgs e)
        {
            // Dismiss the menu
            var popups = VisualTreeHelper.GetOpenPopups(Window.Current);
            foreach (var popup in popups)
            {
                popup.IsOpen = false;
            }
            var content = new EBookReaderMark(BookNavigationData.UserStatus.Done);
            var dlg = new ContentDialog()
            {
                Title = "Mark books sent to eBook Reader as read",
                Content = content,
                // Buttons are part of the content, not here. This is opposite from how the review page works.
            };
            await dlg.ShowAsync();
            await content.RunSavedReviewEachBook();
        }



        /// <summary>
        /// TODO: Shows help for using eBook Readers with Voracious EBOOK Reader
        /// </summary>
        private async void OnEbookReaderHelp(object sender, RoutedEventArgs e)
        {
            uiHelpControl.Visibility = ToggleVisibility(uiHelpControl.Visibility);
            uiReaderControl.Visibility = ToggleVisibility(uiReaderControl.Visibility);

            CurrHelpType = HelpType.eBookReader;
            await uiHelpControl.SetupMarkdown();
        }

        /// <summary>
        /// TODO: set the folder
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void OnEbookReaderSetFolder(object sender, RoutedEventArgs e)
        {
            await EBookFolder.PickFolderAsync();
        }

        bool ShiftIsPressed = false;

        private void CoreWindow_KeyDown(CoreWindow sender, KeyEventArgs e)
        {
            switch (e.VirtualKey)
            {
                case Windows.System.VirtualKey.Shift:
                    ShiftIsPressed = true;
                    break;
            }
        }

        private async void CoreWindow_KeyUp(CoreWindow sender, KeyEventArgs e)
        {
            switch (e.VirtualKey)
            {
                case Windows.System.VirtualKey.Shift:
                    ShiftIsPressed = false;
                    break;


                case Windows.System.VirtualKey.Space:
                    if (ShiftIsPressed)
                    {
                        await uiReaderControl.DoPrevPage();
                        e.Handled = true;
                    }
                    else
                    {
                        await uiReaderControl.DoNextPage();
                        e.Handled = true;
                    }
                    break;

                case Windows.System.VirtualKey.PageUp:
                case Windows.System.VirtualKey.F11:
                    if (uiReaderControl.Visibility == Visibility.Visible)
                    {
                        await uiReaderControl.DoPrevPage();
                        e.Handled = true;
                    }
                    break;
                case Windows.System.VirtualKey.PageDown:
                case Windows.System.VirtualKey.F12:
                    if (uiReaderControl.Visibility == Visibility.Visible)
                    {
                        await uiReaderControl.DoNextPage();
                        e.Handled = true;
                    }
                    break;
            }
        }

        private async void OnKeyDown(object sender, Windows.UI.Xaml.Input.KeyRoutedEventArgs e)
        {
            switch (e.Key)
            {
                case Windows.System.VirtualKey.Shift:
                    ShiftIsPressed = true;
                    break;
            }
        }

        private async void OnKeyUp(object sender, Windows.UI.Xaml.Input.KeyRoutedEventArgs e)
        {
            switch (e.Key)
            {
                case Windows.System.VirtualKey.Shift:
                    ShiftIsPressed = false;
                    break;


                case Windows.System.VirtualKey.Space:
                    if (ShiftIsPressed)
                    {
                        await uiReaderControl.DoPrevPage();
                        e.Handled = true;
                    }
                    else
                    {
                        await uiReaderControl.DoNextPage();
                        e.Handled = true;
                    }
                    break;

                case Windows.System.VirtualKey.PageUp:
                case Windows.System.VirtualKey.F11:
                    if (uiReaderControl.Visibility == Visibility.Visible)
                    {
                        await uiReaderControl.DoPrevPage();
                        e.Handled = true;
                    }
                    break;
                case Windows.System.VirtualKey.PageDown:
                case Windows.System.VirtualKey.F12:
                    if (uiReaderControl.Visibility == Visibility.Visible)
                    {
                        await uiReaderControl.DoNextPage();
                        e.Handled = true;
                    }
                    break;
            }
        }
    }
}
