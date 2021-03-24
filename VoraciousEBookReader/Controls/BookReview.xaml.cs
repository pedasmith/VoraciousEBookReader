using SimpleEpubReader.Database;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

// The User Control item template is documented at https://go.microsoft.com/fwlink/?LinkId=234236

namespace SimpleEpubReader.Controls
{
    /// <summary>
    /// The DataContext is a BookData object
    /// </summary>
    public sealed partial class BookReview : UserControl
    {
        public BookReview()
        {
            this.InitializeComponent();
        }

        public void SaveData()
        {
            if (BookData != null && BookData.Review != null)
            {
                if (BookData.Review == null)
                {
                    BookData.Review = new UserReview();
                }
                BookData.Review.Review = uiReview.TextValue;
                BookData.Review.Tags = uiTags.Text;
                var bookdb = BookDataContext.Get();
                CommonQueries.BookSaveChanges(bookdb);
            }
        }


        private BookData BookData { get; set; }

        public void SetBookData(BookData bookData, string defaultText)
        {
            BookData = bookData;
            if (BookData.Review == null)
            {
                BookData.Review = new UserReview() { BookId = bookData.BookId };
            }
            if (string.IsNullOrEmpty(BookData.Review.Review))
            {
                BookData.Review.Review = defaultText;
            }
            uiBookCard.DataContext = BookData;
            UpdateUI();
        }

        private void UpdateUI()
        {
            if (BookData == null) return;

            uiNCatalogViews.Text = BookData.NavigationData?.NCatalogViews.ToString() ?? "?";
            uiNSwipeLeft.Text = BookData.NavigationData?.NSwipeLeft.ToString() ?? "?";
            uiNSwipeRight.Text = BookData.NavigationData?.NSwipeRight.ToString() ?? "?";
            uiNReading.Text = BookData.NavigationData?.NReading.ToString() ?? "?";
            uiNSpecificSelection.Text = BookData.NavigationData?.NSpecificSelection.ToString() ?? "?";

            uiCurrSpot.Text = BookData.NavigationData?.CurrSpot ?? "?";
            uiCurrStatus.Text = BookData.NavigationData?.CurrStatus.ToString() ?? "?";

            uiBookId.Text = BookData.BookId;
            uiFilePath.Text = BookData.DownloadData?.FilePath ?? "?";
            uiFileName.Text = BookData.DownloadData?.FileName ?? "?";
            uiFileStatus.Text = BookData.DownloadData?.CurrFileStatus.ToString() ?? "?";

            uiTags.Text = BookData.Review?.Tags ?? "";
            if (BookData.Review != null && BookData.Review.NStars > 0) uiNStars.Value = BookData.Review.NStars;
            uiReview.SetText(BookData.Review?.Review ?? "");

            // And get the status from the navigation data. 
            if (BookData.NavigationData != null)
            {
                switch (BookData.NavigationData.CurrStatus)
                {
                    case BookNavigationData.UserStatus.Abandoned:
                        uiUserStatusAbandoned.IsChecked = true;
                        break;
                    case BookNavigationData.UserStatus.Done:
                        uiUserStatusDone.IsChecked = true;
                        break;
                    case BookNavigationData.UserStatus.Reading:
                        uiUserStatusReading.IsChecked = true;
                        break;
                }
            }
        }

        private void OnTagChanged(object sender, TextChangedEventArgs e)
        {
            // Actually saved on unload.
        }

        private void OnStarRatingChanged(RatingControl sender, object args)
        {
            var value = sender.Value;
            BookData.Review.NStars = value;
            var bookdb = BookDataContext.Get();
            CommonQueries.BookSaveChanges(bookdb);
        }

        private void OnUserStatusChecked(object sender, RoutedEventArgs e)
        {
            if (BookData.NavigationData != null)
            {
                if (uiUserStatusAbandoned.IsChecked.Value) BookData.NavigationData.CurrStatus = BookNavigationData.UserStatus.Abandoned;
                if (uiUserStatusDone.IsChecked.Value) BookData.NavigationData.CurrStatus = BookNavigationData.UserStatus.Done;
                if (uiUserStatusReading.IsChecked.Value) BookData.NavigationData.CurrStatus = BookNavigationData.UserStatus.Reading;
                var bookdb = BookDataContext.Get();
                CommonQueries.BookSaveChanges(bookdb);
            }
        }
    }
}
