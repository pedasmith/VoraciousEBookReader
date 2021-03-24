using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

// The User Control item template is documented at https://go.microsoft.com/fwlink/?LinkId=234236

namespace SimpleEpubReader.Controls
{
    /// <summary>
    /// This is hooked to the App UserCustomization (but only for font size....)
    /// </summary>
    public sealed partial class CustomizeControl : UserControl
    {
        public CustomizeControl()
        {
            this.InitializeComponent();

            // Add in the fonts
            foreach (var (key, item) in UserCustomization.UserFonts)
            {
                uiFontSelect.Items.Add(new ComboBoxItem() { Content = key, FontFamily = item });
            }

            this.Loaded += CustomizeControl_Loaded;
        }

        string currFont = null;
        private void CustomizeControl_Loaded(object sender, RoutedEventArgs e)
        {
            var userCustomization = (App.Current as App).Customization;
            currFont = userCustomization.GetUserFontName();

            // Select as the font
            foreach (var item in uiFontSelect.Items)
            {
                var name = (item as ComboBoxItem).Content as string;
                if (name == currFont)
                {
                    uiFontSelect.SelectedItem = item;
                }
            }
            uiFontSelect.SelectionChanged += OnUpdateFont;
        }

        private void OnUpdateFont(object sender, SelectionChangedEventArgs e)
        {
            var cbi  = (sender as ComboBox).SelectedItem as ComboBoxItem;
            if (cbi == null) return;
            var font = cbi.FontFamily;
            currFont = cbi.Content as string;

            var userCustomization = (App.Current as App).Customization;
            userCustomization.StandardFF = font;
            userCustomization.SetSavedUserFontNameAndSize(currFont, userCustomization.FontSize);
        }
#if NEVER_EVER_DEFINED
// Not doing any color stuff until it can be made to work better 
        private void OnSetLight(object sender, RoutedEventArgs e)
        {
            var nav = Navigator.Get();
            nav.SetAppColors(Colors.AntiqueWhite, Colors.DarkGreen);

        }

        private void OnSetDark(object sender, RoutedEventArgs e)
        {
            var nav = Navigator.Get();
            nav.SetAppColors(Colors.DarkRed, Colors.WhiteSmoke);
        }

        private void OnSetFromColors(object sender, RoutedEventArgs e)
        {
            var b = (sender as Button);
            var bg = (b.Background as SolidColorBrush).Color;
            var fg = (b.Foreground as SolidColorBrush).Color;
            var nav = Navigator.Get();
            nav.SetAppColors(bg, fg);
        }
#endif
    }
}
