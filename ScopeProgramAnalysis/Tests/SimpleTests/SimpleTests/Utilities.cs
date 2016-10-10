using Microsoft.VisualStudio.TestTools.UnitTesting;
using static ScopeProgramAnalysis.ScopeProgramAnalysis;
using CodeUnderTest;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis.Sarif;
using System.Text.RegularExpressions;
using System;
using Backend.Analyses;
using static ScopeProgramAnalysis.Util;

namespace SimpleTests
{
    public static class Util
    {
        public static bool ColumnDependsOn(this Run r, string outputColumn, params string[] dependencies)
        {
            var column = r.Results.Where(result => result.Id == "SingleColumn" && ColumnNameMatches(outputColumn, result.GetProperty("column"))).FirstOrDefault();
            if (column == null) return false;
            var dataDependencies = column.GetProperty<List<string>>("data depends");
            if (dependencies.Length != dataDependencies.Count) return false;
            dataDependencies.Sort();
            var orderedDependencies = dependencies.OrderBy(d => d);
            return SetEqual(dependencies, dataDependencies, (x, y) => ColumnNameMatches(x, y) || x.Equals(y));
        }
        public static bool RunIsTop(this Run r)
        {
            if (r.Results.Count != 2) return false;
            var topColumn = r.Results.Where(result => result.Id == "SingleColumn" && result.GetProperty("column").Equals("_TOP_")).FirstOrDefault();
            if (topColumn == null) return false;
            string top;
            if (!topColumn.TryGetProperty("depends", out top)) return false;
            if (top != "_TOP_") return false;

            var summary = r.Results.Where(result => result.Id == "Summary").FirstOrDefault();
            if (summary == null) return false;
            List<string> cols;
            if (!summary.TryGetProperty<List<string>>("Inputs", out cols)) return false;
            if (cols.Count != 1) return false;
            if (cols[0] != "_TOP_") return false;

            if (!summary.TryGetProperty<List<string>>("Outputs", out cols)) return false;
            if (cols.Count != 1) return false;
            if (cols[0] != "_TOP_") return false;
            return true;
        }
        public static bool Inputs(this Run r, params string[] columnNames)
        {
            return Summary(r, "Inputs", columnNames);
        }
        public static bool Outputs(this Run r, params string[] columnNames)
        {
            return Summary(r, "Outputs", columnNames);
        }

        public static bool Summary(this Run r, string table, params string[] columnNames)
        {
            var summary = r.Results.Where(result => result.Id == "Summary").FirstOrDefault();
            if (summary == null) return false;
            List<string> cols;
            if (!summary.TryGetProperty<List<string>>(table, out cols)) return false;
            if (columnNames.Length != cols.Count) return false;
            var orderedcolumnNames = columnNames.OrderBy(d => d);
            cols.Sort();
            for (int i = 0; i < columnNames.Length; i++)
            {
                var requiredColumn = orderedcolumnNames.ElementAt(i);
                var inferredColumn = cols[i];
                if (!ColumnNameMatches(requiredColumn, inferredColumn))
                    return false;
            }
            return true;
        }

        public static bool BothAnalysesAgree(this Run r)
        {
            var summary = r.Results.Where(result => result.Id == "Summary").FirstOrDefault();
            if (summary == null) return true; // Then nothing to compare
            bool agreement;
            // If there result isn't TOP, then there is a comparison already put into the results
            if (summary.TryGetProperty("Comparison", out agreement)) return agreement;
            // Otherwise, make sure that the Inputs and Outputs are TOP and that the BagOColumns is TOP.
            string bagOColumnsResult;
            if (!summary.TryGetProperty("BagOColumns", out bagOColumnsResult)) return false;
            if (bagOColumnsResult == "Column information unknown.")
            {
                List<string> cols;
                if (!summary.TryGetProperty<List<string>>("Inputs", out cols)) return false;
                if (1 != cols.Count) return false;
                if (cols[0] != "_TOP_") return false;
                if (!summary.TryGetProperty<List<string>>("Outputs", out cols)) return false;
                if (1 != cols.Count) return false;
                if (cols[0] != "_TOP_") return false;
            }

            return true;
        }


    }
}