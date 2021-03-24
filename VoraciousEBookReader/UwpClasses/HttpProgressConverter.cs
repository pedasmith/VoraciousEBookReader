using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Web.Http; // OK to Windows.Web.Http; this file is UWP only

namespace SimpleEpubReader.UwpDialogs
{
    class HttpProgressConverter
    {
        public static string Convert(HttpProgress progress)
        {
            var str = "";
            switch (progress.Stage)
            {
                case HttpProgressStage.ConnectingToServer: str = "Connecting\n"; break;
                case HttpProgressStage.WaitingForResponse: str = "Waiting\n"; break;
                case HttpProgressStage.ReceivingHeaders: str = "Getting headers\n"; break;
                case HttpProgressStage.ReceivingContent: str = "Getting catalog\n"; break;
            }
            return str;
        }
    }
}
