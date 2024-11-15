using EpubSharp;
using SimpleEpubReader.Database;
using SimpleEpubReader.UwpClasses;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace SimpleEpubReader.FileWizards
{
    /// <summary>
    /// Used to get data about ebooks. Fill in the FilePath and then call the appropriate
    /// Wizard.GetData(data) routine to learn info about the book. Most critically, will fill
    /// in the BookId that should match the BookId in the catalog database.
    /// </summary>
    public class WizardData
    {
        public string FilePath { get; set; }
        public string FileName { get; set; }

        public void ClearGetData()
        {
            BookId = null;
            BD = null;
        }
        public string BookId { get; set; }
        public BookData BD { get; set; }
    }
    public static class GutenbergFileWizard
    {
        public static int GetBookIdNumber (string bookId, int defaultValue)
        {
            // gutenberg uses ids like ebooks/NNNN
            if (bookId.Length < 2) return defaultValue;
            var slashidx = bookId.IndexOf('/');
            if (slashidx < 0) return defaultValue;
            var rest = bookId.Substring(slashidx + 1);
            if (rest.Length < 1) return defaultValue;
            int id;
            bool ok = Int32.TryParse(rest, out id);
            if (!ok) return defaultValue;
            return id;
        }

#if FULL_EREADER
        public static async Task<WizardData> GetDataAsync(WizardData data, bool getBookData = false)
        {
            if (string.IsNullOrWhiteSpace(data.FilePath)) return data;
            data.ClearGetData(); // clear BookId
            if (getBookData)
            {
                data.BD = new BookData();
            }

            var fileContents = await FileMethods.ReadBytesAsync(data.FilePath);
            // Read as stream to make debugging a little easier.
            Encoding encoding = Encoding.UTF8;
            var stream = new MemoryStream(fileContents);
            var book = EpubReader.Read(stream, false, encoding);

            var title = string.IsNullOrWhiteSpace(book.Title) ? data.FileName.Replace(".epub", "") : book.Title;

            if (data.BD != null)
            {
                data.BD.BookType = BookData.FileType.Text;
                data.BD.Title = title;
                if (data.BD.People == null) data.BD.People = new ObservableCollection<Person>();
                foreach (var author in book.Authors)
                {
                    data.BD.People.Add(new Person(author, Person.Relator.author));
                }
            }
            var xmlstring = book.SpecialResources.Opf.TextContent;
            if (string.IsNullOrWhiteSpace(xmlstring)) return data;

            if (xmlstring.Contains ("<!DOCTYPE html>"))
            {
                // The "Bitcoints for Dummies" problem -- it includes a <!DOCTYPE html> line
                // which some online checkers say makes the file invalid.
                xmlstring = xmlstring.Replace("<!DOCTYPE html>", "");
            }

            var doc = new Windows.Data.Xml.Dom.XmlDocument();
            doc.LoadXml(xmlstring);
            var metalist = doc.GetElementsByTagName("metadata");
            foreach (var metadata in metalist)
            {
                foreach (var child in metadata.ChildNodes)
                {
                    switch (child.NodeName)
                    {
                        case "dc:identifier":
                            {
                                var id = child.InnerText;
                                const string PREFIX = "http://www.gutenberg.org/";
                                if (id.StartsWith(PREFIX))
                                {
                                    // What a surprise. The RDF has an ID, and the data here has an Id.
                                    // And they aren't the same exact string. 
                                    // Generate the correct id string.
                                    id = id.Substring(PREFIX.Length);
                                    if (!id.StartsWith("ebooks/"))
                                    {
                                        id = $"ebooks/{id}";
                                    }
                                }
                                var isId = false;
                                var idAttribute = child.Attributes.GetNamedItem("id");
                                if (idAttribute != null && idAttribute.InnerText == "id") isId = true;

                                // First time through, always set. After that, only set if it's marked as "id".
                                // This is requires by Archive.org books which have multiple identifiers!
                                // e.g. <dc:identifier id="id">atavaracraigkenn00reev</dc:identifier>
                                // and <dc:identifier>Access URL: http://archive.org/details/atavaracraigkenn00reev</dc:identifier>
                                if (string.IsNullOrEmpty(data.BookId) || isId)
                                {
                                    data.BookId = id;
                                    if (data.BD != null) data.BD.BookId = id;
                                }
                            }
                            break;
                        case "dc:language":
                            if (data.BD != null)
                            {
                                data.BD.Language = FixupLanguage (child.InnerText); // APRESS can have this as En instead of en
                            }
                            break;
                        case "dc:publisher":
                            if (data.BD != null)
                            {
                                var text = child.InnerText;
                                data.BD.People.Add(new Person(text, Person.Relator.publisher));
                            }
                            break;
                    }
                }
            }
            return data;
        }
#endif
        private static string FixupLanguage(string input)
        {
            input = input.ToLower();
            var list = input.Split(new char[] { '-' }, 2); // e.g. convert en-us into plain en. It not perfect...
            var retval = list[0];
            return retval;
        }
    }
}
