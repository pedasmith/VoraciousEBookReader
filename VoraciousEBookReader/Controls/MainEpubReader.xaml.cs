using EpubSharp;
using SimpleEpubReader.Database;
using SimpleEpubReader.EbookReader;
using SimpleEpubReader.FileWizards;
using SimpleEpubReader.UwpClasses;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.UI.Popups;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.Web.Http; // needed by OnWebResourceRequested
using static SimpleEpubReader.Controls.Navigator;


namespace SimpleEpubReader.Controls
{


    public sealed partial class MainEpubReader : UserControl, INavigateTo, BookHandler, ISetAppColors
    {
        public MainEpubReader()
        {
            this.InitializeComponent();
            uiHtml.NavigationCompleted += UiHtml_NavigationCompleted;
            CurrPositionSaveTimer = new DispatcherTimer()
            {
                Interval = new TimeSpan(0, 0, 5), // h m s //NOTE: a little longer? Or is this OK?
            };
            CurrPositionSaveTimer.Tick += CurrPositionSaveTimer_Tick;
            // // // Do not need these to be started at the start. CurrPositionSaveTimer.Start();
        }


        // Always set this for convenience
        readonly NavigateControlId ControlId = NavigateControlId.MainReader;
        private EpubBookExt EpubBook { get; set; } = null;
        private BookData BookData { get; set; } = null;



        //TODO: the navigator should be the thing that knows these.
        public ISetChapters SetChapters = null;
        public ISetImages SetImages = null;
        public ISetImages SetImages2 = null;
        public ISetImages SetImages3 = null;
        private string CurrHtml = null;
        int CurrHtmlIndex = -1; // Which of the sub-books is being displayed?
        private string CurrHtmlFileName = null;
        double CurrScrollPosition = double.NaN;
        double CurrSelectPosition = double.NaN;

        // Handle the slight fuzziness of doubles and floats.
        const double TopScrollPosition = 0.001;
        const double BottomScrollPosition = 99.9; // 99.999 was too aggressive

        DateTimeOffset TimeCurrPositionUpdated = DateTimeOffset.MinValue;
        readonly DispatcherTimer CurrPositionSaveTimer;

        /// <summary>
        /// Called externally in order to display a book; will use the BookNavigationData to move
        /// to the previous spot.
        /// </summary>
        /// <param name="bookId"></param>
        /// <returns></returns>
        public async Task DisplayBook(BookData bookData, BookLocation location = null)
        {
            // Reset all of the position values.
            CurrHtml = "";
            CurrHtmlIndex = -1;
            CurrHtmlFileName = null;
            CurrScrollPosition = double.NaN;
            CurrSelectPosition = double.NaN;

            BookData = bookData;
            var bookdb = BookDataContext.Get();
            var dd = bookData.DownloadData ?? CommonQueries.DownloadedBookFind(bookdb, BookData.BookId);
            var nav = bookData.NavigationData ?? CommonQueries.BookNavigationDataFind(bookdb, BookData.BookId);
            if (location == null && !string.IsNullOrEmpty(nav?.CurrSpot))
            {
                location = BookLocation.FromJson(nav.CurrSpot);
            }
            SetReviewSymbol();

            if (dd == null) return;
            var fullpath = dd.FullFilePath;
            if (fullpath.Contains(@"\source\repos\SimpleEpubReader\SimpleEpubReader\bin\x64\Debug\AppX\Assets\PreinstalledBooks"))
            {
                // Whoops. The initial database might incorrectly have a developer path hard-coded.
                // Replace with correct location.
                var installationFolder = FolderMethods.InstallationFolder;
                fullpath = $"{installationFolder}\\Assets\\PreinstalledBooks\\{dd.FileName}";
            }
            else if (fullpath.StartsWith("PreinstalledBooks:"))
            {
                // Preinstalled books are in a sort of relative path. It's designed to be an invalid 
                // (or at least incredibly rare) directory.
                var installationFolder = FolderMethods.InstallationFolder;
                fullpath = $"{installationFolder}\\Assets\\PreinstalledBooks\\{dd.FileName}";
            }
            var openResult = await OpenFile(fullpath, location);
            switch (openResult)
            {
                case OpenResult.OK:
                    // Set up the uiAllUpPosition
                    var sectionSizes = new List<double>();
                    foreach (var file in EpubBook.ResourcesHtmlOrdered)
                    {
                        sectionSizes.Add(file.Content.Length);
                    }
                    uiAllUpPosition.SetSectionSizes(sectionSizes);
                    // No need to set to zero; it's already set in the OpenFile!. uiAllUpPosition.UpdatePosition(0, 0); // set to zero!

                    ApplicationView appView = ApplicationView.GetForCurrentView();
                    appView.Title = bookData.Title.Replace("\n", " -- "); // title is multi-line
                    break;

                case OpenResult.RedownloadableError: // An error. Mark the book as not downloaded + redownload
                    dd.CurrFileStatus = DownloadData.FileStatus.Unknown; // deleted? gone? corrupt? we really don't know.
                    CommonQueries.BookSaveChanges(bookdb);
                    await BookSearch.DoSwipeDownloadOrReadAsync(BookData);
                    break;

                default: // An error. Mark the book as not downloaded
                    dd.CurrFileStatus = DownloadData.FileStatus.Unknown; // deleted? gone? corrupt? we really don't know.
                    CommonQueries.BookSaveChanges(bookdb);
                    break;
            }
        }

