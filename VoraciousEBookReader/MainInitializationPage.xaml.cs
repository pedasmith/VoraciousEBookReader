using SimpleEpubReader.Database;
using SimpleEpubReader.UwpDialogs;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=234238

namespace SimpleEpubReader
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainInitializationPage : Page
    {
        public MainInitializationPage()
        {
            this.InitializeComponent();
            this.Loaded += MainInitializationPage_Loaded;
        }

        private async void MainInitializationPage_Loaded(object sender, RoutedEventArgs e)
        {
            uiProgressRing.IsActive = true;

            await InitializeFilesToGet.CopyAssetDatabaseIfNeededAsync(new UwpRangeConverter (uiProgress));

            uiProgressRing.IsActive = false;
            uiStart.IsEnabled = true;
        }

        private void OnStart(object sender, RoutedEventArgs e)
        {
            var frame = Window.Current.Content as Frame;
            frame.Navigate(typeof(MainPage));
        }
    }
}
