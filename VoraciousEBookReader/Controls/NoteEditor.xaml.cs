using SimpleEpubReader.Database;
using System;
using System.Threading.Tasks;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using static SimpleEpubReader.Controls.Navigator;

// The User Control item template is documented at https://go.microsoft.com/fwlink/?LinkId=234236

namespace SimpleEpubReader.Controls
{
    public sealed partial class NoteEditor : UserControl
    {
        const string DATE_FORMAT = "yyyy-MM-dd H:mm:ss";
        public NoteEditor()
        {
            this.InitializeComponent();
            this.Loaded += NoteEditor_Loaded;
        }

        private void NoteEditor_Loaded(object sender, RoutedEventArgs e)
        {
            RestoreFromContext();
        }

        private void RestoreFromContext()
        {
            // Set up the UI nicely
            var note = DataContext as UserNote;
            if (note == null) return;

            uiText.Text = note.Text;
            uiTags.Text = note.Tags;
            uiCreateDate.Text = note.CreateDate.ToString(DATE_FORMAT);
        }

        private bool SaveToContext()
        {
            bool changed = false;

            // Set up the UI nicely
            var note = DataContext as UserNote;
            if (note == null) return false;

            if (note.Text != uiText.Text)
            {
                note.Text = uiText.Text;
                changed = true;
            }
            if (note.Tags != uiTags.Text)
            {
                note.Tags = uiTags.Text;
                changed = true;
            }
            return changed;
        }
#if THIS_IS_NOT_USED
        private void OnSetCreateDateNow(object sender, RoutedEventArgs e)
        {
            // Set up the UI nicely
            var note = DataContext as UserNote;
            if (note == null) return;

            SaveToContext();
            note.CreateDate = DateTimeOffset.Now;
            RestoreFromContext();
        }
#endif
        public void SaveNoteIfNeeded(NavigateControlId controlId, BookDataContext bookdb)
        {
            var note = DataContext as UserNote;
            if (note == null) return;

            bool changed = SaveToContext();

            if (changed)
            {
                CommonQueries.BookNoteSave(bookdb, note);
                Navigator.Get().UpdatedNotes(controlId);
            }
        }

        /// <summary>
        /// Returns TRUE if a note was edited, FALSE otherwise.
        /// </summary>
        /// <param name="controlId"></param>
        /// <param name="note"></param>
        /// <returns></returns>
        public static async Task<bool> EditNoteAsync(NavigateControlId controlId, UserNote note)
        {
            var bookdb = BookDataContext.Get();
            
            var edited = false;
            var cd = new ContentDialog()
            {
                Title = "Edit Bookmark (Note)",
                PrimaryButtonText = "OK",
                SecondaryButtonText = "Cancel"
            };
            var ne = new NoteEditor();
            ne.DataContext = note;
            cd.Content = ne;
            var result = await cd.ShowAsync();

            switch (result)
            {
                case ContentDialogResult.Primary:
                    ne.SaveNoteIfNeeded(controlId, bookdb);
                    edited = true;
                    break;
                case ContentDialogResult.Secondary:
                    // Cancel means don't save the note.
                    edited = false;
                    break;
            }
            return edited;
        }
    }
}