        private void SetReviewSymbol()
        {
            var bookdb = BookDataContext.Get();
            var review = CommonQueries.UserReviewFind(bookdb, BookData.BookId);
            var button = uiReviewMenuButton;
            var symbol = Symbol.Favorite; // filled favorite
            var outline = Symbol.OutlineStar;
            if (review == null || review.IsNotSet)
            {
                symbol = outline;
            }
            button.Icon = new SymbolIcon(symbol);
        }

        BookLocation UserNavigatedToArgument = null;

        enum OpenResult { OK, RedownloadableError, OtherError }
        private async Task<OpenResult> OpenFile(string fullFilePath, BookLocation location)
        {
            OpenResult retval; // default is = OpenResult.OtherError;
            try
            {
                SetScreenToLoading();
                Logger.Log($"MainEpubReader: about to load book {fullFilePath}");
                var fileContents = await FileMethods.ReadBytesAsync(fullFilePath);
                bool isZip = fullFilePath.ToUpperInvariant().EndsWith(".ZIP");
                if (fileContents == null)
                {
                    // Failure of some sort, but just kind of punt it.
                    retval = OpenResult.RedownloadableError;
                    EpubBook = null;
                    App.Error($"ERROR: book: unable to load file {fullFilePath}");

                    SetScreenToLoading(LoadFailureScreen);
                    var md = new MessageDialog($"Error: book file is missing: {BookData.Title} ")
                    {
                        Title = "Atttempting to re-download book"
                    };
                    await md.ShowAsync();
                    return retval;
                }

                Logger.Log($"MainEpubReader: read raw array {fileContents.Length}");

                // There's a chance that the file is really a text file, not an epub.
                // Make sure it's at least pre
                bool isEpub = false;
                Exception epubException = null;
                try
                {
                    // All epub files start with PK\3\4 (because they are zip files).
                    // If it's not that, then is must be a text or html file.
                    if (EpubWizard.IsEpub(fileContents) && !isZip)
                    {
                        var inner = EpubReader.Read(fileContents);
                        EpubBook = new EpubBookExt(inner);
                        isEpub = inner != null;
                    }
                }
                catch (Exception ex)
                {
                    isEpub = false;
                    epubException = ex;
                }

                if (!isEpub)
                {
                    if (isZip)
                    {
                        throw epubException;
                    }

                    try
                    {
                        var fileString = System.Text.Encoding.UTF8.GetString(fileContents);
                        if (!fileString.ToLower().Contains("<html"))
                        {
                            retval = await OpenFileAsText(fileString, location);
                        }
                        else
                        {
                            // We only understand text file and epub, nothing else.
                            throw epubException;
                        }
                    }
                    catch (Exception)
                    {
                        throw; // Meh
                    }
                }
                Logger.Log($"MainEpubReader: read book length {fileContents.Length}");

                SetChapters?.SetChapters(EpubBook, EpubBook.TableOfContents);
                if (SetChapters == null) App.Error($"ISSUE: got new book but SetChapters is null for {fullFilePath}");
                await SetImages?.SetImagesAsync(EpubBook.Resources.Images);
                await SetImages2?.SetImagesAsync(EpubBook.Resources.Images);
                await SetImages3?.SetImagesAsync(EpubBook.Resources.Images);
                if (SetImages == null) App.Error($"ISSUE: got new book but SetImages is null for {fullFilePath}");

                Logger.Log($"MainEpubReader: about to navigate");

                if (location == null)
                {
                    // Old way: go to the first item in table of contents. New way is to go to file=0 percent=0
                    // but only if there's any actual files
                    //var chapter = EpubWizard.GetFirstChapter(EpubBook.TableOfContents);
                    //if (chapter != null)
                    if (EpubBook.ResourcesHtmlOrdered.Count > 0)
                    {
                        location = new BookLocation(0, 0); // often the first item is the cover page which isn't in the table of contents.
                        //location = EpubChapterData.FromChapter(chapter);
                        // // // location = new BookLocation(chapter.Anchor ?? chapter.FileName); // FAIL: BAEN likes to have file-per-chapter
                    }
                }

                if (location != null)
                {
                    if (Logger.LogExtraTiming)
                    {
                        Logger.Log($"MainEpubReader: OpenFile: About to move to location as needed. {location:F3}");
                    }
                    UserNavigatedToArgument = location;
                    NavigateTo(ControlId, location); // We won't get a hairpin navigation callback
                }
                retval = OpenResult.OK;
            }
            catch (Exception ex)
            {
                // Simple error recovery: keep the downloaded data, but report it as
                // no actually downloaded.
                retval = OpenResult.OtherError;
                EpubBook = null;
                App.Error($"ERROR: book: exception {ex.Message} unable to load file {fullFilePath}");

                SetScreenToLoading(LoadFailureScreen);
                var md = new MessageDialog($"Error: unable to open that book. Internal error {ex.Message}")
                {
                    Title = "Unable to open book"
                };
                await md.ShowAsync();
            }
            return retval;
        }

