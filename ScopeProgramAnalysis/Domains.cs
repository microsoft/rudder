using Backend.Analyses;
using Backend.Model;
using Backend.Utils;
using Model.ThreeAddressCode.Values;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ScopeProgramAnalysis
{

    public interface IAnalysisDomain<T>
    {
        bool IsTop { get; }
        T Clone();
        bool LessEqual(T obj);
        bool Equals(T obj);
        T Join(T right);
    }

    public class DependencyPTGDomain: IAnalysisDomain<DependencyPTGDomain>
    {
        public DependencyDomain Dependencies { get; private set; }
        public PointsToGraph PTG { get; set; }

        public DependencyPTGDomain()
        {
            Dependencies = new DependencyDomain();
            PTG = new PointsToGraph();
        }
        public DependencyPTGDomain(DependencyDomain dependencies, PointsToGraph ptg)
        {
            Dependencies = dependencies;
            PTG = ptg;
        }
        
        public bool IsTop
        {
            get { return Dependencies.IsTop; }
        }

        public DependencyPTGDomain Clone()
        {
            var ptgClone = new PointsToGraph();
            ptgClone.Union(this.PTG);

            var result = new DependencyPTGDomain(this.Dependencies.Clone(), ptgClone);
            return result;
        }

        public DependencyPTGDomain Join(DependencyPTGDomain right)
        {
            var joinedDep = this.Dependencies.Join(right.Dependencies);

            var joinedPTG = this.PTG;
            joinedPTG.Union(right.PTG);

            return new DependencyPTGDomain(joinedDep, joinedPTG);
        }

        public bool LessEqual(DependencyPTGDomain depPTG)
        {
            var leqDep = this.Dependencies.LessEqual(depPTG.Dependencies);
            var leqPTG = this.PTG.Equals(depPTG.PTG);
            return leqDep && leqDep;
        }
        public bool Equals(DependencyPTGDomain depPTG)
        {
            return LessEqual(depPTG) && depPTG.LessEqual(this);
        }

        public void CopyTraceables(IVariable destination, IVariable source)
        {
            HashSet<Traceable> union = GetTraceables(source);
            this.Dependencies.A2_Variables[destination] = union;
        }

        public void AssignTraceables(IVariable destination, IEnumerable<Traceable> traceables)
        {
            this.Dependencies.A2_Variables[destination] = new HashSet<Traceable>(traceables);
        }

        public void AddTraceables(IVariable destination, IVariable source)
        {
            HashSet<Traceable> traceables = GetTraceables(source);
            this.Dependencies.A2_Variables.AddRange(destination, traceables);
        }

        public void AddTraceables(IVariable destination, IEnumerable<Traceable> traceables)
        {
            this.Dependencies.A2_Variables.AddRange(destination, traceables);
        }

        public  HashSet<Traceable> GetTraceables(IVariable arg)
        {
            var union = new HashSet<Traceable>();
            foreach (var argAlias in this.PTG.GetAliases(arg))
            {
                if (this.Dependencies.A2_Variables.ContainsKey(argAlias))
                {
                    union.UnionWith(this.Dependencies.A2_Variables[argAlias]);
                }
            }
            return union;
        }

        public HashSet<Traceable> GetOutputTraceables(IVariable arg)
        {
            var union = new HashSet<Traceable>();
            foreach (var argAlias in this.PTG.GetAliases(arg))
            {
                if (this.Dependencies.A4_Ouput.ContainsKey(argAlias))
                {
                    union.UnionWith(this.Dependencies.A4_Ouput[argAlias]);
                }
            }
            return union;
            //return this.Dependencies.A4_Ouput[arg];
        }


        public void AssignOutputTraceables(IVariable destination, IVariable source)
        {
            HashSet<Traceable> union = GetTraceables(source);
            this.Dependencies.A4_Ouput[destination] = union;
        }

        public void AssignOutputTraceables(IVariable destination, IEnumerable<Traceable> traceables)
        {
            this.Dependencies.A4_Ouput[destination] = new HashSet<Traceable>(traceables);
        }

        public void AddOutputTraceables(IVariable destination, IVariable source)
        {
            HashSet<Traceable> traceables = GetTraceables(source);
            this.Dependencies.A4_Ouput.AddRange(destination, traceables);
        }
        public void AddOutputTraceables(IVariable destination, IEnumerable<Traceable> traceables)
        {
            this.Dependencies.A4_Ouput.AddRange(destination, traceables);
        }

        public void SetTOP()
        {
            this.Dependencies.IsTop = true;
        }
        public override bool Equals(object obj)
        {
            var oth = obj as DependencyPTGDomain;
            return oth!= null && this.Dependencies.Equals(oth.Dependencies) && this.PTG.Equals(oth.PTG);
        }
        public override int GetHashCode()
        {
            return Dependencies.GetHashCode() + PTG.GetHashCode();
        }
        public override string ToString()
        {
            return Dependencies.ToString();
        }
    }

    public class DependencyDomain 
    {
        private bool isTop = false;
        public bool IsTop
        {
            get { return isTop; }
            internal set { isTop = value; }
        }

        public MapSet<IVariable, Traceable> A2_Variables { get; private set; }
        public MapSet<Location, Traceable> A3_Clousures { get; set; }

        public MapSet<IVariable, Traceable> A4_Ouput { get; private set; }

        public ISet<Traceable> A1_Escaping { get; set; }

        public ISet<IVariable> ControlVariables { get; set; }

        //public PointsToGraph PTG { get; set; }

        public DependencyDomain()
        {
            A2_Variables = new MapSet<IVariable, Traceable>();
            A3_Clousures = new MapSet<Location, Traceable>();
            A4_Ouput = new MapSet<IVariable, Traceable>();

            A1_Escaping = new HashSet<Traceable>();

            ControlVariables = new HashSet<IVariable>();

            IsTop = false;
        }

        private bool MapLessEqual<K, V>(MapSet<K, V> left, MapSet<K, V> right)
        {
            var result = false;
            if (!left.Keys.Except(right.Keys).Any())
            {
                return left.All(kv => kv.Value.IsSubsetOf(right[kv.Key]));
                // && left.Any(kv => kv.Value.IsProperSubsetOf(right[kv.Key]));
            }
            return result;
        }

        private bool MapEquals<K, V>(MapSet<K, V> left, MapSet<K, V> right)
        {
            var result = false;
            if (!left.Keys.Except(right.Keys).Any() && left.Keys.Count() == right.Keys.Count())
            {
                return left.All(kv => kv.Value.IsSubsetOf(right[kv.Key]))
                    && right.All(kv => kv.Value.IsSubsetOf(left[kv.Key]));
            }
            return result;
        }

        public bool LessEqual(object obj)
        {
            var oth = obj as DependencyDomain;
            if (oth.IsTop) return true;
            else if (this.isTop) return false;

            return oth != null
                && this.A1_Escaping.IsSubsetOf(oth.A1_Escaping)
                && MapLessEqual(A2_Variables, oth.A2_Variables)
                && MapLessEqual(A3_Clousures, oth.A3_Clousures)
                && MapLessEqual(A4_Ouput, oth.A4_Ouput)
                && ControlVariables.IsSubsetOf(oth.ControlVariables);
        }
        public override bool Equals(object obj)
        {
            // Add ControlVariables
            var oth = obj as DependencyDomain;

            if (oth.IsTop) return this.IsTop;
            return this.LessEqual(oth) && oth.LessEqual(this);
            //return oth != null
            //    && oth.A1_Escaping.IsProperSubsetOf(A1_Escaping)
            //    && MapEquals(oth.A2_Variables, A2_Variables)
            //    && MapEquals(oth.A3_Clousures, A3_Clousures)
            //    && MapEquals(oth.A4_Ouput, A4_Ouput)
            //    && oth.ControlVariables.IsSubsetOf(ControlVariables);
        }
        public override int GetHashCode()
        {
            // Add ControlVariables
            return A1_Escaping.GetHashCode()
                + A2_Variables.GetHashCode()
                + A3_Clousures.GetHashCode()
                + A4_Ouput.GetHashCode()
                + ControlVariables.GetHashCode();

        }
        public DependencyDomain Clone()
        {
            var result = new DependencyDomain();
            result.IsTop = this.IsTop;
            result.A1_Escaping = new HashSet<Traceable>(this.A1_Escaping);
            result.A2_Variables = new MapSet<IVariable, Traceable>(this.A2_Variables);
            result.A3_Clousures = new MapSet<Location, Traceable>(this.A3_Clousures);
            result.A4_Ouput = new MapSet<IVariable, Traceable>(this.A4_Ouput);
            result.ControlVariables = new HashSet<IVariable>(this.ControlVariables);
            return result;
        }
        public DependencyDomain Join(DependencyDomain right)
        {
            var result = new DependencyDomain();

            if (this.IsTop || right.IsTop)
            {
                result.IsTop = true;
            }
            else if (right.LessEqual(this))
            {
                result = this;
            }
            else if (this.LessEqual(right))
            {
                result = right;
            }
            else
            {
                result.IsTop = this.IsTop;
                result.A1_Escaping = new HashSet<Traceable>(this.A1_Escaping);
                result.A2_Variables = new MapSet<IVariable, Traceable>(this.A2_Variables);
                result.A3_Clousures = new MapSet<Location, Traceable>(this.A3_Clousures);
                result.A4_Ouput = new MapSet<IVariable, Traceable>(this.A4_Ouput);

                result.ControlVariables = new HashSet<IVariable>(this.ControlVariables);

                result.isTop = result.isTop || right.isTop;
                result.A1_Escaping.UnionWith(right.A1_Escaping);
                result.A2_Variables.UnionWith(right.A2_Variables);
                result.A3_Clousures.UnionWith(right.A3_Clousures);
                result.A4_Ouput.UnionWith(right.A4_Ouput);

                result.ControlVariables.UnionWith(right.ControlVariables);
            }
            return result;
        }

        public bool GreaterThan(DependencyDomain right)
        {
            if (this.IsTop && !right.IsTop)
                return true;
            var result = !this.LessEqual(right);
            return result; // this.Less(right);
        }
        public override string ToString()
        {
            var result = "";
            if (IsTop) return "__TOP__";
            result += "A3\n";
            foreach (var var in this.A3_Clousures.Keys)
            {
                result += String.Format(CultureInfo.InvariantCulture, "{0}:{1}\n", var, ToString(A3_Clousures[var]));
            }
            result += "A4\n";
            foreach (var var in this.A4_Ouput.Keys)
            {
                result += String.Format(CultureInfo.InvariantCulture, "({0}){1}= dep({2})\n", var, ToString(A2_Variables[var]), ToString(A4_Ouput[var]));
            }
            result += "Escape\n";
            result += ToString(A1_Escaping);

            return result;
        }
        private string ToString(ISet<Traceable> set)
        {
            var result = String.Join(",", set.Select(e => e.ToString()));
            return result;
        }
        public bool OldEquals(object obj)
        {
            // Add ControlVariables
            var oth = obj as DependencyDomain;
            return oth != null
                && oth.IsTop == this.IsTop
                && oth.A1_Escaping.SetEquals(A1_Escaping)
                && oth.A2_Variables.MapEquals(A2_Variables)
                && oth.A3_Clousures.MapEquals(A3_Clousures)
                && oth.A4_Ouput.MapEquals(A4_Ouput)
                && oth.ControlVariables.SetEquals(ControlVariables);

        }

    }

}
