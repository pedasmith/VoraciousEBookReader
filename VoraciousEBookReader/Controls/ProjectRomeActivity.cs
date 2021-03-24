using EpubSharp;
using SimpleEpubReader.Database;
using SimpleEpubReader.EbookReader;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Windows.ApplicationModel.UserActivities;
using Windows.UI.Shell;
using static SimpleEpubReader.Controls.Navigator;

namespace SimpleEpubReader.Controls
{
    class ProjectRomeActivity : SimpleBookHandler, INavigateTo, ISetImages
    {
        static bool ProjectRomeEnabled = false;

        /// <summary>
        /// Called when a book is starting to be displayed. Location is the starting location.
        /// Is used with e.g. the universal note display that shows me all my notes.
        /// </summary>
        /// <param name="book"></param>
        /// <param name="location"></param>
        /// <returns></returns>
        public async Task DisplayBook(BookData book, BookLocation location)
        {
            CurrBookId = book.BookId;
            CurrBook = book;
            CurrLocation = location;
            await Task.Delay(0);
        }

        private async Task CreateActivityAsync(BookData book, BookLocation location, string imageDataUrl)
        {
            if (!ProjectRomeEnabled) return;
            var channel = UserActivityChannel.GetDefault();
            var activity = await channel.GetOrCreateUserActivityAsync(book.BookId);
            activity.VisualElements.DisplayText = $"Reading {book.Title}";
            activity.ActivationUri = AsUri(book.BookId, location);

            var title = Windows.Data.Json.JsonValue.CreateStringValue(book.Title).Stringify();
            var authorvalue = book.BestAuthorDefaultIsNull;
            var author = authorvalue == null ? "\"\"" : Windows.Data.Json.JsonValue.CreateStringValue("By " + authorvalue).Stringify();

            var reviewvalue = book?.Review?.Review;
            var review = reviewvalue == null ? "\"\"" : Windows.Data.Json.JsonValue.CreateStringValue(reviewvalue).Stringify();

            var cardJson =
@"{
	""$schema"": ""http://adaptivecards.io/schemas/adaptive-card.json"", 
	""type"": ""AdaptiveCard"", 
	""version"": ""1.0"",
	""body"": [
		{
			""type"": ""Container"", 
			""items"": [
				{
					""type"":""ColumnSet"",
					""columns"":[
						{
							""type"":""Column"",
							""width"":""auto"",
							""items"":[
								{
									""type"": ""Image"", 
									""url"": """ + imageDataUrl + @""", 
									""size"": ""large""
								}
							]
						},
						{
							""type"":""Column"",
							""width"":""auto"",
							""items"":[
								{
									""type"": ""TextBlock"", 
									""text"": " + title + @", 
									""weight"": ""bolder"", 
									""size"": ""large"", 
									""wrap"": true
								},
								{
									""type"": ""TextBlock"", 
									""text"": " + author + @", 
									""spacing"": ""none"", 
									""isSubtle"": true, 
									""wrap"": true 
								},
								{
									""type"": ""TextBlock"", 
									""text"": " + review + @", 
									""spacing"": ""none"", 
									""wrap"": true 
								}
							]
						}
					]
				}
			]
		}
	]
}";
            var card = AdaptiveCardBuilder.CreateAdaptiveCardFromJson(cardJson);
            activity.VisualElements.Content = card;

            await activity.SaveAsync();

            var session = activity.CreateSession();
            if (Sessions.ContainsKey(book.BookId))
            {
                Sessions[book.BookId] = session;
            }
            else
            {
                Sessions.Add(book.BookId, session);
            }
        }
        BookData CurrBook = null;
        BookLocation CurrLocation = null;
        string CurrBookId = null;
        Dictionary<string, UserActivitySession> Sessions = new Dictionary<string, UserActivitySession>();

        private static Uri AsUri(string bookId, BookLocation location)
        {
            var id = Uri.EscapeUriString(bookId);
            var loc = location == null ? "" : "&location=" + Uri.EscapeUriString(location.ToJson());
            var uri = new Uri($"voracious-reader:///?id={id}{loc}");
            return uri;
        }

        public static (string bookId, BookLocation location) ParseUrl (Uri url)
        {
            if (url.Scheme != "voracious-reader") return (null, null);
            var id = url.AbsolutePath.Trim ('/'); // sigh. Just how life goes.
            BookLocation location = null;
            var query = url.Query;
            if (query.Length > 1 && query[0] == '?') query = query.Substring(1);
            foreach (var queryItem in query.Split(new char[] { '&' }))
            {
                var values = queryItem.Split(new char[] { '=' }, 2);
                if (values[0] == "location" && values.Length >= 2)
                {
                    // Parse out the json...
                    var json = Uri.UnescapeDataString(values[1]);
                    location = BookLocation.FromJson(json);
                }
                else if (values[0] == "id" && values.Length >= 2)
                {
                    id = Uri.UnescapeDataString (values[1]);
                }
            }
            return (id, location);
        }

        public async void NavigateTo(NavigateControlId sourceId, BookLocation location)
        {
            if (!ProjectRomeEnabled) return;
            try
            {
                var channel = UserActivityChannel.GetDefault();
                var activity = await channel.GetOrCreateUserActivityAsync(CurrBookId);
                if (!string.IsNullOrEmpty(activity.VisualElements.DisplayText))
                {
                    // If the activity wasn't already created, don't create it now!
                    activity.ActivationUri = AsUri(CurrBookId, location);
                    await activity.SaveAsync();
                }
            }
            catch (Exception)
            {
                // Project Rome is pretty delicate; it fails for no good reasons.
            }
        }

        public async Task SetImagesAsync(ICollection<EpubByteFile> images)
        {
            // Is called when displaying a book; is a perfect time to make the activity
            if (!ProjectRomeEnabled) return;

            EpubByteFile bestImage = null;
            string imageDataUrl = null;
            foreach (var file in images)
            {
                if (bestImage == null) bestImage = file;
            }
            if (bestImage != null)
            {
                var b64 = Convert.ToBase64String(bestImage.Content);
                imageDataUrl = $"data:{bestImage.MimeType};base64,{b64}"; // image/png
            }
            await CreateActivityAsync(CurrBook, CurrLocation, imageDataUrl);
        }
    }
}