        private async Task<OpenResult> OpenFileAsText(string fileString, BookLocation location)
        {
            await Task.Delay(0);
            Logger.Log($"MainEpubReader: file is a text string; convert to html");

            EpubBook = TextWizard.TextToEpub(fileString);

            if (location == null)
            {
                var chapter = EpubWizard.GetFirstChapter(EpubBook.TableOfContents);
                if (chapter != null)
                {
                    location = EpubChapterData.FromChapter(chapter);
                }
            }

            if (location != null)
            {
                Logger.Log($"MainEpubReader: Show Text: About to move to location as needed. {location}");
                UserNavigatedToArgument = location;
                NavigateTo(ControlId, location); // We won't get a hairpin navigation callback
            }

            return OpenResult.OK;
        }



        string DeferredNavigation = null;

        /// <summary>
        /// NavigateTo means navigate to a spot in the book. Will also do a navigation with User...
        /// </summary>
        /// <param name="sourceId"></param>
        /// <param name="location"></param>
        public void NavigateTo(NavigateControlId sourceId, BookLocation location)
        {
            var bookdb = BookDataContext.Get();

            if (Logger.LogExtraTiming)
            {
                Logger.Log($"MainEpubReader: Navigation: to {location}");
            }
            // Save the fact that we navigated to here. Only the main reader saves this information
            var navigationData = CommonQueries.BookNavigationDataFind(bookdb, BookData.BookId);
            if (navigationData == null)
            {
                navigationData = new BookNavigationData() { BookId = BookData.BookId, CurrStatus = BookNavigationData.UserStatus.Reading };
                CommonQueries.BookNavigationDataAdd(bookdb, navigationData, CommonQueries.ExistHandling.IfNotExists);
            }
            navigationData.CurrSpot = location.Location;
            navigationData.CurrStatus = BookNavigationData.UserStatus.Reading; // If I'm navigating then I'm reading?


            // And now actually navigate. There are two types of navigation: navigation
            // via tags and navigation by percent.
            // Both need to be handled.
            var percent = location.HtmlPercent;
            if (percent >= 0.0)
            {
                if (Logger.LogExtraTiming)
                {
                    Logger.Log($"MainEpubReader: Navigation: to percent");
                }
                NavigateToPercent(location);
            }
            else
            {
                if (Logger.LogExtraTiming)
                {
                    Logger.Log($"MainEpubReader: Navigation: via location, not percent ({location})");
                }
                NavigateToLocation(location);
            }
        }

        private void DoUserNavigateToAsNeeded()
        {
            var location = UserNavigatedToArgument;
            UserNavigatedToArgument = null;
            if (location != null)
            {
                var nav = Navigator.Get();
                nav.UserNavigatedTo(ControlId, location);
            }
        }

        private void NavigateToPercent(BookLocation location)
        {
            var navScript = $"scrollToPercent({location.ScrollPercent});\n";
            uiAllUpPosition.UpdatePosition(location.HtmlIndex, location.HtmlPercent);
            if (CurrHtmlIndex == location.HtmlIndex)
            {
                // Great! just scroll to the place! 
                Logger.Log($"Main EBOOK: navigate to SAME html");
                var task = uiHtml.InvokeScriptAsync("scrollToPercent", new List<string>() { location.ScrollPercent.ToString() });
                DoUserNavigateToAsNeeded();
                return;
            }
            else
            {
                if (Logger.LogExtraTiming)
                {
                    Logger.Log($"Main EBOOK: navigate to other html (htmlindex={location.HtmlIndex})");
                }
                var foundIndex = location.HtmlIndex;
                var foundHtml = EpubWizard.FindHtmlByIndex(EpubBook, foundIndex);
                if (Logger.LogExtraTiming)
                {
                    Logger.Log($"Main EBOOK: navigate to other html {foundIndex}=len {foundHtml?.Length}");
                }

                var html = HtmlFixup.FixupHtmlAll(foundHtml);
                DeferredNavigation = navScript;
                CurrHtml = html;
                CurrHtmlIndex = foundIndex;
                CurrHtmlFileName = location.HtmlFileName;

                Logger.Log($"Main EBOOK: about to navigate with deferred navigation {DeferredNavigation}");
                uiHtml.NavigateToString(html);
            }
            SavePositionEZ();
        }

