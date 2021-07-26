using Microsoft.Toolkit.Uwp.Helpers;
using SimpleEpubReader.Database;
using SimpleEpubReader.Searching;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Storage;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

// The User Control item template is documented at https://go.microsoft.com/fwlink/?LinkId=234236

namespace SimpleEpubReader.Controls
{
    public sealed partial class EBookReaderMark : UserControl
    {
        public ObservableCollection<HelperBookDataWithSelected> Books { get; } = new ObservableCollection<HelperBookDataWithSelected>();

        BookNavigationData.UserStatus NewStatus;
        public EBookReaderMark(BookNavigationData.UserStatus newStatus)
        {
            NewStatus = newStatus;
            this.InitializeComponent();
            this.DataContext = this;
            this.Loaded += EBookReaderMark_Loaded;
        }

        private async void EBookReaderMark_Loaded(object sender, RoutedEventArgs e)
        {
            // Make sure it's not visible
            uiAlternateContent.Visibility = Visibility.Collapsed;
            await UpdateList();
        }

        private IList<BookData> GetSelectedBooks()
        {
            var retval = new List<BookData>();
            foreach (var book in Books)
            {
                if (book.IsSelected)
                {
                    retval.Add(book.RawBook);
                }
            }
            return retval;
        }

        private async Task UpdateList()
        {
            // Get list of currently-reading books

            var startTime = DateTime.Now;
            var search = "";
            var searchType = "CopiedToEBookReader";
            var sortBy = "title";
            var language = "en";

            List<BookData> resultList = null;
            bool andMore = false;

            Books.Clear();

            var searchTask = Task.Run(() => {
                var bookdb = BookDataContext.Get();
                lock (bookdb)
                {
                    resultList = AllBookSearch.SearchInternal(bookdb, searchType, search, language, sortBy, out andMore);
                }
            });
            await searchTask;

            // Finally add to the output
            foreach (var book in resultList)
            {
                Books.Add(new HelperBookDataWithSelected (book));
            }
        }

        private void OnCancel(object sender, RoutedEventArgs e)
        {
            var parent = this.Parent as ContentDialog;
            parent.Hide();
        }

        private async void OnShowChange(object sender, RoutedEventArgs e)
        {
            await UpdateList();
        }

        private void OnSelectAllCheck(object sender, RoutedEventArgs e)
        {
            CheckAll(true);
        }

        private void OnSelectAllUnchecked(object sender, RoutedEventArgs e)
        {
            CheckAll(false);
        }

        private void CheckAll(bool newCheck)
        {
            foreach (var book in Books)
            {
                book.IsSelected = newCheck;
            }
        }

        enum MarkCommandType {  ChangeStatus, NoChange, ReviewEachBook };

        private MarkCommandType GetCommandType()
        {
            var cmd = (uiDoWhat?.SelectedItem as FrameworkElement)?.Tag as string;
            switch (cmd)
            {
                case "MarkAsFinishedRead":
                case "MarkAsAbandoned": 
                case "MarkAsDownloaded":
                case "MarkAsReading": return MarkCommandType.ChangeStatus;

                case "MakeNoChange": return MarkCommandType.NoChange;
                case "ReviewEachBook": return MarkCommandType.ReviewEachBook;
                default:
                    Logger.Log($"ERROR: unknown MarkCommandType {cmd}");
                    return MarkCommandType.NoChange;
            }
        }
        private BookNavigationData.UserStatus GetUserStatus()
        {
            var cmd = (uiDoWhat?.SelectedItem as FrameworkElement)?.Tag as string;
            switch (cmd)
            {
                case "MarkAsFinishedRead": return BookNavigationData.UserStatus.Done;
                case "MarkAsAbandoned": return BookNavigationData.UserStatus.Abandoned;
                case "MarkAsDownloaded": return BookNavigationData.UserStatus.NoStatus;
                case "MarkAsReading": return BookNavigationData.UserStatus.Reading;

                default:
                    Logger.Log($"ERROR: unknown GetUserStatus {cmd}");
                    return BookNavigationData.UserStatus.NoStatus;
            }
        }

        StorageFolder ProgressFolder = null;
        private async Task SetupProgressFolderAsync()
        {
            ProgressFolder = (await EBookFolder.GetFolderSilentAsync()).Folder;
            if (ProgressFolder == null)
            {
                ProgressFolder = await EBookFolder.PickFolderAsync();
            }
        }
        private async Task<bool> DeleteDownloadedBookAsync(IProgressReader progress, BookData bookData)
        {

            if (ProgressFolder == null)
            {
                progress.AddLog("NOTE: no folder selected to save to");
                return false;
            }

            var fname = bookData.DownloadData.FileName;
            progress.SetCurrentBook(fname);
            try
            {
                var exists = await ProgressFolder.FileExistsAsync(fname);
                if (!exists)
                {
                    progress.AddLog($"NOTE: file {fname} is already not present.\n");
                    return false;
                }
                var file = await ProgressFolder.GetFileAsync(fname);
                if (file == null)
                {
                    progress.AddLog ($"NOTE: file {fname} was supposed to be present, but is not.\n");
                    return false;
                }

                await file.DeleteAsync();
            }
            catch (Exception ex)
            {
                var log = $"ERROR: exception thrown when deleting file {fname} exception {ex.Message}\n";
                Logger.Log(log);
                progress.AddLog(log);
                return false;
            }

            return true;
        }


