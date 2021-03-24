using SimpleEpubReader.Database;
using SimpleEpubReader.EbookReader;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using static SimpleEpubReader.Controls.Navigator;

// The User Control item template is documented at https://go.microsoft.com/fwlink/?LinkId=234236

namespace SimpleEpubReader.Controls
{
    public class UserNoteWithTitle
    {
        public UserNoteWithTitle(UserNote baseNote, bool displayTitle)
        {
            BaseNote = baseNote;
            DisplayTitle = displayTitle;
        }
        public UserNote BaseNote { get; set; }
        public BookData CorrespondingBook = null;

        public string Title 
        { 
            get 
            { 
                if (CorrespondingBook == null)
                {
                    var bookdb = BookDataContext.Get();
                    CorrespondingBook = CommonQueries.BookGet(bookdb, BookId);
                }
                return CorrespondingBook?.Title ?? BookId;
            } 
        }
        public bool DisplayTitle { get; set; }

        // Copy of data from UserNote
        public int Id { get { return BaseNote.Id; } }
        public string BookId { get { return BaseNote.BookId; } }
        public DateTimeOffset CreateDate { get { return BaseNote.CreateDate; } }
        public DateTimeOffset MostRecentModificationDate { get { return BaseNote.MostRecentModificationDate; } }
        public string Location { get { return BaseNote.Location; } }
        public string Text { get { return BaseNote.Text; } }
        public string Tags { get { return BaseNote.Tags; } }
        public string Icon { get { return BaseNote.Icon; } }
        public string BackgroundColor { get { return BaseNote.BackgroundColor; } }
        public string ForegroundColor { get { return BaseNote.ForegroundColor; } }
        public string SelectedText { get { return BaseNote.SelectedText; } }
    }
    public sealed partial class NoteList : UserControl, SimpleBookHandler
    {
        public ObservableCollection<UserNoteWithTitle> Notes { get; } = new ObservableCollection<UserNoteWithTitle>();

        /// <summary>
        /// DataContext is just this; data is passed in via the DisplayBook method
        /// </summary>
        public NoteList()
        {
            Notes.Clear();
            Notes.CollectionChanged += Notes_CollectionChanged;
            this.DataContext = this;
            this.InitializeComponent();
            this.Loaded += NoteList_Loaded;
            this.Unloaded += NoteList_Unloaded;
        }

        private void Notes_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            if (Notes.Count == 0)
            {
                uiNoNotes.Visibility = Visibility.Visible;
            }
            else
            {
                uiNoNotes.Visibility = Visibility.Collapsed;
            }
        }

        // Always set this for convenience
        NavigateControlId ControlId = NavigateControlId.NoteListDisplay;

        public CommandBar ParentCommandBar = null;

        AppBarButton DeleteNotes = null;
        AppBarButton EditNote = null;
        AppBarButton SyncPosition = null;
        AppBarSeparator Seperator = null;
        AppBarToggleButton NotesFromAllBooks = null;

        private void NoteList_Unloaded(object sender, RoutedEventArgs e)
        {
            ParentCommandBar.PrimaryCommands.Remove(DeleteNotes);
            DeleteNotes = null;

            ParentCommandBar.PrimaryCommands.Remove(EditNote);
            EditNote = null;

            ParentCommandBar.PrimaryCommands.Remove(SyncPosition);
            SyncPosition = null;

            ParentCommandBar.PrimaryCommands.Remove(Seperator);
            Seperator = null;

            ParentCommandBar.PrimaryCommands.Remove(NotesFromAllBooks);
            NotesFromAllBooks = null;
        }

        private void NoteList_Loaded(object sender, RoutedEventArgs e)
        {
            var ff = new FontFamily("Segoe MDL2 Assets");
            DeleteNotes = new AppBarButton()
            {
                Label = "Delete Notes",
                Icon = new FontIcon() { Glyph = "", FontFamily = ff },
            };
            DeleteNotes.Click += DeleteNotes_Click;
            DeleteNotes.IsEnabled = false;
            ParentCommandBar.PrimaryCommands.Add(DeleteNotes);

            EditNote = new AppBarButton()
            {
                Label = "Edit Note",
                Icon = new FontIcon() { Glyph = "", FontFamily = ff },
            };
            EditNote.Click += EditNote_Click;
            EditNote.IsEnabled = false;
            ParentCommandBar.PrimaryCommands.Add(EditNote);

            SyncPosition = new AppBarButton()
            {
                Label = "Goto note",
                Icon = new FontIcon() { Glyph = "", FontFamily = ff },
            };
            SyncPosition.Click += SyncPosition_Click;
            SyncPosition.IsEnabled = false;
            ParentCommandBar.PrimaryCommands.Add(SyncPosition);

            Seperator = new AppBarSeparator();
            ParentCommandBar.PrimaryCommands.Add(Seperator);

            NotesFromAllBooks = new AppBarToggleButton()
            {
                Label = "Notes from all books",
                Icon = new FontIcon() { Glyph = "", FontFamily = ff },
                IsChecked = false,
            };
            NotesFromAllBooks.Click += ShowAllBooksNotes_Click;
            ParentCommandBar.PrimaryCommands.Add(NotesFromAllBooks);
        }

