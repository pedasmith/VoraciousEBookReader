using EpubSharp;
using ExCSS;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace SimpleEpubReader.FileWizards
{
    public class CssFixup
    {
        public static bool FixupCss(EpubFile cssFile)
        {
            var changed = false;
            var css = System.Text.Encoding.UTF8.GetString(cssFile.Content);
            var parser = new StylesheetParser();
            var sheet = parser.Parse(css);
            var cssBodyList = sheet.StyleRules.Where(rule => rule.SelectorText == "body");
            foreach (var cssBody in cssBodyList)
            {
                var margin = cssBody.Style.Margin;
                if (margin != null && margin.Contains("%"))
                {
                    changed = true;
                    cssBody.Style.Margin = "1em";
                }
                var marginLeft = cssBody.Style.MarginLeft;
                if (marginLeft != null && marginLeft.Contains("%"))
                {
                    changed = true;
                    cssBody.Style.MarginLeft = "0.5em";
                }
                var marginRight = cssBody.Style.MarginRight;
                if (marginRight != null && marginRight.Contains("%"))
                {
                    changed = true;
                    cssBody.Style.MarginRight = "0.5em";
                }


            }

            if (changed)
            {
                var newCss = sheet.ToCss();
                var newBuffer = System.Text.Encoding.UTF8.GetBytes(newCss);
                cssFile.Content = newBuffer;
            }

            return changed;
        }
    }
}
