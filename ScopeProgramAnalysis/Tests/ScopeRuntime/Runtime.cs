using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;

namespace ScopeRuntime
{
    public enum ColumnDataType
    {
    }
    public abstract class ColumnData
    {
        public virtual byte[] Binary { get; }
        public virtual void Set(byte[] o) { }
        public virtual string String { get; }
        public virtual void Set(string o) { }
        public virtual void UnsafeSet(string o) { }
        public virtual double Double { get; }
        public virtual void Set(double o) { }
        public virtual void UnsafeSet(double o) { }
        public virtual ulong ULong { get; }
        public virtual void Set(ulong o) { }
        public virtual void UnsafeSet(ulong o) { }
        public virtual int Integer { get; }
        public virtual void Set(int o) { }
        public virtual void UnsafeSet(int o) { }
        public virtual long Long { get; }
        public virtual void Set(long o) { }
        public virtual void UnsafeSet(long o) { }
        public virtual void CopyTo(ColumnData destination) { }
        public virtual object Value { get; }
        public virtual void Set(object o) { }
        public virtual void UnsafeSet(object o) { }
    }
    public class ColumnInfo
    {
        public ColumnInfo(string name, ColumnDataType type) { }
        public ColumnInfo(string name, string type) { }
        public ColumnInfo(string name, Type type) { }
        public ColumnInfo Source { get; set; }


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
        public bool Contains(string name) { return default(bool); }
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

        public abstract Schema Schema { get; }


    }

    public abstract class Processor : RowSet
    {
        public override IEnumerable<Row> Rows { get; }
        public override Schema Schema { get { return null; } }

        public abstract IEnumerable<Row> Process(RowSet input, Row outputRow, string[] args);
        public virtual Schema Produces(string[] requestedColumns, string[] args, Schema input) { return null; }
    }
    public abstract class Reducer : RowSet
    {
        public virtual bool IsBulk { get; }
        public virtual bool IsRecursive { get; }
        public override IEnumerable<Row> Rows { get; }
        public override Schema Schema { get { return null; } }

        public virtual Schema Produces(string[] requestedColumns, string[] args, Schema input) { return null; }
        public abstract IEnumerable<Row> Reduce(RowSet input, Row outputRow, string[] args);
    }
    public abstract class Combiner : RowSet
    {
        public virtual bool IsBulk { get; }
        public override IEnumerable<Row> Rows { get; }
        public override Schema Schema { get { return null; } }

        public virtual Schema Produces(string[] requestedColumns, string[] args, Schema leftSchema, string leftTable, Schema rightSchema, string rightTable) { return null; }
        public abstract System.Collections.Generic.IEnumerable<Row> Combine(RowSet left, RowSet right, Row outputRow, string[] args);
    }

    public abstract class ScopeArray<T> : IEnumerable<T>, IEnumerable
    {
        public abstract IEnumerator<T> GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator()
        {
            throw new NotImplementedException();
        }
        public abstract bool Contains(T t);
        public abstract int Count { get; }
        public abstract T this[int index] { get; }
    }

    public abstract class ScopeMap<K, V> : IEnumerable<KeyValuePair<K, V>>, IEnumerable
    {
        IEnumerator IEnumerable.GetEnumerator()
        {
            throw new NotImplementedException();
        }

        public abstract IEnumerator<KeyValuePair<K, V>> GetEnumerator();
        public abstract bool ContainsKey(K k);
        public abstract int Count { get; }
        public abstract V this[K key] { get; }
        public abstract ScopeArray<K> Keys { get; }
        public abstract ScopeArray<V> Values { get; }


    }

    public class M
    {
        public static void Main() { }
    }
}
