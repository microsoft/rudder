using Backend.Analyses;
using Backend.Model;
using Backend.Utils;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Backend.ThreeAddressCode.Values;
using Microsoft.Cci;
using ScopeProgramAnalysis.Framework;

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
		public RangeDomain IteratorState { get; set; }
		public RangeDomain BlockState { get; set; }

		public DependencyDomain Dependencies { get; private set; }
        public SimplePointsToGraph PTG { get; set; }

        public DependencyPTGDomain()
        {
            Dependencies = new DependencyDomain();
            PTG = new SimplePointsToGraph();

			IteratorState = RangeDomain.BOTTOM;  // new RangeDomain(-1);
			BlockState =  RangeDomain.BOTTOM;

		}
        public DependencyPTGDomain(DependencyDomain dependencies, SimplePointsToGraph ptg)
        {
            Dependencies = dependencies;
            PTG = ptg;
			IteratorState = RangeDomain.BOTTOM; // new RangeDomain(-1);
			BlockState = RangeDomain.BOTTOM;
		}

		public DependencyPTGDomain(DependencyDomain dependencies, SimplePointsToGraph ptg, RangeDomain itState, RangeDomain blockState)
		{
			Dependencies = dependencies;
			PTG = ptg;
			IteratorState = itState;
			BlockState = blockState;
		}

		public bool IsTop
        {
            get { return Dependencies.IsTop; }
        }

        public DependencyPTGDomain Clone()
        {
            //var ptgClone = new SimplePointsToGraph();
            //ptgClone.Union(this.PTG);
            //var ptgClone = this.PTG.Clone();
            var ptgClone = this.PTG;
            var result = new DependencyPTGDomain(this.Dependencies.Clone(), ptgClone, this.IteratorState, this.BlockState );
            return result;
        }

        public DependencyPTGDomain Join(DependencyPTGDomain right)
        {
            var joinedDep = this.Dependencies.Join(right.Dependencies);

            var joinedPTG = this.PTG.Join(right.PTG);
			//if (!this.IteratorState.Equals(right.IteratorState))
			//{
			//}
            return new DependencyPTGDomain(joinedDep, joinedPTG, this.IteratorState.Join(right.IteratorState), this.BlockState.Join(right.BlockState));
        }

        public bool LessEqual(DependencyPTGDomain depPTG)
        {
            var leqDep = this.Dependencies.LessEqual(depPTG.Dependencies);
            var leqPTG = this.PTG.GraphLessEquals(depPTG.PTG);
            return leqDep && leqPTG;
        }
        public bool Equals(DependencyPTGDomain depPTG)
        {
            return LessEqual(depPTG) && depPTG.LessEqual(this);
        }

        public void CopyTraceables(IVariable destination, IVariable source)
        {
            AssignTraceables(destination, GetTraceables(source));
        }

        public void AssignTraceables(IVariable destination, IEnumerable<Traceable> traceables)
        {
            // TODO: In the future only assign A2_Variable to Scalaras
            //if (destination.Type != null && !destination.Type.IsClassOrStruct())
            {
                this.Dependencies.A2_Variables[destination] = new HashSet<Traceable>(traceables);
            }

            if (destination.Type!=null && destination.Type.IsClassOrStruct())
            {
                foreach (var targetNode in PTG.GetTargets(destination))
                {
                    if (targetNode != SimplePointsToGraph.NullNode)
                        //this.Dependencies.A2_References[targetNode] = new HashSet<Traceable>(traceables);
                        this.Dependencies.A2_References.AddRange(targetNode,new HashSet<Traceable>(traceables));
                }
            }

        }

        public void AddTraceables(IVariable destination, IVariable source)
        {
            HashSet<Traceable> traceables = GetTraceables(source);
            AddTraceables(destination, traceables);
        }

        public void AddTraceables(IVariable destination, IEnumerable<Traceable> traceables)
        {
            // TODO: In the future only assign A2_Variable to Scalaras
            //if (!destination.Type.IsClassOrStruct())
            {
                this.Dependencies.A2_Variables.AddRange(destination, traceables);
            }

            if (destination.Type.IsClassOrStruct())
            {
                foreach (var targetNode in PTG.GetTargets(destination))
                {
                    if(targetNode!=SimplePointsToGraph.NullNode)
                        this.Dependencies.A2_References.AddRange(targetNode, traceables);
                }
            }
        }

        public bool AddHeapTraceables(IVariable destination, IFieldReference field, IVariable source)
        {
            return AddHeapTraceables(destination, field, GetTraceables(source));
        }

        public bool AddHeapTraceables(IVariable destination, IFieldReference field,  IEnumerable<Traceable> traceables)
        {
            var nodes = PTG.GetTargets(destination);
            if (nodes.Any())
            {
                // This should be only for scalars
                foreach (var SimplePTGNode in nodes)
                {
                    if (SimplePTGNode != SimplePointsToGraph.NullNode)
                        AddHeapTraceables(SimplePTGNode, field, traceables);
                }
            }
            else
            {
                return false;
            }
            return true;
        }

        public void AddHeapTraceables(SimplePTGNode SimplePTGNode, IFieldReference field, IEnumerable<Traceable> traceables)
        {

            // If it is a scalar field we modify A3_Field
            if (!field.Type.IsClassOrStruct())
            {
                var location = new Location(SimplePTGNode, field);
                //this.Dependencies.A3_Fields[location] = new HashSet<Traceable>(traceables);
                // TODO:  a weak update
                this.Dependencies.A3_Fields.AddRange(location, traceables);
            }

            // This should be only for references
            // we modify A2_Refs(n) where n \in PT(SimplePTGNode, field)
            if (field.Type.IsClassOrStruct())
            {
                var targets = PTG.GetTargets(SimplePTGNode, field);// .Except(new HashSet<SimplePTGNode>() { SimplePointsToGraph.NullNode } );
                if (targets.Any())
                {
                    foreach (var targetNode in targets)
                    {
                        if (targetNode != SimplePointsToGraph.NullNode)
                        {
                            // TODO: Change for Add
                            this.Dependencies.A2_References.AddRange(targetNode, traceables);
                            //this.Dependencies.A2_References[targetNode] = new HashSet<Traceable>(traceables);
                        }
                    }
                }
                else
                { }
            }


        }


        public bool AssignHeapTraceables(IVariable destination, IFieldReference field, IVariable source)
        {
            return AssignHeapTraceables(destination, field, GetTraceables(source));
        }

        public bool AssignHeapTraceables(IVariable destination, IFieldReference field, IEnumerable<Traceable> traceables)
        {
            var nodes = PTG.GetTargets(destination);
            if (nodes.Any())
            {
                // This should be only for scalars
                foreach (var SimplePTGNode in nodes)
                {
                    if (SimplePTGNode != SimplePointsToGraph.NullNode)
                        AssignHeapTraceables(SimplePTGNode, field, traceables);
                }
            }
            else
            {
                return false;
            }
            return true;
        }

        public void AssignHeapTraceables(SimplePTGNode SimplePTGNode, IFieldReference field, IEnumerable<Traceable> traceables)
        {

            var newTraceables = new HashSet<Traceable>(traceables);
            // If it is a scalar field we modify A3_Field
            if (!field.Type.IsClassOrStruct())
            {
                var location = new Location(SimplePTGNode, field);
                //this.Dependencies.A3_Fields[location] = new HashSet<Traceable>(traceables);
                // TODO:  a weak update
                this.Dependencies.A3_Fields[location]= newTraceables;
            }

            // This should be only for references
            // we modify A2_Refs(n) where n \in PT(SimplePTGNode, field)
            if (field.Type.IsClassOrStruct())
            {
                var targets = PTG.GetTargets(SimplePTGNode, field);// .Except(new HashSet<SimplePTGNode>() { SimplePointsToGraph.NullNode } );
                if (targets.Any())
                {
                    foreach (var targetNode in targets)
                    {
                        if (targetNode != SimplePointsToGraph.NullNode)
                        {
                            // TODO: Change for Add
                            this.Dependencies.A2_References[targetNode] = newTraceables;
                            //this.Dependencies.A2_References[targetNode] = new HashSet<Traceable>(traceables);
                        }
                    }
                }
                else
                { }
            }


        }



        public IEnumerable<Traceable> GetHeapTraceables(IVariable arg, IFieldReference field)
        {
            var result = new HashSet<Traceable>();
            var nodes = PTG.GetTargets(arg); // , field);
            foreach (var node in nodes)
            {
                result.UnionWith(GetHeapTraceables(node,field));
            }
            return result;
        }

        public IEnumerable<Traceable> GetHeapTraceables(SimplePTGNode node, IFieldReference field)
        {
            var location = new Location(node, field);
            var originalTraceables = GetLocationTraceables(location);

            var traceables = new HashSet<Traceable>();


            if (field.Type.IsClassOrStruct())
            {
                foreach (var target in PTG.GetTargets(node, field))
                {
                    if (Dependencies.A2_References.ContainsKey(target) && target!=SimplePointsToGraph.NullNode)
                    {
                        traceables.UnionWith(Dependencies.A2_References[target]);
                    }
                }
            }

            if (traceables.Count > originalTraceables.Count)
            {
            }

            originalTraceables.UnionWith(traceables);

            return originalTraceables;
        }

        private HashSet<Traceable> GetLocationTraceables(Location location)
        {
            var result = new HashSet<Traceable>();
            if (!location.Field.Type.IsClassOrStruct())
            {
                if (Dependencies.A3_Fields.ContainsKey(location))
                    result.UnionWith(Dependencies.A3_Fields[location]);
            }
            //else
            //{ }
            return result;
        }


        /// <summary>
        ///  This one is just for debugging
        /// </summary>
        /// <param name="arg"></param>
        /// <returns></returns>
        public IEnumerable<Tuple<IFieldReference, IEnumerable<Traceable>>> GetHeapTraceables(IVariable arg)
        {
            var result = new HashSet<Tuple<IFieldReference, IEnumerable<Traceable>>>();
            var nodes = PTG.GetTargets(arg);
            foreach (var node in nodes)
            {
                var locations = this.PTG.GetTargets(node).Select(nf => new Location(node, nf.Key));
                result.UnionWith(locations.Select(location => new Tuple<IFieldReference, IEnumerable<Traceable>>(location.Field, GetLocationTraceables(location))));
            }
            return result;
        }



        public bool HasTraceables(IVariable arg)
        {
            return Dependencies.A2_Variables.ContainsKey(arg)
                && Dependencies.A2_Variables[arg].Count > 0;
        }


        public bool HasOutputTraceables(IVariable arg)
        {
            return Dependencies.A4_Ouput.ContainsKey(arg)
                && Dependencies.A4_Ouput[arg].Count > 0;
        }
        public bool HasOutputControlTraceables(IVariable arg)
        {
            return Dependencies.A4_Ouput_Control.ContainsKey(arg)
                && Dependencies.A4_Ouput_Control[arg].Count > 0;
        }

        public HashSet<Traceable> GetTraceables(IVariable arg)
        {
            var union = new HashSet<Traceable>();
            foreach (var argAlias in this.PTG.GetAliases(arg))
            {
                if (this.Dependencies.A2_Variables.ContainsKey(argAlias))
                {
                    union.UnionWith(this.Dependencies.A2_Variables[argAlias]);
                }
            }

            HashSet<Traceable> traceables = GetTraceablesForReferences(arg);

            if (traceables.Count > union.Count)
            { }
            union.UnionWith(traceables);

            return union;
        }

        private HashSet<Traceable> GetTraceablesForReferences(IVariable arg)
        {
            var traceables = new HashSet<Traceable>();

            if (arg.Type.IsClassOrStruct())
            {
                foreach (var SimplePTGNode in PTG.GetTargets(arg))
                {
                    if (SimplePTGNode!=SimplePointsToGraph.NullNode && Dependencies.A2_References.ContainsKey(SimplePTGNode))
                    {
                        traceables.UnionWith(Dependencies.A2_References[SimplePTGNode]);
                    }
                }
            }

            return traceables;
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

        public HashSet<Traceable> GetOutputControlTraceables(IVariable arg)
        {
            var union = new HashSet<Traceable>();
            foreach (var argAlias in this.PTG.GetAliases(arg))
            {
                if (this.Dependencies.A4_Ouput.ContainsKey(argAlias))
                {
                    union.UnionWith(this.Dependencies.A4_Ouput_Control[argAlias]);
                }
            }
            return union;
            //return this.Dependencies.A4_Ouput[arg];
        }

        public void AssignOutputTraceables(IVariable destination, IEnumerable<Traceable> traceables)
        {
            this.Dependencies.A4_Ouput[destination] = new HashSet<Traceable>(traceables);
        }

        public void AddOutputTraceables(IVariable destination, IEnumerable<Traceable> traceables)
        {
            this.Dependencies.A4_Ouput.AddRange(destination, traceables);
        }

        public void AssignOutputControlTraceables(IVariable destination, IEnumerable<Traceable> traceables)
        {
            this.Dependencies.A4_Ouput_Control[destination] = new HashSet<Traceable>(traceables);
        }

        public void AddOutputControlTraceables(IVariable destination, IEnumerable<Traceable> traceables)
        {
            this.Dependencies.A4_Ouput_Control.AddRange(destination, traceables);
        }

		/// <summary>
		/// Obtain a map from output columns to their input dependencies 
		/// </summary>
		/// <param name="outColumnControlMap"></param>
		/// <returns></returns>
		public MapSet<TraceableColumn, Traceable> ComputeOutputDependencies(out MapSet<TraceableColumn, Traceable> outColumnControlMap)
		{
			DependencyPTGDomain depAnalysisResult = this;
			MapSet<TraceableColumn, Traceable> outColumnMap = new MapSet<TraceableColumn, Traceable>();
			outColumnControlMap = new MapSet<TraceableColumn, Traceable>();
			foreach (var outColum in depAnalysisResult.Dependencies.A4_Ouput.Keys)
			{
				var outColumns = depAnalysisResult.GetTraceables(outColum).OfType<TraceableColumn>()
														 .Where(t => t.TableKind == ProtectedRowKind.Output);
				foreach (var column in outColumns)
				{
					if (!outColumnMap.ContainsKey(column))
					{
						outColumnMap.AddRange(column, depAnalysisResult.Dependencies.A4_Ouput[outColum]);
					}
					else
					{
						outColumnMap.AddRange(column, outColumnMap[column].Union(depAnalysisResult.Dependencies.A4_Ouput[outColum]));
					}
					if (!outColumnControlMap.ContainsKey(column))
					{
						outColumnControlMap.AddRange(column, depAnalysisResult.Dependencies.A4_Ouput_Control[outColum]);
					}
					else
					{
						outColumnControlMap.AddRange(column, outColumnControlMap[column].Union(depAnalysisResult.Dependencies.A4_Ouput_Control[outColum]));
					}
				}
			}
			return outColumnMap;
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


        public MapSet<SimplePTGNode, Traceable> A2_References { get; set; }

        public MapSet<IVariable, Traceable> A2_Variables { get; private set; }
        public MapSet<Location, Traceable> A3_Fields { get; set; }

        public MapSet<IVariable, Traceable> A4_Ouput { get; private set; }
        public MapSet<IVariable, Traceable> A4_Ouput_Control { get; private set; }

        public ISet<Traceable> A1_Escaping { get; set; }

        public ISet<IVariable> ControlVariables { get; set; }

        //public SimplePointsToGraph PTG { get; set; }

        public DependencyDomain()
        {
            A2_References = new MapSet<SimplePTGNode, Traceable>();

            A2_Variables = new MapSet<IVariable, Traceable>();
            A3_Fields = new MapSet<Location, Traceable>();
            A4_Ouput = new MapSet<IVariable, Traceable>();
            A4_Ouput_Control = new MapSet<IVariable, Traceable>();

            A1_Escaping = new HashSet<Traceable>();

            ControlVariables = new HashSet<IVariable>();

            IsTop = false;
        }

        private bool MapLessEqual<K, V>(MapSet<K, V> left, MapSet<K, V> right)
        {
            var result = false;
            var cleanLeft= left;
            var cleanRight = right;
            //var cleanLeft = new MapSet<K, V>();
            //foreach (var entry in left)
            //{
            //    if(entry.Value.Count>0)
            //       cleanLeft.Add(entry.Key,entry.Value);
            //}
            //var cleanRight = new MapSet<K, V>();
            //foreach (var entry in right)
            //{
            //    if (entry.Value.Count > 0)
            //        cleanRight.Add(entry.Key, entry.Value);
            //}
            if (cleanLeft.Count <= cleanRight.Count)
            {
                var keySetDiff = cleanLeft.Keys.Except(cleanRight.Keys);
                if (!keySetDiff.Any())
                {
                    return cleanLeft.All(kv => kv.Value.IsSubsetOf(cleanRight[kv.Key]));
                }
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
                && MapLessEqual(A2_References, oth.A2_References)
                && MapLessEqual(A2_Variables, oth.A2_Variables)
                && MapLessEqual(A3_Fields, oth.A3_Fields)
                && MapLessEqual(A4_Ouput, oth.A4_Ouput)
                && MapLessEqual(A4_Ouput_Control, oth.A4_Ouput_Control)
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
            //    && MapEquals(oth.A4_Ouput_Control, A4_Ouput_Control)
            //    && oth.ControlVariables.IsSubsetOf(ControlVariables);
        }
        public override int GetHashCode()
        {
            // Add ControlVariables
            return A1_Escaping.GetHashCode()
                + A2_Variables.GetHashCode()
                + A3_Fields.GetHashCode()
                + A4_Ouput.GetHashCode()
                + ControlVariables.GetHashCode();

        }
        public DependencyDomain Clone()
        {
            var result = new DependencyDomain();
            result.IsTop = this.IsTop;
            result.A2_References = new MapSet<SimplePTGNode, Traceable>(this.A2_References);

            result.A1_Escaping = new HashSet<Traceable>(this.A1_Escaping);
            result.A2_Variables = new MapSet<IVariable, Traceable>(this.A2_Variables);
            result.A3_Fields = new MapSet<Location, Traceable>(this.A3_Fields);
            result.A4_Ouput = new MapSet<IVariable, Traceable>(this.A4_Ouput);
            result.A4_Ouput_Control = new MapSet<IVariable, Traceable>(this.A4_Ouput_Control);
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
                result = this.Clone();
            }
            else if (this.LessEqual(right))
            {
                result = right.Clone();
            }
            else
            {
                result.IsTop = this.IsTop;
                result.A2_References = new MapSet<SimplePTGNode, Traceable>(this.A2_References);

                result.A1_Escaping = new HashSet<Traceable>(this.A1_Escaping);
                result.A2_Variables = new MapSet<IVariable, Traceable>(this.A2_Variables);
                result.A3_Fields = new MapSet<Location, Traceable>(this.A3_Fields);
                result.A4_Ouput = new MapSet<IVariable, Traceable>(this.A4_Ouput);
                result.A4_Ouput_Control = new MapSet<IVariable, Traceable>(this.A4_Ouput_Control);

                result.ControlVariables = new HashSet<IVariable>(this.ControlVariables);

                result.isTop = result.isTop || right.isTop;
                result.A2_References.UnionWith(right.A2_References);

                result.A1_Escaping.UnionWith(right.A1_Escaping);
                result.A2_Variables.UnionWith(right.A2_Variables);
                result.A3_Fields.UnionWith(right.A3_Fields);
                result.A4_Ouput.UnionWith(right.A4_Ouput);
                result.A4_Ouput_Control.UnionWith(right.A4_Ouput_Control);

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
            foreach (var var in this.A3_Fields.Keys)
            {
                result += String.Format(CultureInfo.InvariantCulture, "{0}:{1}\n", var, ToString(A3_Fields[var]));
            }
            result += "A4\n";
            foreach (var var in this.A4_Ouput.Keys)
            {
                var a2_value = "";
                if(A2_Variables.ContainsKey(var))
                {
                    a2_value = ToString(A2_Variables[var]);
                }
                result += String.Format(CultureInfo.InvariantCulture, "({0}){1}= dep({2})\n", var, a2_value , ToString(A4_Ouput[var]));
            }
            result += "A4_Control\n";
            foreach (var var in this.A4_Ouput_Control.Keys)
            {
                var a2_value = "";
                if (A2_Variables.ContainsKey(var))
                {
                    a2_value = ToString(A2_Variables[var]);
                }
                result += String.Format(CultureInfo.InvariantCulture, "({0}){1}= dep({2})\n", var, a2_value, ToString(A4_Ouput_Control[var]));
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
                && oth.A3_Fields.MapEquals(A3_Fields)
                && oth.A4_Ouput.MapEquals(A4_Ouput)
                && oth.A4_Ouput_Control.MapEquals(A4_Ouput_Control)
                && oth.ControlVariables.SetEquals(ControlVariables);
        }

    }

}
