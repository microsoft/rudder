using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ScopeAnalyzer.Interfaces
{
    /// <summary>
    /// Abstract domain for sets. Empty set is bottom, null is universe.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public abstract class SetDomain<T>
    {
        protected List<T> elements = new List<T>();


        public bool IsTop
        {
            get { return elements == null; }
        }

        public bool IsBottom
        {
            get { return elements != null && elements.Count == 0; }
        }

        public void Add(T c)
        {
            if (IsTop || Contains(c)) return;
            elements.Add(c);
        }

        public void AddRange(IEnumerable<T> cs)
        {
            if (IsTop) return;
            foreach (var c in cs) Add(c);         
        }

        public bool Contains(T c)
        {
            if (IsTop) return true;

            foreach (var e in elements)
            {
                if (c.Equals(e)) return true;
            }
            return false;
        }

        public bool Contains(IEnumerable<T> cs)
        {
            if (IsTop) return true;

            if (cs.Count() > Count) return false;

            foreach (var c in cs)
            {
                if (!Contains(c)) return false;
            }
            return true;
        }

        public bool Contains(SetDomain<T> other)
        {
            return Contains(other.Elements);
        }

        public IReadOnlyCollection<T> Elements
        {
            get { return elements.ToList().AsReadOnly(); }
        }

        public int Count
        {
            get { return elements.Count; }
        }

        public void SetTop()
        {
            elements = null;
        }

        public void Join(SetDomain<T> other)
        {
            if (other.IsTop) SetTop();
            if (!IsTop) AddRange(other.Elements);
        }


        public override string ToString()
        {
            if (IsTop) return "Top";
            if (IsBottom) return "Bottom";
            string summary = "Elements:";
            foreach (var e in elements)
            {
                summary += "\t" + e.ToString();
            }
            return summary;
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }

        public override bool Equals(object obj)
        {
            if (this == obj) return true;

            if (!(obj is SetDomain<T>)) return false;

            var other = obj as SetDomain<T>;

            if (IsTop && other.IsTop) return true;
            if (IsBottom && other.IsBottom) return true;
            if (IsTop || other.IsTop) return false;
            if (IsBottom || other.IsBottom) return false;

            if (Count != other.Count) return false;
            return Contains(other.Elements);
        }
    }
}
