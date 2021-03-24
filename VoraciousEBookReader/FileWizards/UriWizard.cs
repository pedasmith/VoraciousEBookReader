using System;


namespace SimpleEpubReader.FileWizards
{
    class UriWizard
    {
        /// <summary>
        /// Given a URI, return an appropriate filename + extension.
        /// </summary>
        /// <param name="uri"></param>
        /// <returns></returns>
        public static (string filename, string ext) GetUriFilename(Uri uri)
        {
            var path = uri.GetComponents(UriComponents.Path, UriFormat.Unescaped);
            // path is e.g. ebooks/14.epub.noimages

            var slashidx = path.LastIndexOf('/');
            if (slashidx >= 0) path = path.Substring(slashidx + 1);
            // path is updated to be e.g. 14.epub.noimages

            //FAIL: why would you give your epubs the wrong extension?
            //Instead of <title>.noimages.epub they are <title>.epub.noimages which makes
            //file pickers etc completely fail.
            var originalFilename = path.Replace(".images", "").Replace(".noimages", "");
            // path is updated to be e.g. 14.epub

            var ext = originalFilename;
            var dotidx = ext.LastIndexOf('.');
            if (dotidx >= 0) ext = ext.Substring(dotidx);

            return (originalFilename, ext);
        }

        /// <summary>
        /// Fixes some broken Gutenberg URLs.
        /// </summary>
        /// <param name="fileName"></param>
        /// <returns></returns>
        public static string FixupUrl(string fileName)
        {
            if (fileName.StartsWith("https:") && fileName.Contains("gutenberg.org"))
            {
                // FAIL: NOTE: Gutenberg has some weird files that start with https://.
                // In reality, the gutenberg server redirects these to http:
                // BUT this is not allowed in HttpClient and there's no way to 
                // force the HttpClient BaseProtocolFilter to allow the redirect.
                fileName = fileName.Replace("https://", "http://");
            }
            return fileName;
        }
    }
}