        private void NavigateToLocation(BookLocation location)
        {
            // Gutenberg: just a location which we have to figure out
            // BAEN: HtmlFileName and (for sub-chapters) a Location, too.
            // Location might have just an id in it
            // OR it might have just an HtmlFileName
            // USPS: the location is an XHTML file that's encoded: Additional%20Resources.xhtml instead of Additional Resources.html
            // 
            // OR an id+HtmlIndex.
            // We actually don't need the index!

            if (!string.IsNullOrEmpty(location.HtmlFileName))
            {
                // Jump to percent 0.0 after finding the html by name.
                // navScript is either to an id or just to the top
                var navScript = string.IsNullOrEmpty(location.Location) ? $"scrollToPercent(0.0)" : $"scrollToId('{location.Location}')";
                var (foundHtml, foundIndex, foundHtmlFileName) = EpubWizard.FindHtmlContainingHtmlFileName(EpubBook, location.HtmlFileName);
                if (foundHtml == null)
                {
                    App.Error($"ERROR: unable to navigate to htmlFileName={location.HtmlFileName}");
                    return; // nuts; can't find it.
                }

                // If we're jumping to the current spot, meh, don't bother to optimize.
                // Maybe the user wants to do the full amount of code because of "issues"

                var html = HtmlFixup.FixupHtmlAll(foundHtml);
                DeferredNavigation = navScript;
                uiHtml.NavigateToString(html);

                CurrHtml = html;
                CurrHtmlIndex = foundIndex;
                CurrHtmlFileName = foundHtmlFileName;

                return;
            }
            else
            {
                string id = location.Location;

                // Find the html with the tag
                // FAIL: BAEN books only navigate by html name. The name will be found in the TOC because there are links!
                // FAIL: Gutenberg John Thorndyke Cases searching for the shoes.png. The long id @public@vhost@g@gutenberg@html@files@13882@13882-h@images@shoes.png
                // is found in the first HTML but only because that HTML includes a list of illustrations which point to an HTML file that includes the
                // shoes.png file (but that html file has a name that starts with the png but then adds on .wrap-0.html.html.
                // The code here used to just see if the current HTML includes the id at all; the better code checks to make sure it's a proper href.
                if (CurrHtml != null && EpubWizard.HtmlStringIdIndexOf (CurrHtml, id, true) >= 0 && CurrHtml.Contains("scrollToId"))
                {
                    var task = uiHtml.InvokeScriptAsync("scrollToId", new List<string>() { id });
                    DoUserNavigateToAsNeeded();
                    return;
                }

                var idList = EpubWizard.GetIdVariants(id);
                var (foundHtml, foundIndex, foundHtmlFileName, foundId) = EpubWizard.FindHtmlContainingId(EpubBook, idList, location.HtmlIndex);
                if (foundHtml == null)
                {
                    App.Error($"ERROR: unable to navigate to {id}");
                    return; // nuts; can't find it.
                }
                var navScript = $"scrollToId('{foundId}')";
                var html = HtmlFixup.FixupHtmlAll(foundHtml);
                DeferredNavigation = navScript;
                uiHtml.NavigateToString(html);

                CurrHtml = html;
                CurrHtmlIndex = foundIndex;
                CurrHtmlFileName = foundHtmlFileName;
            }
            SavePositionEZ();
        }

        const string FakeHttpPrefix = "http://example.com/book/?";

        /// <summary>
        /// Navigation means to a new URL; DeferredNavigation is navigate within the book section
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        private async void UiHtml_NavigationCompleted(WebView sender, WebViewNavigationCompletedEventArgs args)
        {
            if (DeferredNavigation != null)
            {
                var dn = DeferredNavigation;
                DeferredNavigation = null;
                try
                {
                    if (Logger.LogExtraTiming)
                    {
                        Logger.Log($"MainEpubReader: Navigation completed; deferred navigation {dn}");
                    }
                    // The scroll will cause a trigger of the chapter view update 
                    await uiHtml.InvokeScriptAsync("eval", new List<string>() { dn });
                }
                catch (Exception ex)
                {
                    App.Error($"EXCEPTION on deferral: {ex.Message} for {dn} ");
                }
            }
            // Will, e.g. cause the chapter display to update. The chapter display needs the HTML have been
            // fully loaded before it can be called successfully.
            // Correction: doesn't force the chapter update at all.
            DoUserNavigateToAsNeeded();
            await SavePositionNow();

            if (DeferredFont != null)
            {
                await SetFontAndSizeAsync(DeferredFont, DeferredSize);
            }
        }

        private async void OnManipulationComplete(object sender, ManipulationCompletedRoutedEventArgs e)
        {
            // This callback isn't actually useful at all :-(
            await Logger.LogAsync($"MainEpubReader:Manipulation:Complete: At:{e.Position.X}:{e.Position.Y}");
        }



        private void OnWebResourceRequested(WebView sender, WebViewWebResourceRequestedEventArgs args)
        {
            HttpResponseMessage response = null;
            var str = args.Request.RequestUri.OriginalString;
            if (str.StartsWith(FakeHttpPrefix))
            {
                var requestString = str.Replace(FakeHttpPrefix, "");
                // e.g. =@public@vhost@g@gutenberg@html@files@61533@61533-h@images@cbl-2.jpg
                if (true || requestString.Contains("@images"))
                {
                    var requestedFile = EpubWizard.GetInternalFileByName(EpubBook, requestString);
                    if (requestedFile != null)
                    {
                        if (requestString.EndsWith(".css"))
                        {
                            var changed = CssFixup.FixupCss(requestedFile);
                            if (changed)
                            {
                                ;
                            }
                        }
                        if (Logger.LogAllResourceLoads)
                        {
                            Logger.Log($"MainEpubReader:OnWebResourceRequested: FOUND internal asset request {requestString}");
                        }
                        response = new HttpResponseMessage(HttpStatusCode.Ok)
                        {
                            Content = new HttpBufferContent(requestedFile.Content.AsBuffer()),
                        };
                        //response.Content.Headers.ContentType
                    }
                    else
                    {
                        Logger.Log($"MainEpubReader:OnWebResourceRequested: Can't find request {requestString}");
                    }
                }
            }
            else
            {
                // Is a link in the book. Just jump to it via external browser.
                App.Error($"Weird: HTTP is requesting {str}");
                Logger.Log($"MainEpubReader:WebResourceRequested: External URL URL={str}");
            }
            if (response == null)
            {
                response = new HttpResponseMessage(HttpStatusCode.NotFound)
                {
                    ReasonPhrase = "Cant find embedded resource",
                };
            }
            args.Response = response;
        }

