using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Xaml.Controls;

namespace SimpleEpubReader.UwpDialogs
{
    class SimpleDialogs
    {
        public enum Result {  Ok, Cancel};

        public static async Task<Result> HowToPickSaveFolder()
        {
            var cd = new ContentDialog()
            {
                Title = "How to pick a save folder",
                Content =
@"Most people will pick a folder in OneDrive to save bookmarks too. By selecting the same folder for each computer that you're running Voracious Reader on, the different computers will automatically stay synchronized with your bookmarks, notes and reviews.

By default, bookmarks are only saved locally.
",
                CloseButtonText = "Pick save folder",
                SecondaryButtonText = "Cancel",

            };
            var result = await cd.ShowAsync();
            var retval = result == ContentDialogResult.None ? Result.Ok : Result.Cancel;
            return retval;
        }
    }
}
