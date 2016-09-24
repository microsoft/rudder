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
}
