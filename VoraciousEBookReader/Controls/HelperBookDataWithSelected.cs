using SimpleEpubReader.Database;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SimpleEpubReader.Controls
{
    /// <summary>
    /// Helper class for selecting BookData. Initialized with BookData and has two public properties (title and IsSelected) which can be used in a ListView.
    /// </summary>
    public class HelperBookDataWithSelected : INotifyPropertyChanged
    {
        public HelperBookDataWithSelected(BookData data)
        {
            Title = data.Title;
            RawBook = data;
            IsSelected = true;
        }
        public string Title { get; set; }
        public BookData RawBook { get; set; }
        private bool _isSelected = true;
        public bool IsSelected { get { return _isSelected; } set { if (value == _isSelected) return; _isSelected = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("IsSelected")); } }

        public event PropertyChangedEventHandler PropertyChanged;
    }
}
