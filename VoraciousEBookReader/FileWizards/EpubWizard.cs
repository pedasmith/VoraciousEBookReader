using EpubSharp;
using SimpleEpubReader.Database;
using SimpleEpubReader.EbookReader;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Input.Inking.Analysis;

namespace SimpleEpubReader.FileWizards
{
    class EpubWizard
    {
        // All epub files start with PK\3\4 (because they are zip files).
        // If it's not that, then is must be a text or html file.
        public static bool IsEpub(byte[] fileContents)
        {
            if (fileContents.Length >= 4
                && fileContents[0] == 'P'
                && fileContents[1] == 'K'
                && fileContents[2] == 0x03
                && fileContents[3] == 0x04
                )
            {
                return true;
            }
            return false;
        }
        private static bool FindHtmlContainingIdHelper(EpubTextFile html, string str, string id)
        {
            if (html.FileName() == id)
            {
                // some books just work like this. The ID is the name of the html.
                return true;
            }
            if (id.EndsWith(".html"))
            {
                ; // don't allow these as real ids at all.
            }
            else if (HtmlStringIdIndexOf(str, id) >= 0)
            {
                return true;
            }
            else if (str.Contains(id))
            {
                ; // can't find it as an id, but is part of a string?
            }
            return false;
        }

        public static List<string> GetIdVariants(string idInput)
        {
            List<string> idList = new List<string>() { idInput };

            // FAIL: some book include multiple versions of the same input. In at least one case
            // large images are e.g. @public@vhost@g@gutenberg@html@files@98@98-h@images@0403.jpg
            // found images are e.g. @public@vhost@g@gutenberg@html@files@98@98-h@images@0403m.jpg
            // Try both versions. Always try the given version first because .. that makes more sense?
            if (idInput.EndsWith(".jpg") && !idInput.EndsWith("m.jpg"))
            {
                idList.Add(idInput.Replace(".jpg", "m.jpg"));
            }
            return idList;
        }

        public static (string value, int index, string filename, string foundId) FindHtmlContainingId(EpubBookExt EpubBook, List<string> idList, int preferredHtmlIndex)
        {
            foreach (var id in idList)
            {
                if (preferredHtmlIndex >= 0)
                {
                    var html = EpubBook.ResourcesHtmlOrdered[preferredHtmlIndex];
                    var str = System.Text.UTF8Encoding.UTF8.GetString(html.Content);
                    var found = FindHtmlContainingIdHelper(html, str, id);
                    if (found)
                    {
                        return (str, preferredHtmlIndex, html.FileName(), id);
                    }
                }

                var index = 0;
                foreach (var html in EpubBook.ResourcesHtmlOrdered)
                {
                    var str = System.Text.UTF8Encoding.UTF8.GetString(html.Content);
                    var found = FindHtmlContainingIdHelper(html, str, id);
                    if (found)
                    {
                        return (str, index, html.FileName(), id);
                    }
                    index++;
                }
            }
            if (idList[0] != "uiLog")
            {
                App.Error($"ERROR: unable to find html containing id={idList[0]} in the ebook");
            }
            return (null, -1, null, null);
        }