        private async void OnContentLoading(WebView sender, WebViewContentLoadingEventArgs args)
        {
            if (Logger.LogExtraTiming)
            {
                var uri = args.Uri == null ? "null" : args.Uri.ToString();
                await Logger.LogAsync($"MainEpubReader:HTML:OnContentLoading: URL={uri}");
            }
        }
        private async void OnContentLoadCompleted(object sender, Windows.UI.Xaml.Navigation.NavigationEventArgs args)
        {
            if (Logger.LogExtraTiming)
            {
                var uri = args.Uri == null ? "null" : args.Uri.ToString();
                await Logger.LogAsync($"MainEpubReader:HTML:OnContentLoad Completed: URL={uri}");
            }

        }
        private async void OnNavigationStarting(WebView sender, WebViewNavigationStartingEventArgs args)
        {
            if (args.Uri != null)
            {
                // The user clicked a link; kill it right now.
                args.Cancel = true;
                if (args.Uri.Scheme == "about")
                {
                    // The Bitcoin for Dummies uses e.g., about:blank#ind480 for the index.
                    await Logger.LogAsync($"MainEpubReader:HTML:OnNavigationStarting: is about: URL={args.Uri}  path={args.Uri.AbsolutePath} fragment={args.Uri.Fragment}");
                    var section = args.Uri.AbsolutePath;
                    var id = args.Uri.Fragment;
                    if (id.StartsWith("#")) id = id.Substring(1);
                    if (section == "blank")
                    {
                        // Is in this very page; jump to it. 
                        BookLocation bookLocation = new BookLocation() { HtmlFileName="", Location = id };
                        this.NavigateTo(ControlId, bookLocation);
                    }
                    else
                    {
                        // Will also need to find the page and go there.
                        BookLocation bookLocation = new BookLocation() { HtmlFileName = section, Location = id };
                        this.NavigateTo(ControlId, bookLocation);
                    }
                }
                else
                {
                    var task = Windows.System.Launcher.LaunchUriAsync(args.Uri);
                }
            }
            if (Logger.LogExtraTiming)
            {
                var uri = args.Uri == null ? "null" : args.Uri.ToString();
                await Logger.LogAsync($"MainEpubReader:OnNavigationStarting: URL={uri}");
            }
        }



        public string GetChapterContainingId(string id, int preferredHtmlIndex)
        {
            return EpubWizard.GetChapterContainingId(EpubBook, id, preferredHtmlIndex);
        }


        /// <summary>
        /// Requires that the book is already at the right Html (otherwise the javascript calls won't work)
        /// </summary>
        /// <param name="location"></param>
        /// <returns></returns>
        public async Task<string> GetChapterBeforePercentAsync(BookLocation location)
        {
            // Some books are just text files.
            string id = "";
            try
            {
                if (CurrHtml != null && CurrHtml.Contains("getClosestTagNear"))
                {
                    id = await uiHtml.InvokeScriptAsync("getClosestTagNear", new List<string>() { location.ScrollPercent.ToString() });
                }
            }
            catch (Exception)
            {
                ; // will happen if the book was a text file.
            }
            return id;
        }

        string MostRecentSelection { get; set; }

