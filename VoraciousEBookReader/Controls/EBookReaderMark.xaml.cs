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


        private void OnMark(object sender, RoutedEventArgs e)
        {
            int nok = 0;

            var bookdb = BookDataContext.Get();
            var selectedBooks = GetSelectedBooks();
            foreach (var bookData in selectedBooks)
            {
                var srcfullname = bookData.DownloadData.FullFilePath;
                var fname = bookData.DownloadData.FileName;
                Logger.Log($"MARK: setting {fname} to {NewStatus}");
                var nd = CommonQueries.BookNavigationDataEnsure(bookdb, bookData);
                nd.CurrStatus = NewStatus;
                nok++;
            }

            Logger.Log($"COPY: OK={nok}");

            if (nok > 0)
            {
                CommonQueries.BookSaveChanges(bookdb);
            }

            // End by closing the dialog box.
            var parent = this.Parent as ContentDialog;
            parent.Hide();
        }

    }
}