        /// <summary>
        /// Search for an id string (like @public@vhost@g@gutenberg@html@files@13882@13882-h@images@image-5.jpg) in HTML
        /// where the string is entirely surrounded by quotes (either single or double)
        /// AND the string doesn't start with href= because those are not ids
        /// 
        /// </summary>
        /// <param name="html"></param>
        /// <param name="id"></param>
        /// <returns>-1 means that the value wasn't found; otherwise it's the index of the location</returns>
        public static int HtmlStringIdIndexOf(string html, string id, bool allowHref=false)
        {
            var startidx = 0;
            while (startidx >= 0)
            {
                var idx = html.IndexOf(id, startidx);
                startidx = idx >= 0 ? idx + 1 : -1;

                if (idx >= 4)
                {
                    // minimum is id='<id>', so idx must be >= 4
                    int qpos = html.LastIndexOfAny(new char[] { '\'', '"' }, idx);
                    if (qpos < 0) continue; // what, no quote?
                    var qdistance = idx - qpos;
                    if (qdistance > 40) continue; // not sure what's going on, but it's not good. 40 is arbitrary.
                    var prechar = html[idx - 1];
                    const string allowedPre = "'\"/\\";
                    if (!allowedPre.Contains(prechar)) continue; // allow ../cover.jpg to match cover.jpg but don't allow 321.jpg to match 21.jpg.

                    char q = html[qpos]; // was html[idx - 1]; // is either ' or " for id='<id>' or id="id"
                    //FAIL: Planetary Nebula will have images with names like images/Cover.jpg. But the actual image has a src="../images/Cover.jpg"
                    var lastqpos = idx + id.Length;
                    var endq = lastqpos >= html.Length ? q : html[lastqpos];
                    var qisquote = q == '\'' || q == '"';
                    if (!qisquote || q != endq) continue;
                    // it's like '<id>' and not something weird. Now check for the type of id
                    // like id='<id>' src='<id>' are valid ids. Why isn't href="" a valid id?
                    var eqIndex = SkipWsBack(html, qpos - 1); // quote is at idx-1
                    if (eqIndex < 0) continue;
                    if (html[eqIndex] != '=')
                    {
                        App.Error($"ERROR in HtmlStringContainsId at index {eqIndex} expected equals = ");
                        continue;
                    }
                    var tagIndex = SkipWsBack(html, eqIndex - 1);
                    if (tagIndex < 0) continue;
                    if (tagIndex < 2)
                    {
                        App.Error($"ERROR in HtmlStringContainsId at index {eqIndex} expected enough room for a tag {tagIndex} ");
                        continue;
                    }
                    // Get the node name e.g. "<p" or "<span" etc.
                    var ltIndex = html.LastIndexOf('<', tagIndex);
                    var spaceIndex = ltIndex < 0 ? 0 : html.IndexOf(' ', ltIndex);
                    var nodeLen = spaceIndex - ltIndex;
                    var nodeType = ltIndex < 0 || spaceIndex < 0 || nodeLen > 10 ? "<unknown" : html.Substring(ltIndex, nodeLen);

                    // Expected src='id' or id='id' and nothing else. Just check the last two chars.
                    var partial = html.Substring(tagIndex - 1, 2);
                    switch (partial)
                    {
                        case "id": // success!
                        case "rc": // part of src :-)
                            return idx;
                        case "ef": // part of href=
                            // string might have src='id' in one place and id='id' later on.
                            if (nodeType == "<image")
                            {
                                // BAEN books use <image  src="cover.jpeg" /> and we need to find it.
                                return idx;
                            }
                            else
                            {
                                // Logger.Log($"NOTE: EpubWizard: Find({id}) at pos {ltIndex} nodeType={nodeType} but reject because we don't like src=");
                                if (allowHref)
                                {
                                    return idx;
                                }
                            }
                            continue;
                        case "ss":
                            // Part of class="id" -- is seen in e.g. Fireless Locomotive
                            break;
                        default:
                            App.Error($"ERROR in HtmlStringContainsId at index {eqIndex} expected id or (s)rc not {partial} ");
                            break;
                    }
                }
            }

            return -1;
        }

        /// <summary>
        /// Given a string id = 'test' where the first quote is at index 5,
        /// and passing in startIndex (5-1), return the first index that isn't 
        /// a whitespace (space tab). Can return -1 if there are none.
        /// So for id = 'test' and input 4, returns 3, the index of '='
        /// </summary>
        /// <param name="html"></param>
        /// <param name="startIndex"></param>
        /// <returns></returns>
        public static int SkipWsBack(string html, int startIndex)
        {
            for (int i = startIndex; i >= 0; i--)
            {
                if (!char.IsWhiteSpace(html[i]))
                {
                    return i;
                }
            }
            return -1; // was not found.
        }
        public static int Test_HtmlStringIdIndexOf()
        {
            int nerror = 0;

            nerror += Test_HtmlStringIdIndexOf_One(" id='test' ", "test", true);
            nerror += Test_HtmlStringIdIndexOf_One(" id=\"test\" ", "test", true);
            nerror += Test_HtmlStringIdIndexOf_One(" id = 'test' ", "test", true);

            nerror += Test_HtmlStringIdIndexOf_One(" src='test' ", "test", true);

            nerror += Test_HtmlStringIdIndexOf_One(" href='test' ", "test", false);
            nerror += Test_HtmlStringIdIndexOf_One(" href='test' id='test' ", "test", true);

            nerror += Test_HtmlStringIdIndexOf_One(" id='TEST' ", "test", false);
            nerror += Test_HtmlStringIdIndexOf_One(" id='prefix\\test' ", "test", true);
            nerror += Test_HtmlStringIdIndexOf_One(" id='prefix/test' ", "test", true);
            nerror += Test_HtmlStringIdIndexOf_One(" id='prefixtest' ", "test", false);
            nerror += Test_HtmlStringIdIndexOf_One(" id='testno' ", "test", false);
            nerror += Test_HtmlStringIdIndexOf_One(" id=' test' ", "test", false);
            nerror += Test_HtmlStringIdIndexOf_One(" id='test ' ", "test", false);

            return nerror;
        }