        private void OnScriptNotify(object sender, NotifyEventArgs e)
        {
            var parsed = e.Value.Split(new char[] { ':' }, 2);
            var cmd = parsed[0];
            var param = parsed[1];
            var nav = Navigator.Get();
            switch (cmd)
            {
                default:
                    App.Error($"WebView:ERROR:Log:{cmd} and {param} are not valid");
                    break;
                case "dbg":
                    if (Logger.LogExtraTiming || !param.Contains("doWindowReportScroll:"))
                    {
                        Logger.Log($"WebView:Log:{param}");
                    }
                    break;
                case "monopage":
                    // Reported when this particular section doesn't even fit onto a screen.
                    // A next page should always trigger the next section.
                    CurrPageIsMonoPage = true;
                    break;
                case "scroll":
                    {
                        var status = double.TryParse(param, out double value);
                        if (Logger.LogExtraTiming)
                        {
                            Logger.Log($"Report:Scroll: percent={param:F3} status={status} and update position htmlIndex={CurrHtmlIndex} scroll={CurrScrollPosition:F3} to {value:F3}");
                        }
                        if (status) 
                        {
                            CurrScrollPosition = value;
                            SavePositionEZ();
                            uiAllUpPosition.UpdatePosition(CurrHtmlIndex, CurrScrollPosition);
                            // If we're at the bottom of the html section, and there's a next section, enable the button.
                            uiNextPage.IsEnabled = HaveNextPage();
                            uiPrevPage.IsEnabled = HavePreviousPage();
                        }
                        //Logger.Log($"Report:Scroll: finished update");
                    }
                    break;
                case "select":
                    //Logger.Log("Selection!" + param);
                    MostRecentSelection = param;
                    nav.UserSelected(ControlId, param);
                    break;
                case "selectpos":
                    {
                        //Logger.Log("SelectPos!" + param);
                        var status = double.TryParse(param, out double value);
                        if (status)
                        {
                            CurrSelectPosition = value;
                        }
                    }
                    break;
                case "topid":
                    if (Logger.LogExtraTiming)
                    {
                        Logger.Log($"Report:TopId: closest id is {param} htmlIndex={CurrHtmlIndex}");
                    }
                    // FAIL: Into to Planetary Nebula has chapters (like the dedication) with no 'id' tags at all. On navigation,
                    // these report back that they've navigated to the autogenerated 'uiLog' item which then isn't found in any of
                    // the chapters, resulting in the navigation switching back to the title page.
                    if (param == "uiLog")
                    {
                        nav.UserNavigatedTo(ControlId, new BookLocation(CurrHtmlIndex, 0));
                    }
                    else
                    {
                        nav.UserNavigatedTo(ControlId, new BookLocation(CurrHtmlIndex, param));
                    }
                    //Logger.Log($"Report:TopId: finished navigation");
                    break;
                case "topnoid":
                    // The page scrolled, but the page has no id at all. We have to pick a chapter based just on 
                    // the current HTML and hope it's good enough.
                    if (Logger.LogExtraTiming)
                    {
                        Logger.Log($"Report:TopINod: no id, but the htmlIndex={CurrHtmlIndex}");
                    }
                    // Hang on -- why isn't this reporting as the uiLog??
                    nav.UserNavigatedTo(ControlId, new BookLocation(CurrHtmlIndex, 0));
                    break;
            }
        }

        readonly static string LoadingScreen =
@"<!DOCTYPE html>
<html>
    <head>
        <title>Loading....</title>
    </head>
    <body>
        <h1>Loading new ebook</h1>
Please wait while your ebook is loading
    </body>
</html>
";

        readonly static string LoadFailureScreen =
@"<!DOCTYPE html>
<html>
    <head>
        <title>Unable to load e-book</title>
    </head>
    <body>
        <h1>ERROR: unable to load ebook</h1>
An internal error has occured; unable to load ebook
   </body>
</html>
";
        /// <summary>
        /// Clears out the current book HTML and replaces it with a loading screen.
        /// </summary>
        private void SetScreenToLoading(string str = null)
        {
            if (str == null) str = LoadingScreen;
            CurrHtml = str;
            uiHtml.NavigateToString(CurrHtml);
        }

        private async void OnReviewClicked(object sender, RoutedEventArgs e)
        {
            // The review page might have to set navigation data (review includes UserStatus). Let's make 100% sure
            // that the database has correct navigation data set up. Hint: it really should already because if we are
            // here, then the book has been set to "reading", and that should in turn force navigation data.
            var bookdb = BookDataContext.Get();
            EnsureBookNavigationData(bookdb);

            var sh = new ContentDialog()
            {
                Title = "Review Book",
                PrimaryButtonText = "OK",
            };
            // Old style: var review = new BookReview(); // This is the edit control, not the UserReview data
            var review = new ReviewNoteStatusControl(); // This is the edit control, not the UserReview data

            // Set up the potential note, if that's what the user is going to do.
            var location = GetCurrBookLocation().ToJson();
            string currSelection = "";
            if (CurrHtml != null && CurrHtml.Contains("DoGetSelection"))
            {
                currSelection = await uiHtml.InvokeScriptAsync("DoGetSelection", null);
            }
            var note = new UserNote()
            {
                BookId = BookData.BookId,
                CreateDate = DateTimeOffset.Now,
                Location = location,
                Text = currSelection,
            };
            review.SetupNote(note);


            sh.Content = review;
            string defaultReviewText = "";
            if (CurrHtml != null && CurrHtml.Contains("DoGetSelection"))
            {
                defaultReviewText = await uiHtml.InvokeScriptAsync("DoGetSelection", null);
            }
            review.SetBookData(BookData, defaultReviewText);
            var result = await sh.ShowAsync();
            switch (result)
            {
                case ContentDialogResult.Primary:
                    review.SaveData();
                    var nav = Navigator.Get();
                    nav.UpdateProjectRome(ControlId, GetCurrBookLocation());
                    break;
            }
            SetReviewSymbol();
        }

        private BookLocation GetCurrBookLocation()
        {
            double pos = !double.IsNaN(CurrSelectPosition) ? CurrSelectPosition : CurrScrollPosition;
            var location = new BookLocation(CurrHtmlIndex, pos);
            return location;
        }

