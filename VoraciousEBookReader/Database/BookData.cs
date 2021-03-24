using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Collections.ObjectModel;
#if WINDOWS_UWP
using Windows.UI.Xaml.Controls; // is in an #if WINDOWS_UWP
#endif
using SimpleEpubReader.Controls;
using Newtonsoft.Json;
using System.ComponentModel.DataAnnotations.Schema;


#if IS_MIGRATION_PROJECT
// Just dummy this up for the migration project.
interface IGetSearchArea { }
#else
using SimpleEpubReader.Searching;
using System.Threading.Tasks;
#endif

namespace SimpleEpubReader.Database
{
    public partial class BookDataContext : DbContext
    {
        public static string BookDataDatabaseFilename = "BookData.db";
        public DbSet<BookData> Books { get; set; }
        protected override void OnConfiguring(DbContextOptionsBuilder options)
        {
#if IS_MIGRATION_PROJECT
            // EF (Entity Framework Core) requires that we use a "migration" to 
            // make the database tables. The migration can only be done in a 
            // console project, so my solution needs a console project whose only
            // reason in life is to be the thing from which a migration can be
            // created.
            // As a console project, it doesn't have acccess to the local folder, etc.
            //
            // IS_MIGRATION_PROJECT is a value I defined and put into the build settings
            // for the console project.
            var path = "."; 
#else
            var folder = Windows.Storage.ApplicationData.Current.LocalFolder;
            var path = folder.Path;
            folder.CreateFileAsync(BookDataDatabaseFilename, Windows.Storage.CreationCollisionOption.OpenIfExists).AsTask().Wait();
#endif
            string dbpath = Path.Combine(path, BookDataDatabaseFilename);
            options.UseSqlite($"Data Source={dbpath}");
        }

        /// <summary>
        /// See https://docs.microsoft.com/en-us/ef/core/platforms/#universal-windows-platform for why this is more performant
        /// </summary>
        /// <param name="modelBuilder"></param>
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Only do it this way when using the main database. When creating a new
            // database, use the default.
            modelBuilder.HasChangeTrackingStrategy(ChangeTrackingStrategy.ChangingAndChangedNotifications);
        }
        static BookDataContext DbContextSingleton = null;
        public static BookDataContext Get([System.Runtime.CompilerServices.CallerMemberName] string memberName = "")
        {
            // System.Diagnostics.Debug.WriteLine($"{DateTime.Now}: BookData: Get called from {memberName}");
            if (DbContextSingleton == null)
            {
                DbContextSingleton = new BookDataContext();
            }
            return DbContextSingleton;
        }
        public static void ResetSingleton(string newName)
        {
            if (DbContextSingleton != null)
            {
                DbContextSingleton.SaveChanges();
                DbContextSingleton = null;
            }
            BookDataDatabaseFilename = string.IsNullOrEmpty(newName) ? "BookData.db" : newName;
        }
        public const string BookTypeGutenberg = "gutenberg.org";
        public const string BookTypeUser = "User-imported";

