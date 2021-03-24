using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Xaml.Controls;

namespace SimpleEpubReader.UwpDialogs
{
    class UwpRangeConverter
    {
        ProgressBar Progress;
        public UwpRangeConverter(ProgressBar progress)
        {
            Progress = progress;
        }

        public double Value { get { return Progress.Value; } set { Progress.Value = value; } }
        public double Minimum { get { return Progress.Minimum; } set { Progress.Minimum = value; } }
        public double Maximum { get { return Progress.Maximum; } set { Progress.Maximum = value; } }
    }
}
