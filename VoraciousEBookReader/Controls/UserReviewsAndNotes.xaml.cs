using SimpleEpubReader.Database;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

// The User Control item template is documented at https://go.microsoft.com/fwlink/?LinkId=234236

namespace SimpleEpubReader.Controls
{
    /// <summary>
    /// Is this class actually really used for anything at all???? I think not!
    /// </summary>
    public sealed partial class UserReviewsAndNotes : UserControl
    {
        public UserReviewsAndNotes()
        {
            this.InitializeComponent();
            this.Loaded += UserReviewsAndNotes_Loaded;
        }

        private void UserReviewsAndNotes_Loaded(object sender, RoutedEventArgs e)
        {
            // // // EnsureAllDatabase();
            this.DataContext = this;
        }

        // // // private static IList<UserReview> AllUserReviews { get; set; }
        // // // private static IList<DownloadData> AllDownloadData { get; set; }

        private void OnAdd(object sender, RoutedEventArgs e)
        {
            ; //NOTE: implement an editor :-) Or get rid of this control entirely because it's never used!
        }

        private static void EnsureAllDatabase()
        {
#if NEVER_EVER_DEFINED
            if (AllUserReviews == null || AllDownloadData == null)
            {
                var bookdb = BookDataContext.Get();
                AllUserReviews = CommonQueries.UserReviewsGetAll(bookdb);
                AllDownloadData = CommonQueries.DownloadedBooksGetAll(bookdb);
            }
#endif
        }


        public static void AddDownloadData(DownloadData dd)
        {
            // // // EnsureAllDatabase();
            // // // AllDownloadData.Add(dd); // Add to my list AND add to the database
        }
    }
}
