using EpubSharp;
using SimpleEpubReader.EbookReader;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using static SimpleEpubReader.Controls.Navigator;

// The User Control item template is documented at https://go.microsoft.com/fwlink/?LinkId=234236

namespace SimpleEpubReader.Controls
{
    public interface ISetChapters
    {
        void SetChapters(EpubBookExt book, IList<EpubChapter> chapters);
    }

    /// <summary>
    /// We get in EpubChapter values, but internally we need a little bit more data.
    /// Each incoming EpubChapter is converted to an EpubChapterData value.
    /// </summary>
    public class EpubChapterData
    {
        public EpubChapterData(EpubChapter source, int indent)
        {
            Title = source.Title.Trim();
            FileName = source.FileName();
            Anchor = source.HashLocation; // Was renamed from .Anchor; 5cdfc156747a38f26e008950e8a7043b1d45b4c7 2018-04-18
            Indent = indent;
        }

        public string Title { get; set; }
        public string FileName { get; set; }
        public string Anchor { get; set; }
        public int Indent { get; set; }

        public static BookLocation FromChapter(EpubChapter chapter)
        {
            return new BookLocation()
            {
                Location = chapter.HashLocation,  //.Anchor, -- hashlocation in this context is the part of the url after the hash like example.com/page#section31
                HtmlFileName = chapter.FileName(),
            };
        }
        public static BookLocation FromChapter(EpubChapterData chapter)
        {
            return new BookLocation()
            {
                Location = chapter.Anchor,
                HtmlFileName = chapter.FileName,
            };
        }

        public override string ToString()
        {
            return $"{Title} Anchor={Anchor} Indent={Indent} FileName={FileName}";
        }
    }

    public class EpubChapterDataTemplateSelector : DataTemplateSelector
    {
        public DataTemplate Toc1 { get; set; }
        public DataTemplate Toc2 { get; set; }
        public DataTemplate Toc3 { get; set; }

        protected override DataTemplate SelectTemplateCore(object item)
        {
            switch ((item as EpubChapterData)?.Indent ?? -1)
            {
                case -1: 
                    return Toc3;
                case 1: return Toc1;
                case 2: return Toc2;
                case 3: return Toc3;
            }
            return Toc3;
        }
    }

    public sealed partial class ChapterDisplay : UserControl, INavigateTo, ISetChapters
    {
        // We always track which one we are
        const NavigateControlId ControlId = NavigateControlId.ChapterDisplay;
        public ChapterDisplay()
        {
            this.DataContext = this;
            this.InitializeComponent();
        }

        public ObservableCollection<EpubChapterData> Chapters { get; } = new ObservableCollection<EpubChapterData>(); 
        private EpubBookExt Book { get; set; }
        public void SetChapters(EpubBookExt book, IList<EpubChapter> chapters)
        {
            Book = book;
            Chapters.Clear();
            SetChaptersHelper(chapters, 1);
        }

        const int MAX_CHAPTER_DEPTH = 3;
        /// <summary>
        /// Proper recusion for adding chapters.
        /// </summary>
        private void SetChaptersHelper(IList<EpubChapter> chapters, int level)
        {
            if (chapters == null || chapters.Count == 0)
            {
                return; // not an error, just a fact. If there are no chapters, don't add them.
            }


            foreach (var chapter in chapters)
            {
                Chapters.Add(new EpubChapterData(chapter, level));
                if (chapter.SubChapters != null && chapter.SubChapters.Count > 0)
                {
                    if (level >= MAX_CHAPTER_DEPTH)
                    {
                        Logger.Log($"ChapterDisplay:SetChapter: too many levels under chapter {chapter.Title}");
                    }
                    else
                    {
                        SetChaptersHelper(chapter.SubChapters, level + 1);
                    }
                }
            }
        }

