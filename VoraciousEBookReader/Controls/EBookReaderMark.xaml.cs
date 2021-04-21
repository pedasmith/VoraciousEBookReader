using SimpleEpubReader.Database;
using SimpleEpubReader.Searching;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Foundation.Collections;
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
        public ObservableCollection<BookData> Books { get; } = new ObservableCollection<BookData>();
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
            await UpdateList();
        }

        private IList<BookData> GetSelectedBooks()
        {
            var retval = new List<BookData>();
            foreach (var book in Books)
            {
                var lvi = uiBookList.ContainerFromItem(book) as ListViewItem;
                var check = lvi.ContentTemplateRoot as CheckBox; // must be kept in sync with the XAML, of course.
                if (check.IsChecked.Value)
                {
                    retval.Add(book);
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
                Books.Add(book);
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
                var lvi = uiBookList.ContainerFromItem(book) as ListViewItem;
                var check = lvi.ContentTemplateRoot as CheckBox; // must be kept in sync with the XAML, of course.
                check.IsChecked = newCheck;
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


        private void OnMark(object sender, RoutedEventArgs e)
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
            switch (mark)
            {
                case MarkCommandType.ChangeStatus:
                    foreach (var bookData in selectedBooks)
                    {
                        var srcfullname = bookData.DownloadData.FullFilePath;
                        var fname = bookData.DownloadData.FileName;
                        Logger.Log($"MARK: setting {fname} to {NewStatus}");
                        var nd = CommonQueries.BookNavigationDataEnsure(bookdb, bookData);
                        nd.CurrStatus = newStatus;
                        if (deleteBook)
                        {
                            // TODO: How to delete it?
                            ;
                        }
                        nok++;
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
            Logger.Log($"COPY: OK={nok}");

            // End by closing the dialog box.
            var parent = this.Parent as ContentDialog;
            parent.Hide();
        }


        IList<BookData> SavedSelectedBooks = null;
        Boolean SavedDeleteBook = false;

        public async Task RunSavedReviewEachBook()
        {
            if (SavedSelectedBooks == null) return;
            var selectedBooks = SavedSelectedBooks;
            var deleteBook = SavedDeleteBook;

            var bookdb = BookDataContext.Get();
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
                // Old style: var review = new BookReview(); // This is the edit control, not the UserReview data
                var review = new ReviewNoteStatusControl(); // This is the edit control, not the UserReview data
                string defaultReviewText = null;
                var BookData = bookData;

                review.SetBookData(BookData, defaultReviewText);
                sh.Content = review;
                var result = await sh.ShowAsync();
                switch (result)
                {
                    case ContentDialogResult.Primary:
                        review.SaveData();
                        var nav = Navigator.Get();
                        //TODO: setup Rome?? nav.UpdateProjectRome(ControlId, GetCurrBookLocation());
                        break;
                }
                if (deleteBook)
                {
                    // How to delete it?
                    ;
                }
            }
        }
    }
}
