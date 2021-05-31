using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Security.Cryptography;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

// The User Control item template is documented at https://go.microsoft.com/fwlink/?LinkId=234236

namespace SimpleEpubReader.Controls
{
    public interface IProgressReader
    {
        void SetNBooks(int nbooks);
        void SetCurrentBook(string title);
        void AddLog(string log);
    }
    public sealed partial class EbookReaderProgressControl : UserControl, IProgressReader
    {
        public EbookReaderProgressControl()
        {
            this.InitializeComponent();
        }

        public void AddLog(string log)
        {
            uiLog.Text += log;
        }

        public void SetCurrentBook(string title)
        {
            uiProgress.Value++;
            uiCurrentName.Text = title;
            uiLog.Text += $"Start: {title}\n";
        }

        public void SetNBooks(int nbooks)
        {
            uiProgress.Minimum = 0;
            uiProgress.Maximum = nbooks;
            uiProgress.Value = 0;
            uiLog.Text = "";
        }
    }
}
