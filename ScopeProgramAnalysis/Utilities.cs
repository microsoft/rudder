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

		/// <summary>
		/// Writes the given object instance to a binary file.
		/// <para>Object type (and all child types) must be decorated with the [Serializable] attribute.</para>
		/// <para>To prevent a variable from being serialized, decorate it with the [NonSerialized] attribute; cannot be applied to properties.</para>
		/// </summary>
		/// <typeparam name="T">The type of object being written to the XML file.</typeparam>
		/// <param name="filePath">The file path to write the object instance to.</param>
		/// <param name="objectToWrite">The object instance to write to the XML file.</param>
		/// <param name="append">If false the file will be overwritten if it already exists. If true the contents will be appended to the file.</param>
		public static void WriteToBinaryFile<T>(string filePath, T objectToWrite, bool append = false)
		{
			using (Stream stream = File.Open(filePath, append ? FileMode.Append : FileMode.Create))
			{
				var binaryFormatter = new System.Runtime.Serialization.Formatters.Binary.BinaryFormatter();
				binaryFormatter.Serialize(stream, objectToWrite);
			}
		}

		/// <summary>
		/// Reads an object instance from a binary file.
		/// </summary>
		/// <typeparam name="T">The type of object to read from the XML.</typeparam>
		/// <param name="filePath">The file path to read the object instance from.</param>
		/// <returns>Returns a new instance of the object read from the binary file.</returns>
		public static T ReadFromBinaryFile<T>(string filePath)
		{
			using (Stream stream = File.Open(filePath, FileMode.Open))
			{
				var binaryFormatter = new System.Runtime.Serialization.Formatters.Binary.BinaryFormatter();
				return (T)binaryFormatter.Deserialize(stream);
			}
		}
	}
}