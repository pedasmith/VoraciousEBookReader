using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SimpleEpubReader.Searching;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SimpleEpubReader.Database
{
    /// <summary>
    /// Contains all of the common queries in the book databases
    /// </summary>
    static class CommonQueries
    {
        private static int _NQueries = 0;
        public static int NQueries
        {
            get { return _NQueries; }
            set
            {
                _NQueries = value;
                if (_NQueries % 1000 == 0 && _NQueries > 0)
                {
                    ; // Spot to break on; we shouldn't have so many queries during normal operation.
                    // It's a database; the goal is 1 big query, not a bazillion small ones.
                }
            }
        }


        public enum ExistHandling { IfNotExists, SmartCatalogOverride, CatalogOverrideFast }
        /// <summary>
        /// Adds the bookData into the Books database, but only if it's not already present.
        /// If it's already 
        /// </summary>
        /// <param name="bookData"></param>
        /// <returns>0=not added, 1=added. Technical is the count of the number added.</returns>

        public static int BookAdd(BookDataContext bookdb, BookData book, ExistHandling handling)
        {
            int retval = 0;
            NQueries++;
            lock (bookdb)
            {
                switch (handling)
                {
                    case ExistHandling.IfNotExists:
                        if (bookdb.Books.Find(book.BookId) == null)
                        {
                            bookdb.Books.Add(book);
                            retval++;
                        }
                        break;
                    case ExistHandling.CatalogOverrideFast:
                        {
                            var dbbook = bookdb.Books.Find(book.BookId);
                            if (dbbook == null)
                            {
                                bookdb.Books.Add(book);
                                retval++;
                            }
                            else // have to be smart.
                            {
                                if (dbbook.BookSource.StartsWith(BookData.BookSourceBookMarkFile))
                                {
                                    // The database was added to from a bookmark file.
                                    // For these books, the dbbook top-level data isn't correct but the user data is correct.
                                    // At the same time, the new book top-level data IS correct, but the user data is not correct.
                                    BookData.Merge(dbbook, book);
                                    retval++;
                                }
                            }
                        }
                        break;
                    case ExistHandling.SmartCatalogOverride:
                        {
                            var dbbook = bookdb.Books.Find(book.BookId);
                            if (dbbook == null)
                            {
                                bookdb.Books.Add(book);
                                retval++;
                            }
                            else // have to be smart.
                            {
                                if (dbbook.BookSource.StartsWith(BookData.BookSourceBookMarkFile))
                                {
                                    // The database was added to from a bookmark file.
                                    // For these books, the dbbook top-level data isn't correct but the user data is correct.
                                    // At the same time, the new book top-level data IS correct, but the user data is not correct.
                                    BookData.Merge(dbbook, book);
                                    retval++;
                                }
                                else
                                {
                                    // Grab the full data including the number of files
                                    dbbook = CommonQueries.BookGetFiles(bookdb, book.BookId);
                                    // Remove all the kindle books; they aren't interesting
                                    //int bookNNotKindle = 0;
                                    //foreach (var item in book.Files)
                                    //{
                                    //    if (!BookData.FileIsKindle(item.FileName)) bookNNotKindle++; // Database has kindle books, so don't scrub them from the book files
                                    //}
                                    //int dbNNotKindle = 0;
                                    //foreach (var item in dbbook.Files)
                                    //{
                                    //    if (!BookData.FileIsKindle(item.FileName)) dbNNotKindle++; // Database has kindle books, so don't scrub them from the book files
                                    //}
                                    //var mustReplace = bookNNotKindle != dbNNotKindle;

                                    // In case the files don't match exactly....
                                    //if (!mustReplace)
                                    //{
                                    //    //TODO: make faster? Or keep because it's needed functionality?
                                    //    //Update: 2022-10-09: Really only care about epub books
                                    //    mustReplace = !BookData.FilesMatchEpub(book, dbbook);
                                    //}
                                    // Ignore everything we just did :-)
                                    var mustReplace = !BookData.FilesMatchEpub(book, dbbook);
                                    if (mustReplace)
                                    {
                                        //FAIL: project gutenberg LOVES changing their URLs. If the old list doesn't match the 
                                        // new list in number of files, then dump ALL the old values and replace them with the
                                        // new ones.
                                        // TODO: actually verify that the files match?
                                        // Can't use clear because it doesn't work: dbbook.Files.Clear();
                                        // (Seriously: it doesn't work because Files doesn't implement it and will throw)
                                        for(int i=dbbook.Files.Count-1; i>=0; i--)
                                        {
                                            dbbook.Files.RemoveAt(i);
                                        }
                                        foreach (var file in book.Files)
                                        {
                                            if (file.Id != 0) file.Id = 0; // if it's straight from the catalog, it should have no id 
                                            dbbook.Files.Add(file);
                                        }
                                        retval++;
                                    }
                                }
                            }
                        }
                        break;
                }
                return retval;
            }
        }


        public static int BookCount(BookDataContext bookdb)
        {
            NQueries++;
            lock (bookdb)
            {
                var retval = bookdb.Books.Count();
                return retval;
            }
        }

        public static BookData BookGet(BookDataContext bookdb, string bookId)
        {
            NQueries++;
            lock (bookdb)
            {
                var booklist = bookdb.Books
                .Where(b => b.BookId == bookId)
                .Include(b => b.People)
                .Include(b => b.Files)
                .Include(b => b.Review)
                .Include(b => b.Notes)
                .Include(b => b.Notes.Notes)
                .Include(b => b.DownloadData)
                .Include(b => b.NavigationData)
                .AsQueryable();
                ;
                var book = booklist.Where(b => b.BookId == bookId).FirstOrDefault();
                if (book != null && book.BookId == "ebooks/57")
                {
                    ; // A good place to hang a debugger on.
                }
                return book;
            }
        }

        /// <summary>
        /// Returns an abbreviated set of data with just the Files. This is used when merging
        /// a new catalog with an old catalog; the new catalog might have more files than the
        /// old catalog. This is super-common with the latest books which might just be available
        /// as .TXT files at the start.
        /// </summary>
        /// <param name="bookdb"></param>
        /// <param name="bookId"></param>
        /// <returns></returns>
        public static BookData BookGetFiles(BookDataContext bookdb, string bookId)
        {
            NQueries++;
            lock (bookdb)
            {
                var booklist = bookdb.Books
                .Where(b => b.BookId == bookId)
                .Include(b => b.Files)
                .AsQueryable();
                ;
                var book = booklist.Where(b => b.BookId == bookId).FirstOrDefault();
                if (book != null && book.BookId.Contains("62548"))
                {
                    ; // A good place to hang a debugger on.
                }
                return book;
            }
        }

        public static List<BookData> BookGetAllWhichHaveUserData(BookDataContext bookdb)
        {
            NQueries++;
            lock (bookdb)
            {
                var booklist = bookdb.Books
                .Include(b => b.Review)
                .Include(b => b.Notes)
                .Include(b => b.Notes.Notes)
                .Include(b => b.DownloadData)
                .Include(b => b.NavigationData)
                .Where(b => b.Review != null || b.Notes != null || b.NavigationData != null)
                .ToList();
                ;
                return booklist;
            }
        }

        public static TimeSpan LengthForRecentChanges()
        {
            var recentTimeSpan = new TimeSpan(45, 0, 0, 0); // 45 days
            //var recentTimeSpan = new TimeSpan(0, 1, 0, 0); // For debugging: a paltry 1 hour -- used for debugging
            return recentTimeSpan;
        }
        public static List<BookData> BookGetRecentWhichHaveUserData(BookDataContext bookdb)
        {
            NQueries++;
            var now = DateTimeOffset.Now;
            var recentTimeSpan = LengthForRecentChanges();
            lock (bookdb)
            {
                var booklist = bookdb.Books
                .Include(b => b.Review)
                .Include(b => b.Notes)
                .Include(b => b.Notes.Notes)
                .Include(b => b.DownloadData)
                .Include(b => b.NavigationData)
                .Where(b => b.Review != null || b.Notes != null || b.NavigationData != null)
                .ToList()
                .Where(b => now.Subtract(b.NavigationData.MostRecentNavigationDate) < recentTimeSpan)
                .ToList()
                ;
                return booklist;
            }
        }
        public static Task FirstSearchToWarmUpDatabase()
        {
            Task mytask = Task.Run(() => {
                NQueries++;
                //var bookdb = BookDataContext.Get();
                //lock (bookdb)
                {
                    DoCreateIndexFile();
#if NEVER_EVER_DEFINED
                    var booklist = bookdb.Books
                        //.Include(b => b.Review)
                        //.Include(b => b.Notes)
                        //.Include(b => b.Notes.Notes)
                        //.Include(b => b.DownloadData)
                        //.Include(b => b.NavigationData)
                        .ToList();
                    ;
                    return booklist;
#endif
                }
            });
            return mytask;
        }

        public static void BookDoMigrate(BookDataContext bookdb)
        {
            NQueries++;
            bookdb.DoMigration();
        }
        public static void BookRemoveAll(BookDataContext bookdb)
        {
            NQueries++;
            lock (bookdb)
            {
                foreach (var book in bookdb.Books)
                {
                    bookdb.Books.Remove(book);
                }
            }
        }

        public static void BookSaveChanges(BookDataContext bookdb)
        {
            NQueries++;
            lock (bookdb)
            {
                bookdb.SaveChanges();
            }
        }

        public static int BookNavigationDataAdd(BookDataContext bookdb, BookNavigationData bn, ExistHandling handling)
        {
            int retval = 0;
            NQueries++;
            var book = BookGet(bookdb, bn.BookId);
            if (book == null) return retval;
            switch (handling)
            {
                case ExistHandling.IfNotExists:
                    if (book.NavigationData == null)
                    {
                        book.NavigationData = bn;
                        retval++;
                    }
                    break;
            }
            return retval;
        }


        public static BookNavigationData BookNavigationDataEnsure(BookDataContext bookdb, BookData bookData)
        {
            var nd = CommonQueries.BookNavigationDataFind(bookdb, bookData.BookId);
            if (nd == null)
            {
                nd = new BookNavigationData()
                {
                    BookId = bookData.BookId,
                };
                CommonQueries.BookNavigationDataAdd(bookdb, nd, CommonQueries.ExistHandling.IfNotExists);
                nd = CommonQueries.BookNavigationDataFind(bookdb, bookData.BookId);
                CommonQueries.BookSaveChanges(bookdb);
            }
            if (nd == null)
            {
                App.Error($"ERROR: trying to ensure navigation data, but don't have one.");
            }
            return nd;
        }



        public static BookNavigationData BookNavigationDataFind(BookDataContext bookdb, string bookId)
        {
            NQueries++;
            var book = BookGet(bookdb, bookId);
            if (book == null)
            {
                App.Error($"ERROR: attempting to get navigation data for a book={bookId} that doesn't exist");
                return null;
            }
            var retval = book.NavigationData;
            return retval;
        }



        public static int BookNotesAdd(BookDataContext bookdb, BookNotes bn, ExistHandling handling)
        {
            int retval = 0;
            NQueries++;
            var book = BookGet(bookdb, bn.BookId);
            if (book == null) return retval;
            switch (handling)
            {
                case ExistHandling.IfNotExists:
                    if (book.Notes == null)
                    {
                        book.Notes = bn;
                        retval++;
                    }
                    break;
            }
            return retval;
        }


#if NEVER_EVER_DEFINED
        public static void BookNotesDelete(BookDataContext bookdb, IList<UserNote> list)
        {
            NQueries++;
            foreach (var note in list)
            {
                var book = BookGet(bookdb, note.BookId);
                var bn = book?.Notes;
                if (bn != null)
                {
                    var idx = bn.Notes.FindIndex(n => n.Id == note.Id);
                    if (idx >= 0)
                    {
                        bn.Notes.RemoveAt(idx);
                    }
                }
            }
            BookSaveChanges(bookdb);
        }
#endif

#if NEVER_EVER_DEFINED
        public static IList<UserNote> BookUserNotesDuplicate(IList<object> inputList)
        {
            NQueries++;
            List<UserNote> list = new List<UserNote>();

            // Must duplicate the list (otherwise as items get removed, the selected items list
            // is shrunk, and the foreach skips over stuff.
            foreach (var item in inputList)
            {
                var note = item as UserNote;
                if (note == null) continue; // should never happen
                list.Add(note);
            }
            return list;
        }
#endif

        public static BookNotes BookNotesFind(BookDataContext bookdb, string bookId)
        {
            NQueries++;
            var book = BookGet(bookdb, bookId);
            var retval = book.Notes;
            return retval;
        }

        public static void BookNoteSave(BookDataContext bookdb, UserNote note)
        {
            var bn = CommonQueries.BookNotesFind(bookdb, note.BookId);
            if (bn == null)
            {
                bn = new BookNotes();
                bn.BookId = note.BookId;
                CommonQueries.BookNotesAdd(bookdb, bn, CommonQueries.ExistHandling.IfNotExists);
                bn = CommonQueries.BookNotesFind(bookdb, note.BookId);
            }
            if (note.Id == 0) // Hasn't been saved before. The id is 0.
            {
                bn.Notes.Add(note);
            }
            CommonQueries.BookSaveChanges(bookdb);
        }

        public static IList<BookNotes> BookNotesGetAll()
        {
            NQueries++;
            var bookdb = BookDataContext.Get();

            var retval = bookdb.Books
                .Include(b => b.Notes)
                .Where(b => b.Notes != null)
                .Include(b => b.Notes.Notes)
                .Select(b => b.Notes)
                .ToList();
            return retval;
        }

        public static int DownloadedBookAdd(BookDataContext bookdb, DownloadData dd, ExistHandling handling)
        {
            int retval = 0;
            NQueries++;
            var book = BookGet(bookdb, dd.BookId);
            if (book == null) return retval;
            switch (handling)
            {
                case ExistHandling.IfNotExists:
                    if (book.DownloadData == null)
                    {
                        book.DownloadData = dd;
                        retval++;
                    }
                    break;
            }
            return retval;
        }


        public static void DownloadedBookEnsureFileMarkedAsDownloaded(BookDataContext bookdb, string bookId, string folderPath, string filename)
        {
            NQueries++;
            var book = BookGet(bookdb, bookId);
            if (book == null)
            {
                App.Error($"ERROR: trying to ensure that {bookId} is downloaded, but it's not a valid book");
                return;
            }
            var dd = book.DownloadData; 
            if (dd == null)
            {
                dd = new DownloadData()
                {
                    BookId = bookId,
                    FilePath = folderPath,
                    FileName = filename,
                    CurrFileStatus = DownloadData.FileStatus.Downloaded,
                    DownloadDate = DateTimeOffset.Now,
                };
                book.DenormDownloadDate = dd.DownloadDate.ToUnixTimeSeconds();
                CommonQueries.DownloadedBookAdd(bookdb, dd, CommonQueries.ExistHandling.IfNotExists);
                CommonQueries.BookSaveChanges(bookdb);
            }
            else if (dd.CurrFileStatus != DownloadData.FileStatus.Downloaded)
            {
                dd.FilePath = folderPath;
                dd.CurrFileStatus = DownloadData.FileStatus.Downloaded;
                BookSaveChanges(bookdb);
            }
        }

        public static DownloadData DownloadedBookFind(BookDataContext bookdb, string bookId)
        {
            NQueries++;
            var book = BookGet(bookdb, bookId);
            if (book == null)
            {
                App.Error($"ERROR: attempting to get download data for {bookId} that isn't in the database");
                return null;
            }
            var retval = book.DownloadData;
            return retval;
        }

        public static List<DownloadData> DownloadedBooksGetAll(BookDataContext bookdb)
        {
            NQueries++;
            lock (bookdb)
            {
                var bookquery = from b in bookdb.Books where b.DownloadData != null select b.DownloadData;
                var retval = bookquery.ToList();
                return retval;
            }
        }


        public static int UserReviewAdd(BookDataContext bookdb, UserReview review, ExistHandling handling)
        {
            int retval = 0;
            NQueries++;
            var book = BookGet(bookdb, review.BookId);
            if (book == null) return retval;
            switch (handling)
            {
                case ExistHandling.IfNotExists:
                    if (book.Review == null)
                    {
                        book.Review = review;
                        retval++;
                    }
                    break;
            }
            return retval;
        }

        public static UserReview UserReviewFind(BookDataContext bookdb, string bookId)
        {
            NQueries++;
            var book = BookGet(bookdb, bookId);
            if (book == null)
            {
                App.Error($"ERROR: attempting to get user review for {bookId} that isn't in the database");
                return null;
            }
            var retval = book.Review;
            return retval;
        }

        public static List<UserReview> UserReviewsGetAll(BookDataContext bookdb)
        {
            NQueries++;
            lock (bookdb)
            {
                var bookquery = from b in bookdb.Books where b.Review != null select b.Review; // NOTE: update all queries to use the dotted format with includes
                var retval = bookquery.ToList();
                return retval;
            }
        }
#region FAST_SEARCH
        class BookIndex
        {
            public string BookId { get; set; }
            public string Text { get; set; }
            public override string ToString()
            {
                return $"{BookId}\t{Text}"; // assumes bookId will never include a tab.
            }
            public static BookIndex FromBookData(BookData bookData)
            {
                var sb = new StringBuilder();
                Append(sb, bookData.Title);
                Append(sb, bookData.TitleAlternative);
                Append(sb, bookData.Review?.Tags);
                Append(sb, bookData.Review?.Review);
                Append(sb, bookData.BookSeries);
                Append(sb, bookData.Imprint);
                Append(sb, bookData.LCC);
                Append(sb, bookData.LCCN);
                Append(sb, bookData.LCSH);
                if (bookData.Notes != null)
                {
                    foreach (var note in bookData.Notes.Notes)
                    {
                        Append(sb, note.Tags);
                        Append(sb, note.Text);
                    }
                }
                foreach (var people in bookData.People)
                {
                    Append(sb, people.Aliases);
                    Append(sb, people.Name);
                }

                var retval = new BookIndex()
                {
                    BookId = bookData.BookId,
                    Text = sb.ToString(),
                };
                return retval;
            }
            public static StringBuilder Append(StringBuilder sb, string field)
            {
                if (!string.IsNullOrEmpty(field))
                {
                    sb.Append(" ");
                    bool sawWS = true;
                    foreach (var ch in field)
                    {
                        var newch = char.ToLower(ch);
                        if (newch < '0'
                            || (newch >= ':' && newch <= '@')
                            || (newch >= '[' && newch <= '`')
                            || (newch >= '{' && newch <= '~')
                            ) // higher chars stay the same. International support is ... iffy ... 
                            newch = ' ';
                        if (newch != ' ' || !sawWS)
                        {
                            sb.Append(newch);
                        }
                        sawWS = (newch == ' ');
                    }
                }
                return sb;
            }
        }
        static Dictionary<string, BookIndex> BookIndexes = null;

        public static void DoCreateIndexFile()
        {
            if (BookIndexes != null) return;
            BookIndexes = new Dictionary<string, BookIndex>();

            var folder = Windows.Storage.ApplicationData.Current.LocalFolder;
            var path = folder.Path;
            string dbpath = Path.Combine(path, BookDataContext.BookDataDatabaseFilename);

            var startTime = DateTime.UtcNow;
            using (var connection = new SqliteConnection($"Data Source={dbpath}"))
            {
                connection.Open();
                AddFromTable(connection, true, "SELECT BookId,Title,TitleAlternative,LCSH,LCCN,LCC,BookSeries FROM Books");
                AddFromTable(connection, false, "SELECT BookDataBookId,Name,Aliases FROM Person");
                AddFromTable(connection, false, "SELECT BookId,Text,Tags FROM UserNote");
                AddFromTable(connection, false, "SELECT BookId,Review,Tags FROM UserReview");
            }
            var delta = DateTime.UtcNow.Subtract(startTime).TotalSeconds;
#if NEVER_EVER_DEFINED
            for (int i=0; i<30_000; i+=100)
            {
                // Artificially wait 30 seconds
                Task.Delay(100).Wait();
            }
#endif
            Logger.Log($"Time to read index: {delta} seconds");
            ;
        }

        private static void AddFromTable(SqliteConnection connection, bool create, string commandText)
        {
            var command = connection.CreateCommand();
            command.CommandText = commandText;

            using (var reader = command.ExecuteReader())
            {
                while (reader.Read())
                {
                    var bookId = reader.GetString(0);
                    var sb = new StringBuilder();
                    for (int i = 1; i < reader.FieldCount; i++)
                    {
                        if (!reader.IsDBNull(i))
                        {
                            BookIndex.Append(sb, reader.GetString(i));
                        }
                    }
                    if (create)
                    {
                        var index = new BookIndex() { BookId = bookId, Text = sb.ToString() };
                        BookIndexes.Add(index.BookId, index);
                    }
                    else
                    {
                        try
                        {
                            var index = BookIndexes[bookId];
                            index.Text += sb.ToString();
                        }
                        catch (Exception)
                        {
                            ; // Error; why doesn't the book exist?
                        }
                    }
                }
            }
        }
        public static void DoCreateIndexFileEF()
        {
            if (BookIndexes != null) return;
            BookIndexes = new Dictionary<string, BookIndex>();

            var bookdb = BookDataContext.Get();
            var bookList = bookdb.Books
             .Include(b => b.People)
             .Include(b => b.Review)
             .Include(b => b.Notes)
             .Include(b => b.Notes.Notes)
             .ToList();
            //var sb = new StringBuilder();
            foreach (var bookData in bookList)
            {
                var index = BookIndex.FromBookData(bookData);
                BookIndexes.Add(index.BookId, index);
                //sb.Append(index.ToString());
                //sb.Append('\n');
            }
            //var fullIndex = sb.ToString();
            ;
        }
        public static HashSet<string> BookSearchs(ISearch searchOperations)
        {
            DoCreateIndexFile(); // create the index as needed.
            var retval = new HashSet<string>();
            foreach (var (bookid, index) in BookIndexes)
            {
                var hasSearch = searchOperations.MatchesFlat(index.Text);
                if (hasSearch)
                {
                    retval.Add(index.BookId);
                }
            }
            return retval;
        }

#endregion

    }
}
