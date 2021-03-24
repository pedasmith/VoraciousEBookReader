using SimpleEpubReader.Database;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

// The User Control item template is documented at https://go.microsoft.com/fwlink/?LinkId=234236

namespace SimpleEpubReader.Controls
{
    public sealed partial class PersonCard : UserControl
    {
        public PersonCard()
        {
            this.InitializeComponent();
            this.Loaded += PersonCard_Loaded;
            this.DataContextChanged += PersonCard_DataContextChanged;
        }

        private void PersonCard_DataContextChanged(FrameworkElement sender, DataContextChangedEventArgs args)
        {
            UpdateUI();
        }

        private void PersonCard_Loaded(object sender, RoutedEventArgs e)
        { 
            UpdateUI();
        }

        private void UpdateUI()
        { 
            var person = DataContext as Person;
            if (person == null) return;
            // Sometimes the webpagea is e.g. efiles/2365 which is not a value page at all!
            bool isDefaultWeb = person.Webpage == "" || person.Webpage == "http://wikipedia.com" || !person.Webpage.StartsWith("http");
            uiWeb.Visibility = isDefaultWeb ? Visibility.Collapsed : Visibility.Visible;
        }
    }
}
