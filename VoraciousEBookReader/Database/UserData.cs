using Newtonsoft.Json;
#if IS_MIGRATION_PROJECT
#else
using SimpleEpubReader.Controls;
#endif
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace SimpleEpubReader.Database
{

    public class BookNotes : INotifyPropertyChanged, INotifyPropertyChanging
    {
        private int id;
        private string bookId;

        public int Id { get => id; set { if (id != value) { NotifyPropertyChanging(); id = value; NotifyPropertyChanged(); } } }
        public string BookId { get => bookId; set { if (bookId != value) { NotifyPropertyChanging(); bookId = value; NotifyPropertyChanged(); } } }
        public ObservableCollection<UserNote> Notes { get; set; } = new ObservableCollection<UserNote>();

        public event PropertyChangedEventHandler PropertyChanged;
        public event PropertyChangingEventHandler PropertyChanging;
        
        private void NotifyPropertyChanged([CallerMemberName] String propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        private void NotifyPropertyChanging([CallerMemberName] String propertyName = "")
        {
            PropertyChanging?.Invoke(this, new PropertyChangingEventArgs(propertyName));
        }

        /// <summary>
        /// Find the equal-enough matching note by index. Return -1 if not found.
        /// </summary>
        /// <param name="external"></param>
        /// <returns></returns>
        private int FindSameSpot(UserNote external)
        {
            for (int i=0; i<Notes.Count; i++)
            {
                if (Notes[i].AreSameSpot (external))
                {
                    return i;
                }
            }
            return -1;
        }


        /// <summary>
        /// Merge the 'external' review into this review. The newest data wins. Returns >0 iff some data was updated.
        /// Will update the MostRecentModificationDate only if there are changes.
        /// </summary>
        /// <param name="external"></param>
        /// <returns>0 if there are no changes, 1 or more for the number of changes. </returns>
        public int Merge(BookNotes external)
        {
            int retval = 0;
            if (external != null)
            {
                // For each note in the external, see if it's already present here.
                foreach (var externalNote in external.Notes)
                {
                    var index = FindSameSpot(externalNote);
                    if (index < 0)
                    {
                        // Just add it in
                        externalNote.Id = 0; // set to zero for EF
                        Notes.Add(externalNote);
                        retval++;
                    }
                    else
                    {
                        var note = Notes[index];
                        if (note.AreEqual (externalNote))
                        {
                            ; // notes are exactly equal; no need to update anything.
                        }
                        else if (note.MostRecentModificationDate > externalNote.MostRecentModificationDate)
                        {
                            ; // the current note is already up to date
                        }
                        else // external note is newer; replace the old note
                        {
                            Notes.RemoveAt(index);
                            externalNote.Id = 0; // set to zero for EF
                            Notes.Insert(index, externalNote);
                            retval++;
                        }
                    }
                }
            }
            return retval;
        }
    }


    /// <summary>
    /// User reviews a single book
    /// </summary>
    public class UserReview : INotifyPropertyChanged, INotifyPropertyChanging
    {
        private int id;
        private string bookId;
        private DateTimeOffset createDate = DateTimeOffset.UtcNow;
        private DateTimeOffset mostRecentModificationDate = DateTimeOffset.Now;
        private double nStars = 0;
        private string review;
        private string tags;

        public int Id { get => id; set { if (id != value) { NotifyPropertyChanging(); id = value; NotifyPropertyChanged(); } } }
        public UserReview() { }
        public UserReview(string id) { BookId = id; }
        public string BookId { get => bookId; set { if (bookId != value) { NotifyPropertyChanging(); bookId = value; NotifyPropertyChanged(); } } }
        public DateTimeOffset CreateDate { get => createDate; set { if (createDate != value) { NotifyPropertyChanging(); createDate = value; NotifyPropertyChanged(); } } }
        public DateTimeOffset MostRecentModificationDate { get => mostRecentModificationDate; set { if (mostRecentModificationDate != value) { NotifyPropertyChanging(); mostRecentModificationDate = value; NotifyPropertyChanged(); } } }


        public double NStars { get => nStars; set { if (nStars != value) { NotifyPropertyChanging(); nStars = value; NotifyPropertyChanged(); } } }
        public string Review { get => review; set { if (review != value) { NotifyPropertyChanging(); review = value; NotifyPropertyChanged(); } } }
        public string Tags { get => tags; set { if (tags != value) { NotifyPropertyChanging(); tags = value; NotifyPropertyChanged(); } } }

        // space seperated? It's knd of random text

        public bool IsNotSet
        {
            get
            {
                var retval = NStars == 0 && String.IsNullOrEmpty(Review) && String.IsNullOrEmpty(Tags);
                return retval;
            }
        }

        /// <summary>
        /// Merge the 'external' review into this review. The newest data wins. Returns >0 iff some data was updated.
        /// Will update the MostRecentModificationDate only if there are changes.
        /// </summary>
        /// <param name="external"></param>
        /// <returns>0 if there are no changes, 1 or more for the number of changes. </returns>
        public int Merge(UserReview external)
        {
            int retval = 0;
            if (external != null && external.MostRecentModificationDate > this.MostRecentModificationDate)
            {
                if (external.NStars != this.NStars)
                {
                    this.NStars = external.NStars;
                    retval ++;
                }
                if (external.Review != this.Review)
                {
                    this.Review = external.Review;
                    retval ++;
                }
                if (external.Tags != this.Tags)
                {
                    this.Tags = external.Tags;
                    retval ++;
                }
                if (retval > 0)
                {
                    this.MostRecentModificationDate = external.MostRecentModificationDate;
                }
            }
            return retval;
        }

        public event PropertyChangedEventHandler PropertyChanged;
        public event PropertyChangingEventHandler PropertyChanging;
        
        private void NotifyPropertyChanged([CallerMemberName] String propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        private void NotifyPropertyChanging([CallerMemberName] String propertyName = "")
        {
            PropertyChanging?.Invoke(this, new PropertyChangingEventArgs(propertyName));
        }
        public override string ToString()
        {
            return $"{Review.Substring(0, Math.Min(Review.Length, 200))} for {BookId}";
        }
    }

    /// <summary>
    /// A user note is also the bookmark system. 
    /// </summary>
    public class UserNote : INotifyPropertyChanged, INotifyPropertyChanging
    {
        private int id;
        private string bookId = "";
        private DateTimeOffset createDate = DateTimeOffset.Now;
        private DateTimeOffset mostRecentModificationDate = DateTimeOffset.Now;
        private string location = "";
        private string text = "";
        private string tags = "";
        private string icon = "";
        private string backgroundColor = "White";
        private string foregroundColor = "Black";
        private string selectedText = "";

        public int Id { get => id; set { if (id != value) { NotifyPropertyChanging(); id = value; NotifyPropertyChanged(); } } }

        /// <summary>
        /// BookId isn't the key because each book can have multiple notes.
        /// </summary>
        public string BookId { get => bookId; set { if (bookId != value) { NotifyPropertyChanging(); bookId = value; NotifyPropertyChanged(); } } }
        public DateTimeOffset CreateDate { get => createDate; set { if (createDate != value) { NotifyPropertyChanging(); createDate = value; NotifyPropertyChanged(); } } }
        public DateTimeOffset MostRecentModificationDate { get => mostRecentModificationDate; set { if (mostRecentModificationDate != value) { NotifyPropertyChanging(); mostRecentModificationDate = value; NotifyPropertyChanged(); } } }
        /// <summary>
        /// JSON version of the book location
        /// </summary>
        public string Location { get => location; set { if (location != value) { NotifyPropertyChanging(); location = value; NotifyPropertyChanged(); } } }
        public double LocationNumericValue
        {
            get
            {
                var location = LocationToBookLocatation();
                var retval = location?.HtmlPercent ?? -1.0; // not a percent.
                return retval;
            }
        }

        /// <summary>
        /// Two kinds of equal: equal enough that if they are different, the newer one should
        /// take priority, and exactly equal. This one is the equal enough one and uses data
        /// that doesn't change from time to another. The 'id' number might be different from
        /// one machine to another.
        /// </summary>
        /// <param name="external"></param>
        /// <returns></returns>
        public bool AreSameSpot(UserNote external)
        {
            var retval = this.BookId == external.BookId
                && CreateDate == external.CreateDate
                && Location == external.Location
                ;
            return retval;

        }
        public bool AreEqual(UserNote note)
        {
            var retval = this.BookId == note.BookId
                && CreateDate == note.CreateDate
                && MostRecentModificationDate == note.MostRecentModificationDate
                && Location == note.Location
                && Text == note.Text
                && Tags == note.Tags
                && Icon == note.Icon
                && BackgroundColor == note.BackgroundColor
                && ForegroundColor == note.ForegroundColor
                && SelectedText == note.SelectedText
                ;
            return retval;
        }

        public event PropertyChangedEventHandler PropertyChanged;
        public event PropertyChangingEventHandler PropertyChanging;
        
        private void NotifyPropertyChanged([CallerMemberName] String propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        private void NotifyPropertyChanging([CallerMemberName] String propertyName = "")
        {
            PropertyChanging?.Invoke(this, new PropertyChangingEventArgs(propertyName));
        }
        public BookLocation LocationToBookLocatation()
        {
#if IS_MIGRATION_PROJECT
            return null;
#else
            var location = JsonConvert.DeserializeObject<BookLocation>(Location);
            return location;
#endif
        }
#if IS_MIGRATION_PROJECT
        public class BookLocation
        {
            public double  HtmlPercent { get { return 1.0; } }
        }
#endif
        public string Text { get => text; set { if (text != value) { NotifyPropertyChanging(); text = value; NotifyPropertyChanged(); } } }
        public string Tags { get => tags; set { if (tags != value) { NotifyPropertyChanging(); tags = value; NotifyPropertyChanged(); } } }
        // space seperated? Kind of random text?
        public string Icon { get => icon; set { if (icon != value) { NotifyPropertyChanging(); icon = value; NotifyPropertyChanged(); } } }
        // in the Segoe UI symbol font
        public string BackgroundColor { get => backgroundColor; set { if (backgroundColor != value) { NotifyPropertyChanging(); backgroundColor = value; NotifyPropertyChanged(); } } }
        public string ForegroundColor { get => foregroundColor; set { if (foregroundColor != value) { NotifyPropertyChanging(); foregroundColor = value; NotifyPropertyChanged(); } } }
        public string SelectedText { get => selectedText; set { if (selectedText != value) { NotifyPropertyChanging(); selectedText = value; NotifyPropertyChanged(); } } }
    }

    /// <summary>
    /// Info for every book that's been downloaded. This includes information on
    /// how much of the book has been read, and its state. This gets adds for 
    /// every single book that's downloaded or is known. DownloadData is per-computer and doesn't
    /// get added to a bookmark file.
    /// </summary>
    public class DownloadData : INotifyPropertyChanged, INotifyPropertyChanging
    {
        private int id;
        private string bookId = "";
        private string filePath = "";
        private string fileName = "";
        private FileStatus currFileStatus = FileStatus.Unknown;
        private DateTimeOffset downloadDate = DateTimeOffset.Now;

        public enum FileStatus { Unknown, Downloaded, Deleted }; // User can delete a book if they want
        public int Id { get => id; set { if (id != value) { NotifyPropertyChanging(); id = value; NotifyPropertyChanged(); } } }

        public string BookId { get => bookId; set { if (bookId != value) { NotifyPropertyChanging(); bookId = value; NotifyPropertyChanged(); } } }
        public string FilePath { get => filePath; set { if (filePath != value) { NotifyPropertyChanging(); filePath = value; NotifyPropertyChanged(); } } }
        // is full path to the book not including the name
        public string FileName { get => fileName; set { if (fileName != value) { NotifyPropertyChanging(); fileName = value; NotifyPropertyChanged(); } } }
        // just the name of the file

        public string FullFilePath { get { return $"{FilePath}\\{FileName}"; } }
        public FileStatus CurrFileStatus { get => currFileStatus; set { if (currFileStatus != value) { NotifyPropertyChanging(); currFileStatus = value; NotifyPropertyChanged(); } } }
        public DateTimeOffset DownloadDate { get => downloadDate; set { if (downloadDate != value) { NotifyPropertyChanging(); downloadDate = value; NotifyPropertyChanged(); } } }



        public event PropertyChangedEventHandler PropertyChanged;
        public event PropertyChangingEventHandler PropertyChanging;
        
        private void NotifyPropertyChanged([CallerMemberName] String propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        private void NotifyPropertyChanging([CallerMemberName] String propertyName = "")
        {
            PropertyChanging?.Invoke(this, new PropertyChangingEventArgs(propertyName));
        }
    }

    /// <summary>
    /// In progress -- is this what I really want?
    /// </summary>
    public class BookNavigationData : INotifyPropertyChanged, INotifyPropertyChanging
    {
        private int id;
        private string bookId;
        private int nCatalogViews;
        private int nSwipeRight;
        private int nSwipeLeft;
        private int nReading;
        private int nSpecificSelection;
        private string currSpot = "";
        private UserStatus currStatus = UserStatus.NoStatus;
        private DateTimeOffset timeMarkedDone = DateTimeOffset.MinValue;
        private DateTimeOffset firstNavigationDate = DateTimeOffset.Now;
        private DateTimeOffset mostRecentNavigationDate = DateTimeOffset.Now;

        public int Id { get => id; set { if (id != value) { NotifyPropertyChanging(); id = value; NotifyPropertyChanged(); } } }
        public string BookId { get => bookId; set { if (bookId != value) { NotifyPropertyChanging(); bookId = value; NotifyPropertyChanged(); } } }
        public int NCatalogViews { get => nCatalogViews; set { if (nCatalogViews != value) { NotifyPropertyChanging(); nCatalogViews = value; Touch();  NotifyPropertyChanged();  } } }
        public int NSwipeRight { get => nSwipeRight; set { if (nSwipeRight != value) { NotifyPropertyChanging(); nSwipeRight = value; Touch();  NotifyPropertyChanged(); } } }

        // "approve"
        public int NSwipeLeft { get => nSwipeLeft; set { if (NSwipeLeft != value) { NotifyPropertyChanging(); nSwipeLeft = value; Touch(); NotifyPropertyChanged();  } } }

        // "disapprove"
        public int NReading { get => nReading; set { if (nReading != value) { NotifyPropertyChanging(); nReading = value; Touch(); NotifyPropertyChanged(); } } }
        public int NSpecificSelection { get => nSpecificSelection; set { if (nSpecificSelection != value) { NotifyPropertyChanging(); nSpecificSelection = value; Touch();  NotifyPropertyChanged(); } } }

        // number of times the book was specifically selected

        public string CurrSpot { get => currSpot; set { if (currSpot != value) { NotifyPropertyChanging(); currSpot = value; Touch();  NotifyPropertyChanged(); } } }
        // is magically set by the reader
        public enum UserStatus { NoStatus, Reading, Done, Abandoned };

        public UserStatus CurrStatus { get => currStatus; set { if (currStatus != value) { NotifyPropertyChanging(); currStatus = value; Touch(); NotifyPropertyChanged(); } } }
        public bool IsDone { get { return CurrStatus == UserStatus.Done || CurrStatus == UserStatus.Abandoned; } }
        public DateTimeOffset TimeMarkedDone
        { get => timeMarkedDone; 
            set { if (timeMarkedDone != value) { NotifyPropertyChanging(); timeMarkedDone = value; Touch(); NotifyPropertyChanged(); } } 
        }
        public DateTimeOffset FirstNavigationDate { get => firstNavigationDate; set { if (firstNavigationDate != value) { NotifyPropertyChanging(); firstNavigationDate = value; NotifyPropertyChanged(); } } }
        public DateTimeOffset MostRecentNavigationDate { get => mostRecentNavigationDate; set { if (mostRecentNavigationDate != value) { NotifyPropertyChanging(); mostRecentNavigationDate = value; NotifyPropertyChanged(); } } }
        public void Touch()
        {
            MostRecentNavigationDate = DateTimeOffset.Now;
        }

        public int Merge (BookNavigationData external)
        {
            int retval = 0;
            if (external != null && external.MostRecentNavigationDate > this.MostRecentNavigationDate)
            {
                if (external.NCatalogViews != this.NCatalogViews)
                {
                    this.NCatalogViews = external.NCatalogViews;
                    retval++;
                }
                if (external.NSwipeRight != this.NSwipeRight)
                {
                    this.NSwipeRight = external.NSwipeRight;
                    retval++;
                }
                if (external.NSwipeLeft != this.NSwipeLeft)
                {
                    this.NSwipeLeft = external.NSwipeLeft;
                    retval++;
                }
                if (external.NReading != this.NReading)
                {
                    this.NReading = external.NReading;
                    retval++;
                }
                if (external.NSpecificSelection != this.NSpecificSelection)
                {
                    this.NSpecificSelection = external.NSpecificSelection;
                    retval++;
                }
                if (external.CurrSpot != this.CurrSpot)
                {
                    this.CurrSpot = external.CurrSpot;
                    retval++;
                }
                if (external.CurrStatus != this.CurrStatus)
                {
                    this.CurrStatus = external.CurrStatus;
                    retval++;
                }
                if (external.TimeMarkedDone > this.TimeMarkedDone)
                {
                    this.TimeMarkedDone = external.TimeMarkedDone;
                    retval++;
                }
                if (external.FirstNavigationDate != this.FirstNavigationDate)
                {
                    this.FirstNavigationDate = external.FirstNavigationDate;
                    retval++;
                }
                if (retval > 0)
                {
                    this.MostRecentNavigationDate = external.MostRecentNavigationDate;
                }
            }

            return retval;
        }

        public event PropertyChangedEventHandler PropertyChanged;
        public event PropertyChangingEventHandler PropertyChanging;
        
        private void NotifyPropertyChanged([CallerMemberName] String propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        private void NotifyPropertyChanging([CallerMemberName] String propertyName = "")
        {
            PropertyChanging?.Invoke(this, new PropertyChangingEventArgs(propertyName));
        }
    }

}