        private async void OnMark(object sender, RoutedEventArgs e)
        {
            int nok = 0;
            var newStatus = NewStatus;
            MarkCommandType mark = GetCommandType();
            if (mark == MarkCommandType.ChangeStatus)
            {
                newStatus = GetUserStatus();
            }
            var deleteBook = uiDeleteFromReader.IsChecked.Value;

            var bookdb = BookDataContext.Get();
            var selectedBooks = GetSelectedBooks();

            // Setup to delete all selected books from the Nook
            EbookReaderProgressControl progress = null;
            if (deleteBook && mark != MarkCommandType.ReviewEachBook) // ReviewEachBook does this itself.
            {
                await SetupProgressFolderAsync();
                progress = new EbookReaderProgressControl();
                uiAlternateContent.Visibility = Visibility.Visible;
                uiAlternateContent.Children.Clear();
                uiAlternateContent.Children.Add(progress);
                progress.SetNBooks(selectedBooks.Count);
            }

            switch (mark)
            {
                case MarkCommandType.ChangeStatus:
                    foreach (var bookData in selectedBooks)
                    {
                        var srcfullname = bookData.DownloadData.FullFilePath;
                        var fname = bookData.DownloadData.FileName;
                        Logger.Log($"MARK: setting {fname} to {NewStatus}");

                        bool deleteOk = true;
                        if (progress != null)
                        {
                            deleteOk = await DeleteDownloadedBookAsync(progress, bookData);
                        }

                        if (deleteOk)
                        {
                            var nd = CommonQueries.BookNavigationDataEnsure(bookdb, bookData);
                            nd.CurrStatus = newStatus;
                            nok++;
                        }
                    }
                    break;
                case MarkCommandType.NoChange:
                    break;
                case MarkCommandType.ReviewEachBook:
                    SavedSelectedBooks = selectedBooks;
                    SavedDeleteBook = deleteBook;
                    break;
            }

            if (nok > 0)
            {
                CommonQueries.BookSaveChanges(bookdb);
            }

            if (progress != null)
            {
                progress.AddLog($"Book move complete");
                await Task.Delay(5_000); // wait so the user can see something happened.
            }


            Logger.Log($"COPY: OK={nok}");

            // End by closing the dialog box.
            var parent = this.Parent as ContentDialog;
            parent.Hide();
        }


        IList<BookData> SavedSelectedBooks = null;
        Boolean SavedDeleteBook = false;
        /// <summary>
        /// Handles the entire problem of reviewing each of the selected books. Will pop up a little pop-up
        /// and let the user do a review of each one.
        /// </summary>
        /// <returns></returns>
        public async Task RunSavedReviewEachBook()
        {
            if (SavedSelectedBooks == null) return;
            var selectedBooks = SavedSelectedBooks;
            var deleteBook = SavedDeleteBook;

            // Setup to delete all selected books from the Nook
            EbookReaderProgressControl progress = null;
            if (deleteBook)
            {
                await SetupProgressFolderAsync();
                progress = new EbookReaderProgressControl();
                uiAlternateContent.Visibility = Visibility.Visible;
                uiAlternateContent.Children.Clear();
                uiAlternateContent.Children.Add(progress);
                progress.SetNBooks(selectedBooks.Count);
            }

            var bookdb = BookDataContext.Get();
            var sh = new ContentDialog()
            {
                Title = "Review Book",
                PrimaryButtonText = "OK",
                SecondaryButtonText = "Cancel",
            };
            var reviewlist = new ReviewNoteStatusListControl();
            reviewlist.SetBookList(selectedBooks);
            sh.Content = reviewlist;
            var result = await sh.ShowAsync();
            ;
#if NEVER_EVER_DEFINED
// The old code that did a pop-up per book. The new way does one pop-up with a swipable list.
            foreach (var bookData in selectedBooks)
            {
                var srcfullname = bookData.DownloadData.FullFilePath;
                var fname = bookData.DownloadData.FileName;
                var nd = CommonQueries.BookNavigationDataEnsure(bookdb, bookData);
                var sh = new ContentDialog()
                {
                    Title = "Review Book",
                    PrimaryButtonText = "OK",
                    SecondaryButtonText = "Cancel",
                };
                var review = new ReviewNoteStatusControl(); 
                string defaultReviewText = null;
                var BookData = bookData;

                review.SetBookData(BookData, defaultReviewText);
                sh.Content = review;
                var result = await sh.ShowAsync();
                switch (result)
                {
                    case ContentDialogResult.Primary:
                        review.SaveData();

                        bool deleteOk = true;
                        if (progress != null)
                        {
                            deleteOk = await DeleteDownloadedBookAsync(progress, bookData);
                        }

                        var nav = Navigator.Get();
                        //TODO: setup Rome?? nav.UpdateProjectRome(ControlId, GetCurrBookLocation());
                        break;
                }
                if (deleteBook)
                {
                    // TODO: How to delete it?
                    ;
                }
            }
#endif
        }
    }
}
