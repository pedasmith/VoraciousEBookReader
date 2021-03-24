using EpubSharp;
using SimpleEpubReader.EbookReader;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Encodings.Web;
using System.Threading.Tasks;

namespace SimpleEpubReader.FileWizards
{
    public static class TextWizard
    {
        public static EpubBookExt TextToEpub(string fileString)
        {
            fileString = HtmlEncoder.Default.Encode(fileString);
            fileString = @"<html>\n<head>\n</head>\n<body>\n<pre id='START_OF_FILE'>\n" + fileString + "</pre></body></html>";

            var epubBook = new EpubBookExt(null)
            {
                Resources = new EpubResources()
            };
            var file = new EpubTextFile()
            {
                TextContent = fileString,
                //FileName = "Contents.html",
                AbsolutePath = "./Contents.html",
                ContentType = EpubSharp.Format.EpubContentType.Xml,
                MimeType = "text/html",
                Content = System.Text.Encoding.UTF8.GetBytes(fileString),
            };

            epubBook.Resources.Html.Add(file);
            var bookChapter = new EpubChapter()
            {
                Title = "Entire Contents",
                //FileName = "Contents.html",
                AbsolutePath = "./Contents.html",
                HashLocation = "START_OF_FILE",
                //Anchor = "START_OF_FILE"
            };
            epubBook.TableOfContents.Add(bookChapter);
            epubBook.FixupHtmlOrdered();
            return epubBook;
        }
    }
}
