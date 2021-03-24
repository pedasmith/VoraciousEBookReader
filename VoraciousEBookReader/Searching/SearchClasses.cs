using System;
using System.Collections.Generic;

namespace SimpleEpubReader.Searching
{
    public interface ISearch
    {
        bool Matches(IGetSearchArea searchObject); // Assume that the list always matches the SeachArea 
        // (e.g., we never pass in a title when the searecharea is a tag)
        void SetIsNegated(bool isNegated);

        bool MatchesFlat(string text);
    }

    public interface IGetSearchArea
    {
        IList<string> GetSearchArea(string inputArea);
    }

    public class SearchAtom : ISearch
    {
        public SearchAtom(string area, string searchFor)
        {
            SearchArea = area;
            SearchFor = searchFor;
        }

        /// <summary>
        /// Example: tag:#notdisney the searcharea is tag. Areas have to match; neither t:#notdisney nor tagtag:#notdisney will mawtch tag:#notdisney
        /// </summary>
        public string SearchArea { get; set; }
        /// <summary>
        /// Example: tag:#notdisney the searchfor is #notdisney
        /// </summary>
        public string SearchFor { get; set; }
        public enum SearchType { StringSearch }
        /// <summary>
        /// For now, there's only one search type
        /// </summary>
        public SearchType Type { get; set; }

        public bool IsNegated { get; set; } = false;
        public void SetIsNegated(bool isNegated)
        {
            IsNegated = isNegated;
        }
        public bool Matches(IGetSearchArea searchObject)
        {
            IList<string> inputList = searchObject.GetSearchArea(SearchArea); 
            // might be just one string, like title:apple or might be everything like brown
            int index;
            switch (Type)
            {
                // input="mars attacks" searchfor="MARS" --> found
                // input="" searchFor="MARS" --> not found
                // input="mars attack" searchFor="" --> found
                // input="" searchFor="" --> found
                default:
                case SearchType.StringSearch:
                    {
                        if (string.IsNullOrEmpty(SearchFor))
                        {
                            return true; // must return something...
                        }
                        else
                        {
                            // if any any item matches, return true/false based on isNegated
                            foreach (var item in inputList)
                            {
                                index = item.IndexOf(SearchFor, StringComparison.CurrentCultureIgnoreCase);
                                if (index >= 0)
                                {
                                    return IsNegated ? false : true;
                                }
                            }
                        }
                        // Didn't find it. Return false normally, but true if isnegated.
                        return IsNegated ? true : false;
                    }
            }
        }
        public bool MatchesFlat (string search) // is the giant gnarly per-book string in the index file
        {
            if (IsNegated) return true; // ignore all negated values

            if (string.IsNullOrEmpty(SearchFor))
            {
                return true; // must return something...
            }
            else
            {
                var index = search.IndexOf(SearchFor, StringComparison.CurrentCultureIgnoreCase);
                if (index >= 0)
                {
                    return true;
                }
            }
            return false;
        }

        private bool SearchAreaMatches(string inputArea)
        {
            if (string.IsNullOrEmpty(SearchArea)) return true;
            if (string.Compare(inputArea, SearchArea, true) == 0) return true;
            return false;
        }

        public override string ToString()
        {
            var strength = IsNegated ? "!" : "";
            if (string.IsNullOrEmpty(SearchArea)) return strength + SearchFor;
            return $"{strength}{SearchArea}:{SearchFor}";
        }
    }


    class SearchOperator : ISearch
    {
        public SearchOperator(Operator op, List<ISearch> operands)
        {
            if (op == Operator.Invalid) op = Operator.And; // set to be the default search type.
            Op = op;
            foreach (var item in operands)
            {
                Operands.Add(item);
            }
        }

        public enum Operator { Invalid, And, Or }
        public Operator Op { get; set; } = Operator.And;
        public bool IsNegated { get; set; } = false;
        public void SetIsNegated(bool isNegated)
        {
            IsNegated = isNegated;
        }
        public static Operator ConvertToOperator(char ch)
        {
            switch (ch)
            {
                case '&': return Operator.And;
                case '|': return Operator.Or;
                default: return Operator.Invalid;
            }
        }
        public List<ISearch> Operands { get; } = new List<ISearch>();
        public bool Matches(IGetSearchArea searchObject)
        {
            switch (Op)
            {
                default:
                case Operator.Invalid: // should not happen when parsing is correct.
                    return false;
                case Operator.And:
                    if (Operands.Count == 0) return true; // should not every happen gotta return something...
                    foreach (var item in Operands)
                    {
                        var value = item.Matches(searchObject);
                        if (!value) return IsNegated ? true : false;
                    }
                    return IsNegated ? false : true;
                case Operator.Or:
                    if (Operands.Count == 0) return true; // should not every happen gotta return something...
                    foreach (var item in Operands)
                    {
                        var value = item.Matches(searchObject);
                        if (value) return IsNegated ? false : true;
                    }
                    return IsNegated ? true : false;
            }
        }

        public bool MatchesFlat(string search) // is the giant gnarly per-book string in the index file
        {
            if (IsNegated) return true;
            switch (Op)
            {
                default:
                case Operator.Invalid: // should not happen when parsing is correct.
                    return false;
                case Operator.And:
                    if (Operands.Count == 0) return true; // should not every happen gotta return something...
                    foreach (var item in Operands)
                    {
                        var value = item.MatchesFlat(search);
                        if (!value) return false;
                    }
                    return true;
                case Operator.Or:
                    if (Operands.Count == 0) return true; // should not every happen gotta return something...
                    foreach (var item in Operands)
                    {
                        var value = item.MatchesFlat(search);
                        if (value) return true;
                    }
                    return false;
            }
        }

        public override string ToString()
        {
            if (Operands.Count == 0) return "[[null]]";

            var retval = (Operands[0] is SearchAtom) ? Operands[0].ToString() : $"( {Operands[0].ToString()} )";
            for (int i = 1; i < Operands.Count; i++)
            {
                switch (Op)
                {
                    case Operator.And: retval += " & "; break;
                    case Operator.Or: retval += " | "; break;
                }
                var opstr = (Operands[i] is SearchAtom) ? Operands[i].ToString() : $"( {Operands[i].ToString()} )";
                retval += opstr;
            }
            if (IsNegated) retval = $"!( {retval} )";
            return retval;
        }
    }
}
