using System.Collections.Generic;
using System.Linq;
using System;
using Backend.Analyses;
using System.IO;

namespace ScopeProgramAnalysis
{
    public static class Util
    {

        /// <summary>
        /// Returns the index of the first element satisfying the predicate. Returns -1 if no element does.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="xs"></param>
        /// <param name="pred"></param>
        /// <returns></returns>
        public static int IndexOf<T>(this IEnumerable<T> xs, Func<T, bool> pred)
        {
            var i = 0;
            foreach (var x in xs)
            {
                if (pred(x)) return i;
                i++;
            }
            return -1;
        }
        /// <summary>
        /// Compares two sequences without respect to order, i.e., if they represent the same set of values
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <typeparam name="U"></typeparam>
        /// <param name="ts"></param>
        /// <param name="us"></param>
        /// <param name="pred"></param>
        /// <returns></returns>
        public static bool SetEqual<T, U>(IEnumerable<T> ts, IEnumerable<U> us, Func<T, U, bool> pred)
        {
            if (!ts.Any() && !us.Any()) return true;
            if (!ts.Any() && us.Any()) return false;
            var t = ts.First();
            ts = ts.Skip(1);
            if (!us.Any()) return false;
            var i = us.IndexOf(u => pred(t, u));
            if (i == -1) return false;
            us = us.Take(i).Concat(us.Skip(i + 1));
            return SetEqual(ts, us, pred);
        }
        /// <summary>
        /// Matches "X" with "Col(Input,X[0])" or "Col(Input,X)".
        /// Matches "0" with "Col(Input,X[0])" or "Col(Input,0)".
        /// </summary>
        /// <param name="columnNameOrNumber">A column name, e.g., "X"</param>
        /// <param name="columnProperty">A column represented in the analysis encoding, e.g., "Col(Input,X[0])"</param>
        /// <returns>True iff the <paramref name="columnNameOrNumber"/> is the same as the column name in the <paramref name="columnProperty"/></returns>
        public static bool ColumnNameMatches(string columnNameOrNumber, string columnProperty)
        {
            Column c;
            if (!Column.TryParse(columnProperty, out c)) return false;
            return ColumnNameMatches(columnNameOrNumber, c);
        }
        /// <summary>
        /// Matches "X" with "Col(Input,X[0])" or "Col(Input,X)".
        /// Matches "0" with "Col(Input,X[0])" or "Col(Input,0)".
        /// </summary>
        /// <param name="columnNameOrNumber">A column name, e.g., "X"</param>
        /// <param name="columnProperty">A column represented in the analysis encoding, e.g., "Col(Input,X[0])"</param>
        /// <returns>True iff the <paramref name="columnNameOrNumber"/> is the same as the column name in the <paramref name="columnProperty"/></returns>
        public static bool ColumnNameMatches(string columnNameOrNumber, Column c)
        {
            int position;
            if (Int32.TryParse(columnNameOrNumber, out position))
                return position == c.Position.LowerBound;
            return columnNameOrNumber == c.Name;
        }
	}
}