using PCLStorage;
using SharpCompress.Readers;
using SimpleEpubReader.FileWizards;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using Windows.Storage.Pickers;

namespace SimpleEpubReader.Database
{
    static class RdfReader
    {
        public static async Task<int> ReadZipTarRdfFile(BookDataContext bookdb)
        {

            var picker = new FileOpenPicker()
            {
                ViewMode = PickerViewMode.List,
                SuggestedStartLocation = PickerLocationId.DocumentsLibrary,
            };
            picker.FileTypeFilter.Add(".zip");
            var filepick = await picker.PickSingleFileAsync();
            if (filepick == null) return 0;
            var fullpath = filepick.Path;
            var file = await PCLStorage.FileSystem.Current.GetFileFromPathAsync(fullpath);
            var ui = new NullIndexReader(); // don't really do anything :-)
            int retval = 0;
            await Task.Run(async () =>
            {
                var cts = new CancellationTokenSource();
                retval = await ReadZipTarRdfFileAsync(ui, bookdb, file, cts.Token);
                ;
            });
            return retval;
        }


        public enum UpdateType { Full, Fast }


        public static async Task<int> ReadZipTarRdfFileAsync(IndexReader ui, BookDataContext bookdb, IFile file, CancellationToken token, UpdateType updateType = UpdateType.Full)
        {
            SaveAfterNFiles = SaveSkipCount;
            UiAfterNNodes = NodeReadCount;

            // FAIL: Gutenberg includes bad files
            HashSet<string> KnownBadFiles = new HashSet<string>()
            {
                "cache/epub/0/pg0.rdf",
                "cache/epub/999999/pg999999.rdf",
            };
            var startTime = DateTime.Now;
            int nnewfiles = 0;
            int nnodes = 0;
            List<BookData> newBooks = new List<BookData>();

            using (var stream = await file.OpenAsync(PCLStorage.FileAccess.Read))
            {
                using (var reader = ReaderFactory.Open(stream))
                {
                    while (reader.MoveToNextEntry())
                    {
                        if (token.IsCancellationRequested)
                        {
                            break;
                        }
                        System.Diagnostics.Debug.WriteLine($"ZIPREAD: {reader.Entry.Key} size {reader.Entry.Size}");

                        // Is the rdf-files.tar file that Gutenberg uses. 
                        // The zip file has one giant TAR file (rdf-files.tar) embedded in it.
                        if (reader.Entry.Key.EndsWith(".tar"))
                        {
                            using (var tarStream = reader.OpenEntryStream())
                            {
                                using (var tarReader = ReaderFactory.Open(tarStream))
                                {
                                    while (tarReader.MoveToNextEntry())
                                    {
                                        MemoryStream ms = new MemoryStream((int)tarReader.Entry.Size);
                                        tarReader.WriteEntryTo(ms);
                                        ms.Position = 0;
                                        var sr = new StreamReader(ms);
                                        var text = sr.ReadToEnd();
                                        nnodes++;
                                        if (token.IsCancellationRequested)
                                        {
                                            break;
                                        }

                                        if (KnownBadFiles.Contains(tarReader.Entry.Key))
                                        {
                                            // Skip known bad files like entry 999999 -- has weird values for lots of stuff!
                                        }
                                        else
                                        {
                                            // Got a book; let the UI know.
                                            newBooks.Clear();
                                            if (tarReader.Entry.Key.Contains ("62548"))
                                            {
                                                ; // useful hook for debugging.
                                            }

                                            // Reads and saves to database. And does a fancy merge if needed.
                                            var newCount = Read(bookdb, tarReader.Entry.Key, text, newBooks, updateType);
                                            nnewfiles += newCount;

                                            if (nnewfiles > 6000 && nnewfiles < 9000)
                                            {
                                                SaveSkipCount = 100;
                                            }
                                            else
                                            {
                                                SaveSkipCount = 100; // save very frequently. Otherwise, ka-boom!
                                            }

                                            if (nnewfiles >= SaveAfterNFiles)
                                            {
                                                // FAIL: must save periodically. Can't accumulate a large number
                                                // of books (e..g, 60K books in the catalog) and then save all at
                                                // once; it will take up too much memory and will crash.
                                                Log($"At index {CommonQueries.BookCount(bookdb)} file {file.Name} nfiles {nnewfiles}");
                                                CommonQueries.BookSaveChanges(bookdb);

                                                // Try resetting the singleton to reduce the number of crashes.
                                                BookDataContext.ResetSingleton("InitialBookData.Db");
                                                await Task.Delay(100); // Try a pause to reduce crashes.

                                                SaveAfterNFiles += SaveSkipCount;

                                            }
                                            if (newCount > 0)
                                            {
                                                foreach (var bookData in newBooks)
                                                {
                                                    await ui.OnAddNewBook(bookData);
                                                }
                                            }
                                            if (nnodes >= UiAfterNNodes)
                                            {
                                                //await ui.LogAsync($"Book: file {tarReader.Entry.Key}\nNNew: {nfiles} NProcesses {nnodes}\n");
                                                await ui.OnTotalBooks(nnodes);
                                                UiAfterNNodes += NodeReadCount;
                                            }
                                        }
                                    }
                                }
                            }

                        }
                    }
                }
            }
            await ui.OnReadComplete(nnodes, nnewfiles);
            var delta = DateTime.Now.Subtract(startTime).TotalSeconds;
            System.Diagnostics.Debug.WriteLine($"READ: {nnewfiles} in {delta} seconds = {nnewfiles / delta} fps or {delta / nnewfiles * 1000} ms per file");

            CommonQueries.BookSaveChanges(bookdb); // Woot, woot! I've got good book data!
            return nnewfiles;
        }

#if NEVER_EVER_DEFINED
        static int NRead = 0;
        /// <summary>
        /// Returns the number of books read; clears out the original books and saves.
        /// </summary>
        /// <returns></returns>
        public static async Task<int> ReadDirAsync(BookDataContext bookdb)
        {
            var picker = new FolderPicker()
            {
                SuggestedStartLocation = PickerLocationId.DocumentsLibrary,
            };
            picker.FileTypeFilter.Add(".rdf");
            var folder = await picker.PickSingleFolderAsync();
            if (folder == null) return -1;
            NextIndexLogged = LogNIndex;

            NRead = 0;
            CommonQueries.BookRemoveAll(bookdb);
            await ReadDirAsyncFolder(bookdb, folder);
            CommonQueries.BookSaveChanges(bookdb);
            var totlog = logsb.ToString();
            return NRead;
        }
#endif
        static int SaveAfterNFiles = 0;
        static int SaveSkipCount = 100;

