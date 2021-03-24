using EpubSharp;
using SimpleEpubReader.Controls;
using SimpleEpubReader.Database;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SimpleEpubReader.EbookReader
{
    // not a useful warning message.
#pragma warning disable IDE1006
    public interface BookHandler
    {
        EpubFile GetImageByName(string imageName);
        string GetChapterContainingId(string id, int preferredHtmlIndex);
        Task<string> GetChapterBeforePercentAsync(BookLocation location);
        Task DisplayBook(BookData book, BookLocation location = null);
        Task SetFontAndSizeAsync(string font, string size); // sie is e.g. "12pt"
    }
    public interface SimpleBookHandler
    {
        Task DisplayBook(BookData book, BookLocation location);
    }
}
