using Microsoft.EntityFrameworkCore;
using SimpleEpubReader.Database;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SimpleEpubReader.Searching
{
    static public class AllBookSearch
    {
        public const int MaxMatch = 300;

        [Flags]
        enum LinqIncludes
        {
            None = 0,
            Notes = 0x01, Review = 0x02, DownloadData = 0x04, NavigationData = 0x08,
            People = 0x10,
            LanguageExact = 0x20, LanguageExactOrNull = 0x40,
            Files = 0x80,

            UserData = 0x0F, // don't include anything not in the gutenberg catalog
            LanguagesFlags = 0x60, // all of the language flags; generally only do one!
        };

        /// <summary>
        /// 
        /// </summary>
        /// <param name="bookdb"></param>
        /// <param name="searchScope">One of Catalog, Pick, Reading, Downloaded, Finished, CopiedToEBookReader</param>
        /// <param name="search">search query like title:voyage sea</param>
        /// <param name="language">en or *</param>
        /// <param name="sortBy">One of author, title, date_download_asc, date_download_desc</param>
        /// <param name="andMore"></param>
        /// <returns></returns>
        static public List<BookData> SearchInternal(BookDataContext bookdb, string searchScope, string search, string language, string sortBy, out bool andMore)
        {
            // Query list is for the part of the query that can be done in the database.
            // This includes picking what data is returned, simple selection based on whether
            // a book is downloaded or not, and sorting.
            IQueryable<BookData> queryList = bookdb.Books;

            // Enumerable list is after getting a superset of books from the database. It will 
            // reduce the number of books based on the actual search query.
            IEnumerable<BookData> enumerableList = null;

            // Final list of books to return.
            var resultList = new List<BookData>();

            //languages: cases are "*", "en" or anything else.
            // * get all language; "en" get books in english OR with no language, anything else requires that language.

            LinqIncludes includes = LinqIncludes.None;
            ISearch searchOperations = string.IsNullOrWhiteSpace(search) ? null : SearchParser.Parse(search);

            HashSet<string> idlist = searchOperations != null ? CommonQueries.BookSearchs(searchOperations) : null;
            const string DefaultLanguage = "en";

            bool mustIncludeEpub = false;

            if (language == "*")
            {
                ; // nothing special to restrict the languages
            }
            else if (language == DefaultLanguage) // most books that aren't marked are english. 
            {
                includes |= LinqIncludes.LanguageExactOrNull;
            }
            else
            {
                includes |= LinqIncludes.LanguageExact;
            }


            switch (searchScope)
            {
                default:
                    App.Error($"Unknown search scope {searchScope}");
                    includes |= LinqIncludes.UserData | LinqIncludes.People;
                    includes &= ~LinqIncludes.LanguagesFlags;
                    break;

                case "Catalog":
                    includes |= LinqIncludes.UserData | LinqIncludes.People;
                    includes &= ~LinqIncludes.LanguagesFlags;

                    break;
                case "PickToDownload": // pick book to download. Have to be extra good with the query so it's fast.
                    includes |= LinqIncludes.DownloadData | LinqIncludes.Files;
                    if (!string.IsNullOrEmpty(search))
                    {
                        includes |= LinqIncludes.People;
                    }
                    break;
                case "Reading":
                case "Downloaded":
                case "Finished":
                case "CopiedToEBookReader":
                    includes |= LinqIncludes.NavigationData | LinqIncludes.UserData | LinqIncludes.People; // includes download
                    break;
            }

            if (idlist != null) queryList = queryList.Where(b => idlist.Contains(b.BookId));

            //if (includes.HasFlag(LinqIncludes.Files)) queryList = queryList.Include(b => b.Files);
            if (includes.HasFlag(LinqIncludes.People)) queryList = queryList.Include(b => b.People);
            if (includes.HasFlag(LinqIncludes.Notes)) queryList = queryList.Include(b => b.Notes).Include(b => b.Notes.Notes);
            if (includes.HasFlag(LinqIncludes.Review)) queryList = queryList.Include(b => b.Review);
            if (includes.HasFlag(LinqIncludes.DownloadData)) queryList = queryList.Include(b => b.DownloadData);
            if (includes.HasFlag(LinqIncludes.NavigationData)) queryList = queryList.Include(b => b.NavigationData);
            if (includes.HasFlag(LinqIncludes.LanguageExact)) queryList = queryList.Where(b => b.Language == language);
            if (includes.HasFlag(LinqIncludes.LanguageExactOrNull)) queryList = queryList.Where(b => string.IsNullOrEmpty(b.Language) || b.Language == language);

            //
            // The include list (queryable) is set up correctly.
            // Now do the match list.
            //
            IQueryable<BookData> matchList;

            switch (searchScope)
            {
                default:
                case "Catalog":
                    matchList = queryList;
                    break;

                case "PickToDownload":
                    // TODO: doesn't work across computers? The DownloadData seems to always be NULL in the bookmarks
                    // which means when I switch computers, all the books I've downloaded read and finished
                    // will show up in the list?
                    matchList = queryList
                        .Where(b => b.DownloadData == null || b.DownloadData.CurrFileStatus != DownloadData.FileStatus.Downloaded)
                        .Where(b => b.NavigationData == null
                            || (b.NavigationData.NSwipeLeft < 1
                                && b.NavigationData.CurrStatus == BookNavigationData.UserStatus.NoStatus
                            ))
                        ;
                    mustIncludeEpub = true;
                    break;
                case "Downloaded":
                    matchList = queryList
                        .Where(b => b.DownloadData != null && b.DownloadData.CurrFileStatus == DownloadData.FileStatus.Downloaded)
                        .Where(b => b.NavigationData == null
                            || (b.NavigationData.NSwipeLeft < 1
                                && b.NavigationData.CurrStatus == BookNavigationData.UserStatus.NoStatus
                            ))
                        ;
                    break;
                case "Reading":
                    matchList = queryList
                        .Where(b => b.DownloadData != null && b.DownloadData.CurrFileStatus == DownloadData.FileStatus.Downloaded)
                        .Where(b => b.NavigationData != null)
                        .Where(b => b.NavigationData.NSwipeLeft < 1)
                        .Where(b => b.NavigationData.CurrStatus == BookNavigationData.UserStatus.Reading)
                        ;
                    break;
                case "Finished":
                    matchList = queryList
                        .Where(b => b.NavigationData != null)
                        .Where(b => b.NavigationData.NSwipeLeft < 1)
                        .Where(b =>
                            b.NavigationData.CurrStatus == BookNavigationData.UserStatus.Abandoned
                            || b.NavigationData.CurrStatus == BookNavigationData.UserStatus.Done)
                        ;
                    // Matchlist used to also insist that the book be downloaded. But in reality, I might 
                    // finish a book on computer "A" and then want to see that it's finished on computer "B"
                    //.Where(b => b.DownloadData != null) 
                    break;
                case "CopiedToEBookReader":
                    matchList = queryList
                        .Where(b => b.NavigationData != null)
                        .Where(b => b.NavigationData.NSwipeLeft < 1)
                        .Where(b => b.NavigationData.CurrStatus == BookNavigationData.UserStatus.CopiedToEBookReader)
                        ;
                    break;
            }
            // Add in sorting
            switch (sortBy)
            {
                default:
                case "title":
                    matchList = matchList.OrderBy(b => b.Title);
                    break;
                case "author":
                    matchList = matchList.OrderBy(b => b.DenormPrimaryAuthor);
                    break;
                case "date_download_asc":
                    matchList = matchList.OrderBy(b => b.DenormDownloadDate);
                    break;
                case "date_download_desc":
                    matchList = matchList.OrderByDescending(b => b.DenormDownloadDate);
                    break;
            }


            // Step three: filter based on search. Blank searches are special.
            if (searchOperations == null)
            {
                var newlist = new List<BookData>();
                enumerableList = newlist;
                foreach (var book in matchList)
                {
                    if (true || !mustIncludeEpub // turn this off until it works; the .Files are always the same
                        || BookData.FilesIncludesEpub(book))
                    {
                        newlist.Add(book);
                        if (newlist.Count > MaxMatch)
                        {
                            break;
                        }
                    }
                }
                // Simple code removed when the more complex logic for EPUB was introduced
                //enumerableList = matchList
                //    .Take(MaxMatch + 1)
                //    .AsEnumerable();
            }
            else
            {
                var newlist = new List<BookData>();
                enumerableList = newlist;
                foreach (var book in matchList)
                {
                    var epubMatch = true; // turn this off until it works !mustIncludeEpub || BookData.FilesIncludesEpub(book);
                    if (epubMatch && searchOperations.Matches(book))
                    {
                        newlist.Add(book);
                        if (newlist.Count > MaxMatch) // gotta end early :-(
                        {
                            break;
                        }
                    }
                }
            }


            // This seems like a lot of code just to distinguish between
            // "the search returned 150 books" and "the search returned
            // 150 books and there are more if needed."
            andMore = false;
            var nmatch = 0;

            nmatch = 0;
            foreach (var book in enumerableList)
            {
                nmatch++;
                if (nmatch < MaxMatch) resultList.Add(book);
                else if (nmatch > MaxMatch)
                {
                    andMore = true;
                    break;
                }

            }
            return resultList;
        }
    }
}