        private async void OnAddNote(object sender, RoutedEventArgs e)
        {
            var location = GetCurrBookLocation().ToJson();
            string currSelection = "";
            if (CurrHtml != null && CurrHtml.Contains("DoGetSelection"))
            {
                currSelection = await uiHtml.InvokeScriptAsync("DoGetSelection", null);
            }
            var note = new UserNote()
            {
                BookId = BookData.BookId,
                CreateDate = DateTimeOffset.Now,
                Location = location,
                Text = currSelection,
            };

            await NoteEditor.EditNoteAsync(ControlId, note);
        }

        private BookNavigationData EnsureBookNavigationData(BookDataContext bookdb)
        {
            var nd = CommonQueries.BookNavigationDataFind(bookdb, BookData.BookId);
            if (nd == null)
            {
                nd = new BookNavigationData()
                {
                    BookId = BookData.BookId,
                    CurrSpot = GetCurrBookLocation().ToJson()
                };
                CommonQueries.BookNavigationDataAdd(bookdb, nd, CommonQueries.ExistHandling.IfNotExists);
                nd = CommonQueries.BookNavigationDataFind(bookdb, BookData.BookId);
            }
            if (nd == null)
            {
                App.Error($"ERROR: trying to ensure navigation data, but don't have one.");
            }
            return nd;
        }

        string lastSavedPosition = "lskdfjsldkfjlkfjsdlkfj";
        private async void CurrPositionSaveTimer_Tick(object sender, object e)
        {
            // No need to keep on ticking
            CurrPositionSaveTimer.Stop();
            await SavePositionNow();
        }

        /// <summary>
        /// Will do an absolute save position. This is almost never needed; instead use the
        /// SavePositionEZ which will save in a CPU and disk friendlier way.
        /// </summary>
        private async Task SavePositionNow()
        {
            if (BookData == null) return;
            var bookdb = BookDataContext.Get();

            var currPosition = GetCurrBookLocation().ToJson();
            if (currPosition == lastSavedPosition) return;
            var nd = EnsureBookNavigationData(bookdb);
            if (nd == null) return;
            nd.CurrSpot = currPosition;
            lastSavedPosition = currPosition;
            CommonQueries.BookSaveChanges(bookdb);

            var localSettings = Windows.Storage.ApplicationData.Current.LocalSettings;
            localSettings.Values[CURR_READING_BOOK_ID] = BookData.BookId;
            localSettings.Values[CURR_READING_BOOK_POS] = currPosition;

            // Update the bookmark file, too.
            await BookMarkFile.SmartSaveAsync(BookMarkFile.BookMarkFileType.RecentOnly);
        }

        private void SavePositionEZ()
        {
            if (!CurrPositionSaveTimer.IsEnabled)
            {
                CurrPositionSaveTimer.Start();
            }
        }

        const string CURR_READING_BOOK_ID = "CurrBookId";
        const string CURR_READING_BOOK_POS = "CurrBookPos";

        public static (string bookId, string bookPos) GetCurrReadingBook()
        {
            var localSettings = Windows.Storage.ApplicationData.Current.LocalSettings;
            var id = localSettings.Values[CURR_READING_BOOK_ID] as string;
            var pos = localSettings.Values[CURR_READING_BOOK_POS] as string;
            return (id, pos);
        }
        public static void ClearCurrReadingBook()
        {
            var localSettings = Windows.Storage.ApplicationData.Current.LocalSettings;
            localSettings.Values[CURR_READING_BOOK_ID] = null;
            localSettings.Values[CURR_READING_BOOK_POS] = null;
           
        }

        private void MarkBookUserCurrStatus(BookNavigationData.UserStatus status)
        {
            var bookdb = BookDataContext.Get();
            var nd = EnsureBookNavigationData(bookdb);
            if (nd == null) return;
            nd.TimeMarkedDone = DateTimeOffset.Now;
            nd.CurrStatus = status; // e.g. Abandoned, Read, Reading, NoStatus
            CommonQueries.BookSaveChanges(bookdb);
        }

        private void OnFinishedBook(object sender, RoutedEventArgs e)
        {
            MarkBookUserCurrStatus(BookNavigationData.UserStatus.Done);
        }

        private void OnAbandonBook(object sender, RoutedEventArgs e)
        {
            MarkBookUserCurrStatus(BookNavigationData.UserStatus.Abandoned);
        }

        private void OnMakeUnreadBook(object sender, RoutedEventArgs e)
        {
            MarkBookUserCurrStatus(BookNavigationData.UserStatus.NoStatus);
        }

        // Note the subtle different between TappedRoutedEventArgs and RoutedEventArgs :-)
        private async void OnNextPage(object sender, TappedRoutedEventArgs e)
        {
            // Logger.Log($"OnNextPage (tapped on rectangle)");
            await DoNextPage();
        }
        // Note the subtle different between TappedRoutedEventArgs and RoutedEventArgs :-)
        private async void OnNextPage(object sender, RoutedEventArgs e)
        {
            await DoNextPage();
        }

        public async Task DoNextPage()
        {
            // Logger.Log($"DoNextPage: check NextScroll...");
            if (NextScrollGoesToNextSection())
            {
                DoNextSection();
            }
            else
            {
                //Logger.Log($"DoNextPage: about to scroll");
                await uiHtml.InvokeScriptAsync("scrollPage", new List<string>() { "1" });
                //Logger.Log($"DoNextPage: scroll complete");
            }
        }