        private static int Test_HtmlStringIdIndexOf_One(string html, string id, bool expected)
        {
            int nerror = 0;
            var actual = EpubWizard.HtmlStringIdIndexOf(html, id) >= 0;
            if (actual != expected)
            {
                nerror++;
                App.Error($"ERROR: HtmlStringContainsId({html}, {id}) expected {expected} but got {actual}");
            }
            return nerror;
        }
        public static string FindHtmlByIndex(EpubBookExt epubBook, int searchIndex)
        {
            var index = 0;
            byte[] lastcontent = null;
            foreach (var html in epubBook.ResourcesHtmlOrdered)
            {
                lastcontent = html.Content;
                if (index == searchIndex)
                {
                    var str = System.Text.UTF8Encoding.UTF8.GetString(html.Content);
                    return str;
                }
                index++;
            }
            App.Error($"MainEpubReader: FindHtmlByIndex: can't find html index {searchIndex}");
            if (lastcontent != null)
            {
                return System.Text.UTF8Encoding.UTF8.GetString(lastcontent);
            }
            return null;
        }

        public static EpubFile GetInternalFileByName(EpubBookExt epubBook, string requestFileName)
        {
            var requestWithSlash = "/" + requestFileName;
            foreach (var imageFile in epubBook.Resources.Images)
            {
                var fname = imageFile.FileName();
                // FAIL: BAEN 2013 short stories. The cover is requested as "cover.jpeg" from a top-level file. It's listed
                // in the Images as "/cover.jpeg" which then doesn't match anything.
                if (fname == requestFileName || fname == requestWithSlash || imageFile.Href == requestFileName)
                {
                    return imageFile;
                }
            }
            foreach (var otherFile in epubBook.Resources.Css)
            {
                var fname = otherFile.FileName();
                if (fname == requestFileName || fname == requestWithSlash || otherFile.Href == requestFileName)
                {
                    return otherFile; ;
                }
            }
            // FAIL: e.g. UN epub from https://www.unescap.org/publications/accessibility-all-good-practices-accessibility-asia-and-pacific-promote-disability
            // the epub looks for ../Text/FrontCover.html but the index include Text/FrontCover.html
            if (requestFileName.StartsWith("../"))
            {
                return GetInternalFileByName(epubBook, requestFileName.Substring("../".Length));
            }

            App.Error($"MainEpubReader: GetInternalFileByName({requestFileName}) can't find requested file");
            return null;
        }

        /// <summary>
        /// Trivial routine to pull out the id of the first chapter in the book; this is then used
        /// to navigate to the chapter.
        /// </summary>
        /// <param name="chapters"></param>
        /// <returns></returns>
        public static EpubChapter GetFirstChapter(IList<EpubChapter> chapters)
        {
            if (chapters == null) return null;
            foreach (var chapter in chapters)
            {
                return chapter;
            }
            return null;
        }


        /// <summary>
        /// Returns the id of the chapter that contains an anchor. This is used, for example, when
        /// selecting an image and wanting to shift the chapter display.
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public static string GetChapterContainingId(EpubBookExt epubBook, string id, int preferredHtmlIndex)
        {
            if (string.IsNullOrEmpty(id))
            {
                // just return the first chapter
                return GetFirstChapter(epubBook.TableOfContents).HashLocation ?? ""; // ?? "";
            }

            // Step one: find the html with the id
            var idList = EpubWizard.GetIdVariants(id);
            var (foundHtml, foundIndex, foundHtmlName, foundId) = EpubWizard.FindHtmlContainingId(epubBook, idList, preferredHtmlIndex);
            if (foundHtml == null)
            {
                if (id != "uiLog")
                {
                    // uiLog isn't always findable for ... reasons
                    App.Error($"IMPOSSIBLE ERROR: completely unable to find id {id} ");
                }
                return null;
            }

            var pos = EpubWizard.HtmlStringIdIndexOf(foundHtml, id);
            string closest = null;

            FindClosestAnchorHelper(foundHtml, pos, epubBook.TableOfContents, 0, 3, ref closest);

            // Fixup #1: try the TOC directly
            // FAIL: the order of the fixups is really important. BAEN 2013 short stories doesn't include chapter id values
            // and they have one story with nested sections AND they have duplicate ID values (calibre_pb_1 etc.) AND each story is
            // in its own HTML page. If you select the first story after the story with sub-stories, then we really want to find 
            // the story by chapter and don't want the previous story.
            if (closest == null)
            {
                // FAIL: All of me a small town romance: the chapters don't have any anchors at all.
                // Instead of looking for the chapter by id, look for it based on a matching
                // filename. If it matches, return the Filename as the id.
                // No, it's not quite an id, but it is close enough to work :-)

                foreach (var chapter in epubBook.TableOfContents)
                {
                    // The chapters here might have names like ../TextFiles/chapter.xhml
                    // while we're looking for plain TextFiles/chapter.xhml
                    // We have to return the raw chapter name because we'll use it later on.
                    // // // BUG: using the wrong name!!!  var htmlFileNameVariants = MakeHtmlFileNameVariants(foundHtmlName);
                    var htmlFileNameVariants = MakeHtmlFileNameVariants(chapter.FileName());
                    foreach (var fname in htmlFileNameVariants)
                    {
                        if (fname == foundHtmlName)
                        {
                            closest = chapter.FileName();
                        }
                    }
                }
            }

            // Fixup #2: maybe try the previous HTML
            if (closest == null)
            {
                // Didn't find one; that's probably because we're in a gap. We need to find the same thing for the 
                // previous chapter, but with closest set to the end of the html.
                if (foundIndex > 0)
                {
                    foundHtml = FindHtmlByIndex(epubBook, foundIndex - 1);
                    FindClosestAnchorHelper(foundHtml, int.MaxValue, epubBook.TableOfContents, 0, 3, ref closest);
                }
                // First html, and still can't find anything? Give up, we're not going to find anything.
            }

            // All the fixups failed
            if (closest == null)
            {
                App.Error($"ERROR: when asked for matching chapter, can't find it for {id}. Possibly the chapters have no anchors.");
            }

            return closest;
        }

