using System;
using System.Collections.Generic;
using System.Linq;

namespace SimpleEpubReader.Searching
{
    class SearchParser
    {
        List<ISearch> stack = new List<ISearch>();

        // abc ==> 
        public static ISearch Parse(string input)
        {
            var parser = new SearchParser();
            var (result, nextIndex) = parser.Parse(input, 0);
            return result;
        }

        static char[] Opcodes = { '&', '|', '(', ')' };

        private (ISearch result, int nextIndex) Parse (string input, int startIndex)
        {
            SearchOperator.Operator currop = SearchOperator.Operator.Invalid;
            if (startIndex >= input.Length)
            {
                return (null, input.Length);
            }

            // What's the next char?
            List<ISearch> searchList = new List<ISearch>();
            int nAtomInARow = 0;
            int i = startIndex;
            bool keepGoing = true;
            bool nextIsNegated = false;
            while (i<input.Length && keepGoing)
            {
                var beginValue = i; // track this; it's handy for debugging and forcing completion.
                i = SkipWhitespace(input, i);
                if (i >= input.Length)
                {
                    ; // there was nothing but whitespace
                }
                else
                {
                    var (atom, nexti) = ParseAtom(input, i);
                    nAtomInARow = atom == null ? 0 : nAtomInARow+1;
                    i = nexti;
                    if (atom == null && i >= input.Length)
                    {
                        // Is at end
                    }
                    else if (atom == null)
                    {
                        // Not at end. Must be an opcode or paren or something.
                        var ch = input[i];
                        var asop = SearchOperator.ConvertToOperator(ch);
                        switch (ch)
                        {
                            case '&':
                            case '|':
                                if (currop == SearchOperator.Operator.Invalid)
                                {
                                    // Haven't specified an operator yet; pick this one.
                                    currop = asop;
                                }
                                else if (asop == currop)
                                {
                                    // e.g. apple | brown | betty where they are all the same
                                    ; // don't have to do anything.
                                }
                                else // reduce!
                                {
                                    var expression = new SearchOperator(currop, searchList);
                                    expression.SetIsNegated(nextIsNegated);
                                    nextIsNegated = false;
                                    searchList.Clear();
                                    searchList.Add(expression);
                                    currop = asop;
                                }
                                i++; // we went forward a char.
                                break;

                            case ')':
                                keepGoing = false;
                                i++; // we went forward a char.
                                break;
                            case '(':
                                {
                                    // example apple (brown | betty) pie
                                    // do a little recursion.
                                    // the sub-Parse will swallow the terminating ')'
                                    var (expression, nextStartIndex) = Parse(input, i + 1);
                                    expression.SetIsNegated(nextIsNegated);
                                    nextIsNegated = false;
                                    searchList.Add(expression);

                                    i = nextStartIndex;
                                }
                                break;

                            case '+':
                                nextIsNegated = false; // Don't really do + thing, but will accept it in the input
                                i++;
                                break;

                            case '-':
                            case '!':
                                nextIsNegated = !nextIsNegated; // so that !!apple is the same as apple and !!!apple is !apple
                                i++;
                                break;

                            default:
                                // Unrecognized char. Move forward, because we should always move forward.
                                i++; // we went forward a char.
                                break;
                        }
                    }
                    else
                    {
                        // apple | brown betty needs to parse to (apple | brown ) & betty
                        // but don't forget that apple | brown | betty is also possible.
                        // there was no operator, so if the last operator wasn't an & then
                        // it's wrong to make a list
                        if (nAtomInARow >= 2 && currop != SearchOperator.Operator.And && currop != SearchOperator.Operator.Invalid) // another place where the default op is AND
                        {
                            var expression = new SearchOperator(currop, searchList);
                            searchList.Clear();
                            searchList.Add(expression);
                            currop = SearchOperator.Operator.Invalid;
                            searchList.Add(atom);
                            atom.SetIsNegated(nextIsNegated);
                            nextIsNegated = false;
                        }
                        else
                        {
                            atom.SetIsNegated(nextIsNegated);
                            nextIsNegated = false;
                            searchList.Add(atom);
                        }
                    }
                }

                if (i <= beginValue) // it seriously can never go backwards :-)
                {
                    // went through a loop, but didn't increment? That's a bug!
                    NoteError($"ERROR: didn't increment at {i} in {input}");
                    i = input.Length;
                }
            }

            // Just one? return it.
            // Else, make an AND with everything.
            // Return 'i', the index and not intput.Length in order to handle recursive calls.
            switch (searchList.Count)
            {
                case 0: // got nothing.
                    return (null, i);
                case 1: // got just one
                    return (searchList[0], i);
                default:
                    var and = new SearchOperator(currop, searchList); // will handle a default value for currop.
                    return (and, i);
            }
        }

        private (ISearch result, int nextIndex) ParseAtom(string input, int startIndex)
        {
            var currtag = "";
            var currstring = "";
            var gotDQuote = false; // so you can do title:"mars attacks". can also do title:mar"s att"acks and it does the same thing.
            for (int i = startIndex; i < input.Length; i++)
            {
                var ch = input[i];
                if (ch == '"')
                {
                    gotDQuote = !gotDQuote;
                }
                else if (gotDQuote) // when in quote mode, all quotes are ignored
                {
                    currstring = currstring + ch;
                }
                else if (char.IsWhiteSpace (ch) || Opcodes.Contains (ch))
                {
                    if (string.IsNullOrEmpty (currstring))
                    {
                        return (null, i);
                    }
                    else
                    {
                        return (new SearchAtom(currtag, currstring), i);
                    }
                }
                else if (ch == ':') // exaple title:apple author:reeve
                {
                    currtag = currstring;
                    currstring = "";
                }
                else if (ch == '!' || ch == '-' || ch == '+')
                {
                    // All of these end parsing an atom
                    if (string.IsNullOrEmpty(currstring))
                    {
                        return (null, i);
                    }
                    else
                    {
                        return (new SearchAtom(currtag, currstring), i);
                    }
                }
                else
                { 
                    currstring = currstring + ch;
                }
            }
            // If there's a currstring at the end, turn it into a SearchAtom
            if (currstring.Length > 0)
            {
                return (new SearchAtom(currtag, currstring), input.Length);
            }
            return (null, input.Length);
        }

        private int SkipWhitespace (string input, int startIndex)
        {
            for (int i = startIndex; i < input.Length; i++)
            {
                var ch = input[i];
                if (!char.IsWhiteSpace(ch))
                {
                    return i;
                }
            }
            return input.Length;
        }

        public static void NoteError(string err)
        {
            System.Diagnostics.Debug.WriteLine(err);
            ;
        }
    }
}