        bool CurrPageIsMonoPage = false;
        private bool NextScrollGoesToNextSection()
        {
            bool sectionIsDone = (CurrScrollPosition >= (BottomScrollPosition-0.1)) || CurrPageIsMonoPage;
            var retval =  sectionIsDone && (CurrHtmlIndex < (EpubWizard.NHtmlIndex(EpubBook) - 1));
            return retval;
        }

        private bool HaveNextPage()
        {
            var retval = (CurrScrollPosition <= BottomScrollPosition) || (CurrHtmlIndex < (EpubWizard.NHtmlIndex(EpubBook) - 1));
            return retval;

        }
        private bool HavePreviousPage()
        {
            var retval = (CurrScrollPosition >= TopScrollPosition) || CurrHtmlIndex >= 1;
            return retval;
        }

        private void DoNextSection()
        {
            var location = new BookLocation(CurrHtmlIndex + 1, 0);
            NavigateToPercent(location);
            CurrPageIsMonoPage = false;
        }

        private async void OnPrevPage(object sender, RoutedEventArgs e)
        {
            await DoPrevPage();
        }
        public async Task DoPrevPage()
        {
            if ((CurrScrollPosition <= TopScrollPosition) && (CurrHtmlIndex > 0))
            {
                if (Logger.LogExtraTiming)
                {
                    Logger.Log($"MainEpubReader:OnPrevPage: move back one page from {CurrHtmlIndex}");
                }
                var location = new BookLocation(CurrHtmlIndex - 1, 100);
                NavigateToPercent(location);
            }
            else
            {
                await uiHtml.InvokeScriptAsync("scrollPage", new List<string>() { "-1" });
            }

        }

        private void OnFirstPage(object sender, RoutedEventArgs e)
        {
            var location = new BookLocation(0, 0); // section, percent=0
            NavigateToPercent(location);
        }


        string DeferredFont = null;
        string DeferredSize = null; // e.g. "12pt"
        public async Task SetFontAndSizeAsync(string font, string size)
        {
            try
            {
                if (!string.IsNullOrEmpty(CurrHtml) && CurrHtml.Contains ("SetFontAndSize")) // make sure we can actually call the function
                {
                    DeferredFont = null;
                    DeferredSize = null;
                    Logger.Log($"SetFontAndSize Immediate: ({font}, {size})");
                    await uiHtml.InvokeScriptAsync("SetFontAndSize", new List<string>() { font, size });
                }
                else
                {
                    Logger.Log($"SetFontAndSize: Defer ({font}, {size})");
                    DeferredFont = font;
                    DeferredSize = size;
                }
            }
            catch (Exception)
            {
                Logger.Log($"ERROR: unable to set font {font} size {size}");
            }
        }

        // COLORTHEME: don't actually ever set colors
        // As of 2020-05-02, it doesn't work well enough
        private async Task SetColorsAsync(Windows.UI.Color back, Windows.UI.Color fore)
        {
            var bstr = $"#{back.R:X2}{back.G:X2}{back.B:X2}";
            var fstr = $"#{fore.R:X2}{fore.G:X2}{fore.B:X2}";
            Logger.Log($"HTML: setting color bg={bstr} fg={fstr}");
            // // // await uiHtml.InvokeScriptAsync("SetColor", new List<string>() { bstr, fstr }); ;
            await Task.Delay(0); // just make the compiler quiet.
        }

        public void SetAppColors(Windows.UI.Color bg, Windows.UI.Color fg)
        {
            var task = SetColorsAsync(bg, fg);
        }

        Point PointerPressedPosition = new Point(-99999, -99999);
        DateTimeOffset PointerPressedTime = DateTimeOffset.MinValue;

        
        private void OnPointerPressed(object sender, PointerRoutedEventArgs e)
        {
            PointerPressedPosition = e.GetCurrentPoint(null).Position;
            PointerPressedTime = DateTimeOffset.UtcNow;
        }

        const double MAX_DELTA_PRESS_TIME_IN_SECONDS = 0.80;
        const double MAX_DISTANCE = 100;
        private async void OnPointerReleased(object sender, PointerRoutedEventArgs e)
        {
            // Must start and end on the blue rectangle
            if (PointerPressedPosition.X < -50000) return;

            // Must happen fast enough
            var timeDelta = DateTimeOffset.UtcNow.Subtract(PointerPressedTime).TotalSeconds;
            if (timeDelta > MAX_DELTA_PRESS_TIME_IN_SECONDS) return;

            var currPosition = e.GetCurrentPoint(null).Position;
            var dx = currPosition.X - PointerPressedPosition.X;
            var dy = currPosition.Y - PointerPressedPosition.Y;
            var d = Math.Abs(dx) + Math.Abs(dy); // Manhatten (rectangle) distance.
            if (d > MAX_DISTANCE)
            {
                Logger.Log($"TAP ATTEMPT: distance={d}");
                return; // not a tap
            }
            else
            {
                Logger.Log($"TAP OK: distance={d}");
            }

            await DoNextPage();
        }

        public EpubFile GetImageByName(string imageName)
        {
            return EpubWizard.GetInternalFileByName(EpubBook, imageName);
        }
    }
}