        static int UiAfterNNodes = 0;
        static int NodeReadCount = 100;

        const int MaxFilesChecked = 9999999;

#if NEVER_EVER_DEFINED
        private static async Task ReadDirAsyncFolder(BookDataContext bookdb, StorageFolder folder)
        {
            uint startIndex = 0;
            uint maxItems = 1000;

            try
            {
                bool keepGoing = true;
                while (keepGoing)
                {
                    var allitems = await folder.GetItemsAsync(startIndex, maxItems);
                    if (allitems == null || allitems.Count == 0)
                    {
                        keepGoing = false;
                    }
                    else
                    {
                        if (allitems.Count < maxItems)
                        {
                            keepGoing = false;
                        }
                        foreach (var item in allitems)
                        {
                            if (item is StorageFile file)
                            {
                                if (file.Name.EndsWith(".rdf"))
                                {
                                    var text = await FileIO.ReadTextAsync(file);
                                    Read(bookdb, file.Name, text);
                                    if (NRead >= NextIndexLogged)
                                    {
                                        Log($"At index {CommonQueries.BookCount(bookdb)} file {file.Name}");
                                        NextIndexLogged += LogNIndex;
                                    }
                                    if (CommonQueries.BookCount(bookdb) >= MaxFilesChecked) return;
                                }
                            }
                            else if (item is StorageFolder subfolder)
                            {
                                await ReadDirAsyncFolder(bookdb, subfolder);
                                if (CommonQueries.BookCount(bookdb) >= MaxFilesChecked) return;
                            }
                        }
                        startIndex += maxItems;
                    }
                }
            }
            catch (Exception ex)
            {
                Log($"ERROR: got exception while reading {ex.Message}");
            }
        }
#endif
        static StringBuilder logsb = new StringBuilder();
        private static void Log(string str)
        {
            logsb.Append(str + "\n");
            System.Diagnostics.Debug.WriteLine(str);
        }

#if EXAMPLE_XML
    <dcterms:creator>
      <pgterms:agent rdf:about="2009/agents/1139">
        <pgterms:deathdate rdf:datatype="http://www.w3.org/2001/XMLSchema#integer">1938</pgterms:deathdate>
        <pgterms:birthdate rdf:datatype="http://www.w3.org/2001/XMLSchema#integer">1863</pgterms:birthdate>
        <pgterms:webpage rdf:resource="http://en.wikipedia.org/wiki/Katharine_Pyle"/>
        <pgterms:alias>Pyle, Katherine</pgterms:alias> // // // Note: can have multiple aliases
        <pgterms:name>Pyle, Katharine</pgterms:name>
      </pgterms:agent>
    </dcterms:creator>
#endif
        private static Person ExtractCreator(string logname, XmlNode parentnode)
        {
            var retval = new Person();
            try
            {
                var node = parentnode["pgterms:agent"] as XmlNode;
                if (node == null)
                {
                    // Some books (like 1813) just don't have all this set up.
                    //Log($"Error: ExtractCreator doesn't have a pgterms:agent for {parentnode.InnerText} for {logname}");
                    return null;
                }
                string str = "";
                var relator = Person.ToRelator(parentnode.Name); // e.g. marcrel:aui == author of introductions
                if (relator == Person.Relator.otherError)
                {
                    Log($"ERROR: ExtractCreator has unknown relator {parentnode.Name} for {logname}");
                    return null;
                }
                retval.PersonType = relator;
                str = (node["pgterms:deathdate"] as XmlNode)?.InnerText;
                if (!string.IsNullOrEmpty(str)) retval.DeathDate = Int32.Parse(str);
                str = (node["pgterms:birthdate"] as XmlNode)?.InnerText;
                if (!string.IsNullOrEmpty(str)) retval.BirthDate = Int32.Parse(str);
                str = (node["pgterms:webpage"] as XmlNode)?.Attributes?.GetNamedItem("rdf:resource")?.Value;
                if (!string.IsNullOrEmpty(str)) retval.Webpage = str;
                str = (node["pgterms:alias"] as XmlNode)?.InnerText;
                if (!string.IsNullOrEmpty(str)) retval.AddAlias(str);
                str = (node["pgterms:name"] as XmlNode)?.InnerText;
                if (!string.IsNullOrEmpty(str)) retval.Name = str;
            }
            catch (Exception)
            {
                Log($"ERROR: unable to extract person from {parentnode.Value} for {logname}");
                return null;
            }
            return retval;
        }
        private static FilenameAndFormatData ExtractHasFormat(string logname, XmlNode node)
        {
            bool extentIsZero = false;
            var retval = new FilenameAndFormatData();
            int nchild = 0;
            try
            {
                foreach (var childFileObj in node.ChildNodes)
                {
                    var childFile = childFileObj as XmlNode;
                    if (childFile == null)
                    {
                        Log($"ERROR: hasFormat child isn't an XmlNode with {node.InnerText} for {logname}");
                        continue;
                    }
                    nchild++;
                    if (nchild > 1)
                    {
                        Log($"ERROR: hasFormat has too many child with {node.InnerText} for {logname}");
                        continue;
                    }
                    retval.FileName = childFile.Attributes["rdf:about"].Value; // The super critical part!
                    foreach (var valueObj in childFile.ChildNodes)
                    {
                        var value = valueObj as XmlNode;
                        if (value == null)
                        {
                            Log($"ERROR: hasFormat grandchild isn't an XmlNode with {childFile.InnerText} for {logname}");
                            continue;
                        }
                        switch (value.Name)
                        {
                            case "dcterms:format":
                                foreach (var descriptionObj in value.ChildNodes)
                                {
                                    var description = descriptionObj as XmlNode;
                                    if (description == null)
                                    {
                                        Log($"ERROR: hasFormat description grandchild isn't an XmlNode with {value.InnerText} for {logname}");
                                        continue;
                                    }
                                    foreach (var dvalueObj in description.ChildNodes)
                                    {
                                        var dvalue = dvalueObj as XmlNode;
                                        if (dvalue == null)
                                        {
                                            Log($"ERROR: hasFormat description grandchild isn't an XmlNode with {description.InnerText} for {logname}");
                                            continue;
                                        }
                                        switch (dvalue.Name)
                                        {
                                            case "rdf:value":
                                                retval.MimeType = dvalue.InnerText;
                                                break;
                                            case "dcam:memberOf":
                                                break;
                                            default:
                                                Log($"ERROR: Unknown member {dvalue.Name} for {logname}");
                                                break;
                                        }
                                    }
                                }
                                break;
                            case "dcterms:modified":
                                if (retval.LastModified != "")
                                {
                                    Log($"ERROR: hasFormat has multiple {value.Name} for {logname}");
                                }
                                retval.LastModified = value.InnerText;
                                break;
                            case "dcterms:extent":
                                if (retval.Extent != -1)
                                {
                                    Log($"ERROR: hasFormat has multiple {value.Name} for {logname}");
                                }
                                retval.Extent = Int32.Parse(value.InnerText);
                                if (retval.Extent == 0) extentIsZero = true;
                                break;
                            case "dcterms:isFormatOf":
                                if (retval.BookId != "")
                                {
                                    Log($"ERROR: hasFormat has multiple {value.Name} for {logname}");
                                }
                                retval.BookId = value.Attributes["rdf:resource"].Value;
                                break;
                            default:
                                Log($"ERROR: hasFormat unknown child {value.Name} for {logname}");
                                break;
                        }
                    }
                }
            }
            catch (Exception)
            {
                Log($"ERROR: unable to extract hasFormat from {node.Value} for {logname}");
            }

            if (retval.FileName == "") Log($"ERROR: hasFormat: doesn't include filename for {logname}");
            if (retval.Extent < 1 && !extentIsZero) Log($"ERROR: hasFormat: doesn't include extent for {logname} for {retval.FileName}");
            if (retval.LastModified == "") Log($"ERROR: hasFormat: doesn't include modified for {logname}");
            if (retval.BookId == "") Log($"ERROR: hasFormat: doesn't include bookId for {logname}");
            if (!retval.IsKnownMimeType) Log($"ERROR: hasFormat: Unknown mime type {retval.MimeType} for {logname}");
            return retval;
        }

#if EXAMPLE_XML
    <dcterms:language>
      <rdf:Description rdf:nodeID="Nc3827dd334c44413ab159b8f40d432ec">
        <rdf:value rdf:datatype="http://purl.org/dc/terms/RFC4646">en</rdf:value>
      </rdf:Description>
    </dcterms:language>

