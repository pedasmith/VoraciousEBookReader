using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

// The User Control item template is documented at https://go.microsoft.com/fwlink/?LinkId=234236

namespace SimpleEpubReader.Controls
{
    public sealed partial class MarkdownEditor : UserControl
    {

        public MarkdownEditor()
        {
            this.InitializeComponent();
        }

        public void SetText(string value)
        {
            TextValue = value;
            uiMarkdown.Text = TextValue;
            uiText.Text = TextValue;

            if (value == "") SetTab(MarkdownTab.Edit); // switch to the edit tab so people can enter a value.
        }

        public string TextValue { get; set; } = "#Markdown\nsample of markdown!";
        private void OnTabChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.AddedItems.Count != 1) return;
            switch ((e.AddedItems[0] as FrameworkElement).Tag as string)
            {
                case "view":
                    uiMarkdown.Text = TextValue;
                    break;
                case "edit":
                    break;
            }
        }

        private enum MarkdownTab {  View, Edit };
        private void SetTab(MarkdownTab tab)
        {
            switch (tab)
            {
                case MarkdownTab.Edit: uiTabs.SelectedItem = uiEditTab; break;
                case MarkdownTab.View: uiTabs.SelectedItem = uiViewTab; break;
            }
        }

        private void OnTextChanged(object sender, TextChangedEventArgs e)
        {
            TextValue = uiText.Text;
        }
    }
}
