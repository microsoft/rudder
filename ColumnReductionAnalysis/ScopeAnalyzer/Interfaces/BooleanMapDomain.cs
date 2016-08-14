using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Backend.ThreeAddressCode.Values;

namespace ScopeAnalyzer.Interfaces
{
    public abstract class BooleanMapDomain<Key>
    {
        protected Dictionary<Key, Boolean> mapping;

        public bool IsTop
        {
            get { return mapping.Values.All(b => b); }
        }

        public bool IsBottom
        {
            get { return mapping.Values.All(b => !b); }
        }

        public void SetTrue(Key v)
        {
            if (!mapping.ContainsKey(v)) throw new InvalidVarsDomainOperation("Key not in the domain!");
            mapping[v] = true;
        }

        public void SetFalse(Key v)
        {
            if (!mapping.ContainsKey(v)) throw new InvalidVarsDomainOperation("Key not in the domain!");
            mapping[v] = false;
        }


        public bool IsTrue(Key v)
        {
            if (!mapping.ContainsKey(v)) throw new InvalidVarsDomainOperation("Key not in the domain!");
            return mapping[v];
        }

        public void SetAllTrue()
        {
            for (int i = 0; i < mapping.Count; i++)
            {
                mapping[mapping.Keys.ElementAt(i)] = true;
            }
        }

        public int Count
        {
            get { return mapping.Count; }
        }

        public override bool Equals(object obj)
        {
            var other = obj as BooleanMapDomain<Key>;
            if (this.Count != other.Count) return false;

            foreach (var f in mapping.Keys)
            {
                if (this.IsTrue(f) != other.IsTrue(f)) return false;
            }

            return true;
        }

        public void Join(BooleanMapDomain<Key> vs)
        {
            if (this.Count != vs.Count) throw new InvalidVarsDomainOperation("Key not in the domain!");
            for (int i = 0; i < mapping.Keys.Count; i++)
            {
                var v = mapping.Keys.ElementAt(i);
                mapping[v] |= vs.IsTrue(v);
            }
        }

        public override string ToString()
        {
            string summary = String.Empty;
            foreach (var v in mapping.Keys)
            {
                summary += String.Format("\t{0}: {1}\n", v.ToString(), mapping[v].ToString());
            }
            return summary;
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }

        class InvalidVarsDomainOperation : Exception
        {
            public InvalidVarsDomainOperation(string message) : base(message) { }
        }
    }
}
