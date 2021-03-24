using System;
using System.Collections.Generic;


namespace SimpleEpubReader.EbookReader
{
    class AllUpBookPositionCalculations
    {
        private List<double> SectionSizes = null;
        public void SetSectionSizes(List<double> value)
        {
            SectionSizes = value;
        }
        public bool SectionSizesOk {  get { return (SectionSizes != null && SectionSizes.Count >= 1); } }

        public double TotalSize 
        {  get
            {
                double totalSize = 0.0;
                for (int i = 0; i < SectionSizes.Count; i++)
                {
                    totalSize += SectionSizes[i];
                }
                return totalSize;
            }
        }

        public (int htmlIndex, double percentPosition) GetPercentPosition(double xvalue)
        {
            double totalSize = 0.0;
            for (int i = 0; i < SectionSizes.Count; i++)
            {
                totalSize += SectionSizes[i];
            }
            double preSize = 0.0;
            for (int htmlIndex = 0; htmlIndex < SectionSizes.Count; htmlIndex++)
            {
                // Am I in the right section?
                var sectionSize = SectionSizes[htmlIndex];
                var startPos = preSize / totalSize;
                var endPos = startPos + (sectionSize / totalSize);
                if (xvalue >= startPos && xvalue <= endPos)
                {
                    // Found it!
                    var delta = endPos - startPos;
                    var percentPosition = 100 * (xvalue - startPos) / delta;
                    return (htmlIndex, percentPosition);
                }
                preSize += sectionSize;
            }
            return (-1, -1);
        }

        public List<double> GetLines(double canvasActualWidth)
        {
            var retval = new List<double>();
            double totalSize = TotalSize;
            double preSize = 0.0;
            for (int i = 0; i < SectionSizes.Count - 1; i++)
            {
                var ratio = (SectionSizes[i] + preSize) / totalSize;
                var x = ratio * canvasActualWidth;
                retval.Add(x);
                preSize += SectionSizes[i];
            }
            return retval;
        }
        public double UpdatePosition(int htmlIndex, double percentPosition) // percentPosition is 0..100
        {
            double value = 0.0;
            if (!SectionSizesOk) return -1.0;
            if (htmlIndex >= SectionSizes.Count)
            {
                value = 1.0; // at the end.
            }
            else if (htmlIndex < 0)
            {
                value = 0.0;
            }
            else
            {
                double totalSize = 0.0;
                double preSize = 0.0;
                for (int i = 0; i < SectionSizes.Count; i++)
                {
                    totalSize += SectionSizes[i];
                    if (i < htmlIndex) preSize += SectionSizes[i];
                }
                var sectionSize = SectionSizes[htmlIndex];
                if (totalSize == 0)
                {
                    value = 0.5;
                }
                else
                {
                    var startPos = preSize / totalSize;
                    var endPos = startPos + (sectionSize / totalSize);
                    var delta = endPos - startPos;
                    if (double.IsNaN(percentPosition))
                    {
                        percentPosition = 0.0; // gotta pick something!
                    }
                    value = startPos + (percentPosition / 100.0) * delta;
                }
            }
            return value;
        }

    }
}
