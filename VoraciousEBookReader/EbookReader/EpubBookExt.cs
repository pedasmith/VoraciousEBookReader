using EpubSharp;
using System.Collections.Generic;
using System.Net;
using Windows.Media.Core;

namespace SimpleEpubReader.EbookReader
{
    public class EpubBookExt
    {
        public EpubBookExt (EpubBook originalBook)
        {
            inner = originalBook;
            if (inner == null)
            {
                TableOfContents = new List<EpubChapter>();
            }
        }
        private EpubBook inner;

        /// <summary>
        /// The original EpubBook has only a getter for the Resources value, but I need more than that.
        /// 
        /// </summary>
        EpubResources _ResourcesExt = null;
        public EpubResources Resources
        {
            get { return _ResourcesExt ?? inner.Resources; }
            set { _ResourcesExt = value; }
        }

        public IList<EpubTextFile> ResourcesHtmlOrdered
        { 
            get
            {
                // This is often filled in
                var list = inner.SpecialResources.HtmlInReadingOrder;
                if (list != null && list.Count > 0)
                {
                    return inner.SpecialResources.HtmlInReadingOrder;
                }

                if (_ResourcesHtmlOrdered == null)
                {
                    //FixupHtmlOrdered();
                    // This exists, so why no use it directly rather than attempt to build
                    // up the right list of files? (Or is that a bad mistake?)
                    _ResourcesHtmlOrdered = inner.SpecialResources.HtmlInReadingOrder;
                }
                return _ResourcesHtmlOrdered;
            }
        }
        // FAIL: Intro to Planetary Nebula is the first book where the set of HTML files in the <manifest> doesn't match the
        // order of files in the <spine toc="ncx">. The order is almost the same, but almost only counts in horseshoes and
        // hand grenades. I have to skip through the spine and for each item, find it in the Resources.Html value.
        private IList<EpubTextFile> _ResourcesHtmlOrdered = null;

        /// <summary>
        /// Creates an ordered set of HTML files for the chapters in the book. For many books this will be the files in the manifest
        /// BUT for books like Intro to Planetary Nebula, it's a different order and doesn't have all of the files in the manifest.
        /// </summary>
        public void FixupHtmlOrdered()
        {
            _ResourcesHtmlOrdered = new List<EpubTextFile>();
            foreach (var chapter in TableOfContents)
            {
                FixupHtmlOrderedAdd(chapter);
            }

            // FAIL: Programming In Go includes a cover, titlepage and copyrights page, none of which are in
            // the table of contents. Walk through all of the HTML pages, looking for pages that haven't been added
            // yet and put them in.
            int addIndex = 0; // if in front, the index to add to; if negative then add to back.
            foreach (var html in Resources.Html)
            {
                if (IsInFixup(html.AbsolutePath))
                {
                    addIndex = -1;
                }
                else if (html.AbsolutePath == inner.Format.Paths.NavAbsolutePath)
                {
                    // FAIL: we have to add in all of the items in the manifest BUT we should ignore the
                    // (Programming in Go) the nav.xhtml file because it's useless. Luckily that file's path
                    // is marked in inner.Format.Paths.NavAbsolutePath.
                    ; // Skip over the useless nax.xhtml file
                }
                else
                {
                    if (Logger.LogExtraTiming)
                    {
                        var loc = addIndex >= 0 ? addIndex.ToString() : "AT END";
                        Logger.Log($"EpubBookExt: Adding html {html.Href} insertIndex={loc}");
                    }
                    if (addIndex < 0)
                    {
                        _ResourcesHtmlOrdered.Add(html);
                    }
                    else
                    {
                        _ResourcesHtmlOrdered.Insert(addIndex, html);
                        addIndex++;
                    }
                }
            }
        }
        private void FixupHtmlOrderedAdd(EpubChapter chapter)
        {
            if (chapter == null) return;

            if (!IsInFixup (chapter.AbsolutePath))
            {
                var htmlForChapter = FindHtml(chapter);
                if (htmlForChapter == null)
                {
                    App.Error($"ERROR: unable to find match HTML for chapter {chapter.Title}");
                }
                else
                {
                    _ResourcesHtmlOrdered.Add(htmlForChapter);
                }
            }

            if (chapter.SubChapters != null)
            {
                foreach (var subChapter in chapter.SubChapters)
                {
                    FixupHtmlOrderedAdd(subChapter);
                }
            }
        }

        private bool IsInFixup(string absolutePath)
        {
            foreach (var html in _ResourcesHtmlOrdered)
            {
                if (html.AbsolutePath == absolutePath)
                {
                    return true;
                }
            }
            return false;
        }

        private EpubTextFile FindHtml(EpubChapter chapter)
        {
            foreach (var html in Resources.Html)
            {
                if (html.AbsolutePath == chapter.AbsolutePath)
                {
                    return html;
                }
            }
            return null;
        }

        IList<EpubChapter> _TableOfContentsExt = null;
        public IList<EpubChapter> TableOfContents
        {
            get { return _TableOfContentsExt ?? inner?.TableOfContents; }
            set { _TableOfContentsExt = value; }
        }

        // public new EpubFormat Format { get { return inner.Format; } set { throw; }
#if NEVER_EVER_DEFINED
        public EpubFormat Format { get; }
        public string Title { get; }
        public IEnumerable<string> Authors { get; }
        public EpubResources Resources { get; }
        public EpubSpecialResources SpecialResources { get; }
        public byte[] CoverImage { get; }

        public string ToPlainText();
#endif

    }

    public static class EpubFileExtensionMethods
    {
        public static string FileName(this EpubChapter epub)
        {
            var str = epub.AbsolutePath;
            var lastIndex = str.LastIndexOf('/');
            if (lastIndex < 0) return str;
            var retval = str.Substring(lastIndex + 1);
            return retval;
        }
        public static string FileName(this EpubFile epub)
        {
            var str = epub.AbsolutePath;
            var lastIndex = str.LastIndexOf('/');
            if (lastIndex < 0) return str;
            var retval = str.Substring(lastIndex + 1);
            return retval;
        }
        public static string FileName(this EpubByteFile epub)
        {
            var str = epub.AbsolutePath;
            var lastIndex = str.LastIndexOf('/');
            if (lastIndex < 0) return str;
            var retval = str.Substring(lastIndex + 1);
            return retval;
        }
        public static string FileName(this EpubTextFile epub)
        {
            var str = epub.AbsolutePath;
            var lastIndex = str.LastIndexOf('/');
            if (lastIndex < 0) return str;
            var retval = str.Substring(lastIndex + 1);
            return retval;
        }
    }
}
