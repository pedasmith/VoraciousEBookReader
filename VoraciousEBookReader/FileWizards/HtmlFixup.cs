using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace SimpleEpubReader.FileWizards
{
    public static class HtmlFixup
    {
        static string FakeHttpPrefix = "http://example.com/book/?";

        // Fix up the <img src=""> tags -- they need to be http:// for the WebView
        // WebResourceRequested to kick in
        // While I'm at it, set a reasonable id for each image!
        public static string FixupHtmlAll(string foundHtml)
        {
            foundHtml = FixupXmlHeader(foundHtml);
            foundHtml = FixupImgAndLink(foundHtml);
            foundHtml = FixupAddScripts(foundHtml);
            foundHtml = FixupBodyPadding(foundHtml);
            foundHtml = FixupEmptyAnchorTag(foundHtml);

            return foundHtml;
        }

        private static string FixupEmptyAnchorTag(string html)
        {
#if NEVER_EVER_DEFINED
            // this is the old fixup.
            string[] KnownBad = {
                "<a id=\"AuthorTOC\"/>",
            };
            foreach (var bad in KnownBad)
            {
                if (html.Contains(bad))
                {
                    html = html.Replace(bad, "");
                }
            }
#endif
            // FAIL: Chronicles of Copper Boom has an unclosed <a id="AuthorTOC"/> tag
            // FAIL: O'Reilly Mintduino has lots of empty anchor (a) tags. Try this trivial fix as a band-aid.
            //     <a id="I_mediaobject2_d1e467"/> ==> <a id="I_mediaobject2_d1e467"></a>
            // FAIL: Flying Girl and her Chum has a <div class="figcenter c2"/> ; this causes the entire first section to be centered 
            //    with a hefty margin left and right.
            Regex[] regexList = {
                new Regex("<(?<tag>a)(?<middle> id=\"[^\"]*\")(?<end>/>)"),
                new Regex("<(?<tag>div)(?<middle> [a-z]*=\"[^\"]*\")(?<end>/>)"),
            };

            // FAIL: starting in 2024, the Project Gutenberg books include a little <pre/> tag.
            // This is OK for XHML (which the book I debugged was), but is a fail for HTML. In
            // particular, the <pre/> tag is read as just a <pre> tag, and the rest of the
            // book (which is all of it) is rendered by my WebView as having pre-formatted lines,
            // which then don't wrap.
            if (html.Contains("<pre/>"))
            {
                html = html.Replace("<pre/>", "<pre></pre>");
            }


            //FAIL: O'Reilly Mintduino copyright page has a <title/> element. This is 100% incorrect; the result is that browsers will interpret the entire
            //rest of the page as one enormous title. This eventually causes a script failure with scrollTo.
            html = html.Replace("<title/>", "<title></title>");

            for (int i = 0; i < regexList.Length; i++)
            {
                var regex = regexList[i];
                var startIdx = 0;
                while (startIdx >= 0)
                {
                    var match = regex.Match(html, startIdx);
                    if (match.Success)
                    {
                        var tag = match.Groups["tag"];
                        var middle = match.Groups["middle"];
                        var replace = $"<{tag}{middle} ></{tag}>";
                        html = html.Substring(0, match.Index) + replace + html.Substring(match.Index + match.Length);
                        startIdx = match.Index + replace.Length;
                    }
                    else
                    {
                        startIdx = -1;
                    }
                }
            }
            return html;
        }

        private static string FixupAddScripts(string html)
        {
            html = html.Replace("</head>", @"<script>
//<![CDATA[	
// FAIL: must embed using CDATA. Otherwise the less-than signs are invalid when embedded in XHTML
// XHTML is used by BAEN 2013 short stories in the title page

    document.onselect= function (event) { onDocumentSelect(event) };
    window.onscroll = function() { onWindowScroll() };

function DoGetSelection() { 
  var s = window.getSelection(); 
  if (s==null || s.type=='None') { s=''; window.external.notify('dbg:getSelection is None'); } else { s=s.toString(); }
  return s;
}

function onDocumentSelect(event) {
  var s = window.getSelection();
  var oRange = s.getRangeAt(0);
  var top = oRange.getBoundingClientRect().top;
  var st = document.body.scrollTop || document.documentElement.scrollTop;
  var sh = document.documentElement.scrollHeight;
  var ch = document.documentElement.clientHeight;
  var pct = sh==ch ? 0  : ((st+top) / (sh-ch)) * 100;
  window.external.notify ('selectpos:' + pct);
  window.external.notify ('select:' + s);
}

function onWindowScroll() {
  doWindowReportScroll();
  doWindowReportTopid('onWindowScroll');
}

function doWindowReportScroll() {
  var st = document.body.scrollTop || document.documentElement.scrollTop;
  var sh = document.documentElement.scrollHeight;
  var ch = document.documentElement.clientHeight;
  var pct = sh==ch ? 0 : (st / (sh-ch)) * 100;
  window.external.notify('dbg:doWindowReportScroll:pct='+pct+' st='+st+' currScrollTop='+document.body.scrollTop+' :: :: sh='+sh+' ch='+ch);

  window.external.notify('scroll:'+pct);
  if(sh<=ch) {
    window.external.notify('monopage:'+(sh-ch));
  }
}

function doWindowReportTopid(reason) {
  var st = document.body.scrollTop || document.documentElement.scrollTop;
  var sh = document.documentElement.scrollHeight;
  var ch = document.documentElement.clientHeight;
  var pct = sh==ch ? 0  : (st / (sh-ch)) * 100;

  var topPct = pct + ch/sh*20;
  var id = getClosestTagType(topPct, '|IMG|LINK|SPAN|'); // don't get images or links. Must include those extra bars!
  //FAIL: why not report the id of spans? Answer is that How to Code in Go has a bazillion Id values
  //with the format cb1-1. These are used for code blocks. The problem is that these id values are not
  //unique across the HTML files (!). So when I scroll near one of them, scrolling flips to an unrelated chapter.
  // Luckily all of these extra things are all in SPAN values
  window.external.notify('dbg:doWindowReportTopid reason='+reason+' id='+id);
  window.external.notify(id ? 'topid:'+id : 'topnoid:');
}

function scrollToId(id) {
    var el = document.getElementById(id);
    if (el != null) {
        scrollToElement(el);
    } else {
        ellist = document.getElementsByTagName('img');
        for(i = 0; i<ellist.length; i++) {
            el = ellist[i];
            if (el.hasAttribute ('src')) {
                var src = el.getAttribute('src');
                if (src.endsWith (id)) {
                    scrollToElement(el);
                }
            }
        }
    }
}
function scrollToElement(el) {
    if (el != null) {
        el.scrollIntoView(true);
        doWindowReportScroll();
        doWindowReportTopid('scrollToId');
    }
}

function scrollToPercent(pct) {
  var sh = document.documentElement.scrollHeight;
  var ch = document.documentElement.clientHeight;
  var st = sh==ch ? 0  : pct * (sh-ch) / 100;
  var maxst = sh-ch;
  if (st > maxst) st = maxst;
  if (st < 0) st = 0;
  window.external.notify('dbg:scrollToPercent: from='+document.body.scrollTop+' to='+st+' pct='+pct);
  if (document.body.scrollTop == st) doWindowReportScroll(); // we were explicitly asked to move, so generate a report.
  document.body.scrollTop = st;
  doWindowReportTopid('scrollToPercent')
}

function scrollPage(npage) {
  var sh = document.documentElement.scrollHeight;
  var ch = document.documentElement.clientHeight;
  var pageSize = ch*.95;
  var st = document.body.scrollTop + pageSize*npage;
  var maxst = sh-ch;
  if (st > maxst) st = maxst;
  if (st < 0) st = 0;

  window.external.notify('dbg:scrollPage:sh='+sh+' ch='+ch+' maxst='+maxst);
  window.external.notify('dbg:scrollPage:about to scroll to '+st);
  document.body.scrollTop =st;
  //No need; when the window scroll it will trigger the topid
  //doWindowReportTopid('scrollPage')
  // window.external.notify('dbg:scrollPage:about to return ');
}

function getClosestTag(pctToFind) {
  return getClosestTagType(pctToFind, '');
}

// Finds the id higher than the top of the screen, but allowing
// for some amount of overlap (20%) at the top.
function getClosestTagNear(pctToFind) {
  var sh = document.documentElement.scrollHeight;
  var ch = document.documentElement.clientHeight;
  var topPct = pctToFind + (ch/sh)*80;
  if (topPct < 0) topPct = 0;
  return getClosestTagType(topPct, '');
}

function getClosestTagType(pctToFind, invalidTagList) {
  var closest = -99999999;
  var retval;
  
  var st = document.body.scrollTop || document.documentElement.scrollTop;
  var sh = document.documentElement.scrollHeight;
  var ch = document.documentElement.clientHeight;
  
  var list = document.getElementsByTagName('*');
  // Log ('ClosestTagType: called and list length='+list.length);
  for (var i=0; i<list.length; i++) {
      var el = list[i];
	  if (el.id && !invalidTagList.includes ('|'+el.tagName+'|')) {
	      // client rect is negative when it's above the viewport.
	      var r = el.getBoundingClientRect();
          var pct = sh==ch ? 0  : ((st+r.top) / (sh-ch)) * 100;
          // FAIL: Chronicles of Copper Boom: Chilling Warmth include calibre_toc_2 in a display:none paragraph.
          // That paragraph is then given a fake display location of always being at the top of the window,
          // resulting in very weird scrolling behavior.
          var computedDisplay = window.getComputedStyle(el).display;
          var isDisplayNone = computedDisplay == 'none';
          var innerText='fail';
          try {
              innerText=el.innerText;
              innerText=innerText.substring(0,Math.min(innerText.length,15));
          } catch (innererror) { innerText='(exception)'+innererror; }
		  // Log ('ClosestTagType: Looking at['+i+'] id=' + el.id + ' tagName='+el.tagName+' pct='+pct+' target pct='+pctToFind+' innerText='+innerText);
		  if (pct < pctToFind && pct >= closest && pct <= 100 && !isDisplayNone) {
		      retval = el.id;
			  closest = pct;
			  // Log ('ClosestTagType: New best: El at pct=' + pct + ' id=' + el.id);
		  }

          // handy debugging output
		  if (pct < pctToFind && pct >= closest && pct <= 100) {
			  // Log ('ClosestTagType: potential new best: El at ' + pct + ' id=' + el.id);
		  }
		  else {
			  // Log ('ClosestTagType: rejected: El pct=' + pct + ' id=' + el.id + ' closest=' + closest);
		  }
	  } else {
          // tag was rejected.
          if (el.id) {
			  // Log ('ClosestTagType: tag at ['+i+'] id='+el.id+' was rejected because tag type='+el.tagName);
          }
      }
  }
  Log ('Return ' + retval + ' (' + pctToFind + ')');
  return retval;
}

function Log(str) {
    // document.getElementById('uiLog').innerText = str;
    window.external.notify('dbg:' + str);
}

function SetFontAndSize(font, size) {
    document.body.style.fontFamily = font;
    document.body.style.fontSize = size;
}

function SetColor(background, foreground) {
    document.body.style.backgroundColor = background;
    document.body.style.color = foreground;
}

//]]>
</script>
</head>");
            return html;
        }

        private static string FixupBodyPadding(string html)
        {
            //FAIL: must include body padding so that the scroll bar doesn't cover up the words
            //var loggingDiv = "<div id='uiLog' style='position:fixed; bottom:50px; background-color:white'></div>\n";
            // The logging div isn't always used -- find function Log(str) and uncomment document.getElementById('uiLog').innerText = str;
            var loggingDiv = "<div id='uiLog' style='position:fixed; bottom:50px;'></div>\n";

            var userCustomization = (App.Current as App).Customization;
            var fontFamily = userCustomization.GetFontFamilyName();
            var fontSize = userCustomization.GetFontSizeHtml();

            // FAIL:
            // https://www.unescap.org/intergovernmental-meetings/asia-pacific-regional-review-25th-anniversary-beijing-declaration
            // From the Annotated Professional Agenda (epub)
            // There is not a <body> tag; it's <body ... >
            // <body xml:lang="en" xmlns:xml="http://www.w3.org/XML/1998/namespace">
            var idx = html.IndexOf("<body");
            if (idx < 0)
            {
                App.Error($"ERROR: MainEpubReader: FixupBodyPadding: can't find the <body tag!");
                return html;
            }
            var endidx = html.IndexOf(">", idx + 3); // find the end
            html = html.Insert(endidx + 1, $"\n{loggingDiv}");
            html = html.Replace("<body", $"<body style=\"padding-right:40px; font-size:{fontSize}; font-family:'{fontFamily}'\" ");
            return html;
        }

        private static string FixupImgAndLink(string html)
        {
            if (html == null)
            {
                return "<b>No ebook found</b>";
            }
            // example: start with <img src="@public@vhost@g@gutenberg@html@files@61533@61533-h@images@fig-051.jpg" alt=""/>
            // create <img id="@public@vhost@g@gutenberg@html@files@61533@61533-h@images@fig-051.jpg" src="http://example.com/book?@public@vhost@g@gutenberg@html@files@61533@61533-h@images@fig-051.jpg" alt=""/>
            // <img src="ramesses1.jpg" alt="" class="calibre7"/> ==> <img src="http://example.com/book/?ramesses1.jpg"/>
            // Only match the first part of the <img tag!
            // FAIL: Baen puts their covers in an SVG (!) like this: <image width="450" height="680" xlink:href="cover.jpeg"/>
            Regex[] regexList = {
                new Regex("<(?<tag>image) (?<pre>(([^x]|(x[^l]))[a-z]*=\"[^\"]*\"[ ]*)*)xlink:href=\"(?<src>[^\"]*)\""),
                new Regex("<(?<tag>img) (?<pre>(([^s]|(s[^r]))[a-z\\-]*=\"[^\"]*\"[ ]*)*)src=\"(?<src>[^\"]*)\""),
                new Regex("<(?<tag>link) (?<pre>(([^h]|(h[^r]))[a-z]*=\"[^\"]*\"[ ]*)*)href=\"(?<src>[^\"]*)\""),
            };

            int nimgReplaced = 0;
            for (int i = 0; i < regexList.Length; i++)
            {
                var regex = regexList[i];
                var startIdx = 0;
                while (startIdx >= 0)
                {
                    var match = regex.Match(html, startIdx);
                    if (match.Success)
                    {
                        var pre = match.Groups["pre"];
                        var src = match.Groups["src"];
                        var tagtype = match.Groups["tag"].ToString(); // img or link or image
                        var linkname = "";
                        switch (tagtype)
                        {
                            case "link": linkname = "href"; break;
                            case "img": linkname = "src"; break;
                            case "image": linkname = "xlink:href"; break;
                        }
                        var img = $"<{tagtype} {pre} id=\"{src}\" {linkname}=\"{FakeHttpPrefix}{src}\""; // only replace the first part of the <img tag
                        html = html.Substring(0, match.Index) + img + html.Substring(match.Index + match.Length);
                        startIdx = match.Index + img.Length;
                        if (tagtype == "img") nimgReplaced++; // don't count <image tags; they are also ignored in CountImgTags.
                    }
                    else
                    {
                        startIdx = -1;
                    }
                }
            }
            var nImgTags = CountImgTags(html);
            if (nimgReplaced != nImgTags)
            {
                if (nImgTags > 0)
                {
                    App.Error($"NOTE: replaced {nimgReplaced} but there were {nImgTags}");
                }
                ; // an error
            }
            return html;
        }

        private static string FixupXmlHeader(string html)
        {
            // FAIL: Needed for BAEN 2013 Short Stories cover page -- it's got an SVG that will only display as DTD XHTML.
            // Note that the titlepage.xhtml in the file does not validate as XHTML.
            // Note to the future: xhtml was the idea that if only we have completely valid XML as our HTML, 
            // our HTML would work better. It was a complete disaster and was dropped shortly after introduction,
            // preserved only by old build pipelines maintained by libertarians.
            var doctype = "<!DOCTYPE html PUBLIC \"-//W3C//DTD XHTML 1.0 Transitional//EN\" \"http://www.w3.org/TR/xhtml1/DTD/xhtml1-transitional.dtd\">\r\n";
            if (html.Contains ("<svg"))
            {
                html = doctype + html;
            }


            return html;
        }

        private static int CountImgTags(string html)
        {
            int count = 0;
            int startIndex = 0;
            bool keepGoing = true;
            while (keepGoing)
            {
                var nexti = html.IndexOf("<img", startIndex);
                if (nexti >= 0)
                {
                    startIndex = nexti + 1;
                    count++;
                }
                else
                {
                    keepGoing = false;
                }
            }
            return count;
        }
    }
}
