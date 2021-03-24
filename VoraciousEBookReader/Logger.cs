using System;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Xaml.Controls;

namespace SimpleEpubReader
{
    public static class Logger
    {
        public static TextBlock LogBlock = null;
        private static StringBuilder _logsb = new StringBuilder();

        public static void Clear()
        {
            if (LogBlock == null) return;
            LogBlock.Text = "";
        }

        public static bool LogExtraTiming { get; } = true;
        public static bool LogAllResourceLoads { get; } = false;

        static DateTimeOffset startTime = DateTimeOffset.UtcNow;
        private static string GetStartDeltaNice()
        {
            var now = DateTimeOffset.UtcNow;
            var delta = now.Subtract(startTime).TotalMilliseconds;
            var retval = delta < 500 ? $"{delta:F0}" : $"** {delta:F0}";
            startTime = now;
            return retval;
        }
        public static async Task LogAsync(string str)
        {
            var line = GetStartDeltaNice() + ": " + str;
            System.Diagnostics.Debug.WriteLine(line); 
            _logsb.AppendLine(line);

            if (LogBlock == null) return;
            await LogBlock.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, 
                () => {
                    LogBlock.Text += str + '\n';
                });
        }
        public static bool IsFirstCall = true;
        public static void Log (string str)
        {
            var line = GetStartDeltaNice() + ": " + str;

            if (IsFirstCall)
            {
                System.Diagnostics.Debug.WriteLine(line);
                _logsb.AppendLine("Times are delta in milliseconds since the last logging line");
                IsFirstCall = false;
            }
            System.Diagnostics.Debug.WriteLine(line); 
            _logsb.AppendLine(line);

            if (LogBlock == null) return;
            var task =  LogBlock.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal,
                () => {
                    LogBlock.Text += str + '\n';
                });
        }
    }
}