        public static string FindClosestAnchorHelper(string foundHtml, int maxPosition, IList<EpubChapter> chapterList, int currDepth, int maxDepth, ref string closest)
        {
            foreach (var chapter in chapterList)
            {
                // FAIL: for many ebooks, the anchors are complex values that unlikely to be found in
                // any book. But for Fire at Red Lake, there's a set of transcriber's notes in their own
                // chapter. The chapter anchor for these is "tn". No surprise, there's plenty of words
                // that includes a 'tn' and therefore the transcribers notes are preferentially found.
                var chpos = EpubWizard.HtmlStringIdIndexOf(foundHtml, chapter.HashLocation ?? "***NO ANCHOR***"); //was .Anchor
                if (chpos >= 0 && chpos <= maxPosition)
                {
                    // Found a possible match. The same html might be referred to by multiple chapters, 
                    // so keep on going; there might be a better match.
                    closest = chapter.HashLocation; //  .Anchor;
                }
                if ((currDepth + 1 < maxDepth) && (chapter.SubChapters.Count > 0))
                {
                    FindClosestAnchorHelper(foundHtml, maxPosition, chapter.SubChapters, currDepth + 1, maxDepth, ref closest);
                }
            }
            return closest;
        }


        private static string[] MakeHtmlFileNameVariants(string htmlFileName)
        {
            int n = 1;
            if (htmlFileName.StartsWith("../")) n++;
            if (htmlFileName.Contains("%")) n++;

            string[] retval = new string[n];
            n = 0;
            retval[n++] = htmlFileName;
            if (htmlFileName.StartsWith("../")) retval[n++] = htmlFileName.Substring(3);
            if (htmlFileName.Contains("%")) retval[n++] = Uri.UnescapeDataString(htmlFileName);
            return retval;
        }
        private static bool FileNameMatches(EpubFile file, string[] htmlFileName)
        {
            for (int i = 0; i < htmlFileName.Length; i++)
            {
                if (file.FileName() == htmlFileName[i])
                {
                    return true;
                }
            }
            return false;
        }

        public static (string value, int index, string filename) FindHtmlContainingHtmlFileName(EpubBookExt epubBook, string htmlFileName)
        {
            var htmlFileNameVariants = MakeHtmlFileNameVariants(htmlFileName);
            var index = 0;
            foreach (var html in epubBook.ResourcesHtmlOrdered)
            {
                // FAIL: Might be encoded: we get file%20space.xhml but need to find file<sp>space.xhml
                // FAIL: e.g. UN epub from https://www.unescap.org/publications/accessibility-all-good-practices-accessibility-asia-and-pacific-promote-disability
                // the epub looks for ../Text/FrontCover.html but the index include Text/FrontCover.html
                if (FileNameMatches(html, htmlFileNameVariants))
                {
                    var str = System.Text.UTF8Encoding.UTF8.GetString(html.Content);
                    return (str, index, html.FileName());
                }
                index++;
            }

            App.Error($"MainEpubReader: FindHtmlContainingHtmlFileName: can't find {htmlFileName}");
            return (null, -1, null);
        }

        /// <summary>
        /// Returns a count of the number of Html sections in the ebook.
        /// </summary>
        /// <returns></returns>
        public static int NHtmlIndex(EpubBookExt epubBook)
        {
            var index = 0;
            foreach (var html in epubBook.ResourcesHtmlOrdered)
            {
                index++;
            }
            return index;
        }



    }
}
