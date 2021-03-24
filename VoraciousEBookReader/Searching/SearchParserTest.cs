using System;

namespace SimpleEpubReader.Searching
{
    static class SearchParserTest
    {
        public static int Test_Parser()
        {
            int nerror = 0;

            nerror += TestSimple();
            nerror += TestList();

            if (nerror > 0)
            {
                ; // A place to hang the debugger.
            }
            return nerror;
        }
        private static void NoteError(string err)
        {
            System.Diagnostics.Debug.WriteLine(err);
        }

        private static int TestList()
        {
            int nerror = 0;

            nerror += TestConvert("apple", "apple");
            nerror += TestConvert("apple brown", "apple & brown");
            nerror += TestConvert("apple brown betty", "apple & brown & betty");
            nerror += TestConvert("apple | brown | betty", "apple | brown | betty");
            nerror += TestConvert("apple | brown betty", "( apple | brown ) & betty");

            nerror += TestConvert("!apple", "!apple");
            nerror += TestConvert("!apple brown", "!apple & brown");
            nerror += TestConvert("apple !brown", "apple & !brown");
            nerror += TestConvert("apple !brown betty", "apple & !brown & betty");
            nerror += TestConvert("apple | !brown | betty", "apple | !brown | betty");
            nerror += TestConvert("apple !( brown | betty )", "apple & ( !( brown | betty ) )");

            nerror += TestConvert("apple ( brown | betty )", "apple & ( brown | betty )");
            nerror += TestConvert("apple ( brown | betty", "apple & ( brown | betty )"); // handle missing paren
            nerror += TestConvert("apple ( brown | betty )", "apple & ( brown | betty )");
            nerror += TestConvert("apple | ( brown & betty )", "apple | ( brown & betty )");
            nerror += TestConvert("apple ( crust ( brown | betty ) )", "apple & ( crust & ( brown | betty ) )");


            nerror += TestConvert("title:apple", "title:apple");
            nerror += TestConvert("title:apple pie", "title:apple & pie");
            nerror += TestConvert("title:apple pie author:brown", "title:apple & pie & author:brown");


            nerror += TestConvert("app\"le brown be\"tty", "apple brown betty");

            return nerror;
        }

        private static int TestConvert (string input, string expected)
        {
            int nerror = 0;
            var result = SearchParser.Parse(input);
            var actual = result.ToString();
            if (actual != expected)
            {
                nerror++;
                NoteError($"ERROR: Parse({input}) expected {expected} but got {actual}");
            }
            return nerror;
        }

        private static int TestSimple()
        {
            int nerror = 0;
            var searchstr = "apple";
            var result = SearchParser.Parse(searchstr);
            if (result == null)
            {
                NoteError($"Parse({searchstr}) should not return a null");
                nerror++;
            }
            else if (result is SearchAtom atom)
            {
                if (atom.SearchArea != "")
                {
                    NoteError($"Parse({searchstr}) SearchArea was {atom.SearchArea} but it should have been blank");
                    nerror++;
                }
                if (atom.SearchFor != "apple")
                {
                    NoteError($"Parse({searchstr}) SearchFor was {atom.SearchFor} but it should have been apple");
                    nerror++;
                }
            }
            else
            {
                NoteError($"Parse({searchstr}) should have return an atom");
                nerror++;
            }
            return nerror;
        }
    }
}
