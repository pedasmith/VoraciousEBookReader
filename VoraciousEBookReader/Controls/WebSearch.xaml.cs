using System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using static SimpleEpubReader.Controls.Navigator;

// The User Control item template is documented at https://go.microsoft.com/fwlink/?LinkId=234236

namespace SimpleEpubReader.Controls
{
    public sealed partial class WebSearch : UserControl, ISelectTo
    {
        //ControlId is needed when we push data up to the Navigator
        //NavigateControlId ControlId = NavigateControlId.WebSearchDisplay;
        public WebSearch()
        {
            this.InitializeComponent();
            this.Loaded += WebSearch_Loaded;
            this.Unloaded += WebSearch_Unloaded;
        }
        public CommandBar ParentCommandBar = null;

        private void WebSearch_Unloaded(object sender, RoutedEventArgs e)
        {
            ParentCommandBar.PrimaryCommands.Remove(ForwardButton);
            ForwardButton = null;

            ParentCommandBar.PrimaryCommands.Remove(BackButton);
            BackButton = null;
        }

        private void WebSearch_Loaded(object sender, RoutedEventArgs e)
        {
            var ff = new FontFamily("Segoe MDL2 Assets");


            BackButton = new AppBarButton()
            {
                Label = "Backward",
                Icon = new FontIcon() { Glyph = "", FontFamily = ff },
            };
            BackButton.Click += BackButton_Click;
            BackButton.IsEnabled = true;
            ParentCommandBar.PrimaryCommands.Add(BackButton);

            ForwardButton = new AppBarButton()
            {
                Label = "Forward",
                Icon = new FontIcon() { Glyph = "", FontFamily = ff },
            };
            ForwardButton.Click += ForwardButton_Click;
            ForwardButton.IsEnabled = true;
            ParentCommandBar.PrimaryCommands.Add(ForwardButton);
        }

        private void ForwardButton_Click(object sender, RoutedEventArgs e)
        {
            uiSearchWeb.GoForward();
        }
        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            uiSearchWeb.GoBack();
        }

        AppBarButton ForwardButton = null;
        AppBarButton BackButton = null;


        public void SelectTo(NavigateControlId sourceId, string selected)
        {
            var urlTemplate = (uiSearchUrl.SelectedItem as ComboBoxItem).Tag as string;
            var escaped = Uri.EscapeUriString(selected);
            var dataescaped = Uri.EscapeDataString(selected);
            var url = urlTemplate.Replace("{SEARCH}", escaped);
            uiSearchWeb.Navigate(new Uri(url));
        }
    }
}