        private void ShowAllBooksNotes_Click(object sender, RoutedEventArgs e)
        {
            if ((e.OriginalSource as AppBarToggleButton).IsChecked.Value)
            {
                // All books, please
                var list = CommonQueries.BookNotesGetAll();
                var titleList = new List<UserNoteWithTitle>();
                foreach (var bn in list)
                {
                    foreach (var note in bn.Notes)
                    {
                        titleList.Add(new UserNoteWithTitle(note, true));
                    }
                }
                var sortedList = titleList.OrderBy(item => item.Title).ThenBy(item => item.BaseNote.LocationNumericValue);
                Notes.Clear();
                foreach (var note in sortedList)
                {
                    Notes.Add(note);
                }
            }
            else
            {
                // Just the one book
                SetNotes(CurrBookData);
            }
        }

        private void SyncPosition_Click(object sender, RoutedEventArgs e)
        {
            var bookdb = BookDataContext.Get();
            
            // The button is only clickable when there's exactly one item selected
            // (baring race conditions, of course)
            foreach (var item in uiList.SelectedItems)
            {
                var noteWithTitle = item as UserNoteWithTitle;
                if (noteWithTitle == null) return; // should never happen.
                var note = noteWithTitle.BaseNote;
                if (note == null) continue; // should never happen
                var location = note.LocationToBookLocatation();
                if (location == null) continue; // should never happen
                // Are we in the same book, or a different one?

                var nav = Navigator.Get();
                if (noteWithTitle.BookId != CurrBookData.BookId)
                {
                    var book = CommonQueries.BookGet(bookdb, noteWithTitle.BookId);
                    CurrBookData = book; // make sure to reset this!
                    nav.DisplayBook(ControlId, book, location);
                }
                else
                {
                    nav.UserNavigatedTo(ControlId, location);
                }
            }
        }

        private async void EditNote_Click(object sender, RoutedEventArgs e)
        {
            // The button is only clickable when there's exactly one item selected
            // (baring race conditions, of course)
            foreach (var item in uiList.SelectedItems)
            {
                var note = item as UserNoteWithTitle;
                if (note == null) continue; // should never happen
                var edited = await NoteEditor.EditNoteAsync(ControlId, note.BaseNote);
                if (edited)
                {
                    // Update the list...
                    for (int i = 0; i < Notes.Count; i++)
                    {
                        if (Notes[i].Id == note.Id)
                        {
                            Notes.RemoveAt(i);
                            Notes.Insert(i, new UserNoteWithTitle(note.BaseNote, note.DisplayTitle));
                        }
                    }
                }
            }
        }

        private void DeleteNotes_Click(object sender, RoutedEventArgs e)
        {
            var bookdb = BookDataContext.Get();
            UserNoteWithTitle[] list = new UserNoteWithTitle[uiList.SelectedItems.Count];
            int i = 0;
            foreach (var item in uiList.SelectedItems)
            {
                list[i++] = item as UserNoteWithTitle;
            }
            foreach (var note in list)
            {
                Notes.Remove(note);
                var bookId = note.BookId;
                var bn = CommonQueries.BookNotesFind(bookdb, bookId);
                if (bn == null)
                {
                    ;
                }
                else if (bn.Notes != null)
                {
                    bn.Notes.Remove(note.BaseNote);
                }
            }
            CommonQueries.BookSaveChanges(bookdb);
        }

        BookData CurrBookData = null;

        private void SetNotes(BookData bookData)
        {
            var bookdb = BookDataContext.Get();
            if (bookData == null)
            {
                // Do a refresh as needed
                bookData = CurrBookData;
                if (bookData == null)
                {
                    return; // very uncommon -- perhaps race conditions and startup?
                }
            }
            CurrBookData = bookData;
            Notes.Clear();
            var bookId = CurrBookData.BookId;
            var bn = CommonQueries.BookNotesFind(bookdb, bookId);
            if (bn == null)
            {
                ;
            }
            else if (bn.Notes != null)
            {
                var sorted = bn.Notes.OrderBy(note => note.LocationNumericValue).ToList();
                foreach (var note in sorted)
                {
                    Notes.Add(new UserNoteWithTitle (note, false));
                }
            }
        }

        public async Task DisplayBook(BookData book, BookLocation location)
        {
            await Task.Delay(0); // just to make the compiler happy.
            SetNotes(book);
            // Location isn't used at all.
        }


        private void OnNotesSelectedChanged(object sender, SelectionChangedEventArgs e)
        {
            var count = (sender as ListView).SelectedItems.Count;
            // Commands depend on how many notes are selected.
            if (DeleteNotes != null) DeleteNotes.IsEnabled = count >= 1;
            if (EditNote != null) EditNote.IsEnabled = count == 1;
            if (SyncPosition!= null) SyncPosition.IsEnabled = count == 1;
        }
    }
}
