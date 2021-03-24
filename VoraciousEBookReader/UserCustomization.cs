using SimpleEpubReader.Controls;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Windows.UI.Xaml.Media; // Needed for FontFamily

namespace SimpleEpubReader
{
    public class UserCustomization: INotifyPropertyChanged
    {
        const string USER_FONT_NAME = "FontName";
        const string USER_FONT_SIZE = "FontSize";


        private double _fontSize = 12;
        public double FontSize
        {
            get { return _fontSize; }
            set
            {
                _fontSize = value;
                OnPropertyChanged();
                OnPropertyChanged("ButtonFontSize");
                OnPropertyChanged("CaptionFontSize");
                OnPropertyChanged("ClickableCaptionFontSize");
                OnPropertyChanged("DescriptionFontSize");
                OnPropertyChanged("HeaderTabFontSize");
                OnPropertyChanged("HeaderInputFontSize");
                OnPropertyChanged("HeaderTextFontSize");
                OnPropertyChanged("HyperlinkFontSize");
                OnPropertyChanged("IconFontSize");
                OnPropertyChanged("InfoFontSize");
                OnPropertyChanged("InputFontSize");
                OnPropertyChanged("PeopleFontSize");
                OnPropertyChanged("TextFontSize");
                OnPropertyChanged("TitleFontSize");
                OnPropertyChanged("Toc1FontSize");
                OnPropertyChanged("Toc2FontSize");
                OnPropertyChanged("Toc3FontSize");
                var nav = Navigator.Get();
                var task = nav.MainBookHandler.SetFontAndSizeAsync(StandardFF.Source, $"{_fontSize}pt");

                var localSettings = Windows.Storage.ApplicationData.Current.LocalSettings;
                localSettings.Values[USER_FONT_SIZE] = value;

            }
        }

        const double MinFontSize = 6.0;

        public double ButtonFontSize
        {
            get { return _fontSize * 1.2; }
        }
        public double CaptionFontSize
        {
            get { return Math.Max(MinFontSize, _fontSize * 0.6); }
        }
        public double ClickableCaptionFontSize
        {
            get { return Math.Max(MinFontSize, _fontSize * 0.85); }
        }
        public double DescriptionFontSize
        {
            get { return _fontSize * 1.0; }
        }
        public double HeaderTabFontSize
        {
            get { return _fontSize * 1.2; }
        }
        public double HeaderInputFontSize
        {
            get { return _fontSize * 1.1; }
        }
        public double HeaderTextFontSize
        {
            get { return _fontSize * 1.8; }
        }

        public double HyperlinkFontSize
        {
            get { return _fontSize * 1.0; }
        }
        public double IconFontSize
        {
            get { return _fontSize * 2.2; }
        }
        public double InfoFontSize
        {
            get { return _fontSize * 1.0; }
        }
        public double InputFontSize
        {
            get { return _fontSize * 1.0; }
        }
        public double PeopleFontSize
        {
            get { return _fontSize * 1.0; }
        }
        public double TextFontSize
        {
            get { return _fontSize * 1.1; }
        }
        public double TitleFontSize
        {
            get { return _fontSize * 1.0; }
        }
        public double Toc1FontSize
        {
            get { return _fontSize * 1.4; }
        }
        public double Toc2FontSize
        {
            get { return _fontSize * 1.2; }
        }
        public double Toc3FontSize
        {
            get { return _fontSize * 1.0; }
        }




        private static FontFamily DefaultFF = new FontFamily("Consolas");

        private FontFamily _StandardFF = DefaultFF;
        public FontFamily StandardFF
        {
            get { return _StandardFF; }
            set
            {
                if (value == _StandardFF) return;
                _StandardFF = value;
                OnPropertyChanged();

                var nav = Navigator.Get();
                var task = nav.MainBookHandler.SetFontAndSizeAsync(StandardFF.Source, $"{_fontSize}pt");
            }
        }

        public string GetFontSizeHtml()
        {
            return $"{FontSize}pt";
        }


        public static Dictionary<string, FontFamily> UserFonts = new Dictionary<string, FontFamily>()
        {
            { "Arial", new FontFamily("Arial") },
            { "Baskerville", new FontFamily("Baskerville Old Face") },
            { "Book Antiqua", new FontFamily("Book Antiqua") },
            { "Calibri", new FontFamily("Calibri") },
            { "Cambria", new FontFamily("Cambria") },
            { "Consolas", new FontFamily("Consolas") },
            { "Courier", new FontFamily("Courier New") },
            { "Georgia", new FontFamily("Georgia") },
            { "Lucida Sans", new FontFamily("Lucida Sans") },
            { "Palatino", new FontFamily("Palatino Linotype") },
            { "Segoe", new FontFamily("Segoe UI") },
            { "Segoe Script", new FontFamily("Segoe Script") },
            { "Times", new FontFamily("Times New Roman") },
            { "Verdana", new FontFamily ("Verdana") },
        };

        public string GetUserFontName()
        {
            var localSettings = Windows.Storage.ApplicationData.Current.LocalSettings;
            var currFont = localSettings.Values[USER_FONT_NAME] as string;
            if (string.IsNullOrEmpty(currFont)) currFont = "Georgia";
            return currFont;
        }

        public string GetFontFamilyName()
        {
            var name = GetUserFontName();
            var retval = UserFonts[name].Source;
            return retval;
        }

        public double GetSavedUserFontSize()
        {
            const double DEFAULT_FONT_SIZE = 12.0;

            double fontSize = DEFAULT_FONT_SIZE;
            var localSettings = Windows.Storage.ApplicationData.Current.LocalSettings;
            try
            {
                var fs = localSettings.Values[USER_FONT_SIZE];
                fontSize = fs != null ? (double)fs : double.NaN;
                if (double.IsNaN (fontSize))
                {
                    fontSize = DEFAULT_FONT_SIZE;
                }
            }
            catch(Exception)
            {
                fontSize = DEFAULT_FONT_SIZE;
            }
            return fontSize;
        }

        public void SetSavedUserFontNameAndSize(string fontName, double fontSize)
        {
            var localSettings = Windows.Storage.ApplicationData.Current.LocalSettings;
            localSettings.Values[USER_FONT_NAME] = fontName;
            localSettings.Values[USER_FONT_SIZE] = fontSize;
        }

        public void Initialize()
        {
            var fontName = GetUserFontName();
            var fontSize = GetSavedUserFontSize();
            FontSize = fontSize;
            StandardFF = UserFonts[fontName];
        }


        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
