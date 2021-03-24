using SimpleEpubReader.Database;
using SimpleEpubReader.EbookReader;
using SimpleEpubReader.Searching;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using static SimpleEpubReader.Controls.Navigator;

// The User Control item template is documented at https://go.microsoft.com/fwlink/?LinkId=234236

namespace SimpleEpubReader.Controls
{
    // Is a simplebookHandler because it will get a BookDisplay when the notes have changed.
    public sealed partial class BookSearch : UserControl, SimpleBookHandler
    {
        // We always track which one we are
        const NavigateControlId ControlId = NavigateControlId.ChapterDisplay;
        public CommandBar ParentCommandBar = null;

        const string BookSearch_SearchType = "BookSearch_SearchType";
        const string BookSearch_Language = "BookSearch_Language";

        public ObservableCollection<BookData> Books { get; set; } = new ObservableCollection<BookData>();
        static private Dictionary<string, BookNotes> AllNotes { get; set; } = null; // Organized by bookId
        static private Dictionary<string, DownloadData> AllDownloadData { get; set; } = null; // Organized by bookId
        string SearchType = "Downloaded"; // Downloaded Reading
        bool isInitialized = false;
        static BookSearch BookSearchSingleton = null;

        public BookSearch()
        {
            BookSearchSingleton = this; // SIGH.
            this.InitializeComponent();
            this.DataContext = this;
            this.Loaded += BookSearch_Loaded;
        }
        //NOTE: moving the LanguagePickButton to the search bar.
        //AppBarButton LanguagePickButton = null;

        private string GetSetting (string id, string defaultValue)
        {
            var retval = defaultValue;
            var localSettings = Windows.Storage.ApplicationData.Current.LocalSettings;
            if (localSettings.Values.ContainsKey(id))
            {
                var newvalue = localSettings.Values[id] as string;
                if (newvalue != null)
                {
                    retval = newvalue;
                }
            }
            return retval;
        }

        /// <summary>
        /// Saves the search type (Pick, Downloaded, Reading)
        /// </summary>
        /// <param name="id"></param>
        /// <param name="value"></param>
        private void SetSetting (string id, string value)
        {
            var localSettings = Windows.Storage.ApplicationData.Current.LocalSettings;
            localSettings.Values[id] = value;
        }

        private async void BookSearch_Loaded(object sender, RoutedEventArgs e)
        {
            var localSettings = Windows.Storage.ApplicationData.Current.LocalSettings;
            var sortBy = localSettings.Values[SEARCH_SORT] as string;
            if (!string.IsNullOrEmpty (sortBy))
            {
                foreach (var item in uiSort.Items)
                {
                    var cbi = item as ComboBoxItem;
                    if ((cbi.Tag as string) == sortBy)
                    {
                        uiSort.SelectedItem = cbi;
                    }
                }
            }

            UserRequiredLanguageMatch = GetSetting(BookSearch_Language, UserRequiredLanguageMatch);
            SearchType = GetSetting(BookSearch_SearchType, SearchType);
            SetSearchTypeSelectionUI(SearchType);
            // We get called when the user switches tabs -- but in those cases,
            // we don't need to redo the search.
            if (Books.Count == 0)
            {
                await DoSearchAsync();
            }

            isInitialized = true; // finally initialized; can handle UI elements.
        }

        private async void LanguagePickButton_Click(object sender, RoutedEventArgs e)
        {
            var plc = new PickLanguageControl();
            plc.SetInitialLanguage(UserRequiredLanguageMatch);
            var cd = new ContentDialog()
            {
                Content = plc,
                PrimaryButtonText = "OK",
                SecondaryButtonText = "Cancel",
            };
            var result = await cd.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                UserRequiredLanguageMatch = plc.GetSelectedLanguage();
                SetSetting(BookSearch_Language, UserRequiredLanguageMatch);
            }
        }

        private void SetSearchTypeSelectionUI (string tag)
        {
            foreach (var child in uiSearchType.Children)
            {
                var grid = child as Grid;
                if (grid == null) continue;
                foreach (var child2 in grid.Children)
                {
                    var fe = child2 as FrameworkElement;
                    if (fe == null) return;
                    fe.Opacity = (fe.Tag as string) == tag ? 1.0 : 0.4;
                }
            }
        }