        OR
            <dcterms:language rdf:datatype="http://purl.org/dc/terms/RFC4646">it</dcterms:language>

#endif
        private static string ExtractLanguageValue(string logname, XmlNode node, string defaultValue = "")
        {
            string retval = defaultValue;
            try
            {
                var descrNode = node["rdf:Description"] as XmlNode;
                if (descrNode != null)
                {
                    var language = descrNode["rdf:value"];
                    retval = language.InnerText.ToLower(); // APRESS can have this as En instead of en
                }
                else
                {
                    var inner = node.InnerText;
                    if (inner.Length >= 2 && inner.Length <= 2)
                    {
                        retval = inner.ToLower(); // APRESS can have this as En instead of en
                    }
                    else
                    {
                        Log($"ERROR: unrecognized inner-text language {inner} from {node.Value} for {logname}");
                    }
                }
            }
            catch (Exception)
            {
                Log($"ERROR: unable to extract language from {node.Value} for {logname}");
            }
            return retval;
        }


#if EXAMPLE_XML
    <dcterms:subject>
      <rdf:Description rdf:nodeID="Nd480a15e424645cb91dab86e7bade625">
        <rdf:value>Kittens -- Juvenile fiction</rdf:value>
        <dcam:memberOf rdf:resource="http://purl.org/dc/terms/LCSH"/>
      </rdf:Description>
    </dcterms:subject>
#endif
        enum SubjectType { Other, LCSH, LCC, }
        private static (SubjectType, string) ExtractSubjectValue(string logname, XmlNode node, string defaultValue = "")
        {
            var subjectType = SubjectType.Other;
            string retval = defaultValue;
            try
            {
                var description = (node["rdf:Description"] as XmlNode);
                retval = description["rdf:value"].InnerText;
                var subjectString = description["dcam:memberOf"].GetAttribute("rdf:resource");
                switch (subjectString)
                {
                    case "http://purl.org/dc/terms/LCSH": subjectType = SubjectType.LCSH; break;
                    case "http://purl.org/dc/terms/LCC": subjectType = SubjectType.LCC; break;
                    default: Log("$ERROR: unknown subject type {subjectString} for {logname}"); break;
                }
            }
            catch (Exception)
            {
                Log($"ERROR: unable to extract subject from {node.Value} for {logname}");
            }
            return (subjectType, retval);
        }

#if EXAMPLE_XML
    <dcterms:type>
      <rdf:Description rdf:nodeID="N738949286f564334b4d9053893d634cd">
        <rdf:value>Text</rdf:value>
        <dcam:memberOf rdf:resource="http://purl.org/dc/terms/DCMIType"/>
      </rdf:Description>
    </dcterms:type>
#endif
        private static string ExtractType(string logname, XmlNode node, string defaultValue = "")
        {
            string retval = defaultValue;
            try
            {
                var language = (node["rdf:Description"] as XmlNode)["rdf:value"];
                retval = language.InnerText;
            }
            catch (Exception)
            {
                Log($"ERROR: unable to extract type from {node.Value} for {logname}");
            }
            return retval;
        }

#if EXAMPLE_XML
    From pg19.rdf Hiawatha 
    <marcrel:edt>
      <pgterms:agent rdf:about="2009/agents/26859">
        <pgterms:name>Morris, Woodrow W.</pgterms:name>
      </pgterms:agent>
    </marcrel:edt>

