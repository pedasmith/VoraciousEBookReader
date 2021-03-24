# Things to know about EPUB files

I made an ebook reader! And along the way, I learned a bunch about existing EPUB files. My book reader is targetted mostly at Project Gutenberg, but I also want to be universal: most EPUB ebooks should "just work". My validation plan is to simply get a bunch of EPUBs from a variety of sources (thank-you, all the government offices that supply EPUB files!) and try them out.

Result: so many weirdness in EPUB files. So. Many. Weird. EPUB. Files. This list is my ongoing attempt to 
track all of the changes I had to make in my code to accomodate.

## Displaying Images, problem #1: modifying img src

A EPUB file is really just a ZIP file with stuff and some specially-crafted HTML. The HTML is then 
displayed in a normal Windows UWP WebView control. The HTML in turn includes references to images 
(super common) and CSS files (not really needed, but see the Beetles problem further down). Since 
those are neatly packaged in the EPUB, I can just hook the WebView WebResourceRequested event, and from
there I can return the images, etc.

**Except** that WebView is from Jupiter. A typical link in the EPUB file doesn't include the http:// prefix; they 
are relative URLS, not absolute. And WebView simply ignores URLS like that!

**Solution**: I find every instance of <img src="..." /> and replace it with <img src="http://example.com/book?..." />

**Details**: The new URL has to be an http:// url (or https://, I guess), and needs to be something that won't show 
up in a normal EPUB. The original src value must be part of a query because the relative URL might be something like

    @public@vhost@g@gutenberg@html@files@61533@61533-h@images@cbl-2.jpg
	
See all those @ signs? Hah!

## Finding the chapter with the image

Normally finding the chapter that includes an image is simple: each image has an ID, and you just search for an <img> tag where the id is the correct id.

**Except** for Keys to Spiritual Growth where the image id is like "20.jpg" and the id is like "d14e3286" (which is otherwise nowhere to be found in the ebook)

**Solution** is that in the case of .jpg ids, accept <img> src values. Note that the image source value might be like "images/20.jpg" which doesn't perfectly match the id (add a / to the 20.img and do a case-insenstive ends-with). 

## Downloading given the URL in the RDF catalog

Security? We don't need no stinking security! The Gutenberg RDF catalog has a nice simple URL for each 
different file format that any one book includes. That is, each book is available in multiple formats,
and for each format, there's a different URL to download that particular book from

**Except** that some books are presented with an https:// encoding. The Gutenberg project will happily 
redirect these https:// URLs to the correct and support http:// url. But HTTP downloaders will quite correctly
decline to allow the switch from one to the other.

**Solution** is to always convert all https:// URLs to plain http:// urls regardless of whether the the
https:// url is actually supported or not.

## Solid, complete catalog

Project Gutenberg includes a complete Resource Description Framework (RDF) formatted books. Each book
is described by a Machine Readable Catalog (MARC) file. The Gutenberg Project has done a good job 
at making a full and complete catalog.

**Except** that there are two known bad books in the catalog: number 0, and number 99999.

**Solution** is to have an explicit list of known bad files (the KnownBadFiles list) so that they
won't be read in.

## File extensions? Where we're going, we don't need file extensions!