        public void DoMigration()
        {
            //SLOW: the migration is 3 seconds. This is probably the EF+DB startup time.
            this.Database.Migrate();
            Logger.Log($"BookData:done migration");
            this.SaveChanges();
            Logger.Log($"BookData:done save changes");
        }
    }



    /// <summary>
    /// Person can be used for Author, Illustrator, Editor, Translator, etc.
    /// </summary>
    public class Person : INotifyPropertyChanged, INotifyPropertyChanging
    {
        private int id;
        private string name = "Unknown";
        private string aliases = "";
        private Relator personType;
        private int birthDate = -999999;
        private int deathDate = 999999;
        private string webpage = "http://wikipedia.com";

        public Person()
        {

        }
        public Person(string name, Person.Relator personType)
        {
            Name = name;
            PersonType = personType;
        }
        public int Id { get => id; set { if (id != value) { NotifyPropertyChanging(); id = value; NotifyPropertyChanged(); } } }
        public enum Relator
        {
            otherError, adapter, artist, authorOfAfterward, annotator, arranger, author, authorOfIntroduction,
            collaborator, commentator, compiler, composer, conductor, contributor,
            dubiousAuthor,
            editor, editorOfCompilation, engraver,
            illustrator, librettist,
            other,
            performer, photographer, publisher,
            researcher, transcriber, translator, unknown,

            // NEW VALUES AT THE END OF THE LIST!
            printer,
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

        // unknown is e.g. book http://www.gutenberg.org/ebooks/2822 where Daniel Defoe is somehow part of this book, we just don't know how.
        // In the text, the book is attributed to Defoe.
        public static Relator ToRelator(string value)
        {
            switch (value)
            {
                // https://www.loc.gov/marc/relators/relaterm.html
                case "dcterms:creator": return Relator.author; // little bit of magic :-)
                case "marcrel:adp": return Relator.adapter;
                case "marcrel:art": return Relator.artist;
                case "marcrel:aft": return Relator.authorOfAfterward;
                case "marcrel:ann": return Relator.annotator;
                case "marcrel:arr": return Relator.arranger;
                case "marcrel:aui": return Relator.authorOfIntroduction;
                case "marcrel:aut": return Relator.author;
                case "marcrel:clb": return Relator.collaborator;
                case "marcrel:cmm": return Relator.commentator;
                case "marcrel:cmp": return Relator.composer;
                case "marcrel:cnd": return Relator.conductor;
                case "marcrel:com": return Relator.compiler;
                case "marcrel:ctb": return Relator.contributor;
                case "marcrel:dub": return Relator.dubiousAuthor;
                case "marcrel:edc": return Relator.editorOfCompilation;
                case "marcrel:edt": return Relator.editor;
                case "marcrel:egr": return Relator.engraver;
                case "marcrel:ill": return Relator.illustrator;
                case "marcrel:lbt": return Relator.librettist;
                case "marcrel:oth": return Relator.other;
                case "marcrel:pbl": return Relator.publisher;
                case "marcrel:pht": return Relator.photographer;
                case "marcrel:prf": return Relator.performer;
                case "marcrel:prt": return Relator.printer;
                case "marcrel:res": return Relator.researcher;
                case "marcrel:trc": return Relator.transcriber;
                case "marcrel:trl": return Relator.translator;
                case "marcrel:unk": return Relator.unknown;


                default:
                    return Relator.otherError; // Distinguish codes that aren't in this list from the actual "other" category
            }
        }
        public string Name { get => name; set { if (name != value) { NotifyPropertyChanging(); name = value; NotifyPropertyChanged(); } } }
        /// <summary>
        /// Aliases is stored as a series of + seperated names
        /// So it could be "john jones" or "john jones+samantha sams"
        /// Never fiddle with the value directly! Use the AddAlias to add each alias.
        /// </summary>
        public string Aliases { get => aliases; set { if (aliases != value) { NotifyPropertyChanging(); aliases = value; NotifyPropertyChanged(); } } }
        public void AddAlias(string value)
        {
            if (value.Contains('+')) value = value.Replace('+', '&');
            if (string.IsNullOrEmpty(Aliases)) Aliases = value;
            else Aliases = "+" + value;
        }
        public Relator PersonType { get => personType; set { if (personType != value) { NotifyPropertyChanging(); personType = value; NotifyPropertyChanged(); } } }
        // e.g. aut=author ill=illustator from id.loc.gov/vocabulary/relators.html

        public int GetImportance()
        {
            switch (PersonType)
            {
                case Relator.author: return 10;
                case Relator.artist: return 20;
                case Relator.editor: return 30;
                case Relator.photographer: return 40;
                case Relator.translator: return 50;
                case Relator.illustrator: return 60;

                case Relator.adapter: return 70;
                case Relator.annotator: return 80;
                case Relator.authorOfAfterward: return 90;
                case Relator.arranger: return 100;
                case Relator.compiler: return 110;
                case Relator.composer: return 120;
                case Relator.conductor: return 130;
                case Relator.performer: return 140;
                case Relator.librettist: return 150;

                case Relator.authorOfIntroduction: return 160;
                case Relator.collaborator: return 170;
                case Relator.commentator: return 180;
                case Relator.contributor: return 190;
                case Relator.dubiousAuthor: return 200;
                case Relator.editorOfCompilation: return 210;
                case Relator.engraver: return 220;
                case Relator.other: return 230;
                case Relator.publisher: return 240;
                case Relator.researcher: return 250;
                case Relator.transcriber: return 260;
                case Relator.unknown: return 270;
                case Relator.otherError: return 280;
            }
            return 999;
        }

        /// <summary>
        /// Examples:
        /// by Samantha Jones
        /// by Samantha Jones (dubious) 1777-1810
        /// illustrated by Samantha Jones 
        /// </summary>
        public string Summary
        {
            get
            {
                var dates = "";
                if (BirthDate != -999999 && DeathDate != 999999) dates = $"{BirthDate}—{DeathDate}";
                else if (BirthDate != -999999) dates = $"{BirthDate}—";
                else if (DeathDate != 999999) dates = $"—{DeathDate}";
                string retval;
                switch (PersonType)
                {
                    case Relator.adapter: retval = $"adapted by {Name}"; break;
                    case Relator.annotator: retval = $"annotated by {Name}"; break;
                    case Relator.arranger: retval = $"arranged by {Name}"; break;
                    case Relator.artist: retval = $"{Name} (artist)"; break;
                    case Relator.author: retval = $"by {Name}"; break;
                    case Relator.authorOfAfterward: retval = $"afterword by {Name}"; break;
                    case Relator.authorOfIntroduction: retval = $"introduction by {Name}"; break;
                    case Relator.collaborator: retval = $"{Name} (collaborator)"; break;
                    case Relator.commentator: retval = $"{Name} (commentator)"; break;
                    case Relator.compiler: retval = $"compiled by {Name}"; break;
                    case Relator.composer: retval = $"composed by {Name}"; break;
                    case Relator.conductor: retval = $"conducted by {Name}"; break;
                    case Relator.contributor: retval = $"{Name} (contributor)"; break;
                    case Relator.dubiousAuthor: retval = $"by {Name} (dubious)"; break;
                    case Relator.editor: retval = $"edited by {Name}"; break;
                    case Relator.editorOfCompilation: retval = $"edited by {Name} (compilation)"; break;
                    case Relator.engraver: retval = $"engraved by {Name}"; break;
                    case Relator.illustrator: retval = $"illustrated by {Name}"; break;
                    case Relator.librettist: retval = $"{Name} (librettist)"; break;
                    case Relator.otherError: retval = $"{Name}"; break;
                    case Relator.performer: retval = $"performed by {Name}"; break;
                    case Relator.photographer: retval = $"{Name} (photographer)"; break;
                    case Relator.publisher: retval = $"published by {Name}"; break;
                    case Relator.researcher: retval = $"{Name} (researcher)"; break;
                    case Relator.transcriber: retval = $"transcribed by {Name}"; break;
                    case Relator.translator: retval = $"translated by {Name}"; break;
                    case Relator.unknown: retval = $"{Name}"; break;
                    default: retval = $"{Name}"; break;
                }
                if (dates != "") retval = retval + " " + dates;
                if (Aliases != "") retval += $" ({Aliases})";
                return retval;
            }
        }


        public int BirthDate { get => birthDate; set { if (birthDate != value) { NotifyPropertyChanging(); birthDate = value; NotifyPropertyChanged(); } } }
        public int DeathDate { get => deathDate; set { if (deathDate != value) { NotifyPropertyChanging(); deathDate = value; NotifyPropertyChanged(); } } }
        public string Webpage { get => webpage; set { if (webpage != value) { NotifyPropertyChanging(); webpage = value; NotifyPropertyChanged(); } } }
        public Uri WebpageUri
        {
            get
            {

                try
                {
                    // Some web pages are malformed e.g. eebooks/3453 and won't be converted into URI.
                    // May as well filter these out since they will never work.
                    if (Webpage.StartsWith("http"))
                    {
                        return new Uri(Webpage);
                    }
                }
                catch (Exception)
                {
                }
                return new Uri("http://wikipedia.com");
            }
        }

        public override string ToString()
        {
            return $"{Name} relator {PersonType}={(int)PersonType})";
        }
    }


    public class FilenameAndFormatData : INotifyPropertyChanged, INotifyPropertyChanging
    {
        private int id;
        private string fileName = "";
        private string fileType = "";
        private string lastModified = "";
        private string bookId = "";
        private int extent = -1;
        private string mimeType = "";

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
        /// The files are the variants of an ebook (plus ancilary stuff like title pages).
        /// Given a list of possible files, return an ordered list of the most appropriate
        /// files for the book, filtering to remove extra ones. For examples, if there's an
        /// epub with images and an epub without images, only include the epub with images.
        /// </summary>
        /// <param name="start"></param>
        /// <returns></returns>
        public static IList<FilenameAndFormatData> GetProcessedFileList(IList<FilenameAndFormatData> start)
        {
            // For example: if there's an epub with images, don't include the epub without images.
            // If there's a high-res cover, don't include a low-res cover.
            // If there are any text files at all, don't include HTML.
            // Assumes that the original list is pretty random (which seems to be the case
            // for the XML data) and that the order doesn't matter. This is probably OK
            // because I don't expect, e.g., multiple conflicting files like two different
            // text versions.
            // FAIL: actually, the audio books includes a bazillion OGG etc files.

            var sortedlist = new List<FilenameAndFormatData>();
            var retval = new List<FilenameAndFormatData>();

            foreach (var item in start) sortedlist.Add(item);
            sortedlist.Sort((a, b) => { return a.GetFileType().CompareTo(b.GetFileType()); });
            bool haveEpub = false;
            bool haveCover = false;
            bool haveHtml = false;
            bool haveText = false;

            // Step one: figure out what we've got.
            foreach (var item in sortedlist)
            {
                var itemtype = item.GetFileType();
                switch (itemtype)
                {
                    case ProcessedFileType.CoverMedium:
                    case ProcessedFileType.CoverSmall:
                        if (!haveCover)
                        {
                            retval.Add(item);
                            haveCover = true;
                        }
                        break;
                    case ProcessedFileType.EPub:
                    case ProcessedFileType.EPubNoImages:
                        if (!haveEpub)
                        {
                            retval.Add(item);
                            haveEpub = true;
                        }
                        break;
                    case ProcessedFileType.PDF:
                    case ProcessedFileType.PS:
                    case ProcessedFileType.Tex:
                        // Only include the PDF/PS if we don't have an epub. Although some people
                        // might prefer a PDF, the Gutenberg reality is that they public epubs,
                        // not pdfs. The few cases with PDFs are a total anomoly.
                        if (!haveEpub)
                        {
                            retval.Add(item);
                        }
                        break;
                    case ProcessedFileType.RDF:
                        retval.Add(item);
                        break;
                    case ProcessedFileType.Text:
                    case ProcessedFileType.TextNotUtf8:
                        if (!haveText)
                        {
                            retval.Add(item);
                            haveText = true;
                        }
                        break;
                    case ProcessedFileType.Html:
                    case ProcessedFileType.HtmlNotUtf8:
                        if (!haveHtml) // Only inlude HTML if we don't have epub. And only include one.
                        {
                            if (!haveEpub)
                            {
                                retval.Add(item);
                            }
                            haveHtml = true;
                        }
                        break;

                    case ProcessedFileType.MobiPocket:
                    case ProcessedFileType.Unknown:
                        if (!haveEpub)
                        {
                            retval.Add(item);
                        }
                        break;
                }
            }

            return retval;
        }

        // Book can't be the primary key because there are duplicates. Use a synthasized Id
        // which will be maintained by the database.
        public int Id { get => id; set { if (id != value) { NotifyPropertyChanging(); id = value; NotifyPropertyChanged(); } } }
        public string FileName { get => fileName; set { if (fileName != value) { NotifyPropertyChanging(); fileName = value; NotifyPropertyChanged(); } } }
        public string FileType { get => fileType; set { if (fileType != value) { NotifyPropertyChanging(); fileType = value; NotifyPropertyChanged(); } } }
        public string LastModified { get => lastModified; set { if (lastModified != value) { NotifyPropertyChanging(); lastModified = value; NotifyPropertyChanged(); } } }
        public string BookId { get => bookId; set { if (bookId != value) { NotifyPropertyChanging(); bookId = value; NotifyPropertyChanged(); } } }
        public int Extent { get => extent; set { if (extent != value) { NotifyPropertyChanging(); extent = value; NotifyPropertyChanged(); } } }
        public string MimeType { get => mimeType; set { if (mimeType != value) { NotifyPropertyChanging(); mimeType = value; NotifyPropertyChanged(); } } }
        /// <summary>
        /// These are in the default preference order
        /// </summary>
        public enum ProcessedFileType
        {
            EPub, EPubNoImages, PDF, Text, TextNotUtf8, PS, Tex, CoverMedium, CoverSmall, Html, HtmlNotUtf8, MobiPocket, RDF, Unknown,
        };



        public string FileTypeAsString()
        {
            switch (GetFileType())
            {
                case ProcessedFileType.CoverMedium: return "Image file (book cover)";
                case ProcessedFileType.CoverSmall: return "Image file (book cover)";
                case ProcessedFileType.EPub: return "EPUB";
                case ProcessedFileType.EPubNoImages: return "EPUB (no images)";
                case ProcessedFileType.Html: return "HTML web file";
                case ProcessedFileType.MobiPocket: return "Kindle (MobiPocket)";
                case ProcessedFileType.PDF: return "PDF";
                case ProcessedFileType.PS: return "PostScript";
                case ProcessedFileType.RDF: return "RDF Index File";
                case ProcessedFileType.Tex: return "Tex pre-press file";
                case ProcessedFileType.Text: return "Plain text file";
                case ProcessedFileType.TextNotUtf8: return "Plain text file";
                case ProcessedFileType.Unknown:
                default:
                    return $"Other file type ({MimeType})";
            }
        }

        public ProcessedFileType GetFileType()
        {
            switch (MimeType)
            {
                case "application/epub+zip":
                    return (FileName.Contains(".images")) ? ProcessedFileType.EPub : ProcessedFileType.EPubNoImages;

                case "application/octet-stream": // seemingly obsolete -- used for old books only?
                    return ProcessedFileType.Unknown;

                case "application/pdf": // PDF file
                    return ProcessedFileType.PDF;

                case "application/postscript": // postscript, of course
                    return ProcessedFileType.PS;

                case "application/prs.tex": // TEX files!
                    return ProcessedFileType.Tex;

                case "application/rdf+xml": // the RDF file
                    return ProcessedFileType.RDF;

                case "application/x-mobipocket-ebook": // kindle
                    return ProcessedFileType.MobiPocket;

                case "application/zip": // HTML has two formats: /zip and /html
                    return ProcessedFileType.Html;

                case "image/jpeg": // cover images
                    if (String.IsNullOrEmpty(FileName)) return ProcessedFileType.CoverSmall;
                    if (FileName.Contains("cover.small")) return ProcessedFileType.CoverSmall;
                    if (FileName.Contains("cover.medium")) return ProcessedFileType.CoverMedium;
                    return ProcessedFileType.CoverSmall;

                case "text/html":
                case "text/html; charset=iso-8859-1":
                case "text/html; charset=us-ascii":
                    return ProcessedFileType.HtmlNotUtf8;

                case "text/html; charset=utf-8":
                    return ProcessedFileType.Html;

                case "text/plain":
                case "text/plain; charset=iso-8859-1":
                case "text/plain; charset=us-ascii":
                    return ProcessedFileType.TextNotUtf8;

                case "text/plain; charset=utf-8":
                    return ProcessedFileType.Text;
                default:
                    return ProcessedFileType.Unknown;
            }
        }
        public bool IsKnownMimeType
        {
            get
            {
                switch (MimeType)
                {
                    case "application/epub+zip":
                    case "application/msword": // word doc e.g. 10681 and 80+ others
                    case "application/octet-stream": // seemingly obsolete -- used for old books only?
                    case "application/pdf": // PDF file
                    case "application/postscript": // postscript, of course
                    case "application/prs.tei": // XML text file (about 520) -- see https://en.wikipedia.org/wiki/Text_Encoding_Initiative
                    case "application/prs.tex": // TEX files!
                    case "application/rdf+xml": // the RDF file
                    case "application/x-mobipocket-ebook": // kindle
                    case "application/x-iso9660-image": // USed by the CD and DVD projects e.g. 10802 -- about 200+
                    case "application/zip": // HTML has two formats: /zip and /html
                    case "audio/midi": // MIDI music files e.g. jingle bells 10535 about 2500+
                    case "audio/mp4": // MP4 e.g. 19450 about 9000+
                    case "audio/mpeg": // MPEG about 23000+
                    case "audio/ogg": // OGG VORBIS format about 23000+
                    case "audio/x-ms-wma": // Microsoft format e.g. 36567 (really, just that one)
                    case "audio/x-wav": //
                    case "image/gif": // cover images
                    case "image/jpeg": // cover images
                    case "image/png": // image
                    case "image/tiff": // image
                    case "text/html":
                    case "text/html; charset=iso-8859-1":
                    case "text/html; charset=iso-8859-2":
                    case "text/html; charset=iso-8859-15":
                    case "text/html; charset=us-ascii":
                    case "text/html; charset=utf-8":
                    case "text/html; charset=windows-1251":
                    case "text/html; charset=windows-1252":
                    case "text/html; charset=windows-1253":
                    case "text/plain":
                    case "text/plain; charset=big5": // just one, 11002
                    case "text/plain; charset=ibm850": // just one, 11522
                    case "text/plain; charset=iso-8859-1":
                    case "text/plain; charset=iso-8859-2": // about 13
                    case "text/plain; charset=iso-8859-3": // about 4
                    case "text/plain; charset=iso-8859-7": // about 5
                    case "text/plain; charset=iso-8859-15": // about 16
                    case "text/plain; charset=us-ascii":
                    case "text/plain; charset=utf-7": // about 2 both of them  7467
                    case "text/plain; charset=utf-8":
                    case "text/plain; charset=utf-16": // seriously? 1, 13083
                    case "text/plain; charset=windows-1250": // 
                    case "text/plain; charset=windows-1251": // 
                    case "text/plain; charset=windows-1252": // 
                    case "text/plain; charset=windows-1253": // 
                    case "text/rtf": // 
                    case "text/rtf; charset=iso-8859-1": // 
                    case "text/rtf; charset=us-ascii": // 
                    case "text/x-rst": // reStructured Text https://en.wikipedia.org/wiki/ReStructuredText
                    case "text/rst; charset=us-ascii": // reStructured Text https://en.wikipedia.org/wiki/ReStructuredText
                    case "text/xml":
                    case "text/xml; charset=iso-8859-1":
                    case "video/mpeg":
                    case "video/quicktime":
                    case "video/x-msvideo":
                        return true;
                    default:
                        return false;
                }
            }

        }
        public override string ToString()
        {
            return this.FileName;
        }
    }



    /// <summary>
    /// One Gutenberg record for a book (not all data is saved)
    /// </summary>
    public class BookData : IGetSearchArea, INotifyPropertyChanged, INotifyPropertyChanging
    {
        /// <summary>
        /// Return either "" (is valid) or a loggable string of why the book has problems.
        /// </summary>
        /// <returns></returns>
        public string Validate()
        {
            var retval = "";
            if (string.IsNullOrWhiteSpace(BookId)) retval += "ERROR: BookId is not set\n";
            if (string.IsNullOrWhiteSpace(Title)) retval += "ERROR: Title is not set\n";
            if (!string.IsNullOrWhiteSpace(TitleAlternative) && string.IsNullOrWhiteSpace(Title)) retval += "ERROR: TitleAlternative is set but Title is not\n";

            if (Issued == "None") retval += "ERROR: Book was not issued\n";
            if (Title == "No title" && Files.Count == 0) retval += "ERROR: Gutenberg made a book with no title or files";
            if (retval != "" && Files.Count == 0) retval += "ERROR: Book has no files";
            return retval;
        }

        [System.ComponentModel.DataAnnotations.Key]
        public string BookId { get => bookId; set { if (bookId != value) { NotifyPropertyChanging(); bookId = value; NotifyPropertyChanged(); } } }

        public const string BookSourceGutenberg = "gutenberg.org";
        public const string BookSourceUser = "User-imported";
        public const string BookSourceBookMarkFile = "From-bookmark-file:";
        public string BookSource { get => bookSource; set { if (bookSource != value) { NotifyPropertyChanging(); bookSource = value; NotifyPropertyChanged(); } } }

        public enum FileType { other, Text, Collection, Dataset, Image, MovingImage, Sound, StillImage }; // most are Text. Human genome project e.g 3501 is Dataset.
        public FileType BookType { get => bookType; set { if (bookType != value) { NotifyPropertyChanging(); bookType = value; NotifyPropertyChanged(); } } }
        /// <summary>
        /// Examples:
        /// <dcterms:description>There is an improved edition of this title, eBook #29888</dcterms:description>
        /// <dcterms:description>Illustrated by the author.</dcterms:description>
        /// </summary>
        public string Description { get => description; set { if (description != value) { NotifyPropertyChanging(); description = value; NotifyPropertyChanged(); } } }

        /// <summary>
        /// Examples:
        /// #28: <pgterms:marc260>Houston: Advantage International, The PaperLess Readers Club, 1992</pgterms:marc260>
        /// </summary>
        public string Imprint { get => imprint; set { if (imprint != value) { NotifyPropertyChanging(); imprint = value; NotifyPropertyChanged(); } } }

        public string Issued { get => issued; set { if (issued != value) { NotifyPropertyChanging(); issued = value; NotifyPropertyChanged(); } } }
        /// <summary>
        /// <dcterms:title>Three Little Kittens</dcterms:title>
        /// </summary>
        public string Title { get => title; set { if (title != value) { NotifyPropertyChanging(); title = value; NotifyPropertyChanged(); } } }

        /// <summary>
        /// Ued when there is already a title
        /// <dcterms:alternative>Alice in Wonderland</dcterms:alternative>
        /// </summary>
        public string TitleAlternative { get => titleAlternative; set { if (titleAlternative != value) { NotifyPropertyChanging(); titleAlternative = value; NotifyPropertyChanged(); } } }

        /// <summary>
        /// People include authors, illustrators, etc.
        /// </summary>
        public ObservableCollection<Person> People { get; set; } = new ObservableCollection<Person>();

#if WINDOWS_UWP
        // Per https://stackoverflow.com/questions/52187625/uwp-swipe-control-list-items-based-on-condition
        // Says "0 references" only because VS doesn't see the references in BookSearch.xaml ("leftItems="{binding DefaultSwipeAction}")
        [NotMapped][JsonIgnore]
        public SwipeItems DefaultSwipeActions
        {
            get
            {
                EnsureSwipeItems();
                var fileStatus = DownloadData == null ? DownloadData.FileStatus.Unknown : DownloadData.CurrFileStatus;
                switch (fileStatus)
                {
                    case DownloadData.FileStatus.Downloaded:
                        return SwipeItemsRead;
                    default:
                        return SwipeItemsDownload;
                }
            }
        }

        SwipeItems SwipeItemsDownload = null;
        SwipeItems SwipeItemsRead = null;
        private void EnsureSwipeItems()
        {
            if (SwipeItemsDownload == null)
            {
                var sitems = new SwipeItems() { Mode = SwipeMode.Execute };
                var si = new SwipeItem() { Text = "Download", IconSource = new SymbolIconSource() { Symbol = Symbol.Download } };
                si.Invoked += Si_Invoked;
                sitems.Add(si);
                SwipeItemsDownload = sitems;
            }
            if (SwipeItemsRead == null)
            {
                var sitems = new SwipeItems() { Mode = SwipeMode.Execute };
                var si = new SwipeItem() { Text = "Read", IconSource = new FontIconSource() { Glyph = "" } };
                si.Invoked += Si_Invoked;
                sitems.Add(si);
                SwipeItemsRead = sitems;
            }
        }

        private async void Si_Invoked(SwipeItem sender, SwipeItemInvokedEventArgs args)
        {
#if FULL_EREADER
            await BookSearch.DoSwipeDownloadOrReadAsync(this);
#else
            await Task.Delay(0);
#endif
        }
#if ORIGINAL_XAML_NOT_DEFINED_EVER
        <SwipeItems x:Key="DownloadSwipe" Mode="Execute">
            <SwipeItem Text="DOWNLOAD" IconSource="{StaticResource DownloadIcon}" Invoked="OnSwipeDownload"/>
        </SwipeItems>
        
        <SwipeItems x:Key="ReadSwipe" Mode="Execute">
            <SwipeItem Text="READ" IconSource="{StaticResource DownloadIcon}" Invoked="OnSwipeDownload"/>
        </SwipeItems>
#endif
#endif

        public string BestAuthorDefaultIsNull
        {
            get
            {
                var personlist = from person in People orderby person.GetImportance() ascending select person;
                var author = personlist.FirstOrDefault();
                if (author == null)
                {
                    return null;
                }
                return author.Name;
            }
        }

        /// <summary>
        /// Get a shortened title with author name suitable for being a filename.
        /// </summary>
        /// <returns></returns>
        public string GetBestTitleForFilename()
        {
            var personlist = from person in People orderby person.GetImportance() ascending select person;
            var author = personlist.FirstOrDefault();
            return TitleConverter(Title, author?.Name);
        }
        const int NICE_MIN_LEN = 20;
        const int NICE_MAX_LEN = 30;
        private string bookId;
        private string bookSource = BookSourceGutenberg;
        private FileType bookType = FileType.other;
        private string description;
        private string imprint;
        private string issued = "";
        private string title;
        private string titleAlternative;
        private string language;
        private string lCSH = "";
        private string lCCN = "";
        private string pGEditionInfo;
        private string pGProducedBy;
        private string pGNotes;
        private string bookSeries;
        private string lCC = "";
        private UserReview review = null;
        private BookNotes notes = null;
        private DownloadData downloadData = null;
        private BookNavigationData navigationData = null;
        private long denormDownloadDate; // unix time seconds

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
        /// Used by the TitleConverter to get a nice potential filename from the title+author.
        /// The file should include both title and author if possible
        /// </summary>
        /// <param name="value"></param>
        /// <param name="min"></param>
        /// <param name="max"></param>
        /// <returns></returns>
        private static string ChopString(string value, int min = NICE_MIN_LEN, int max = NICE_MAX_LEN)
        {
            if (value == null) return value;
            if (value.Length > min)
            {
                var nextspace = value.IndexOf(' ', min);
                if (nextspace < 0 && value.Length < max)
                {
                    ; // no next space, but the total title isn't too long. Keep it as-is
                }
                else if (nextspace < 0)
                {
                    // Too long, and no next space at all. Chop it ruthlessly.
                    value = value.Substring(0, min);
                }
                else if (nextspace < max)
                {
                    value = value.Substring(0, nextspace);
                }
                else
                {
                    // Too long, and no convenient next space. Chop it ruthlessly.
                    value = value.Substring(0, min);
                }
            }
            value = value.Trim();
            return value;
        }
        /// <summary>
        /// Given a title and author, generate a nice possible file string. Uses ASCII only (sorry, everyone
        /// with a name or title that doesn't convert)
        /// </summary>
        /// <param name="title"></param>
        /// <param name="author"></param>
        /// <returns></returns>
        private static string TitleConverter(string title, string author)
        {
            title = ChopString(title);
            author = ChopString(author);
            var potentialRetval = author == null ? title : $"{title}_by_{author}";
            potentialRetval = potentialRetval.Replace(" , ", ",").Replace(", ", ","); // because smith , jane is better as smith_jane
            char[] remove = { '\'', '.' };
            var sb = new StringBuilder();
            foreach (var ch in potentialRetval)
            {
                char c = ch;
                if (!remove.Contains(c))
                {
                    if (char.IsControl(ch)) c = '-';
                    else if (char.IsWhiteSpace(ch)) c = '_';
                    else if (char.IsLetterOrDigit(ch)) c = ch;
                    else if (ch < 128) c = '_';
                    else c = ch; // allow all of the unicode chars
                    sb.Append(c);
                }
            }
            var retval = sb.ToString();
            retval = retval.Trim('_');
            return retval;
        }

        /// <summary>
        /// List of all of the files for this book and their formats.
        /// </summary>
        public ObservableCollection<FilenameAndFormatData> Files { get; set; } = new ObservableCollection<FilenameAndFormatData>();
        /// <summary>
        /// <dcterms:language>
        ///     <rdf:Description rdf:nodeID="Nc3827dd334c44413ab159b8f40d432ec">
        ///         <rdf:value rdf:datatype="http://purl.org/dc/terms/RFC4646">en</rdf:value>
        ///     </rdf:Description>
        /// </dcterms:language>
        /// </summary>
        /// 
        public static bool FilesMatch(BookData a, BookData b)
        {
            var retval = true;
            foreach (var afile in a.Files)
            {
                var hasMatch = false;
                foreach (var bfile in b.Files)
                {
                    if (afile.FileName == bfile.FileName)
                    {
                        hasMatch = true;
                        break;
                    }
                }
                if (!hasMatch)
                {
                    retval = false;
                    break;
                }
            }
            return retval;
        }

        public string Language { get => language; set { if (language != value) { NotifyPropertyChanging(); language = value; NotifyPropertyChanged(); } } }
        // e.g. en. Apress raw data can be captialized as En, which IMHO is wrong.

        /// <summary>
        /// <dcterms:subject>
        ///     <rdf:Description rdf:nodeID="N0d26c4c9a07a454789d1f6545628914b">
        ///         <rdf:value>Cats -- Juvenile fiction</rdf:value>
        ///         <dcam:memberOf rdf:resource= "http://purl.org/dc/terms/LCSH" />
        ///     </rdf:Description>
        ///     </dcterms:subject>
        /// </summary>
        public string LCSH { get => lCSH; set { if (lCSH != value) { NotifyPropertyChanging(); lCSH = value; NotifyPropertyChanged(); } } }
        // is the Cats -- Juvenile fiction

        public string LCCN { get => lCCN; set { if (lCCN != value) { NotifyPropertyChanging(); lCCN = value; NotifyPropertyChanged(); } } }
        // Marc010 e.g. 18020634 is https://catalog.loc.gov/vwebv/search?searchArg=18020634&searchCode=GKEY%5E*&searchType=0&recCount=25&sk=en_US

        public string PGEditionInfo { get => pGEditionInfo; set { if (pGEditionInfo != value) { NotifyPropertyChanging(); pGEditionInfo = value; NotifyPropertyChanged(); } } }

        // Marc250
        public string PGProducedBy { get => pGProducedBy; set { if (pGProducedBy != value) { NotifyPropertyChanging(); pGProducedBy = value; NotifyPropertyChanged(); } } }

        // Marc508 e.g. Produced by Biblioteca Nacional Digital (http://bnd.bn.pt),\n
        public string PGNotes { get => pGNotes; set { if (pGNotes != value) { NotifyPropertyChanging(); pGNotes = value; NotifyPropertyChanged(); } } }

        // Marc546 e.g. This ebook uses a 19th century spelling for pg11299.rdf
        public string BookSeries { get => bookSeries; set { if (bookSeries != value) { NotifyPropertyChanging(); bookSeries = value; NotifyPropertyChanged(); } } }

        // Marc440 e.g. The Pony Rider Boys, number 8 for pg12980.rdf

        /// <summary>
        /// Example. Note that 'subject' might be LCC or LCSH
        /// <dcterms:subject>
        ///     <rdf:Description rdf:nodeID="N5e552155376c46acba0f56226354c4a8">
        ///         <dcam:memberOf rdf:resource="http://purl.org/dc/terms/LCC"/>
        ///         <rdf:value>PZ</rdf:value>
        ///     </rdf:Description>
        /// </dcterms:subject>
        /// 
        /// </summary>
        public string LCC { get => lCC; set { if (lCC != value) { NotifyPropertyChanging(); lCC = value; NotifyPropertyChanged(); } } }
        // is the PZ. Is a CSV because e.g. book 1 is both JK and E201

        //
        // Denormalized data used the make sorting go faster
        //
        public string DenormPrimaryAuthor { get; set; }
        public long DenormDownloadDate { get => denormDownloadDate; set { if (denormDownloadDate != value) { NotifyPropertyChanging(); denormDownloadDate = value; NotifyPropertyChanged(); } } }


        //
        // Next is all of the user-settable things
        //

        public UserReview Review { get => review; set { if (review != value) { NotifyPropertyChanging(); review = value; NotifyPropertyChanged(); } } }
        public BookNotes Notes { get => notes; set { if (notes != value) { NotifyPropertyChanging(); notes = value; NotifyPropertyChanged(); } } }
        public DownloadData DownloadData { get => downloadData; set { if (downloadData != value) { NotifyPropertyChanging(); downloadData = value; NotifyPropertyChanged(); } } }
        public BookNavigationData NavigationData { get => navigationData; set { if (navigationData != value) { NotifyPropertyChanging(); navigationData = value; NotifyPropertyChanged(); } } }
        // Used by the search system
        public IList<string> GetSearchArea(string inputArea)
        {
            var retval = new List<string>();
            var area = (inputArea + "...").Substring(0, 3).ToLower(); // e.g. title --> ti
            switch (area)
            {
                case "...":
                    AddTitle(retval);
                    AddPeople(retval);
                    AddLCC(retval);
                    AddNotes(retval);
                    break;

                case "tit": // title
                    AddTitle(retval);
                    break;

                case "by.":
                    AddPeople(retval);
                    break;

                case "aut": // author is part of by
                    foreach (var person in People)
                    {
                        if (person.PersonType == Person.Relator.author
                            || person.PersonType == Person.Relator.artist
                            || person.PersonType == Person.Relator.collaborator
                            || person.PersonType == Person.Relator.contributor
                            || person.PersonType == Person.Relator.dubiousAuthor)
                        {
                            retval.Add(person.Name);
                            if (!string.IsNullOrEmpty(person.Aliases)) retval.Add(person.Aliases);
                        }
                    }
                    break;

                case "edi": // editor is part of by
                    foreach (var person in People)
                    {
                        if (person.PersonType == Person.Relator.editor
                            || person.PersonType == Person.Relator.editorOfCompilation
                            || person.PersonType == Person.Relator.printer
                            || person.PersonType == Person.Relator.publisher
                            )
                        {
                            retval.Add(person.Name);
                            if (!string.IsNullOrEmpty(person.Aliases)) retval.Add(person.Aliases);
                        }
                    }
                    break;

                case "lc.": // e.g. just the LCC=PS or the LCN=E305
                    if (!string.IsNullOrEmpty(LCC)) retval.Add(LCC);
                    if (!string.IsNullOrEmpty(LCCN)) retval.Add(LCCN);
                    break;
                case "lcc": // LCC includes all LC
                    AddLCC(retval);
                    break;

                case "ill": // illustrator is part of by
                    foreach (var person in People)
                    {
                        if (person.PersonType == Person.Relator.illustrator
                            || person.PersonType == Person.Relator.artist
                            || person.PersonType == Person.Relator.engraver
                            || person.PersonType == Person.Relator.photographer)
                        {
                            retval.Add(person.Name);
                            if (!string.IsNullOrEmpty(person.Aliases)) retval.Add(person.Aliases);
                        }
                    }
                    break;

                case "not": // notes and reviews
                    AddNotes(retval);
                    break;

                case "ser": // series, like the Pony Rider (is part of title)
                    if (!string.IsNullOrEmpty(BookSeries)) retval.Add(BookSeries);
                    break;

                case "sub": // lcc subject headings e.g. just the LCC=PS or the LCN=E305
                    if (!string.IsNullOrEmpty(LCSH)) retval.Add(LCSH);
                    break;
            }
            return retval;
        }

        private void AddLCC(List<string> retval)
        {
            if (!string.IsNullOrEmpty(LCC)) retval.Add(LCC);
            if (!string.IsNullOrEmpty(LCCN)) retval.Add(LCCN);
            if (!string.IsNullOrEmpty(LCSH)) retval.Add(LCSH);
        }

        private void AddNotes(List<string> retval)
        {
            if (!string.IsNullOrEmpty(Review?.Tags)) retval.Add(Review.Tags);
            if (!string.IsNullOrEmpty(Review?.Review)) retval.Add(Review.Review);
            if (Notes != null && Notes.Notes != null && Notes.Notes.Count > 0)
            {
                foreach (var note in Notes.Notes)
                {
                    if (!string.IsNullOrEmpty(note.Tags)) retval.Add(note.Tags);
                    if (!string.IsNullOrEmpty(note.Text)) retval.Add(note.Text);
                }
            }
        }
        private void AddPeople(List<string> retval)
        {
            foreach (var person in People)
            {
                retval.Add(person.Name);
                if (!string.IsNullOrEmpty(person.Aliases)) retval.Add(person.Aliases);
            }
        }
        private void AddTitle(List<string> retval)
        {
            retval.Add(Title);
            if (!string.IsNullOrEmpty(TitleAlternative)) retval.Add(TitleAlternative);
            if (!string.IsNullOrEmpty(BookSeries)) retval.Add(BookSeries);
        }
        /// <summary>
        /// Merge two book data items together where one is directly from a catalog and has
        /// no user data (like a review or notes)
        /// TODO: finish!
        /// </summary>
        /// <param name="existing"></param>
        /// <param name="catalog"></param>
        public static void Merge(BookData existing, BookData catalog)
        {
            // book id: keep existing
            existing.BookSource = catalog.BookSource;
            existing.BookType = catalog.BookType;
            existing.Description = catalog.Description;
            existing.Imprint = catalog.Imprint;
            existing.Issued = catalog.Issued;
            existing.Title = catalog.Title;
            existing.TitleAlternative = catalog.TitleAlternative;
            while (existing.People.Count >0)
            {
                existing.People.RemoveAt(0);
            }
            // To heck with collections that can't clear! existing.People.Clear();
            foreach (var person in catalog.People)
            {
                if (person.Id != 0) person.Id = 0; // Straight from a catalog there should be no person id values set.
                existing.People.Add(person);
            }
            while (existing.Files.Count > 0)
            {
                existing.Files.RemoveAt(0);
            }
            // to heck with...existing.Files.Clear();
            foreach (var file in catalog.Files)
            {
                if (file.Id != 0) file.Id = 0; // Straight from a catalog there should be no file id values set.
                existing.Files.Add(file);
            }
            existing.Language = catalog.Language;
            existing.LCSH = catalog.LCSH;
            existing.LCCN = catalog.LCCN;
            existing.PGEditionInfo = catalog.PGEditionInfo;
            existing.PGProducedBy = catalog.PGProducedBy;
            existing.PGNotes = catalog.PGNotes;
            existing.BookSeries = catalog.BookSeries;
            existing.LCC = catalog.LCC;
            existing.DenormPrimaryAuthor = catalog.DenormPrimaryAuthor;
            // unchanged: existing.DenormDownloadDate in case anything has been downloaded
            // unchanged: review
            // unchanged: notes
            // unchanged: downloaddata
            // unchanged: navigationdata
        }
        public override string ToString()
        {
            return $"{Title.Substring(0, Math.Min(Title.Length, 20))} for {BookId}";
        }


    }
}