        private async void uiSearchFor_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == Windows.System.VirtualKey.Enter)
            {
                await DoSearchAsync();
            }
        }

        private void UISearchFor_Changed(object sender, TextChangedEventArgs e)
        {
            // Don't search per keyboard (it's not fast enough): DoSearch();
        }
        
        private async void OnSearchNow(object sender, RoutedEventArgs e)
        {
            await DoSearchAsync();
        }

        static string UserRequiredLanguageMatch = "en";


        public static void ReloadAllNotes()
        {
            AllNotes = new Dictionary<string, BookNotes>();
            var bn = CommonQueries.BookNotesGetAll();
            foreach (var note in bn)
            {
                AllNotes[note.BookId] = note;
            }
        }

        private static void EnsureBooksLoaded()
        {
            if (AllNotes == null)
            {
                ReloadAllNotes();
            }
        }

        private void SetSearchRingVisible(bool isVisible)
        {
            uiSearchRing.Visibility = isVisible ? Visibility.Visible : Visibility.Collapsed;
            uiSearchRing.IsActive = isVisible;
        }

        const string SEARCH_SORT = "SearchSort";

        private async Task DoSearchAsync()
        {
            Books.Clear();
            EnsureBooksLoaded();
            SetSearchRingVisible(true);

            uiSearchStatus.Text = "..searching...";
            try
            {
                var startTime = DateTime.Now;
                var search = uiSearchFor.Text.Trim();
                var sortBy = (uiSort.SelectedItem as ComboBoxItem)?.Tag as string;
                var language = UserRequiredLanguageMatch;
                var localSettings = Windows.Storage.ApplicationData.Current.LocalSettings;
                localSettings.Values[SEARCH_SORT] = sortBy;

                List<BookData> resultList = null;
                bool andMore = false;

                var searchTask= Task.Run(() => {
                    var bookdb = BookDataContext.Get();
                    lock (bookdb)
                    {
                        resultList = AllBookSearch.SearchInternal(bookdb, SearchType, search, language, sortBy, out andMore);
                    }
                });
                await searchTask;

                // Finally add to the output
                foreach (var book in resultList)
                {
                    Books.Add(book);
                }
                var nmatch = Books.Count;
                uiSearchStatus.Text = andMore ? $"Found more than {AllBookSearch.MaxMatch} books" : $"Found {nmatch} books";

                var endTime = DateTime.Now;
                var smTime = Math.Round(endTime.Subtract(startTime).TotalSeconds, 2);
                uiSearchStatus.Text += $"\nin {smTime} seconds";
            }
            catch (Exception ex)
            {
                uiSearchStatus.Text = $"ERROR: search threw an exception :-( ({ex.Message})";
            }

            SetSearchRingVisible(false);
        }



        private async void OnSelectSearchType(object sender, TappedRoutedEventArgs e)
        {
            var tag = (sender as FrameworkElement).Tag as string;
            if (tag != SearchType)
            {
                SetSearchTypeSelectionUI(tag); // highlights the right tag
                SearchType = tag;
                SetSetting(BookSearch_SearchType, SearchType);
                await DoSearchAsync();
            }
        }

        private void OnCardTapped(object sender, TappedRoutedEventArgs e)
        {
            var bookdb = BookDataContext.Get();
            var book = (sender as BookCard)?.DataContext as BookData;
            if (book == null) return;
            var dd = CommonQueries.DownloadedBookFind(bookdb, book.BookId);
            if (dd == null) return; // Can't display a book that isn't there!
            var nav = Navigator.Get();
            nav.DisplayBook (ControlId, book);
        }

        BookData CurrBook = null;
        // Is called when the notes are updated. I don't really display a book.
        public async Task DisplayBook(BookData book, BookLocation location)
        {
            await Task.Delay(0);
            ReloadAllNotes();
            uiBookList.ScrollIntoView(book);
            uiBookList.SelectedItem = book; // get it selected, too, so it shows up!
            CurrBook = book;
            // Location isn't used at all.
        }

        private BookCard GetBookCardFromSwipe(SwipeItemInvokedEventArgs args)
        {
            var item = args.SwipeControl.DataContext as BookData;
            if (item == null) return null;
            var container = uiBookList.ContainerFromItem(item) as ListViewItem;
            if (container == null) return null;
            var swipe = container.ContentTemplateRoot as SwipeControl;
            if (swipe == null) return null;
            var bookcard = swipe.Content as BookCard;
            if (bookcard == null) return null;
            return bookcard;
        }
        private BookCard GetBookCardFromBookData(BookData bookData)
        {
            if (bookData == null) return null;
            var container = uiBookList.ContainerFromItem(bookData) as ListViewItem;
            if (container == null) return null;
            var swipe = container.ContentTemplateRoot as SwipeControl;
            if (swipe == null) return null;
            var bookcard = swipe.Content as BookCard;
            if (bookcard == null) return null;
            return bookcard;
        }


        public static async Task DoSwipeDownloadOrReadAsync(BookData bookData)
        {
            if (BookSearchSingleton == null) return;
            var bookcard = BookSearchSingleton.GetBookCardFromBookData(bookData);
            if (bookcard != null)
            {
                bookcard.ResetDownloadPanel();
                await BookSearchSingleton.DoSwipeDownload(bookData);
            }
        }
        private async void OnSwipeDownload(SwipeItem sender, SwipeItemInvokedEventArgs args)
        {
            var bookcard = GetBookCardFromSwipe(args);
            if (bookcard == null) return;
            var bookData = bookcard.GetBookData();
            await DoSwipeDownload(bookData);
        }



        private async Task DoSwipeDownload(BookData bookData)
        {
            var bookcard = GetBookCardFromBookData(bookData);

            var bookdb = BookDataContext.Get();
            var nd = CommonQueries.BookNavigationDataEnsure(bookdb, bookData);
            nd.NSwipeRight++;
            nd.NSpecificSelection++;
            CommonQueries.BookSaveChanges(bookdb);

            // Before I can download, make sure that the download file list is set up.
            SetupDownloadsIfNeeded(bookData);

            // But wait! If the book is already downloaded, then just display it
            var fileStatus = bookData.DownloadData == null ? DownloadData.FileStatus.Unknown : bookData.DownloadData.CurrFileStatus;
            switch (fileStatus)
            {
                case DownloadData.FileStatus.Downloaded:
                    var nav = Navigator.Get();
                    nav.DisplayBook(ControlId, bookData);
                    break;
                default:
                    await bookcard.DoDownloadAsync();
                    break;
            }
        }

        private void OnSwipeRemove(SwipeItem sender, SwipeItemInvokedEventArgs args)
        {
            var bookdb = BookDataContext.Get();
            var bookcard = GetBookCardFromSwipe(args);
            if (bookcard == null) return;

            var bookData = bookcard.GetBookData();
            var nd = CommonQueries.BookNavigationDataEnsure(bookdb, bookData);
            nd.NSwipeLeft++;
            CommonQueries.BookSaveChanges(bookdb);

            bookData = args.SwipeControl.DataContext as BookData;
            Books.Remove(bookData);
        }

        private void SetupDownloadsIfNeeded(BookData bookData)
        {
            if (bookData != null)
            {
                var containerObject = uiBookList.ContainerFromItem(bookData);
                if (containerObject != null)
                {
                    var bookCard = (((containerObject as ListViewItem).ContentTemplateRoot as SwipeControl).Content as BookCard);
                    bookCard.EnsureDownloadPanel();
                    bookCard.CardIsSelected(true);
                }
            }

        }

        private void OnBookListSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var bookData = e.RemovedItems.Count == 1 ? e.RemovedItems[0] as BookData : null;
            if (bookData != null)
            {
                var containerObject = uiBookList.ContainerFromItem(bookData);
                // If from the Download list I download and then read a book, the book is automatically removed from the book list. 
                // When I get this remove call, then, the containerObject might well be gone.
                if (containerObject != null)
                {
                    (((containerObject as ListViewItem).ContentTemplateRoot as SwipeControl).Content as BookCard).CardIsSelected(false);
                }
            }

            bookData = e.AddedItems.Count == 1 ? e.AddedItems[0] as BookData : null;
            SetupDownloadsIfNeeded(bookData);
        }

        private void OnAddAuthorSearch(object sender, TappedRoutedEventArgs e)
        {
            var pre = (string.IsNullOrEmpty(uiSearchFor.Text)) ? "" : " ";
            var text = CurrBook?.BestAuthorDefaultIsNull;
            if (text != null)
            {
                var str = $"{pre}author:\"{text}\"";
                uiSearchFor.Text += str;
            }
        }

        private async void OnClearSearch(object sender, TappedRoutedEventArgs e)
        {
            uiSearchFor.Text = "";
            await DoSearchAsync();
        }

        private void OnAddSubjectSearch(object sender, TappedRoutedEventArgs e)
        {
            var pre = (string.IsNullOrEmpty(uiSearchFor.Text)) ? "" : " ";
            var text = CurrBook?.LCCN;
            if (!string.IsNullOrEmpty(text))
            {
                var str = $"{pre}lc:\"{text}\"";
                uiSearchFor.Text += str;
                pre = " ";
            }

            text = CurrBook?.LCC;
            if (!string.IsNullOrEmpty(text))
            {
                var str = $"{pre}lc:\"{text}\"";
                uiSearchFor.Text += str;
                pre = " ";
            }

            text = CurrBook?.LCSH;
            if (!string.IsNullOrEmpty(text))
            {
                var str = $"{pre}lcc:\"{text}\"";
                uiSearchFor.Text += str;
                pre = " ";
            }
        }


        const string SearchFormat =
@"#Search Format
Searching for a word will find it anywhere (title, author, notes, etc)

Put words in quotes to find those words exactly like this
**""oliver twist""

## Prefixes will search in just specific places
**author:dickens** will find books written by dickens

**title:dickens** will find books where the title includes dickens

**lc:DA** finds books in Library of Congress section DA (world history, great britain)

**lcc:poetry** finds book with LC subject heading including Poetry
";

        private async void OnShowSearchFormatHelp(object sender, TappedRoutedEventArgs e)
        {
            var md = new Microsoft.Toolkit.Uwp.UI.Controls.MarkdownTextBlock()
            {
                Text = SearchFormat,
                MaxWidth = 500,
                MinWidth = 250,
                IsTextSelectionEnabled = true,
            };
            var cd = new ContentDialog()
            {
                Content = md,
                PrimaryButtonText = "Ok",
            };
            await cd.ShowAsync();
        }

        private async void OnSortChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!isInitialized) return; //is the first call; we're not really initialized yet, so don't make the call.
            await DoSearchAsync();
        }
    }
}