    <marcrel:trl>
      <pgterms:agent rdf:about="2009/agents/19">
        <pgterms:alias>Townsend, George Tyler</pgterms:alias>
        <pgterms:webpage rdf:resource="http://en.wikipedia.org/wiki/George_Fyler_Townsend"/>
        <pgterms:deathdate rdf:datatype="http://www.w3.org/2001/XMLSchema#integer">1900</pgterms:deathdate>
        <pgterms:birthdate rdf:datatype="http://www.w3.org/2001/XMLSchema#integer">1814</pgterms:birthdate>
        <pgterms:name>Townsend, George Fyler</pgterms:name>
      </pgterms:agent>
    </marcrel:trl>
#endif


        private static BookData ExtractBook(string logname, XmlNode node)
        {
            var book = new BookData();
            var id = node.Attributes["rdf:about"]?.Value;
            if (!string.IsNullOrEmpty(id))
            {
                book.BookId = id;
            }
            else
            {
                Log($"ERROR: BookId: missing rdf:about with an id? for {logname}");
                return null;
            }
            foreach (var cn in node.ChildNodes)
            {
                var value = cn as XmlNode;
                if (value == null) continue;
                Person person = null;
                switch (value.Name)
                {
                    case "dcterms:alternative": // <...>Alice in Wonderland</...>
                        book.TitleAlternative = value.InnerText;
                        break;
                    case "dcterms:creator":
                        person = ExtractCreator(logname, value);
                        if (person != null) book.People.Add(person);
                        break;
                    case "dcterms:description": // <...>Illustrated by the author.</...>
                        book.Description = value.InnerText;
                        break;
                    case "dcterms:hasFormat": //  direct info about how to download a book
                        var format = ExtractHasFormat(logname, value);
                        if (format.Extent > 0)
                        {
                            // Gutenberg has a bunch of badly-made files which end up as zero size.
                            // In all cases where the extend it zero, the actual file on project gutenberg in fact
                            // exists but has no bytes. There's no point in adding a catalog entry for something that
                            // can't actually show up.
                            book.Files.Add(format);
                        }
                        break;
                    case "dcterms:issued": // <... rdf:datatype="http://www.w3.org/2001/XMLSchema#date">2015-03-06</...>
                        book.Issued = value.InnerText; // e.g. 1997-12-01 or None
                        break;
                    case "dcterms:language":
                        book.Language = ExtractLanguageValue(logname, value);
                        break;
                    case "dcterms:license": // <dcterms:license rdf:resource="license"/>
                        break;
                    case "dcterms:publisher": // <dcterms:publisher>Project Gutenberg</dcterms:publisher>
                        break;
                    case "dcterms:rights": // <dcterms:rights>Public domain in the USA.</dcterms:rights>
                        break;
                    case "dcterms:subject": // <dcterms:rights>Public domain in the USA.</dcterms:rights>
                        var (subjectType, subject) = ExtractSubjectValue(logname, value);
                        switch (subjectType)
                        {
                            case SubjectType.LCC:
                                if (!string.IsNullOrEmpty(book.LCC)) book.LCC += ",";
                                book.LCC += subject;
                                break;
                            case SubjectType.LCSH:
                                if (!string.IsNullOrEmpty(book.LCSH)) book.LCSH += ",";
                                book.LCSH += subject;
                                break;
                            default:
                                Log($"ERROR: unable to understand dcterms:subject {value.InnerText} for {logname}");
                                break;
                        }
                        break;
                    case "dcterms:tableOfContents":
                        // Alas: these are often not actually very useful. The epubs have correct TOC 
                        // with links set up.
                        break;
                    case "dcterms:title": // <dcterms:title>Three Little Kittens</dcterms:title>
                        book.Title = value.InnerText;
                        break;
                    case "dcterms:type":
                        var bookType = ExtractType(logname, value, "???");
                        switch (bookType)
                        {
                            case "Collection": book.BookType = BookData.FileType.Collection; break;
                            case "Dataset": book.BookType = BookData.FileType.Dataset; break;
                            // Dataset from e.g. http://www.gutenberg.org/ebooks/3503
                            // These are e.g. the human genome project. They are 100% uninteresting.
                            case "Image": book.BookType = BookData.FileType.Image; break;
                            case "MovingImage": book.BookType = BookData.FileType.MovingImage; break;
                            case "Sound": book.BookType = BookData.FileType.Sound; break;
                            case "StillImage": book.BookType = BookData.FileType.StillImage; break;
                            case "Text": // OK, this is normal
                                break;
                            default:
                                Log($"ERROR: Unknown book type {bookType} for {logname}");
                                break;
                        }
                        break;
                    // https://www.loc.gov/marc/relators/relaterm.html
                    case "marcrel:adp": // 
                    case "marcrel:aft": // author forward
                    case "marcrel:ann": // annotator
                    case "marcrel:arr": // arranger
                    case "marcrel:art": // 
                    case "marcrel:aui": // author of introduction
                    case "marcrel:aut": // author (not used?)
                    case "marcrel:clb": // collaborator
                    case "marcrel:cmm": // commentator
                    case "marcrel:cmp": // composer
                    case "marcrel:cnd": // 
                    case "marcrel:com": // compiler
                    case "marcrel:ctb": // contributor
                    case "marcrel:dub": // 
                    case "marcrel:edc": // editor of compilation
                    case "marcrel:edt": // editor 
                    case "marcrel:egr": // 
                    case "marcrel:ill": // illustrator
                    case "marcrel:lbt": // libretist
                    case "marcrel:oth": // other
                    case "marcrel:pbl": // publisher
                    case "marcrel:pht": // 
                    case "marcrel:prf": // performer
                    case "marcrel:prt": // printer
                    case "marcrel:res": // 
                    case "marcrel:trc": // 
                    case "marcrel:trl": // translator
                    case "marcrel:unk": // 
                        person = ExtractCreator(logname, value);
                        if (person != null) book.People.Add(person);
                        break;
                    case "pgterms:bookshelf":
                        break;
                    case "pgterms:downloads": // <... rdf:datatype="http://www.w3.org/2001/XMLSchema#integer">11</...>
                        break;

                    // almost always the LCCN number
                    case "pgterms:marc010": book.LCCN = value.InnerText; break;

                    // e.g. The Charles Dickens Edition for pg766.rdf
                    case "pgterms:marc250": book.PGEditionInfo = value.InnerText; break;

                    // <...>Houston: Advantage International, The PaperLess Readers Club, 1992</...>
                    case "pgterms:marc260": book.Imprint = value.InnerText; break;

                    // e.g. The Pony Rider Boys, number 1 for pg6067.rdf
                    case "pgterms:marc440": book.BookSeries = value.InnerText; break;

                    // e.g. EBook produced by David Starner and Heather Martino for pg12962.rdf
                    case "pgterms:marc508": book.PGProducedBy = value.InnerText; break;

                    // e.g. This ebook uses a beginning of the 20th century spelling. for pg17193.rdf
                    case "pgterms:marc546": book.PGNotes = value.InnerText; break;

                    case "pgterms:marc300": // with 8 diagrams
                    case "pgterms:marc520": // A fun and wonderfully illustrated version of 
                    case "pgterms:marc902": // http://www.gutenberg.org/dirs/8/7/8/8789/8789-h/images/titlepage.jpg 
                    case "pgterms:marc903": // http://www.gutenberg.org/files/22761/22761-page-images/cover.tif
                        // Not really unknown, just uninteresting to me: Log($"Unknown marc: {value.Name} == {value.InnerText} for {logname}");
                        break;

                    case "pgterms:marc020": // Mystery number: 0-397-00033-2
                        // Don't care: Log($"Unknown marc: {value.Name} == {value.InnerText} for {logname}");
                        break;

                    // Front cover e.g. http://www.gutenberg.org/files/3859/3859-h/images/cover.jpg
                    case "pgterms:marc901": break;

                    default:
                        Log($"XML: {value.Name} but expected e.g. dcterms:hasFormat or dcterms:language etc. for {logname}");
                        break;
                }
            }

            // Do a little validation
            // Correction: don't do this validation. Too many Gutenberg "books" in the catalog are just plain
            // errors. OTOH, they are o
            if (book.Issued != "None")
            {
                if (book.Files.Count == 0)
                {
                    Log($"XML: book has no files for {logname}");
                }
            }
            return book;
        }
        private static BookData[] BookDataArray = new BookData[10];
        private static int BookDataArrayIndex = -1;

