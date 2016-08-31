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

    public class ProjectOnlyOneColumnProcessor : Processor
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

}
