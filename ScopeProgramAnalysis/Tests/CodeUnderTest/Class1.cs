using ScopeRuntime;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CodeUnderTest
{
    public class CopyProcessor : Processor
    {
        public override Schema Produces(string[] requested_columns, string[] args, Schema input_schema)
        {
            var output_schema = input_schema.CloneWithSource();
            return output_schema;
        }

        public override IEnumerable<Row> Process(RowSet input_rowset, Row output_row, string[] args)
        {
            foreach (Row input_row in input_rowset.Rows)
            {
                input_row.CopyTo(output_row);
                yield return output_row;
            }
        }
    }

    public class ModifyExistingColumnProcessor : Processor
    {
        public override Schema Produces(string[] requested_columns, string[] args, Schema input_schema)
        {
            var output_schema = input_schema.Clone();
            return output_schema;
        }

        public override IEnumerable<Row> Process(RowSet input_rowset, Row output_row, string[] args)
        {
            foreach (Row input_row in input_rowset.Rows)
            {
                input_row.CopyTo(output_row);
                string market = input_row[0].String;
                output_row[0].Set("FOO" + market);
                yield return output_row;
            }
        }
    }

    public class AddOneColumnProcessor : Processor
    {
        public override Schema Produces(string[] requested_columns, string[] args, Schema input_schema)
        {
            var output_schema = input_schema.Clone();
            var newcol = new ColumnInfo("NewColumn", typeof(string));
            output_schema.Add(newcol);
            return output_schema;
        }

        public override IEnumerable<Row> Process(RowSet input_rowset, Row output_row, string[] args)
        {
            foreach (Row input_row in input_rowset.Rows)
            {
                input_row.CopyTo(output_row);
                string market = input_row[0].String;
                output_row[2].Set("FOO" + market);
                yield return output_row;
            }
        }
    }

    public class TestDictProcessor : Processor
    {
        public override Schema Produces(string[] requested_columns, string[] args, Schema input_schema)
        {
            var output_schema = input_schema.Clone();
            var newcol = new ColumnInfo("NewColumn", typeof(string));
            output_schema.Add(newcol);
            return output_schema;
        }

        public override IEnumerable<Row> Process(RowSet input_rowset, Row output_row, string[] args)
        {
            var dict = new Dictionary<int, string>();
            var count = 0;
            foreach (Row input_row in input_rowset.Rows)
            {
                string market = input_row[0].String;
                dict.Add(count++, market);
            }
            foreach (var value in dict.Values)
            {
                output_row[2].Set("FOO" + value);
                yield return output_row;
            }

        }
    }

    public class SubtypeOfCopyProcessor : CopyProcessor { }

    public class ProcessReturningMethodCall : Processor
    {
        public override Schema Produces(string[] requested_columns, string[] args, Schema input_schema)
        {
            var output_schema = input_schema.CloneWithSource();
            return output_schema;
        }

        private IEnumerable<Row> Foo() { return null; }

        public override IEnumerable<Row> Process(RowSet input_rowset, Row output_row, string[] args)
        {
            return Foo();
        }

    }
    public class TopN : Reducer
    {
        public override Schema Produces(string[] requested_columns, string[] args, Schema input_schema)
        {
            var output_schema = input_schema.CloneWithSource();
            return output_schema;
        }

        public override IEnumerable<Row> Reduce(RowSet input, Row output, string[] args)
        {
            int currentRowNumber = 0;
            int maxRowNumber = int.Parse(args[0]);
            foreach (Row current in input.Rows)
            {
                if (currentRowNumber >= maxRowNumber)
                {
                    break;
                }
                current.CopyTo(output);
                yield return output;
                currentRowNumber++;
            }
            yield break;
        }
    }
    public class AccumulateList : Reducer
    {
        public override Schema Produces(string[] requested_columns, string[] args, Schema input_schema)
        {
            var output_schema = input_schema.CloneWithSource();
            return output_schema;
        }

        public override IEnumerable<Row> Reduce(RowSet input, Row output, string[] args)
        {
            var ys = new List<int>();
            ulong x = 0;
            int line = 0;

            foreach (Row row in input.Rows)
            {
                if (0 == line)
                {
                    x = row["X"].ULong;
                }
                line++;
                int y = row["Y"].Integer;
                if (!ys.Contains(y))
                    ys.Add(y);
            }
            foreach (var y in ys)
            {
                output["X"].Set(x);
                output["Y"].Set(y);
                yield return output;
            }
        }
    }
    public class UseDictionary : Reducer
    {
        public override Schema Produces(string[] requested_columns, string[] args, Schema input_schema)
        {
            var output_schema = input_schema.CloneWithSource();
            return output_schema;
        }

        public class Record
        {
            public long X;
            public Record(long x) { this.X = x; }
        }
        public override IEnumerable<Row> Reduce(RowSet input, Row output, string[] args)
        {
            var d = new Dictionary<int, Record>();
            foreach (Row current in input.Rows)
            {
                var r = new Record(current["X"].Long);
                d.Add(current["Y"].Integer, r);
            }
            foreach (var v in d.Values)
            {
                output["X"].Set(v.X);
                yield return output;
            }
            yield break;
        }
    }
    public class LastX : Reducer
    {
        public override Schema Produces(string[] columns, string[] args, Schema input)
        {
            var output_schema = input.CloneWithSource();
            return output_schema;
        }
        public override IEnumerable<Row> Reduce(RowSet input, Row output, string[] args)
        {
            double lastX = 0;
            foreach (Row row in input.Rows)
            {
                if (row["X"].Double != lastX)
                {
                    output["X"].Set(lastX);
                    lastX = row["X"].Double;
                    yield return output;
                }
            }
            output["X"].Set(lastX);
            yield return output;
        }
    }

    public class ConditionalSchemaWriteColumn : Reducer
    {
        public override Schema Produces(string[] columns, string[] args, Schema input)
        {
            var output_schema = input.CloneWithSource();
            return output_schema;
        }
        public override IEnumerable<Row> Reduce(RowSet input, Row output, string[] args)
        {
            double lastX = 0;
            foreach (Row row in input.Rows)
            {
                if (input.Schema.Contains("X"))
                {
                    output["X"].Set(row["X"].Double);
                }
            }
            output["X"].Set(lastX);
            yield return output;
        }
    }
    public class GenericProcessor<T> : Reducer
    {
        public override Schema Produces(string[] columns, string[] args, Schema input)
        {
            var output_schema = input.CloneWithSource();
            return output_schema;
        }
        public override IEnumerable<Row> Reduce(RowSet input, Row output, string[] args)
        {
            foreach (Row input_row in input.Rows)
            {
                input_row.CopyTo(output);
                yield return output;
            }
        }
    }
    public class SubtypeOfGenericProcessor : GenericProcessor<int> { }

    public class IterateOverColumns : Reducer
    {
        public override Schema Produces(string[] columns, string[] args, Schema input)
        {
            var output_schema = input.CloneWithSource();
            return output_schema;
        }
        public override IEnumerable<Row> Reduce(RowSet input, Row output, string[] args)
        {
            foreach (Row current in input.Rows)
            {
                for (int i = 0; i < input.Schema.Count; i++)
                {
                    current[i].CopyTo(output[i]);
                    yield return output;
                }
            }
            yield break;
        }
    }
    [Reads("X"), Writes("X", "Y")]
    public class ReadOnlyX : Processor
    {
        public override Schema Produces(string[] columns, string[] args, Schema input)
        {
            var output_schema = input.CloneWithSource();
            return output_schema;
        }
        public override IEnumerable<Row> Process(RowSet input, Row output, string[] args)
        {
            foreach (Row current in input.Rows)
            {
                output["X"].Set(current["X"].String);
                output["Y"].Set(3);
                yield return output;
            }
            yield break;
        }
    }
    public class CallMethodOnInputRow : Reducer
    {
        public class FilterChecker
        {
            public FilterChecker(string s) { }
            public bool CheckCondition(Row r) { return true; }
        }
        public override Schema Produces(string[] columns, string[] args, Schema input)
        {
            var output_schema = input.CloneWithSource();
            return output_schema;
        }
        public override IEnumerable<Row> Reduce(RowSet input, Row output, string[] args)
        {
            FilterChecker filterChecker = new FilterChecker((args.Length == 0) ? "" : args[0]);
            foreach (Row current in input.Rows)
            {
                current.CopyTo(output);
                if (filterChecker.CheckCondition(current))
                    yield return output;
            }
            yield break;
        }
    }

    public class ScopeMap01 : Reducer
    {
        public override Schema Produces(string[] columns, string[] args, Schema input)
        {
            var output_schema = input.Clone();
            var newcol = new ColumnInfo("ScopeMapColumn", typeof(string));
            output_schema.Add(newcol);
            return output_schema;
        }
        public override IEnumerable<Row> Reduce(RowSet input, Row output, string[] args)
        {
            foreach (Row current in input.Rows)
            {
                current.CopyTo(output);
                var scopeMap = current[0].Value as ScopeRuntime.ScopeMap<string, int>;
                if (scopeMap.ContainsKey("a"))
                {
                    var foo = scopeMap["a"];
                    output[1].Set(foo);
                }
                yield return output;
            }
            yield break;
        }

    }

    public class FirstRowReducer : Reducer
    {
        public override Schema Produces(string[] columns, string[] args, Schema input)
        {
            return input.CloneWithSource();
        }
        public override IEnumerable<Row> Reduce(RowSet input, Row output, string[] args)
        {
            using (var enumerator = input.Rows.GetEnumerator())
            {
                if (enumerator.MoveNext())
                {
                    Row current = enumerator.Current;
                    yield return current;
                }
            }
            yield break;
        }

    }
    public class RowCountReducer : Reducer
    {
        public override Schema Produces(string[] columns, string[] args, Schema input)
        {
            return input.CloneWithSource();
        }
        public override IEnumerable<Row> Reduce(RowSet input, Row output, string[] args)
        {
            long limit = long.Parse(args[0]);
            long currentCount = 0L;
            foreach (Row current in input.Rows)
            {
                currentCount += 1L;
                if (currentCount > limit)
                {
                    break;
                }
                yield return current;
            }
            yield break;
        }

    }
    [Reads("A"), Writes("OutputColumn")]
    public class ConditionalColumnReducer : Reducer
    {
        public override Schema Produces(string[] columns, string[] args, Schema input)
        {
            var s = new Schema();
            ColumnInfo columnInfo = new ColumnInfo("OutputColumn", typeof(string));
            if (input.Contains("A"))
            {
                columnInfo.Source = input[input.IndexOf("A")];
            }
            else
            {
                columnInfo.Source = input[input.IndexOf("B")];
            }
            s.Add(columnInfo);
            return s;
        }
        public override IEnumerable<Row> Reduce(RowSet input, Row output, string[] args)
        {
            var indexOfA = -1;
            var indexOfB = -1;
            if (input.Schema.Contains("A"))
            {
                indexOfA = input.Schema["A"];
            }
            else
            {
                indexOfB = input.Schema["B"];
            }
            foreach (Row current in input.Rows)
            {
                if (indexOfA >= 0)
                {
                    current[indexOfA].CopyTo(output[0]);
                }
                else
                {
                    current[indexOfB].CopyTo(output[0]);
                }
                yield return output;
            }
            yield break;
        }

    }

    [Reads("A"), Writes("A")]
    public class PassColumnValuesToMethodReducer : Reducer
    {
        public class SillyClass
        {
            public string AValue { get; set; }
        }
        public override Schema Produces(string[] columns, string[] args, Schema input)
        {
            var output_schema = input.CloneWithSource();
            return output_schema;
        }
        public override IEnumerable<Row> Reduce(RowSet input, Row output, string[] args)
        {
            var aValues = new List<SillyClass>();
            foreach (Row current in input.Rows)
            {
                var aValue = current["A"].String;
                aValues.Add(new SillyClass() { AValue = aValue, });
                output["A"].Set(aValue);
                yield return output;
            }
            yield break;
        }

    }
    public class ReturnRowReducer : Reducer
    {
        public override Schema Produces(string[] columns, string[] args, Schema input)
        {
            var output_schema = input.CloneWithSource();
            return output_schema;
        }
        public override IEnumerable<Row> Reduce(RowSet input, Row output, string[] args)
        {
            foreach (Row current in input.Rows)
            {
                 yield return current;
            }
            yield break;
        }
    }
}

