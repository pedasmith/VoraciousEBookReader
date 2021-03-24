using System;
using System.Collections.ObjectModel;
using PCLStorage;
using SimpleEpubReader.UwpClasses;
using Windows.System.Threading;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;

// The User Control item template is documented at https://go.microsoft.com/fwlink/?LinkId=234236

namespace SimpleEpubReader.Controls
{
    public sealed partial class HelpControl : UserControl
    {
        public ObservableCollection<ImageSource> ImageList { get; } = new ObservableCollection<ImageSource>();
        public HelpControl()
        {
            this.InitializeComponent();
            this.DataContext = this; // set to myself so that the ImageList works.
            this.Loaded += HelpControl_Loaded;
        }

        private async void HelpControl_Loaded(object sender, RoutedEventArgs e)
        {
            var lastIndex = 0;
            bool userMovedHelpImages = false;

            // Start up the help file display
            var screenShotPath = await FolderMethods.GetScreenShotsFolderAsync();
            var helpScreenShot = await FileSystem.Current.GetFolderFromPathAsync(screenShotPath);

            var files = await helpScreenShot.GetFilesAsync();
            foreach (var file in files)
            {
                var filename = file.Name;
                if (filename.ToLower().EndsWith(".png"))
                {
                    var uri = new Uri($"ms-appx:///Assets/HelpScreenShots/{filename}");
                    var bmi = new BitmapImage(uri);
                    ImageList.Add(bmi);
                }
            }

            var period = TimeSpan.FromSeconds(5.0); // Eyeballing it, 5 seconds is a good speed.
            var periodicTimer = ThreadPoolTimer.CreatePeriodicTimer(async (source) =>
            {
                await this.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
                {
                    if (this.Visibility != Visibility.Visible) return;

                    if (uiImageFlip.SelectedIndex != lastIndex)
                    {
                        userMovedHelpImages = true;
                    }
                    if (!userMovedHelpImages)
                    {
                        // If the user wants to see them, let them!
                        var nextIndex = uiImageFlip.SelectedIndex + 1;
                        if (nextIndex >= ImageList.Count) nextIndex = 0;
                        uiImageFlip.SelectedIndex = nextIndex;
                        lastIndex = nextIndex;
                    }
                });
            }, period);
        }
    }
}