        /// <summary>
        /// Reads in data form the xml file and potentially adds it to the database via a fancy merge.
        /// </summary>
        /// <param name="bookdb"></param>
        /// <param name="logname"></param>
        /// <param name="xmlData"></param>
        /// <param name="newBooks"></param>
        /// <returns></returns>
        private static int Read(BookDataContext bookdb, string logname, string xmlData, IList<BookData> newBooks, UpdateType updateType)
        {
            // This is a state machine, which means that some pretty important parts are just a little
            // bit buried. When a book is done, take a look at "pgterms:ebook" which has ExtractBook,
            // some fixup, and then saves the book in the database but only if it's valid.
            int retval = 0;
            var doc = new XmlDocument();
            doc.LoadXml(xmlData);
            foreach (var childNode in doc.ChildNodes) // there is just the one
            {
                var node = childNode as XmlNode;
                switch (node.Name)
                {
                    case "rdf:RDF":
                        foreach (var possibleBook in node.ChildNodes)
                        {
                            var bookNode = possibleBook as XmlNode;
                            if (bookNode == null) continue;
                            switch (bookNode.Name)
                            {
                                case "pgterms:ebook":
                                    var book = ExtractBook(logname, bookNode);

                                    //
                                    // Set the denorm data
                                    //
                                    book.DenormPrimaryAuthor = (book.BestAuthorDefaultIsNull ?? "-Unknown").ToLower();
                                    var idnumber = GutenbergFileWizard.GetBookIdNumber(book.BookId, -365);
                                    var dto = new DateTimeOffset(2000, 1, 1, 0, 0, 0, TimeSpan.Zero);
                                    dto = dto.AddMinutes(idnumber);
                                    book.DenormDownloadDate = dto.ToUnixTimeSeconds();

                                    // A book might be invalid. For example, Gutenberg includes
                                    // ebooks/0 in the RDF catalog even though it doesn't exist.
                                    // Books with no downloads aren't considered real books.
                                    if (book != null && book.Validate() == "") //"" is OK (yes, it's weird)
                                    {
                                        // If it already exists, we don't need to add it.
                                        // NOTE: At some point in the future we might want to see if the
                                        // record has changed at all. 

                                        // TODO: 
                                        // Especially if we read from a bookmark file from a computer with a more
                                        // advanced index, so our book data is actually pretty sketchy.
                                        // Sketchy == many fields are made up. The source should be 
                                        // BookSourceBookMarkFile = "From-bookmark-file:"; so that we know when we're 
                                        // in this situation.
                                        // See if source in the database is BookSourceBookMarkFile
                                        // in order to fix this.

                                        // Save an indicator of the books
                                        // The current index is always the last location
                                        // This is really just used for debugging.
                                        BookDataArrayIndex = (BookDataArrayIndex + 1) % BookDataArray.Length;
                                        BookDataArray[BookDataArrayIndex] = book;

                                        var dbbook = bookdb.Books.Find(book.BookId);

                                        // Actually save the book! (possibly with a fancy merge)
                                        // A fast update just adds new books; a full update will merge in data as needed.
                                        var existHandling = updateType == UpdateType.Full
                                            ? CommonQueries.ExistHandling.SmartCatalogOverride
                                            : CommonQueries.ExistHandling.CatalogOverrideFast;

                                        var nAdded = CommonQueries.BookAdd(bookdb, book, existHandling);
                                        if (nAdded > 0 && newBooks != null)
                                        {
                                            newBooks.Add(book);
                                        }
                                        retval += nAdded;
                                    }
                                    break;
                                case "cc:Work": // cc:Work rdf:about=""> 
                                                // <... rdf:resource="https://creativecommons.org/publicdomain/zero/1.0/"/>
                                    break;
                                case "rdf:Description":
                                    // <<... rdf:about="http://en.wikipedia.org/wiki/Katharine_Pyle">
                                    break;
                                default:
                                    Log($"XML: ebook: {bookNode.Name} but just expected pgterms:ebook for {logname}");
                                    break;
                            }
                        }
                        break;
                    case "xml": // <?xml version="1.0" encoding="utf-8"?>
                        break;

                    default:
                        Log($"XML: rdf:rdf: {node.Name} but just expected rdf:RDF for {logname}");
                        break;
                }
            }
            return retval;
        }
    }
}
