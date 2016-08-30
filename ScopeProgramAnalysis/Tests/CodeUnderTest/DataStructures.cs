using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;

namespace FakeRuntime
{
    public enum ColumnDataType
    {
    }
    public abstract class ColumnData
    {
        public virtual string String { get; }
        public virtual void Set(string o) { }
    }
    public class ColumnInfo
    {
        public ColumnInfo(string name, ColumnDataType type) { }
        public ColumnInfo(string name, string type) { }
        public ColumnInfo(string name, Type type) { }

    }
    public class Schema
    {
        public Schema() { }
        public Schema(string schema) { }
        public Schema(string[] schema) { }

        public IEnumerable<ColumnInfo> Columns { get; }
        public int Count { get; }
        public Collection<ColumnInfo> PartitioningSet { get; }
        public string Table { get; }

        public ColumnInfo this[int index] { get { return null; } }
        public int this[string name] { get { return default(int); } }

        public void Add(ColumnInfo columnInfo) { }
        public void AddRange(IEnumerable<ColumnInfo> columnInfos) { }
        public Schema Clone() { return null; }
        public Schema CloneWithSource() { return null; }
        public int IndexOf(string name) { return default(int); }
    }
    public class Row
    {
        protected ColumnData[] _columns;
        protected Schema _schema;

        public Row() { }
        public Row(Schema schema) { }
        public Row(Schema schema, ColumnData[] columns) { }

        public ColumnData[] Columns { get; }
        public int Count { get; }
        public Schema Schema { get; }
        public virtual int Size { get; }

        public virtual ColumnData this[int index] { get { return null; } set { } }
        public ColumnData this[string tag] { get { return null; } }

        public void CopyTo(Row destination) { }

    }

    public abstract class RowSet
    {
        protected Row _outputRow;
        protected Schema _outputSchema;

        protected RowSet() { }

        public abstract IEnumerable<Row> Rows { get; }

    }

    public abstract class Processor : RowSet
    {
        public override IEnumerable<Row> Rows { get; }

        public abstract IEnumerable<Row> Process(RowSet input, Row outputRow, string[] args);
        public virtual Schema Produces(string[] requestedColumns, string[] args, Schema input) { return null; }
    }
    public abstract class Reducer : RowSet
    {
        public virtual bool IsBulk { get; }
        public virtual bool IsRecursive { get; }
        public override IEnumerable<Row> Rows { get; }

        public virtual Schema Produces(string[] requestedColumns, string[] args, Schema input) { return null; }
        public abstract IEnumerable<Row> Reduce(RowSet input, Row outputRow, string[] args);
    }
}