The Project Gutenberg people, bless them, will ship you an EPUB either with images like normal people,
or without images for not particularly good reason at all. That's not a problem; there probably are some
people who would rather save the now-inconsequential amount of space the images take up. (when Gutenberg
started, this was a more reasonable problem: a typical floppy disk only holds about 1 meg of data; 
for many books, the non-image version will fit onto a floppy, and the one with images will not.

**Except** that the files are given new names. Instead of calling their files names like book_title.images.epub and
book_title.noimages.epub, preserving the file name, they instead call their files names like book_title.epub.images
and book_title.epub.noimages

**Solution** the .images and .noimages are simply removed from the file name.


## Some text ebooks are zipped

Project Gutenberg sometimes delays adding in the .EPUB files to their catalog. This can be a problem since the next-preferred format might be a zipped .TXT file. This is actually a ridiculous format: HTTP servers are perfectly capable of automatically zipping up the text files as needed. And on modern computers (really, anything in the last ten years) has plenty of disk space for a non-compressed text file.

Worse, the first bytes of an EPUB file, PK\3\4, (the magic number) are the same as the bytes of a ZIP file. So the only way to tell from the other is to see if the extension is a .ZIP extension.


**Solution** is to test the extension

## Finding HTML files, problem #1: encoding

The EPUB file includes chapters; the chapters in turn say what "html file" they refer to. To display them (and this
is the normal path), you just look up the string that in the table of contents against the list of HTML
files actually present in the EPUB. That is, the chapter includes a key, then you look up the key in the 
HTML dictionary.

**Except** that the chapter key is an escaped HTML fragment, and the dictionary of HTML is unescaped. The 
chapter key might be "file%20space.xhml" and then the actual HTML is located in "file space.xhml".

**Solution**: the key is run through Uri.UnescapeDataString first

## Finding HTML files, problem #2: just plain wrong

Thank you, United Nations, for all the work you do. But some of your EPUB files are plain weird. Example: you publish
a perfectly good book about accessibility. Inside, the HTML files are named think like Test/FrontCover.html. But
when you reference them in the Chapter list, you reference them like this: "../Text/FrontCover.html". The starting
"../" is not only a waste, but it means that the key lookup fails.

**Solution**: after a key isn't found, I check to see if it starts with "../". If so, I do a lookup without the "../"

**Except** that of Clocks and Time also asks for the wrong files, in a subtly surprising way. The image file is listed with an absolute path like /OEBPS/images/bk978-1-6817-4096-6ch5f1_online.jpg. There's also an Href path like images/bk978-1-6817-4096-6ch5f1_online.jpg. Before I only looked at the absolute path; now I match either one. This leads (like make of these solutions!) to a surprise security issue: a malicious ebook could have both the absolute path and the href point to different files. One reader program might read the file one way, and another might read it another way. 

## CSS? We don't need no stinking CSS! (AKA, the Beetle problem)

The United States Department of Agriculture (USDA) publishes some actually fairly gross books about beetle 
infestations. Until this set of EPUBs came my way, I happily ignored the CSS file that the EPUB books held:
after all, the books should just be normal HTML, and until the USDA files were found, every EPUB book was
perfectly understandable.

**Except** those pesky USDA books. Those were entirely unreadable: the fonts were tiny, but the spacing between
the words was enormous, so that the books were mostly blank with a scattering of little black dots. Which is
actually a little be appropos: the books looked a bit like they'd been chewed on by agricultural pests.

**Solution** actually read the CSS files. I reused the code that finds images, updating it to modify the CSS links,
so that the CSS file appear to be exernal and can be replaced. Result is fabulous; the pesky pests are displayed
in full color glory.

## Regex and HTML? What could go wrong?

As mentioned before, the <img> tags and css links are rewritten to be full "external" links; this is just so 
that the code gets notified that some file is needed and can be supplied. The original code simply looked for 
<img src="..." /> and replaced it with <img src="http://example.com/books/?... />. 

**Except** that books include important information after the src="" -- like, they include CSS style information.
And then include stuff before the src="..." as well, and that needs to be preserved.

**Solution** the regexs are way more complicated. At this point, I'm just good enough at Regexs to write them;
I'm not smart enough to debug them. The instant they break, I'm screwed.

## ID for you, and an ID for you...

Navigation is done by ID: the EPUB HTML is full of ids (e.g., pgebookid0001), often on every paragraph. The 
list of chapters in the EPUB includes an ID (which they call an Anchor, probably because it matches up 
neatly with the HTML anchor concept). When you click on  chapter, I get the id that corresponds with the chapter,
and then I just have to find the hunk of HTML that includes that id/anchor.

**Except** BAEN books, where every chapter is a different HTML file. There isn't an anchor, just a HTML.

**Solution** Massive restructuring of the code. I used to just pass a string which represented the anchor; now
I pass around a full BookLocation which might be a percentage, might be an id, and might be an HTML file name.

**Except** and this also made the code to sync the book and the chapter list to fail in the All Of Me, a
Small Town Romance (Bridemaids Club, book 1). 

**Solution** is to return a chapter "id" (anchor) as the filename
when appropriate, and then selecting that chapter. It's not pretty, but it works.

**Except** that How to Code in Go includes a bazillion duplicate id values. In particular, the code samples for each chapter have ids like *cb1-1*; these are duplicated everywhere. So when I see one and try to look it up in the table of contents, I will jump to the first chapter that includes a code sample.


**Solution** is to not use id numbers from span items.

**Except** that Baen books (the Free Stories 2013) has, at the start of each HTML chapter, a set of empty divs with ids like <div class="calibre2" id="calibre_pb_1">. These are all duplicated from one HTML to the next.

**Solution** is that when searching for the chapter an ID is in (e.g., because you scrolled, and are looking for the chapter that contains the ID that's at or near the top of the screen), always start with the current HTML page by index. 

**Except** for books that have anchor (a) tags with an id, but no href, and which are self-closed (like this: \<a id="" />). This is correct? maybe incorrect? HTML. Or incorrect XHTML? Regardless, WebView doesn't recognize that the tag is closed correctly. 

The way this shows up is that when I look at all tags, trying to find the first element on the page, I get multiple element with the same id. This often don't cause a problem, but do in O'Reilly Mintduino: the  chapter 3 section "Upload your first task" when clicked will instead show the previous chapter. That's because the section is found (yay!) but the next tag to be found is one of the duplicate tags, and it got started before the subsection. 

**Solution** is to find all anchor (a) tags that are self-closed and replace them with non-self-closed tags.


## Some chapters have no ids?

**Problem** some books (Introduction to Planetary Nebula) have chapters (like the Dedication) that don't include any id values at all. When the user navigates to one of these sections, the returned 'top' id will be the only id in the section: the artificially created 'uiLog' section. When the Chaper display attempts to go to the correct chapter, it can't find 'uiLog' anywhere and will select to the first chapter in the book (the title page)

**Solution** is that when we get a 'topid' report back in OnScriptNotify, if it's the uiLog, then just ignore it. This isn't perfectly ideal, but it's the best we can do.

## Linking chapter to html and back to chapter

When you click a chapter, it forces a jump to that chapter which in turn causes a navigation event which tries to set the chapter display. That navigation has a (hopefully) unique id (ahem, see previous problem) which we can match back to the chapter.

**Problem**  in the book Introduction to Planetary Nebula, the Author's Bio chaper doesn't have a unique id. When selected in the chapter view, the correct chapter is display in the viewer. But this triggers a navigation event which only knows about the HtmlIndex (which in this book is unique to the chapter) and the id (which is not unique to the chapter)

Instead of displaying the Author's Bio, the Preface (which is earlier and has a matching id) is instead highlighed.

**Solution** is fragile. We can't just pick the first chapter which matches the html href because (a) they don't match up correctly (the Href is xhtml/Author_biography.xhtml but the Chapter filename is just Author_biography.xhtml) and (b) there can easily be multiple chapters that use the same HTML file.

So the solution is to first see if there's an exact  match to the id. Then see if there's an HTML match to the provided HTML index (if one is even provided). Then fall back to just displaying the first chapter, because we have to display something. But mark this as an error.

## Nothing to see here but us scroll bars

The WebView has a simple, straightforward scrollbar. It works exactly like everyone would want a scroll bar to work

**Except** that it covers over the very right hand edge of the content. For many ebooks, that's OK because they
already have decent margins.

**Solution** change the <body> tag to be <body style='padding-right:40px'>. This works perfectly, and leaves a nice
neat margin!

## Scroll bars, part 2

The padding fix for the scroll bars works great: the ebooks have a bit of margin that pretty well is filled up
with the scroll bar. The content is fully visible, and the scroll bar is fully usable.

**Except** for the United Nations (UN) Asia-Pacifiic regional review of the 25th aniversary of the Beiing declaration.
That ebook doesn't have a <body> tag; it has a <body xml:lang="en" xmlns:xml="http://www.w3.org/XML/1998/namespace"> 
tag. 

**Solution** look for <body, not <body>. The padding will be first, and then any additional stuff is placed after.
This is actually a bit awkward, because I had also set up a DIV that's placed at the bottom of the screen, overlayed
on the content, and used for debugging. Now adding that div is more complicated.


## Almost-empty e-books

Project Gutenberg has a catalog with a ton of books, often in multiple formats. My Voracious Book Reader strongly
prefers the EBUB version, and so that's what's downloaded by default.

**Except** I'm looking at you, Sam Vaknin, author of numerous books in Project Gutenberg. You've got a ton of books published, 
and they all claim to have a corresponding EPUB file. But the actual EPUB just says "see some other file for the real 
ebook".

**Solution** public humiliation? Or just look at the sizes, and for epubs which are substantially smaller than a
corresponding RTF, simply IGNORE the book. But then I need to open the file with a different reader program.

## Chapter titles naturally need lots of white space

The EPUB format includes titles for chapters; each title get a simple string that is displayed to the user. These 
strings are sometimes plain ("Chapter 4") and sometimes include a full title.

**Except** the chapter title for Chronicles of Copper Boom (temptations) includes spaces before and after and includes carriage returns. 

**Solution** the titles are all string.Trim'd before being used.

## Title tags should be well-formed

Many EPUB HTML files include a \<title> tag. This is all well and good. 

**Except** that O'Reilly Mintduino has a blank, self-closed title tag (\<title/>) in the Copyright page (and no other). This is not to spec. WebView will take this malformed HTML and will simply place the entire chapter into the title, resulting in a page with no body and worse, with all of the injected JavaScript placed as the title. This in turn means that any attempt to call the JavaScript will throw an exception and fail.

**Solution** is to search for \<title/> and replace it with \<title>\</title>. See also the issues with anchor tags which were also found in the O'Reilly Mintduino book and in Chronicles of Copper Boom.


## Locating a place in the book by id

HTML elements (and remember, EPUB files are essentially somewhat weird HTML files) can include an id; a unique
value. There are methods to go through the page document model (DOM), looking at each tag, and based on the 
id, doing different things. Voracious Reader does this to synchronize the book (which the user can scroll)
and the chapter display: I look at each element in the DOM that has an id; the one closest to the top of the
viewport (the screen) has the id returned back. I then find the chapter whose chapter location is closest
to the id.

**Except** that Chronicles of Copper Boom, the Chilling Warmth is generated by OpenOffice and from there
using HtmlTidy, and somehow there's some calibre tags, too. In the end, there's a paragraph with an id
where the display is set to 'none'. This paragraph is actually in the middle of the book, but it's always
display at the top of the screen. When scrolling, it will somewhat randomly be selected as "the" correct 
location.

**Solution** the JavaScript that finds the best id now checks to see if the element is set to display:none.

## Using Entity Framework (EF) saves lots of times

The Voracious Reader uses Entity Framework (EF) (specifically, the EF Core version); this is a system in .NET
that lets me design my database in code, and the database is generated mostly automatically. When I create the 
initial database, for example, I can delete all of the existing book records and then recreate them and save 
out the new database

**Except** that EF can't handle 60K books being added at once. The SaveChanges() call will crash instead of working.

**Solution** is to save the database periodically on creation. This is gated by the NextIndexLogged value.

## Location a place in the book by id, part 2

Another weirdness with ebooks and tyring to navigate by id. When I scroll in *A Cruise In the Sky*, scrolling 
in chapter X **Desperate Needs andd a Bold Appeal**, there's a really weird glitch. Moving down the section,
everything works until about the middle, and then the Chapters tab moves to the last section in the book!
Scroll a little more, and it comes back!

**Cause** is that the ID I search for is "Page_113". I don't search for id="Page_113"; it's just a search for
that text. And in the last section is a reference to Page_113 as a link. Worse, this unwanted section comes
before the real section in the list, so it's preferred.

**Solution** is to always search for the id in quotes. Note that HTML allows both single and double quotes!

## Some books have no people associated with them

Almost every book has some person associated with it -- an author, usually, or at least a Dubious Author.
Commercial books have a publisher, and there are also translators, and illustrators. The library of congress
has an large list of ways that people can be associated with an ebook.

The Gutenberg Magna Carta, on the other hand, has no people associated with it at all. 

**Solution** Carefully mark the BestAuthor property as potentially returning null.