        /// <summary>
        /// Called when the user has navigated to somewhere in the book. The chapter display
        /// tries to sync itself to the value. The chapter display depends on the caller being
        /// fully initialized first!
        /// </summary>
        /// <param name="sourceId"></param>
        /// <param name="location"></param>
        public async void NavigateTo(NavigateControlId sourceId, BookLocation location)
        {
            string chapterid = "";
            var nav = Navigator.Get();
            if (!double.IsNaN(location.ScrollPercent))
            {
                chapterid = await nav.MainBookHandler.GetChapterBeforePercentAsync (location);
            }
            else
            {
                chapterid = nav.MainBookHandler.GetChapterContainingId(location.Location, location.HtmlIndex);
            }
            EpubChapterData foundChapter = null;

            if (foundChapter == null && location.HtmlIndex >= 0)
            {
                var html = Book.ResourcesHtmlOrdered[location.HtmlIndex];
                foreach (var chapter in Chapters)
                {
                    // FAIL: Intro to Planetary Nebulae the location is html 8, id tit1 which is shared by multiple chapters.
                    if (html.Href.EndsWith(chapter.FileName) && chapter.Anchor == chapterid)
                    {
                        foundChapter = chapter;
                        break;
                    }
                }
            }

            // Most common: there's an id, and it matches a single chapter.
            if (foundChapter == null)
            {
                foreach (var chapter in Chapters)
                {
                    if (chapter.Anchor == chapterid || chapter.FileName == chapterid)
                    {
                        foundChapter = chapter;
                        break;
                    }
                }
            }

            if (foundChapter == null && location.HtmlIndex >= 0)
            {
                var html = Book.ResourcesHtmlOrdered[location.HtmlIndex];
                foreach (var chapter in Chapters)
                {
                    // FAIL: Intro to Planetary Nebulae the location is html 8, id tit1 which is shared by multiple chapters.
                    if (html.Href.EndsWith(chapter.FileName))
                    {
                        foundChapter = chapter;
                        break;
                    }
                }
            }

            // Worse case scenario, but it's better to display something
            if (foundChapter == null)
            {
                foreach (var chapter in Chapters)
                {
                    if (string.IsNullOrEmpty(chapterid))
                    {
                        App.Error($"ChapterDisplay:Navigate({location}) was asked to find an empty chapter");
                        foundChapter = chapter;
                        break;
                    }
                }
            }

            if (foundChapter == null)
            {
                // Truly desperate.
                if (Chapters.Count > 0)
                {
                    App.Error($"ChapterDisplay:Navigate({location}) completely failed");
                    foundChapter = Chapters[0];
                }
                else
                {
                    App.Error($"ChapterDisplay:Navigate({location}) last ditch completely failed -- no chapters at all!");
                }
            }

            if (foundChapter != null)
            {
                // Select this one
                uiChapterList.SelectedItem = foundChapter;
                uiChapterList.ScrollIntoView(foundChapter);
                return; // all done!
            }
        }

        private void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            //We just do taps now -- working via selection has two different problems:
            //1. selection because of some other control's navigation would trigger this callback, 
            //     resulting in user navigation loops
            //2. Tappingn on an already-selected chapter didn't reselect even though we want it to.

            //if (e.AddedItems.Count != 1) return;
            //EpubChapter chapter = e.AddedItems[0] as EpubChapter;
        }

        private void OnSelectionTapped(object sender, TappedRoutedEventArgs e)
        {
            var chapter = (sender as FrameworkElement).DataContext as EpubChapterData;
            UserDidNavigationRequest(chapter);
        }

        private void UserDidNavigationRequest(EpubChapterData chapter)
        {
            if (chapter == null) return;
            var nav = Navigator.Get();
            BookLocation location = null;
            if (chapter == Chapters[0])
            {
                // First one is special: we always go to the very start of the book, including stuff that's not in
                // the classic table of contents. For example, Gutenberg Campfire Girls Station Island has a cover that's
                // not in the TOC but which is in the inner.SpecialResources.HtmlInReadingOrder and which is placed first.
                // Additionally, that book also has the weird thing that the ID for the first section (in the TOC) is
                // near the end of the section, so when you click on it, it scrolls to there, but then the next TOC entry
                // (chapter 1) is visible near the top, and so the selection jumps to there.
                location = new BookLocation(0, 0); // first HTML page, at the very top.
            }
            else
            {
                location = EpubChapterData.FromChapter(chapter);
            }
            nav.UserNavigatedTo(ControlId, location);
        }
    }
}
