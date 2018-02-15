using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Backend.Model;
using Backend.ThreeAddressCode;
using Backend.Utils;
using System.Globalization;
using ScopeProgramAnalysis;
using static Backend.Analyses.SimplePointsToAnalysis;
using ScopeProgramAnalysis.Framework;
using Backend.Analyses;
using Backend.ThreeAddressCode.Values;
using Microsoft.Cci;
using Backend.ThreeAddressCode.Expressions;
using Backend.ThreeAddressCode.Instructions;
using Backend.Visitors;
using System.Text.RegularExpressions;
using RuntimeLoader;

namespace Backend.Analyses
{
    #region Dependency Analysis (based of SongTao paper)
    public abstract class Traceable
    {
        public string TableName { get; set; }
        public ProtectedRowKind TableKind;
        public Traceable(string name, ProtectedRowKind tableKind)
        {
            this.TableName = name;
            this.TableKind = tableKind;
        }
        public Traceable(Traceable table)
        {
            this.TableName = table.TableName;
            this.TableKind = table.TableKind;
        }

        public override bool Equals(object obj)
        {
            var oth = obj as Traceable;
            return oth!= null && oth.TableName.Equals(this.TableName)
                && oth.TableKind.Equals(this.TableKind);
        }
        public override int GetHashCode()
        {
            return TableName.GetHashCode()+TableKind.GetHashCode();
        }
        public override string ToString()
        {
            return TableName;
        }

    }


    public class Other : Traceable
    {
        public Other(string name) : base(name, ProtectedRowKind.Unknown)
        {
        }
    }
    public class TraceableTable: Traceable
    {
        private ProtectedRowNode node;

        public TraceableTable(ProtectedRowNode node) : base(node.KindToString(), node.RowKind)
        {
            this.node = node;
        }
        public override string ToString()
        {
            return String.Format(CultureInfo.InvariantCulture, "Table({0})", this.node.KindToString());
        }

    }

	public class TraceableJson : TraceableColumn
	{
		public TraceableColumn TColumn { get; private set; } 
		public TraceableJson(TraceableColumn tc) : base(tc.Table, tc.Column) // base("Json", ProtectedRowKind.Json)
		{
			this.TColumn = tc;
		}
		public override string ToString()
		{
			return String.Format(CultureInfo.InvariantCulture, "Json({0})", TColumn);
		}
	}

	public class TraceableJsonCollectionElement : TraceableJson
	{
		public TraceableJson TJson{ get; private set; }
		public TraceableJsonCollectionElement(TraceableJson tj) : base(tj.TColumn)
		{
			this.TJson = tj;
		}
		public override string ToString()
		{
			return String.Format(CultureInfo.InvariantCulture, "{0}.[*]", TJson.ToString());
		}
	}

	/// <summary>
	/// Representation of a column in a SCOPE table.
	/// 
	/// If the actual column is known, then this contains its name, position, and type.
	/// There might also be partial information, e.g., the name might be known, but not
	/// the position or type.
	/// 
	/// There is a special instance that represents *all* columns (e.g., if the schema
	/// is unknown and there is a call to Row.CopyTo) and a special instance that represents
	/// an unknown column (i.e., TOP).
	/// Every instance is exactly one of TOP, ALL, or known.
	/// 
	/// TODO: Should instances be immutable? 
	/// </summary>
	public class Column
    {
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Security", "CA2104:DoNotDeclareReadOnlyMutableReferenceTypes")]
        public static readonly Column TOP = new Column() { IsTOP = true };
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Security", "CA2104:DoNotDeclareReadOnlyMutableReferenceTypes")]
        public static readonly Column ALL = new Column() { IsAll = true };

        /// <summary>
        /// Null represents unknown name
        /// </summary>
        public string Name { get; private set; }
        /// <summary>
        /// Null represents unknown position
        /// </summary>
        public RangeDomain Position { get; private set; }
        /// <summary>
        /// Null represents unknown type
        /// </summary>
        public string Type { get; private set; }

        public virtual bool IsTOP { get; private set; }
        public virtual bool IsAll { get; private set; }

        private Column() { }
        
        public Column(string name, RangeDomain position, string type)
        {
            this.Name = name;
            this.Position = position;
            this.Type = type;
        }

        public override string ToString()
        {
            if (IsTOP)
                return "_TOP_";
            if (IsAll)
                return "_All_";
            if (Name == null && !Position.Equals(RangeDomain.BOTTOM))
                return this.Position.ToString();
            if (Name != null && Position.Equals(RangeDomain.BOTTOM))
                return this.Name;
            return String.Format("{0}[{1}]", this.Name, this.Position);
        }
        public override bool Equals(object obj)
        {
            var other = obj as Column;
            if (other == null) return false;
            return this.IsTOP == other.IsTOP && this.IsAll == other.IsAll
                && this.Name == other.Name && this.Position.Equals(other.Position) && this.Type == other.Type;
        }
        public override int GetHashCode()
        {
            uint hash = (uint)IsTOP.GetHashCode();
            hash = (hash << 2) | (hash >> 30);
            hash ^= (uint)IsAll.GetHashCode();
            if (Name != null)
            {
                hash = (hash << 2) | (hash >> 30);
                hash ^= (uint)Name.GetHashCode();
            }
            if (!Position.Equals(RangeDomain.BOTTOM))
            {
                hash = (hash << 2) | (hash >> 30);
                hash ^= (uint)Position.GetHashCode();
            }
            if (Type != null)
            {
                hash = (hash << 2) | (hash >> 30);
                hash ^= (uint)Type.GetHashCode();
            }
            return (int)hash;
        }

        public static bool TryParse(string s, out Column c)
        {
            c = null;
            if (String.IsNullOrWhiteSpace(s)) return false;
            var x = Regex.Match(s, @"Col\((\w+),(\w+)\[(\d+)\]\)");
            if (x.Success) {
                var tableName = x.Groups[1].Value;
                var columnNameInProperty = x.Groups[2].Value;
                var positionString = x.Groups[3].Value;
                int position;
                if (!Int32.TryParse(positionString, out position)) return false;
                c = new Column() { Name = columnNameInProperty, Position = new RangeDomain(position), };
                return true;
            }
            x = Regex.Match(s, @"Col\((\w+),(\d+)\)");
            if (x.Success)
            {
                var tableName = x.Groups[1].Value;
                var positionString = x.Groups[2].Value;
                int position;
                if (!Int32.TryParse(positionString, out position)) return false;
                c = new Column() { Position = new RangeDomain(position), };
                return true;
            }
            x = Regex.Match(s, @"Col\((\w+),(\w+)\)");
            if (x.Success)
            {
                var tableName = x.Groups[1].Value;
                var columnNameInProperty = x.Groups[2].Value;
                c = new Column() { Name = columnNameInProperty, };
                return true;
            }
            return false;
        }

    }

    public class Schema
    {
        public IEnumerable<Column> Columns { get; private set; }

        public Schema(IEnumerable<Column> columns)
        {
            this.Columns = columns;
        }
        public Column GetColumn(RangeDomain rd)
        {
            return this.Columns.Where(c => c.Position.Equals(rd)).FirstOrDefault();
        }
        public Column GetColumn(string name)
        {
            return this.Columns.Where(c => c.Name == name).FirstOrDefault();
        }


    }

    public class TraceableColumn : Traceable
    {
        public TraceableTable Table { get; private set; }
        public Column Column { get; private set; }
        public TraceableColumn(TraceableTable table,  Column column) : base(table)
        {
            this.Table = table;
            this.Column = column;
        }
        public override string ToString()
        {
            return String.Format(CultureInfo.InvariantCulture, "Col({0},{1})", TableName, Column);
        }
        public override bool Equals(object obj)
        {
            var oth = obj as TraceableColumn;
            return oth != null && oth.Table.Equals(this.Table) 
                               && oth.Column.Equals(this.Column) 
                               && base.Equals(oth);
        }
        public override int GetHashCode()
        {
            return base.GetHashCode() + Column.GetHashCode()  + Table.GetHashCode();
        }
    }

    public class TraceableScopeMap: TraceableColumn
    {
        public string Key { get; private set; }
        public TraceableScopeMap(TraceableTable table, Column column, String key) : base(table,column)
        {
            this.Key = key;
        }
        public override string ToString()
        {
            return String.Format(CultureInfo.InvariantCulture, "Col({0},{1}[{2}])", TableName, Column, Key);
        }
        public override bool Equals(object obj)
        {
            var oth = obj as TraceableScopeMap;
            return oth != null && oth.Key.Equals(this.Key)
                               && base.Equals(oth);
        }
        public override int GetHashCode()
        {
            return base.GetHashCode() + Key.GetHashCode() ;
        }
    }


	public class TraceableJsonField : TraceableColumn
	{
		public string Key { get; private set; }
		public TraceableJson JsonTraceable { get; private set; }
		public TraceableJsonField(TraceableJson tj, String key) : base(tj.TColumn.Table, tj.TColumn.Column)
		{
			this.JsonTraceable = tj;
			this.Key = key;
		}
		public override string ToString()
		{
			return String.Format(CultureInfo.InvariantCulture, "Col({0},{1}.{2})", TableName, JsonTraceable, Key);
		}
		public override bool Equals(object obj)
		{
			var oth = obj as TraceableJsonField;
			return oth != null && oth.Key.Equals(this.Key)
							   && base.Equals(oth);
		}
		public override int GetHashCode()
		{
			return base.GetHashCode() + Key.GetHashCode() + 1;
		}
	}


	public class TraceableCounter : Traceable
    {
        public TraceableTable Table { get; private set; }
        public TraceableCounter(TraceableTable table) : base(table)
        {
            this.Table = table;
        }
        public override string ToString()
        {
            return String.Format(CultureInfo.InvariantCulture, "RC({0})", TableName);
        }
        public override bool Equals(object obj)
        {
            var oth = obj as TraceableCounter;
            return oth != null && oth.Table.Equals(this.Table)
                               && base.Equals(oth);
        }
        public override int GetHashCode()
        {
            return 1 + Table.GetHashCode() + base.GetHashCode();
        }
    }


    public class Location // : SimplePTGNode
    {
        SimplePTGNode SimplePTGNode = null;
        public IFieldReference Field { get; set; }

        public Location(SimplePTGNode node, IFieldReference f) 
        {
            this.SimplePTGNode = node;
            this.Field = f;
        }

        public Location(IFieldReference f) 
        {
            this.SimplePTGNode = SimplePointsToGraph.GlobalNode;
            this.Field = f;
        }
        public override bool Equals(object obj)
        {
            var oth = obj as Location;
            return oth!=null && oth.SimplePTGNode.Equals(this.SimplePTGNode)
                && oth.Field.Equals(this.Field);
        }
        public override int GetHashCode()
        {
            return SimplePTGNode.GetHashCode() + Field.GetHashCode();
        }
        public override string ToString()
        {
            return "[" + SimplePTGNode.ToString() +"."+  Field.ToString() + "]";
        }
    }

    /// <summary>
    /// Not used yet: Part of the plan of having A2 : SymbolicValue -> 2^Traceables instead of A2 : Variable -> 2^Traceables 
    /// </summary>
    public interface ISymbolicValue
    {
        string Name { get; }
    }
    public class EscalarVariable: ISymbolicValue
    {
        private IVariable variable;
        public EscalarVariable(IVariable variable)
        {
            this.variable = variable;
        }

        public string Name
        {
            get
            {
                return variable.Name;
            }
        }
        public override bool Equals(object obj)
        {
            var oth = obj as EscalarVariable;
            return oth!=null && variable.Equals(oth.variable);
        }
        public override int GetHashCode()
        {
            return variable.GetHashCode();
        }
    }
    public class AbstractObject : ISymbolicValue
    {
        // private IVariable variable;
        private SimplePTGNode SimplePTGNode = null;
        public AbstractObject(SimplePTGNode SimplePTGNode)
        {
            //this.variable = variable;

        }
        public string Name
        {
            get
            {
                return String.Join(",", SimplePTGNode.Variables);

            }
        }
        public override bool Equals(object obj)
        {
            var oth = obj as AbstractObject;
            return oth!=null && oth.SimplePTGNode.Equals(this.SimplePTGNode);
        }
        public override int GetHashCode()
        {
            return SimplePTGNode.GetHashCode();
        }
    }

    public class IteratorDependencyAnalysis : ForwardDataFlowAnalysis<DependencyPTGDomain>
    {
        // This class maintains info about columns and tables 
        // TODO: I think the column info can be replace by a column propagation dataflow 
        // And the schema are already propagated by A2_Variables
        public class ScopeInfo
        {
            internal MapSet<IVariable, TraceableTable> schemaTableMap = new MapSet<IVariable, TraceableTable>();
            internal IDictionary<IFieldReference, IVariable> schemaFieldMap = new Dictionary<IFieldReference, IVariable>();
            internal IDictionary<IVariable, string> columnVariable2Literal = new Dictionary<IVariable, string>();
            internal IDictionary<IFieldReference, string> columnFieldMap = new Dictionary<IFieldReference, string>();
   
            internal ScopeInfo()
            {
                columnVariable2Literal = new Dictionary<IVariable, string>();
            }
            internal void UpdateSchemaMap(IVariable callResult, IVariable arg, DependencyPTGDomain dependencies)
            {
                if(!dependencies.Dependencies.A2_Variables.ContainsKey(arg))
                { }
                var tables = dependencies.GetTraceables(arg).OfType<TraceableTable>();
                this.schemaTableMap[callResult] = new HashSet<TraceableTable>(tables);
            }
            internal IEnumerable<TraceableTable> GetTableFromSchemaMap(IVariable arg)
            {
                return this.schemaTableMap[arg];
            }
            internal bool HasTableForSchemaVar(IVariable arg)
            {
                return this.schemaTableMap.ContainsKey(arg);
            }
            internal void UpdateColumnLiteralMap(MethodCallInstruction methodCallStmt, Column columnLiteral) {
                columnVariable2Literal[methodCallStmt.Result] = columnLiteral.Name;
                //columnVariable2Literal[methodCallStmt.Result] = columnLiteral.ToString();
            }

            internal void PropagateLoad(LoadInstruction loadStmt, InstanceFieldAccess fieldAccess, DependencyPTGDomain dependencies)
            {
                if (this.columnFieldMap.ContainsKey(fieldAccess.Field))
                {
                    this.columnVariable2Literal[loadStmt.Result] = this.columnFieldMap[fieldAccess.Field];
                }

                if (fieldAccess.Instance.Name == "this" && this.schemaFieldMap.ContainsKey(fieldAccess.Field))
                {
                    var recoveredVar = this.schemaFieldMap[fieldAccess.Field];
                    this.schemaTableMap[loadStmt.Result] = this.schemaTableMap[recoveredVar];
                }
                if(loadStmt.HasResult && dependencies.HasTraceables(loadStmt.Result))
                {
                    this.schemaTableMap[loadStmt.Result] = new HashSet<TraceableTable>(dependencies.GetTraceables(loadStmt.Result).OfType<TraceableTable>());
                }
            }
            internal void PropagateStore(StoreInstruction instruction, InstanceFieldAccess fieldAccess)
            {
                // This is to connect the column field with the literal
                // Do I need this?
                if (this.columnVariable2Literal.ContainsKey(instruction.Operand))
                {
                    var columnLiteral = this.columnVariable2Literal[instruction.Operand];
                    this.columnFieldMap[fieldAccess.Field] = columnLiteral;
                }

                if (this.schemaTableMap.ContainsKey(instruction.Operand))
                {
                    this.schemaFieldMap[fieldAccess.Field] = instruction.Operand;
                }
            }
            internal void PropagateCopy(LoadInstruction loadStmt, IVariable v)
            {
                if (this.columnVariable2Literal.ContainsKey(v))
                {
                    this.columnVariable2Literal[loadStmt.Result] = this.columnVariable2Literal[v];
                }
            }
			internal void PropagateCopy(IVariable source, IVariable dest)
			{
				if (this.columnVariable2Literal.ContainsKey(source))
				{
					this.columnVariable2Literal[dest] = this.columnVariable2Literal[source];
				}
			}
		}

        internal class MoveNextVisitorForDependencyAnalysis : InstructionVisitor
        {
			private static readonly int MAX_ITERATIONS = 5;

            private IDictionary<IVariable, IExpression> equalities;
            private IteratorDependencyAnalysis iteratorDependencyAnalysis;

            private ScopeInfo scopeData;
            internal DependencyPTGDomain State { get; private set; }
            private SimplePointsToGraph currentPTG;
            private CFGNode cfgNode;
            private IMethodDefinition method;
            private PTAVisitor visitorPTA;
            private VariableRangeDomain variableRanges;
			private bool validBlock;
			// Used to check if all predecessors where traversed at least once. Maybe no longer needed
			private bool predecessorsVisited;

			int numberOfVisits;

			public MoveNextVisitorForDependencyAnalysis(IteratorDependencyAnalysis iteratorDependencyAnalysis,
                                   CFGNode cfgNode,  DependencyPTGDomain oldInput, int numberOfVisits ,bool predecessorsVisited = true)
            {

                // A visitor for the points-to graph
                var visitorPTA = new PTAVisitor(oldInput.PTG, iteratorDependencyAnalysis.pta);

                this.iteratorDependencyAnalysis = iteratorDependencyAnalysis;
                this.equalities =  iteratorDependencyAnalysis.equalities;
                this.scopeData = iteratorDependencyAnalysis.scopeData;
                this.State = oldInput;
                this.currentPTG = oldInput.PTG;
                this.cfgNode = cfgNode;
                this.method = iteratorDependencyAnalysis.method;
                this.visitorPTA = visitorPTA;
                this.variableRanges = this.iteratorDependencyAnalysis.rangeAnalysis.Result[cfgNode.Id].Output;

				this.numberOfVisits = numberOfVisits;
				this.predecessorsVisited = predecessorsVisited; 
			}

            private bool IsClousureType(IVariable instance)
            {
                return instance.Type.TypeEquals(this.iteratorDependencyAnalysis.iteratorClass);
            }

			private bool IsClousureInternalField(IVariable instance, IFieldReference field)
			{
				return this.iteratorDependencyAnalysis.iteratorClass.Equals(field.ContainingType);
			}
					

		private bool ISClousureField(IVariable instance, IFieldReference field)
            {
                if(SongTaoDependencyAnalysis.IsScopeType(field.Type))
                {
                    return true;
                }
                //if(IsClousureParamerField(fieldAccess))
                //{
                //    return true;
                //}

                var instanceType = instance.Type;
                if(instanceType is IPointerType)
                {
                    instanceType= (instanceType as IPointerType).TargetType;
                }

                if ( instanceType.TypeEquals(this.iteratorDependencyAnalysis.iteratorClass)) 
                {
                    return true;
                }
                var isClousureField = this.iteratorDependencyAnalysis.iteratorClass.Equals(field.ContainingType);

                bool instanceIsCompilerGeneratedSibblingClass = false;
                var typeAsClass = (instance.Type as INamedTypeReference);
                var iteratorClass = this.iteratorDependencyAnalysis.iteratorClass as INestedTypeDefinition;

                if (typeAsClass != null && typeAsClass.ResolvedType != null)
                {
                    var typeAsClassResolved = (typeAsClass.ResolvedType as INestedTypeDefinition);
                    instanceIsCompilerGeneratedSibblingClass = MyTypesHelper.IsCompiledGeneratedClass(typeAsClassResolved)
                                            && typeAsClassResolved.ContainingType != null 
                                            && iteratorClass!= null
                                            && typeAsClassResolved.ContainingType.TypeEquals(iteratorClass.ContainingType);
                }
                var isReducerField = iteratorClass != null
                    && iteratorClass.ContainingType.TypeEquals(field.ContainingType);

                if(isClousureField || isReducerField || instanceIsCompilerGeneratedSibblingClass)
                {
                    return true;
                }

                return false;
            }

			//private ISet<ISymbolicValue> GetSymbolicValues(IVariable v)
			//{
			//    if(v.Type.TypeKind == TypeKind.ValueType)
			//    {
			//        return new HashSet<ISymbolicValue>() { new EscalarVariable(v) } ;
			//    }
			//    var res = new HashSet<ISymbolicValue>();
			//    if(currentPTG.Contains(v))
			//    {
			//        res.UnionWith(currentPTG.GetTargets(v).Select( ptg => new AbstractObject(ptg) ));
			//    }
			//    return res;
			//}
			//private ISet<SimplePTGNode> GetSimplePTGNodes(IVariable v)
			//{
			//    var res = new HashSet<SimplePTGNode>();
			//    if (currentPTG.Contains(v))
			//    {
			//        res.UnionWith(currentPTG.GetTargets(v));
			//    }
			//    return res;
			//}

			//private ISet<IVariable> GetAliases(IVariable v)
			//{
			//    return currentPTG.GetAliases(v);
			//}

			public override void Visit(IInstructionContainer container)
			{
				this.validBlock = true;
				foreach (var instruction in container.Instructions)
				{
					instruction.Accept(this);
					// If we discover that the block should not be analyzed
					// we stop
					if (!validBlock)
						break;
				}
			}


			public override void Visit(LoadInstruction instruction)
            {
                instruction.Accept(visitorPTA);
				this.State.PTG = visitorPTA.State;

				var loadStmt = instruction;
                var operand = loadStmt.Operand;
                // Try to handle a = C.f, a = b.f, a = b, a = K, etc
                var isHandledLoad = HandleLoadWithOperand(loadStmt, operand);
                // These cases should be handled with more care (escape?)
                if (!isHandledLoad)
                {
                    if (operand is Reference)
                    {
                        var referencedValue = (operand as Reference).Value;
                        //if (SongTaoDependencyAnalysis.IsScopeType(referencedValue.Type))
                        {
                            var isHandled = HandleLoadWithOperand(loadStmt, referencedValue);
                            if (!isHandled)
                            {
                                this.State.SetTOP();
                                AnalysisStats.AddAnalysisReason(new AnalysisReason(this.method, loadStmt, "Load Reference not Supported"));
                            }
                        }
                    }
                    else if (operand is Dereference)
                    {
                        var reference = (operand as Dereference).Reference;
                        //if (SongTaoDependencyAnalysis.IsScopeType(reference.Type))
                        {
                            var isHandled = HandleLoadWithOperand(loadStmt, reference);
                            if (!isHandled)
                            {
                                this.State.SetTOP();
                                AnalysisStats.AddAnalysisReason(new AnalysisReason(this.method, loadStmt, "Load Dereference not Supported"));
                            }
                        }
                    }
                    else if (operand is IndirectMethodCallExpression)
                    {
                        AnalysisStats.AddAnalysisReason(new AnalysisReason(this.method, loadStmt, "Indirect method invocation not Supported"));
                        this.State.SetTOP();
                    }
                    else if (operand is StaticMethodReference || loadStmt.Operand is VirtualMethodReference)
                    {
                        // Now handled by the PT Analysis
                    }
                    else
                    {
                        AnalysisStats.AddAnalysisReason(new AnalysisReason(this.method, loadStmt, "Unsupported load"));
                        this.State.SetTOP();
                    }
                }
            }

            private bool HandleLoadWithOperand(LoadInstruction loadStmt, IValue operand)
            {
                var result = true;
                //  v = C.f   
                if (operand is StaticFieldAccess)
                {
                    ProcessStaticLoad(loadStmt, operand as StaticFieldAccess);
                }
                //  v = o.f   (v is instruction.Result, o.f is instruction.Operand)
                else if (operand is InstanceFieldAccess)
                {
                    var fieldAccess = operand as InstanceFieldAccess;
                    ProcessLoad(loadStmt, fieldAccess);

                    // TODO: Filter for columns only
                    scopeData.PropagateLoad(loadStmt, fieldAccess, this.State);
                }
                else if (operand is ArrayElementAccess)
                {
                    var arrayAccess = operand as ArrayElementAccess;
                    var baseArray = arrayAccess.Array;

                    // TODO: Add dependencies in indices
                    // var indices = arrayAccess.Indices;
                    // a2:= [v <- a2[o] U a3[loc(o.f)] if loc(o.f) is CF
                    // TODO: Check this. I think it is too conservative to add a2[o]
                    // this is a2[o]
                    var traceables = this.State.GetTraceables(baseArray);

                    var fakeField = new FieldReference("[]", arrayAccess.Type, method.ContainingType);
                    traceables.UnionWith(this.State.GetHeapTraceables(baseArray, fakeField));

                    var targets = this.State.PTG.GetTargets(baseArray, fakeField);
                    if(!targets.Any() && SongTaoDependencyAnalysis.IsScopeType(arrayAccess.Type))
                    {
                        this.State.SetTOP();
                        AnalysisStats.AddAnalysisReason(new AnalysisReason(this.method, loadStmt, "Trying to access index array with no objects associated"));
                    }

                    //foreach (var SimplePTGNode in currentPTG.GetTargets(baseArray))
                    //{
                    //    // TODO: I need to provide a BasicType. I need the base of the array 
                    //    // Currenly I use the method containing type
                    //    var fakeField = new FieldReference("[]", arrayAccess.Type, method.ContainingType);
                    //    var loc = new Location(SimplePTGNode, fakeField);
                    //    if (this.State.Dependencies.A3_Fields.ContainsKey(loc))
                    //    {
                    //        traceables.UnionWith(this.State.Dependencies.A3_Fields[loc]);
                    //    }
                    //}

                    //this.State.AssignTraceables(loadStmt.Result, traceables);
                }
                else if (operand is ArrayLengthAccess)
                {
                    UpdateUsingDefUsed(loadStmt);
                }
                // copy 
                else if (operand is IVariable)
                {
                    var v = operand as IVariable;
                    this.State.CopyTraceables(loadStmt.Result, v);
                    if (v.Type.IsInteger())
                    {
                        scopeData.PropagateCopy(loadStmt, v);
                    }
                }
                // For these cases I'm doing nothing
                else if (operand is Constant)
                {
                    var constant = operand as Constant;
                    this.State.AssignTraceables(loadStmt.Result, new Traceable[] { new Other(constant.Type.GetName()) });
                }
                else
                {
                    result = false;
                }
                return result;
            }
                        

            private void ProcessLoad(LoadInstruction loadStmt, InstanceFieldAccess fieldAccess)
            {

                // a2:= [v <- a2[o] U a3[loc(o.f)] if loc(o.f) is CF
                // TODO: Check this. I think it is too conservative to add a2[o]
                // this is a2[o]
                var traceables = new HashSet<Traceable>();
				// TODO: Check this. I think it is too conservative to add a2[o]
				// this is a2[o]

				if (SongTaoDependencyAnalysis.IsScopeType(fieldAccess.Instance.Type) || IsClousureInternalField(fieldAccess.Instance, fieldAccess.Field))
				{
					var itState = this.State.IteratorState;
				}

				var validHeap = true;
				if (fieldAccess.Type.IsClassOrStruct())
				{
					var nodes = currentPTG.GetTargets(fieldAccess.Instance);
					validHeap = nodes.Any();
				}
				// this is a[loc(o.f)]
				if (validHeap)
				{
					// TODO: SHould I only consider the clousure fields?
					traceables.UnionWith(this.State.GetHeapTraceables(fieldAccess.Instance, fieldAccess.Field));
					//if(IsClousureType(fieldAccess.Instance))
					{
						traceables.AddRange(this.State.GetTraceables(fieldAccess.Instance));
					}
					//var jsonNodes = nodes.Where(n => n.Kind == SimplePTGNodeKind.Json);
					//if (jsonNodes.Any())
					//{
					//	var jsonTraceables = traceables.OfType<TraceableColumn>().Where(t => t.TableKind == ProtectedRowKind.Input).Select(t => new TraceableJsonField(t.Table, t.Column, fieldAccess.FieldName));
					//	this.State.AssignTraceables(loadStmt.Result, jsonTraceables);
					//	return;
					//}

					if (!IsClousureInternalField(fieldAccess.Instance,fieldAccess.Field) &&  traceables.OfType<TraceableJson>().Any())
					{
						var jsonTraceables = traceables.OfType<TraceableJson>()
														.Select(jsonTraceable => new TraceableJsonField(jsonTraceable, fieldAccess.FieldName));
						this.State.AssignTraceables(loadStmt.Result, jsonTraceables);
						return;
					}
				}
				else
				{
					this.State.SetTOP();
					AnalysisStats.AddAnalysisReason(new AnalysisReason(this.method, loadStmt, "Trying to load a field with no objects associated"));
				}
                //}
                this.State.AssignTraceables(loadStmt.Result, traceables);
            }

            private bool MaybeProctectedNode(SimplePTGNode node)
            {
                return this.iteratorDependencyAnalysis.protectedNodes.Contains(node);
            }
            private bool IsProctectedAccess(IVariable instance, IFieldReference field)
            {
                var nodes  = this.State.PTG.GetTargets(instance, field);
                return nodes.Intersect(this.iteratorDependencyAnalysis.protectedNodes).Any();
            }

            private void ProcessStaticLoad(LoadInstruction loadStmt, StaticFieldAccess fieldAccess)
            {
                var iteratorClass = this.iteratorDependencyAnalysis.iteratorClass as INestedTypeDefinition;

                // TODO: Move to IsClousureField()
                var isClousureField = iteratorClass!=null &&  iteratorClass.Equals(fieldAccess.Field.ContainingType);
                var isReducerField = iteratorClass!=null 
                                        && iteratorClass.ContainingType.TypeEquals(fieldAccess.Field.ContainingType);
                // TODO: Hack. I need to check for private fields and properly model 
				// DIEGODIEGO: I should try to get read of this method
                if (ISClousureField(SimplePointsToAnalysis.GlobalVariable, fieldAccess.Field))
                //    if (isClousureField || isReducerField)
                {
                    var traceables = new HashSet<Traceable>();
                    traceables.UnionWith(this.State.GetHeapTraceables(SimplePointsToGraph.GlobalNode, fieldAccess.Field));

                    // a2:= [v <- a3[loc(o.f)] if loc(o.f) is CF
                    // if (ISClousureField(SimplePointsToGraph.GlobalNode.Variables.Single(), fieldAccess.Field))
                    //{
                    //    // this is a[loc(C.f)]
                    //    var loc = new Location(SimplePointsToGraph.GlobalNode, fieldAccess.Field);
                    //    if (this.State.Dependencies.A3_Fields.ContainsKey(loc))
                    //    {
                    //        traceables.UnionWith(this.State.Dependencies.A3_Fields[loc]);
                    //    }

                    //}
                    this.State.AssignTraceables(loadStmt.Result, traceables);
                }
                else
                { }
            }


            public override void Visit(StoreInstruction instruction)
            {
                instruction.Accept(visitorPTA);
				this.State.PTG = visitorPTA.State;

				var result = instruction.Result;
                if (!HandleStdStore(instruction, result))
                {
                    if (result is Reference)
                    {
                        var referencedValue = (result as Reference).Value;
                        //if (SongTaoDependencyAnalysis.IsScopeType(referencedValue.Type))
                        // I allways copy
                        {
                            var v = referencedValue as IVariable;
                            this.State.AddTraceables(v, instruction.Operand);
                        }
                        //AnalysisStats.AddAnalysisReason(new AnalysisReason(this.method.Name, instruction, "Unsupported Store Deference"));
                        //this.State.IsTop = true;
                    }
                    else if(result is Dereference)
                    {
                        var reference = (result as Dereference).Reference;
                        // if (SongTaoDependencyAnalysis.IsScopeType(reference.Type))
                        // I allways copy
                        {
                            this.State.AddTraceables(reference, instruction.Operand);
                        }
                    }
                    else
                    {
                        AnalysisStats.AddAnalysisReason(new AnalysisReason(this.method, instruction, "Unsupported Store"));
                        this.State.SetTOP();
                    }
                }

            }

            private bool HandleStdStore(StoreInstruction instruction, IAssignableValue instructionResult)
            {
                var result = true;
                //  o.f = v  (v is instruction.Operand, o.f is instruction.Result)
                if (instructionResult is InstanceFieldAccess)
                {
                    var fieldAccess = instructionResult as InstanceFieldAccess;

                    var o = fieldAccess.Instance;
                    var field = fieldAccess.Field;

					if (SongTaoDependencyAnalysis.IsScopeType(fieldAccess.Instance.Type) || IsClousureInternalField(fieldAccess.Instance, fieldAccess.Field))
					{
						var itState = this.State.IteratorState;
						if (this.iteratorDependencyAnalysis.processToAnalyze.ProcessorClass.GetName() == "ResourceDataTagFlattener")
						{ }

					}


					//if (ISClousureField(fieldAccess.Instance, fieldAccess.Field))
					{
						var arg = instruction.Operand;
                        var inputTable = equalities.GetValue(arg);

						var iteratorFSMState = this.variableRanges.GetValue(arg);

						if (field.Name.Value.EndsWith("__state"))
						{
							this.State.IteratorState = iteratorFSMState;
						}


						// a3 := a3[loc(o.f) <- a2[v]] 
						// union = a2[v]
						var traceables = this.State.GetTraceables(instruction.Operand);

                        var OK = true;
                        if (o.Name == "this")
                        {
                            OK = this.State.AssignHeapTraceables(o, field, traceables);
                        }
                        else
                        {
                            OK = this.State.AddHeapTraceables(o, field, traceables);
                        }

                        //var traceables = this.State.GetTraceables(instruction.Operand);
                        //var nodes = currentPTG.GetTargets(o);
                        //if (nodes.Any())
                        //{
                        //    foreach (var SimplePTGNode in nodes)
                        //    {
                        //        var location = new Location(SimplePTGNode, field);
                        //        //this.State.Dependencies.A3_Fields[location] = traceables;
                        //        // Now I do a weak update
                        //        this.State.Dependencies.A3_Fields.AddRange(location, traceables);
                        //    }
                        //}
                        if(!OK)
                        {
                            this.State.SetTOP();
                            AnalysisStats.AddAnalysisReason(new AnalysisReason(this.method, instruction, "Trying to Store a field with no objects associated"));
                        }

                    }

                    scopeData.PropagateStore(instruction, fieldAccess);
                }
                else if (instructionResult is ArrayElementAccess)
                {
                    var arrayAccess = instructionResult as ArrayElementAccess;
                    var baseArray = arrayAccess.Array;
                    // TODO: Add dependencies in indices
                    // var indices = arrayAccess.Indices;
                    var arg = instruction.Operand;
                    var inputTable = equalities.GetValue(arg);

                    // a3 := a3[loc(o[f]) <- a2[v]] 
                    // union = a2[v]
                    var traceables = this.State.GetTraceables(instruction.Operand);
                    var fakeField = new FieldReference("[]", arrayAccess.Type, method.ContainingType);
                    var OK = this.State.AddHeapTraceables(baseArray, fakeField, instruction.Operand);
					this.State.AddTraceables(baseArray, traceables);
                    //foreach (var SimplePTGNode in currentPTG.GetTargets(baseArray))
                    //{
                    //    // TODO: I need to provide a BasicType. I need the base of the array 
                    //    // Currenly I use the method containing type
                    //    var fakeField = new FieldReference("[]", arrayAccess.Type, method.ContainingType);
                    //    //fakeField.ContainingType = PlatformTypes.SystemObject;
                    //    var loc = new Location(SimplePTGNode, fakeField);
                    //    this.State.Dependencies.A3_Fields[new Location(SimplePTGNode, fakeField)] = traceables;
                    //}
                }
                else if (instructionResult is StaticFieldAccess)
                {
                    var field = (instructionResult as StaticFieldAccess).Field;
                    var traceables = this.State.GetTraceables(instruction.Operand);

                    this.State.AddHeapTraceables(SimplePointsToGraph.GlobalNode, field, traceables);

                    //this.State.Dependencies.A3_Fields[new Location(SimplePointsToGraph.GlobalNode, field)] = traceables;

                    this.State.Dependencies.A1_Escaping.UnionWith(traceables.Where(t => !(t is Other)));
                }
                else
                {
                    result = false;
                }
                return result;
            }


            public override void Visit(ConditionalBranchInstruction instruction)
            {
                instruction.Accept(visitorPTA);
				this.State.PTG = visitorPTA.State;

				this.State.Dependencies.ControlVariables.UnionWith(instruction.UsedVariables.Where( v => this.State.GetTraceables(v).Any()));
				//this.State.Dependencies.ControlTraceables.UnionWith(instruction.UsedVariables.SelectMany(v => this.State.GetTraceables(v)));
			}
            public override void Visit(ReturnInstruction instruction)
            {
                instruction.Accept(visitorPTA);
				this.State.PTG = visitorPTA.State;

				if (instruction.HasOperand)
                {
					// var rv = this.iteratorDependencyAnalysis.ReturnVariable;
					var rv = instruction.Operand;
                    this.State.CopyTraceables(this.iteratorDependencyAnalysis.ReturnVariable, rv);
                }
            }
            public override void Visit(CreateObjectInstruction instruction)
            {
                var traceables = new HashSet<Traceable>();
                traceables.Add(new Other(instruction.AllocationType.GetName()));
                instruction.Accept(visitorPTA);
				this.State.PTG = visitorPTA.State;

				this.State.AssignTraceables(instruction.Result, traceables);
                
            }
			/// <summary>
			/// Processing of a method invocation. Most of Scope operations are calls to the Scope API
			/// So we need to understand each call to discover use of columns and propagate the dependencies
			/// </summary>
			/// <param name="instruction"></param>
			public override void Visit(MethodCallInstruction instruction)
			{
				// Updates the Points-to information with the call 
				instruction.Accept(visitorPTA);
				this.State.PTG = visitorPTA.State;

				var methodCallStmt = instruction;
				var methodInvoked = methodCallStmt.Method;
				var callResult = methodCallStmt.Result;
				var candidateClass = method.ContainingType.ResolvedType;
				// We are analyzing instructions of the form this.table.Schema.IndexOf("columnLiteral")
				// to maintain a mapping between column numbers and literals 
				var isSchemaMethod = HandleSchemaRelatedMethod(methodCallStmt, methodInvoked);
				if (!isSchemaMethod)
				{
					// Analyze Row related methods (get, set, copy, etc)
					var isScopeRowMethod = HandleScopeRowMethods(methodCallStmt, methodInvoked);
					if (!isScopeRowMethod)
					{
						// Analyze methods that parte JsonObjects (that can be accessed as "fields" in columns)
						var isJsonMethod = HandleJsonRelatedMethod(methodCallStmt, methodInvoked);
						if (!isJsonMethod)
						{
							// Analyze collection handling methods (lists, sets, dictionaries)
							// UDO use to use this kind of method for internal processing of input rows 
							var isCollectionMethod = HandleCollectionMethod(methodCallStmt, methodInvoked);
							if (!isCollectionMethod)
							{
								// DIEGODIEGO: I should add a whitelist for method that do not propagate traceables
								// At least they propagate only the Other traceables for the passthrough analysis

								// For a pure a = a0.m(a1,...,an)  we propagate traceables from a0...an to a
								// and update the points-to graph to conservately make the return_value reach a0...an (since we do not analyze the methdod) 
								if (IsPureMethod(methodCallStmt))
								{
									UpdateCall(methodCallStmt);
								}
								else
								{
									// Now we are analyzing a non-scope related, non-collection, non-pure methods
									// We need to understand whether we need to analyze this method 
									// The reason for analyzing the method are:
									// - one argument is a Row or can reach a row 
									// - one argument refers to traceable (e.g., a column) and we may generate a new dependency on it 
									// withing the method (e.g, a helper method, doing some Json stuff)

									// We first check in the calle may a input/output row
									var argRootNodes = methodCallStmt.Arguments.SelectMany(arg => currentPTG.GetTargets(arg, false))
														.Where(n => n != SimplePointsToGraph.NullNode);
									// If it is a method within the same class it will be able to acesss all the fields 
									// We also see that compiler generated methods (like lambbas should also access)
									var isInternalClassInvocation = TypeHelper.TypesAreEquivalent(methodInvoked.ContainingType, this.iteratorDependencyAnalysis.iteratorClass);
									var isCompiledGeneratedLambda = this.method.ContainingType.IsCompilerGenerated()
																	  && (this.method.ContainingType as INestedTypeDefinition).ContainingType != null 
																	  && TypeHelper.TypesAreEquivalent(methodInvoked.ContainingType, (this.iteratorDependencyAnalysis.iteratorClass as INestedTypeDefinition).ContainingType);
									Predicate<Tuple<SimplePTGNode, IFieldReference>> fieldMustbeIgnored = 
														(nodeField => 
															// For internal/copmpiler generated methods we allow any field acess
															!(isInternalClassInvocation || isCompiledGeneratedLambda)
															// otherwise we skip fields that are not from the iterator class
															|| (TypeHelper.TypesAreEquivalent(nodeField.Item2.ContainingType, this.iteratorDependencyAnalysis.iteratorClass)
															// or accessible from this artifitial field (only use to connect columns with rows
																&&  nodeField.Item2.Name.Value == "$scope$return"));
									// We check if a row is reacheable using valid fields 
									// Compiler generated fields are rejected because methods outside the compiler generated class cannot access these fields
									var reachableNodes = currentPTG.ReachableNodes(argRootNodes, fieldMustbeIgnored);

									var escaping = reachableNodes.Intersect(this.iteratorDependencyAnalysis.protectedNodes).Any();
									
									// We need to add this to handled cases like Json object that send traceables as parameters even if they do not 
									// access the row
									var traceablesInArguments = methodCallStmt.Arguments.SelectMany(arg => State.GetTraceables(arg)).Where(t => !(t is Other));

									var calleeRequiringAnalysis = escaping || traceablesInArguments.Any();

									if (methodInvoked.Name.Value == "GetCurrentEntity")
									{
									}

									if (methodInvoked.Name.Value == "DeSerializeFromString")
									{
									}

									if (methodInvoked.Name.Value == "DeSerializeFromBase64String")
									{
									}

							


									if (calleeRequiringAnalysis)
									{
										var isMethodToInline = IsMethodToInline(methodInvoked, this.iteratorDependencyAnalysis.iteratorClass, traceablesInArguments.Any());

										if (this.iteratorDependencyAnalysis.InterProceduralAnalysisEnabled || isMethodToInline)
										{

											// For the demo I'll skip this methods that do anything important
											//if (isMethodToInline && !methodInvoked.IsConstructor())
											//{ }
											//else
											{
												// This updates the Dep Domain and the PTG
												var computedCalles = this.iteratorDependencyAnalysis.interproceduralManager.ComputePotentialCallees(instruction, currentPTG);
												AnalyzeResolvedCallees(instruction, methodCallStmt, computedCalles.Item1);

												// If there are unresolved calles
												if (computedCalles.Item2.Any() || !computedCalles.Item1.Any())
												{
													HandleNoAnalyzableMethod(methodCallStmt);
												}
											}
										}
										else
										{
											HandleNoAnalyzableMethod(methodCallStmt);
										}
									}
									else
									{

										UpdateCall(methodCallStmt);

										// I should at least update the Poinst-to graph
										// or make the parameters escape
										foreach (var escapingNode in argRootNodes.Where(n => n.Kind != SimplePTGNodeKind.Null))
										{
											var escapingField = new FieldReference("escape", Types.Instance.PlatformType.SystemObject, this.method.ContainingType);
											// TODO: Check if this is always necessary
											currentPTG.PointsTo(SimplePointsToGraph.GlobalNode, escapingField, escapingNode);
										}
									}
								}
							}
						}
					}
				}
			}

			private bool HandleJsonRelatedMethod(MethodCallInstruction methodCallStmt, IMethodReference methodInvoked)
			{
				if (methodInvoked.Name.Value == "ToString" && methodCallStmt.Arguments[0].Type.GetFullName().Contains("Newtonsoft.Json"))
				{
					this.State.CopyTraceables(methodCallStmt.Result, methodCallStmt.Arguments[0]);
					UpdatePTAForScopeMethod(methodCallStmt);
					return true;
				}
				else if (methodInvoked.ContainingType.GetFullName() == "Newtonsoft.Json.JsonConvert")
				{
					if (methodInvoked.Name.Value == "DeserializeObject")
					{
						var arg = methodCallStmt.Arguments[0];
						var jsontraceables = this.State.GetTraceables(arg).OfType<TraceableColumn>().Select(t => new TraceableJson(t));
						this.State.AssignTraceables(methodCallStmt.Result, jsontraceables);

						UpdatePTAForScopeMethod(methodCallStmt);

						return true;
					}
					else if (methodInvoked.Name.Value == "SerializeObject")
					{
						var arg = methodCallStmt.Arguments[0];
						AddJsonColumnFieldToTraceables(methodCallStmt, arg, "*");
						return true;
					}
					else
					{
					}
				}
				else if ((methodInvoked.ContainingType.GetFullName() == "Newtonsoft.Json.Linq.JObject"
							|| methodInvoked.ContainingType.GetFullName() == "Newtonsoft.Json.Linq.JArray"
							|| methodInvoked.ContainingType.GetFullName() == "Newtonsoft.Json.Linq.JToken")
							&& methodInvoked.Name.Value == "Parse")
				{
					var arg = methodCallStmt.Arguments[0];
					var traceables = this.State.GetTraceables(arg).Where(t => !(t is Other));
					var jsontraceables = traceables.OfType<TraceableColumn>().Select(t => new TraceableJson(t));
					this.State.AssignTraceables(methodCallStmt.Result, jsontraceables);
					return true;
				}
				else if (methodInvoked.Name.Value == "get_Item" && methodInvoked.ContainingType.GetFullName() == "Newtonsoft.Json.Linq.JObject")
				{
					var arg = methodCallStmt.Arguments[0];
					var col = methodCallStmt.Arguments[1];

					var columRange = this.variableRanges.GetValue(col);
					if (!columRange.IsBottom)
					{
						var columnLiteral = columRange.Literal;

						AddJsonColumnFieldToTraceables(methodCallStmt, arg, columnLiteral);
					}
					else
					{
						this.State.SetTOP();
						AnalysisStats.AddAnalysisReason(new AnalysisReason(this.method, methodCallStmt, "We are expecting a string for a columns but get null"));
					}

					return true; ;
				}
				else if (methodInvoked.Name.Value == "SelectToken" && methodInvoked.ContainingType.GetFullName() == "Newtonsoft.Json.Linq.JToken")
				{
					var arg = methodCallStmt.Arguments[0];
					var col = methodCallStmt.Arguments[1];

					var columRange = this.variableRanges.GetValue(col);
					if (!columRange.IsBottom)
					{
						var columnLiteral = columRange.Literal;

						AddJsonColumnFieldToTraceables(methodCallStmt, arg, columnLiteral);
					}
					else
					{
						this.State.SetTOP();
						AnalysisStats.AddAnalysisReason(new AnalysisReason(this.method, methodCallStmt, "We are expecting a string for a columns but get null"));
					}
					return true; ;
				}
				else if (methodInvoked.ContainingType.GetFullName() == "Newtonsoft.Json.Linq.JToken" || methodInvoked.ContainingType.GetFullName() == "ScopeRuntime.StringColumnData")
				{
					if (methodInvoked.Name.Value == @"op_Explicit" || methodInvoked.Name.Value == @"op_Implicit")
					{
						this.State.CopyTraceables(methodCallStmt.Result, methodCallStmt.Arguments[0]);
						UpdatePTAForScopeMethod(methodCallStmt);
						return true; ;
					}
					else if (methodInvoked.Name.Value == @"get_Item")
					{
						var arg0 = methodCallStmt.Arguments[0];
						var arg1 = methodCallStmt.Arguments[1];
						var columName = variableRanges.GetValue(arg1).Literal;
						if (columName != null)
						{
							AddJsonColumnFieldToTraceables(methodCallStmt, arg0, columName);
						}
						return true;
					}
					else
					{
					}
				}
				else if (methodInvoked.ContainingType.GetFullName() == "Microsoft.DataMap.Common.Tag")
				{
					if (methodInvoked.Name.Value.StartsWith(@"get_"))
					{
						var arg = methodCallStmt.Arguments[0];
						var columName = methodInvoked.Name.Value.Substring(4);
						AddJsonColumnFieldToTraceables(methodCallStmt, arg, columName);
						return true;
					}
					else
					{
					}
				}
				return false;
			}

			private void AddJsonColumnFieldToTraceables(MethodCallInstruction methodCallStmt, IVariable arg, string columnLiteral)
			{
				var jsonFields = this.State.GetTraceables(arg).OfType<TraceableJson>()
									.Select(tjs => new TraceableJsonField(tjs, columnLiteral));

				UpdatePTAForScopeMethod(methodCallStmt);
				this.State.AssignTraceables(methodCallStmt.Result, jsonFields);

				//this.iteratorDependencyAnalysis.InputColumns.AddRange(jsonFields.Where(t => t.TableKind == ProtectedRowKind.Input));
				//this.iteratorDependencyAnalysis.OutputColumns.AddRange(jsonFields.Where(t => t.TableKind == ProtectedRowKind.Output));

				CheckFailure(methodCallStmt, jsonFields);
			}

			/// <summary>
			/// Updates the points-to graph using only the info from parameter
			/// TODO: We should actually follow the ideas of our IWACO paper...
			/// </summary>
			/// <param name="instruction"></param>
			private void UpdatePTAForScopeMethod(MethodCallInstruction instruction)
			{
				UpdatePTAForPure(instruction, true);
			}
			private void UpdatePTAForPure(MethodCallInstruction instruction, bool useSpecialField = false)
            {
                if (instruction.HasResult && instruction.Result.Type.IsClassOrStruct())
                {
                    var returnNode = new SimplePTGNode(new PTGID(new MethodContex(this.method), (int)instruction.Offset), instruction.Result.Type, SimplePTGNodeKind.Object);

                    foreach (var result in instruction.ModifiedVariables)
                    {
                        var allNodes = new HashSet<SimplePTGNode>();
                        foreach (var arg in instruction.UsedVariables)
                        {
                            var nodes = this.currentPTG.GetTargets(arg, false);
                            allNodes.UnionWith(currentPTG.ReachableNodes(nodes));
                            //allNodes.UnionWith(nodes);
                        }

                        currentPTG.RemoveRootEdges(result);
                        currentPTG.PointsTo(result, returnNode);
						FieldReference returnField = null;
						if(useSpecialField)
							returnField = new FieldReference("$scope$return", Types.Instance.PlatformType.SystemObject, this.method.ContainingType);
						else
							returnField = new FieldReference("$return", Types.Instance.PlatformType.SystemObject, this.method.ContainingType);

						foreach (var SimplePTGNode in allNodes)
                        {
                            // I remove this filter until I can have a more permissive compatibility analysis for collections and Row methods
                            // e.g., I need RowSet to be compatible with IEnumerable<Row>
                            // Another option is improve the HandleRowScopeRowMethods function to do something similar I did with collections
                            //if (TypeHelper.TypesAreAssignmentCompatible(SimplePTGNode.Type.ResolvedType, instruction.Result.Type.ResolvedType))
                            {
                                currentPTG.PointsTo(returnNode, returnField, SimplePTGNode);
                            }
                        }
                    }

                }
            }

            private void AnalyzeResolvedCallees(MethodCallInstruction instruction, MethodCallInstruction methodCallStmt, IEnumerable<IMethodDefinition> calles)
            {
                if (calles.Any())
                {
                    var callStates = new HashSet<DependencyPTGDomain>();
                    foreach (var resolvedCallee in calles)
                    {
                        try
                        {
                            var input = this.State;

                            var interProcInfo = new InterProceduralCallInfo()
                            {
                                Caller = this.method,
                                Callee = resolvedCallee,
                                CallArguments = methodCallStmt.Arguments,
                                CallLHS = methodCallStmt.Result,
                                CallerState = this.State,
                                CallerPTG = currentPTG,
                                ScopeData = this.scopeData,
                                Instruction = instruction,
								VariableRanges = variableRanges,
								ProtectedNodes = this.iteratorDependencyAnalysis.protectedNodes

                            };

                            var interProcResult = this.iteratorDependencyAnalysis.interproceduralManager.DoInterProcWithCallee(interProcInfo);
                            callStates.Add(interProcResult.State);

							this.iteratorDependencyAnalysis.InputColumns.AddRange(interProcResult.InputColumns);
							this.iteratorDependencyAnalysis.OutputColumns.AddRange(interProcResult.OutputColumns);

							this.State = interProcResult.State;
                            currentPTG = interProcResult.State.PTG;
							this.visitorPTA.State = interProcResult.State.PTG; 

						}
                        catch (Exception e)
                        {
                            //Console.WriteLine("Could not analyze {0}", resolvedCallee.ToString());
                            //Console.WriteLine("Exception {0}\n{1}", e.Message, e.StackTrace);
                            AnalysisStats.AddAnalysisReason(new AnalysisReason(this.method, methodCallStmt,
                                        String.Format(CultureInfo.InvariantCulture, "Callee {2} throw exception {0}\n{1}", e.Message, e.StackTrace.ToString(), resolvedCallee.ToString())));
                            AnalysisStats.TotalofFrameworkErrors++;
                            HandleNoAnalyzableMethod(methodCallStmt);
                        }

                    }
                    this.State = callStates.Aggregate((s1, s2) => s1.Join(s2));
                }
            }

            private void HandleNoAnalyzableMethod(MethodCallInstruction methodCallStmt)
            {
                UpdateCall(methodCallStmt);
                // I already know that the are argument escaping (because I only invoke this method in that case
                //var argRootNodes = methodCallStmt.Arguments.SelectMany(arg => ptg.GetTargets(arg, false));
                //var escaping = ptg.ReachableNodes(argRootNodes).Intersect(this.iteratorDependencyAnalysis.protectedNodes).Any();
                //if(escaping)
                //{
                this.State.Dependencies.A1_Escaping.UnionWith(methodCallStmt.Arguments
                                        .SelectMany(arg => this.State.GetTraceables(arg).Where(t => !(t is Other))));
                    AnalysisStats.AddAnalysisReason(new AnalysisReason(this.method, methodCallStmt, 
                                                    String.Format(CultureInfo.InvariantCulture, "Invocation to {0} not analyzed with argument potentially reaching the columns", methodCallStmt.Method)));
                    this.State.SetTOP();
                // }
            }

            private bool IsPureMethod(MethodCallInstruction metodCallStmt)
            {
				var whiteListedTypes = new HashSet<string> { "System.Convert", "System.String", "System.Text.Encoding" };
				var specialMethods = new Tuple<string, string>[] { Tuple.Create("System.IDisposable", "Dispose"),
										Tuple.Create(@"___Scope_Generated_Classes___.Helper","trimNamespace"),
										Tuple.Create("System.IO.Stream","Write")
										 };
				var result = false;

                if(metodCallStmt.Method.IsPure())
                {
                    return true;
                }

				if (metodCallStmt.Method.Name.Value == "ToString")
					return true;

				var containingType = metodCallStmt.Method.ContainingType;

                if (containingType.IsString())
                {
                    return true;
                }
                if (containingType.IsTuple())
                {
                    return true;
                }
                if (containingType.IsValueType())
                {
                    return true;
                }

				if (whiteListedTypes.Contains(containingType.GetFullName()))
					return true;

				if (metodCallStmt.Method.Name.Value == "ToString")
					return true;

				result = specialMethods.Any(sm => sm.Item1 == containingType.GetFullName() 
												&& sm.Item2 == metodCallStmt.Method.Name.Value);

                return result;
            }

			public override void Visit(PhiInstruction instruction)
            {
                instruction.Accept(visitorPTA);
				this.State.PTG = visitorPTA.State;

				var traceables = new HashSet<Traceable>();
                foreach (var arg in instruction.UsedVariables)
                {
                    var tables = this.State.GetTraceables(arg);
                    //if(!this.State.HasTraceables(arg))
                    //{
                    //    if(arg is DerivedVariable)
                    //    {
                    //        var temp= arg as DerivedVariable;
                    //        tables = this.State.GetTraceables(temp.Original);
                    //    }
                    //}
                    traceables.UnionWith(tables);
                }
                this.State.AssignTraceables(instruction.Result, traceables);
            }

            public override void Visit(ConvertInstruction instruction)
            {
                var traceables = this.State.GetTraceables(instruction.Operand);
                instruction.Accept(visitorPTA);
				this.State.PTG = visitorPTA.State;

				this.State.AssignTraceables(instruction.Result, traceables);


            }

            /// <summary>
            /// Default treatment of statement using Def/Use information
            /// TODO: Check for soundness
            /// </summary>
            /// <param name="instruction"></param>
            public override void Default(Instruction instruction)
            {
                instruction.Accept(visitorPTA);
				this.State.PTG = visitorPTA.State;

				UpdateUsingDefUsed(instruction);
                // base.Default(instruction);
            }

			/// <summary>
			/// Special treatment for collection methdod: some are pure, other only modify the receiver
			/// </summary>
			/// <param name="methodCallStmt"></param>
			/// <param name="methodInvoked"></param>
			/// <returns></returns>
			private bool HandleCollectionMethod(MethodCallInstruction methodCallStmt, IMethodReference methodInvoked)
			{
				var pureCollectionMethods = new HashSet<String>() { "Contains", "ContainsKey", "Count", "get_Count", "First"};
				var pureEnumerationMethods = new HashSet<String>() { "Select", "Where", "Any", "Count", "GroupBy", "Max", "Min", "First","ToList" };

				var result = true;
                // For constructors of collections we create an small summary for the PTA
                if(methodInvoked.IsConstructor() && methodInvoked.ContainingType.IsCollection())
                {
                    var arg = methodCallStmt.Arguments[0];
                    this.iteratorDependencyAnalysis.pta.CreateSummaryForCollection(this.State.PTG, methodCallStmt.Offset, arg);

                } 
                // For GetEnum we need to create an object iterator that points-to the colecction
                else if (methodInvoked.Name.Value == "GetEnumerator"
                    && (methodInvoked.ContainingType.IsIEnumerable() || methodInvoked.ContainingType.IsEnumerable()))
                {
                     var arg = methodCallStmt.Arguments[0];
                    var traceables = this.State.GetTraceables(arg);
                    // This method makes method.Result point to the iterator 
                    this.iteratorDependencyAnalysis.pta.ProcessGetEnum(this.State.PTG, methodCallStmt.Offset, arg, methodCallStmt.Result);
                    // We copy the traceables from the collection to the iterator
                    this.State.AssignTraceables(methodCallStmt.Result, traceables);
                }
                // For Current we need to obtain one item from the collection
                else if ( methodInvoked.Name.Value == "get_Current"  
					&& (methodInvoked.ContainingType.IsIEnumerator() || methodInvoked.ContainingType.IsEnumerator())
					// DIEGODIEGO: Add this if you want to handle First (or other operations as get an element from enumeration)
					//|| (methodInvoked.Name.Value == "First" && methodInvoked.ContainingType.IsEnumerable())
					)
                {
					var arg = methodCallStmt.Arguments[0];
					var traceables = this.State.GetTraceables(arg);

				   var adaptedTraceables = traceables.Select( t => t is TraceableJson? new TraceableJsonCollectionElement(t as TraceableJson): t);
					
                    // This method makes method.Result point to the collections item, so automatically getting the traceables from there
                    bool createdNode;
                    this.iteratorDependencyAnalysis.pta.ProcessGetCurrent(this.State.PTG, methodCallStmt.Offset, arg, methodCallStmt.Result, out createdNode);
					this.visitorPTA.State = this.State.PTG;

					// TODO: Warning: This only works if we keep the mapping A2_References
					// If we remove that mapping we MUST do the AssignTraceables
					if (createdNode)
                    {
                        this.State.AssignTraceables(methodCallStmt.Result, adaptedTraceables);
                    } else
                    {
                        this.State.AddTraceables(methodCallStmt.Result, adaptedTraceables);
                    }
                }
                // set_Item add an element to the colecction using a fake field "$item"
                else if (methodInvoked.Name.Value == "set_Item" && (methodInvoked.ContainingType.IsCollection() || methodInvoked.ContainingType.IsDictionary() || methodInvoked.ContainingType.IsSet()))
                {
                    var traceables = this.State.GetTraceables(methodCallStmt.Arguments[2]);

                    PropagateArguments(methodCallStmt, methodCallStmt.Arguments[0]);
                    var itemField = this.iteratorDependencyAnalysis.pta.AddItemforCollection(this.State.PTG, methodCallStmt.Offset, methodCallStmt.Arguments[0], methodCallStmt.Arguments[2]);
                    this.State.AddHeapTraceables(methodCallStmt.Arguments[0], itemField, traceables);
                    // Notice that we add the traceables to the receiver object (arg0.Add(args...))
                    this.State.AddTraceables(methodCallStmt.Arguments[0], traceables);
                }
                else if (( methodInvoked.Name.Value == "get_Values" || methodInvoked.Name.Value == "get_Keys") &&  methodInvoked.ContainingType.IsDictionary())
                {
                    var arg = methodCallStmt.Arguments[0];
                    var traceables = this.State.GetTraceables(arg);
                    // This method makes method.Result point to the iterator 
                    this.iteratorDependencyAnalysis.pta.ProcessCopy(this.State.PTG, methodCallStmt.Result, arg);
					this.visitorPTA.State = this.State.PTG;

					// We copy the traceables from the collection to the iterator
					this.State.AssignTraceables(methodCallStmt.Result, traceables);
                }

                // for Add we need to add an element the collection using a fake field "$item"
                else if (methodInvoked.Name.Value.StartsWith("Add") 
                    && (methodInvoked.ContainingType.IsCollection() || methodInvoked.ContainingType.IsDictionary() || methodInvoked.ContainingType.IsSet()))
                {
                    PropagateArguments(methodCallStmt, methodCallStmt.Arguments[0]);
                    if (methodInvoked.ContainingType.IsDictionary())
                    {
						string columnLiteral = null;
						//if (methodCallStmt.Arguments.Count > 1)
						//{
						//	columnLiteral = variableRanges.GetValue(methodCallStmt.Arguments[1]).Literal;
						//}
						//if (columnLiteral == "HasSecondaryIpConfigurations")
						//{ }


						var itemField = this.iteratorDependencyAnalysis.pta.AddItemforCollection(this.State.PTG, methodCallStmt.Offset, methodCallStmt.Arguments[0], methodCallStmt.Arguments[2],columnLiteral);
						this.visitorPTA.State = this.State.PTG;

						this.State.AddHeapTraceables(methodCallStmt.Arguments[0], itemField, methodCallStmt.Arguments[2]);
                        // Notice that we add the traceables to the receiver object (arg0.Add(args...))
                        this.State.AddTraceables(methodCallStmt.Arguments[0], this.State.GetTraceables(methodCallStmt.Arguments[2]));
                    }
                    else
                    {
                        var itemField = this.iteratorDependencyAnalysis.pta.AddItemforCollection(this.State.PTG, methodCallStmt.Offset, methodCallStmt.Arguments[0], methodCallStmt.Arguments[1]);
                        this.State.AddHeapTraceables(methodCallStmt.Arguments[0], itemField, methodCallStmt.Arguments[1]);
                        //this.State.AssignTraceables(methodCallStmt.Arguments[0], this.State.GetTraceables(methodCallStmt.Arguments[1]));
                        this.State.AddTraceables(methodCallStmt.Arguments[0], this.State.GetTraceables(methodCallStmt.Arguments[1]));
                    }
                }
                // get_Item recover an element to the colecction using a fake field "$item"
                else if (methodInvoked.Name.Value == "get_Item"  && (methodInvoked.ContainingType.IsCollection() || methodInvoked.ContainingType.IsDictionary() || methodInvoked.ContainingType.IsSet()))
                {
                    if (methodInvoked.ContainingType.IsDictionary())
                    {
						string columnLiteral = null;
						if (methodCallStmt.Arguments.Count>1)
						{
							columnLiteral = variableRanges.GetValue(methodCallStmt.Arguments[1]).Literal;
							if (columnLiteral != null && this.State.GetTraceables(methodCallStmt.Arguments[0]).OfType<TraceableJson>().Any())
								AddJsonColumnFieldToTraceables(methodCallStmt, methodCallStmt.Arguments[0], String.Format("[{0}]",columnLiteral));
						}
						//if (columnLiteral == "HasSecondaryIpConfigurations")
						//{ }

						//var itemField = this.iteratorDependencyAnalysis.pta.GetItemforCollection(this.State.PTG, methodCallStmt.Offset, methodCallStmt.Arguments[0], methodCallStmt.Result, columnLiteral);

						var itemField = this.iteratorDependencyAnalysis.pta.GetItemforCollection(this.State.PTG, methodCallStmt.Offset, methodCallStmt.Arguments[0], methodCallStmt.Result);
						this.visitorPTA.State = this.State.PTG;

						var heapTraceables = this.State.GetHeapTraceables(methodCallStmt.Arguments[0], itemField);
						this.State.AddTraceables(methodCallStmt.Result, heapTraceables);
						// this.State.AddTraceables(methodCallStmt.Result, this.State.GetTraceables(methodCallStmt.Arguments[0]));
						
                    }
                    else
                    {
                        // For the case of a list or other colections we treat it as an unknowm call (but pure)
                        UpdateCall(methodCallStmt);
                    }
                }
                // For movenext we treated as an unknowm call (but pure, even it modified the it)
                else if (methodInvoked.Name.Value == "MoveNext" && (methodInvoked.ContainingType.IsIEnumerator()) || methodInvoked.ContainingType.IsEnumerator())
                {
                    var arg = methodCallStmt.Arguments[0];
                    var traceables = this.State.GetTraceables(arg);
                    UpdateCall(methodCallStmt);
                    this.State.AssignTraceables(methodCallStmt.Result, traceables);
                    
                }
                //else if (methodInvoked.Name == "get_Current" && methodInvoked.ContainingType.IsIEnumerable())
                //{
                //    var arg = methodCallStmt.Arguments[0];
                //    this.State.CopyTraceables(methodCallStmt.Result, arg);
                //    UpdatePTAForPure(methodCallStmt);
                //}
                //else if (methodInvoked.Name == "GetEnumerator"
                //    && methodInvoked.ContainingType.IsIEnumerable())
                //{
                //    var arg = methodCallStmt.Arguments[0];
                //    this.State.CopyTraceables(methodCallStmt.Result, arg);
                //    UpdatePTAForPure(methodCallStmt);
                //}
                else if (methodInvoked.Name.Value == "Any") //  && methodInvoked.ContainingType.FullName == "Enumerable")
                {
                    UpdateCall(methodCallStmt);
                }
                // Check for a predefined set of pure methods and we just propagate the arguments to the return value (and update the PT graph)
                else if(pureCollectionMethods.Contains(methodInvoked.Name.Value) 
                    && methodInvoked.ContainingType.IsCollection())
                {
                    UpdateCall(methodCallStmt);
                }
                else if(methodInvoked.IsPure() || pureEnumerationMethods.Contains(methodInvoked.Name.Value) 
                    && methodInvoked.ContainingType.IsEnumerable())
                {
                    UpdateCall(methodCallStmt);
                }
                else if(pureCollectionMethods.Contains(methodInvoked.Name.Value) 
                    && methodInvoked.IsContainerMethod())
                {
                    UpdateCall(methodCallStmt);
                }
                else if (pureCollectionMethods.Contains(methodInvoked.Name.Value) 
                    && methodInvoked.ContainingType.IsSet())
                {
                    UpdateCall(methodCallStmt);
                }
                else if (pureCollectionMethods.Contains(methodInvoked.Name.Value) 
                    && methodInvoked.ContainingType.IsDictionary())
                {
                    UpdateCall(methodCallStmt);
                }
                else
                {
                    result = false;
                }
                    
                return result;
            }

            private void CheckFailure(Instruction instruction, IEnumerable<Traceable> traceables)
            {
                if (!traceables.Any())
                {
					// DIEGODIEGODIEGO: We need to check this
					// When the analysis fail I'm giving a new opportunity 
					// and I mark the block as invalid. 
					if (this.numberOfVisits > MAX_ITERATIONS)
					{
						this.State.SetTOP();
						AnalysisStats.AddAnalysisReason(new AnalysisReason(this.method, instruction, "We are expecting a traceable and there isn't any"));
					}
					else
						this.validBlock = false;
					

                }
            }

            private void CheckFailure(Instruction instruction, IVariable var)
            {
                CheckFailure(instruction, this.State.GetTraceables(var));
            }

			private bool HandleScopeRowMethods(MethodCallInstruction methodCallStmt, IMethodReference methodInvoked)
			{
				var result = true;
				if (methodInvoked.Name.Value == "Clone" && methodInvoked.ContainingType.IsRowType())
				{
					var arg = methodCallStmt.Arguments[0];
					var traceables = this.State.GetTraceables(arg);
					UpdatePTAForScopeMethod(methodCallStmt);
					this.State.AssignTraceables(methodCallStmt.Result, traceables);
					this.scopeData.UpdateSchemaMap(methodCallStmt.Result, arg, this.State);
				}
				// This is when you get rows
				// a2 = a2[v<- a[arg_0]] 
				else if (methodInvoked.Name.Value == "get_Rows" && methodInvoked.ContainingType.IsRowSetType())
				{
					var arg = methodCallStmt.Arguments[0];

					var traceables = this.State.GetTraceables(arg);
					// DIEGODIEGO: We this I handle the RowList as an Enumerator
					// DIEGODIEGO: this.iteratorDependencyAnalysis.pta.ProcessGetEnum(this.State.PTG, methodCallStmt.Offset, arg, methodCallStmt.Result);
					// DIEGODIEGO: remove this line if handle as enumerator 
					UpdatePTAForScopeMethod(methodCallStmt);
					this.State.AssignTraceables(methodCallStmt.Result, traceables);

					// TODO: I don't know I need this
					scopeData.UpdateSchemaMap(methodCallStmt.Result, arg, this.State);

					CheckFailure(methodCallStmt, traceables);
				}
				// This is when you get enumerator (same as get rows)
				// a2 = a2[v <- a[arg_0]] 
				else if (methodInvoked.Name.Value == "GetEnumerator"
					&& (methodInvoked.ContainingType.IsIEnumerableRow()
					   || methodInvoked.ContainingType.IsIEnumerableScopeMapUsage()))
				{
					var arg = methodCallStmt.Arguments[0];
					var traceables = this.State.GetTraceables(arg);
					UpdatePTAForScopeMethod(methodCallStmt);
					this.State.AssignTraceables(methodCallStmt.Result, traceables);

					CheckFailure(methodCallStmt, traceables);
				}
				// v = arg.Current
				// a2 := a2[v <- Table(i)] if Table(i) in a2[arg]
				else if (methodInvoked.Name.Value == "get_Current"
					&& (methodInvoked.ContainingType.IsIEnumeratorRow()
						 || methodInvoked.ContainingType.IsIEnumeratorScopeMapUsage()))
				{
					//if (this.iteratorDependencyAnalysis.processToAnalyze.ProcessorClass.GetName() == "ResourceDataTagFlattener")
					//{ }

					var arg = methodCallStmt.Arguments[0];
					var traceables = this.State.GetTraceables(arg);
					UpdatePTAForScopeMethod(methodCallStmt);
					this.State.AssignTraceables(methodCallStmt.Result, traceables);

					CheckFailure(methodCallStmt, traceables);
				}
				// v = arg.Current
				// a2 := a2[v <- Table(i)] if Table(i) in a2[arg]
				else if (methodInvoked.Name.Value == "MoveNext" && methodInvoked.ContainingType.IsIEnumerator())
				{
					var arg = methodCallStmt.Arguments[0];
					var tablesCounters = this.State.GetTraceables(arg).OfType<TraceableTable>()
										.Select(table_i => new TraceableCounter(table_i));

					UpdatePTAForScopeMethod(methodCallStmt);
					this.State.AssignTraceables(methodCallStmt.Result, tablesCounters);

				}
				// v = arg.getItem(col)
				// a2 := a2[v <- Col(i, col)] if Table(i) in a2[arg]
				else if (methodInvoked.Name.Value == "get_Item" && methodInvoked.ContainingType.IsRowType())
				{
					if (this.iteratorDependencyAnalysis.processToAnalyze.ProcessorClass.GetName() == "TableReducerBase")
					{ }

					var arg = methodCallStmt.Arguments[0];
					var col = methodCallStmt.Arguments[1];

					var s = TryToGetSchemaForTable(arg, methodCallStmt);
					if (s != null)
					{
						var columnLiteral = UpdateColumnData(methodCallStmt, s); //  ObtainColumn(col, s);

						AddColumnTraceable(methodCallStmt, arg, columnLiteral);
					}
					//scopeData.UpdateSchemaMap(methodCallStmt.Result, arg, this.State);
				}
				// arg.Set(arg1)
				// a4 := a4[arg0 <- a4[arg0] U a2[arg1]] 
				else if ((methodInvoked.Name.Value == "Set" || methodInvoked.Name.Value == "UnsafeSet") && methodInvoked.ContainingType.IsColumnDataType())
				//                                        && methodInvoked.ContainingType.ContainingAssembly.Name == "ScopeRuntime")
				{
					if (this.iteratorDependencyAnalysis.processToAnalyze.ProcessorClass.GetName() == "TableReducerBase")
					{ }

					var arg0 = methodCallStmt.Arguments[0];
					var arg1 = methodCallStmt.Arguments[1];

					var traceables = this.State.GetTraceables(arg1);
					UpdatePTAForScopeMethod(methodCallStmt);
					this.State.AddOutputTraceables(arg0, traceables);

					//var controlTraceables = this.State.Dependencies.ControlTraceables; // this.State.Dependencies.ControlVariables.SelectMany(controlVar => this.State.GetTraceables(controlVar));
					var controlTraceables = this.State.Dependencies.ControlVariables.SelectMany(controlVar => this.State.GetTraceables(controlVar));
					this.State.AddOutputControlTraceables(arg0, controlTraceables);

					//CheckFailure(methodCallStmt, traceables);
				}
				else if (methodInvoked.Name.Value == "CopyTo" && methodInvoked.ContainingType.IsColumnDataType())
				//                                        && methodInvoked.ContainingType.ContainingAssembly.Name == "ScopeRuntime")
				{
					var arg0 = methodCallStmt.Arguments[0];
					var arg1 = methodCallStmt.Arguments[1];

					var traceables = this.State.GetTraceables(arg0);
					UpdatePTAForScopeMethod(methodCallStmt);
					this.State.AddOutputTraceables(arg1, traceables);

					//var controlTraceables = this.State.Dependencies.ControlTraceables; //this.State.Dependencies.ControlVariables.SelectMany(controlVar => this.State.GetTraceables(controlVar));
					var controlTraceables = this.State.Dependencies.ControlVariables.SelectMany(controlVar => this.State.GetTraceables(controlVar));
					this.State.AddOutputControlTraceables(arg1, controlTraceables);

					//CheckFailure(methodCallStmt, traceables);

				}
				// arg.Copy(arg1)
				// a4 := a4[arg1 <- a4[arg1] U a2[arg0]] 
				else if (methodInvoked.Name.Value == "CopyTo" && methodInvoked.ContainingType.IsRowType())
				{
					// TODO: This is a pass-through!
					var arg0 = methodCallStmt.Arguments[0];
					var arg1 = methodCallStmt.Arguments[1];

					var inputSchema = this.iteratorDependencyAnalysis.processToAnalyze.InputSchema;

					//var inputTable = scopeData.GetTableFromSchemaMap(arg0).First(t => t.TableKind == ProtectedRowKind.Input); 
					//var outputTable = scopeData.GetTableFromSchemaMap(arg1).First(t => t.TableKind == ProtectedRowKind.Output);
					var inputTable = TryToGetTable(arg0);
					var outputTable = TryToGetTable(arg1);

					//if (this.State.HasTraceables(arg0) && this.State.HasTraceables(arg1))
					if (inputTable != null && inputTable.TableKind == ProtectedRowKind.Input
										   && outputTable != null && outputTable.TableKind == ProtectedRowKind.Output)
					{
						this.iteratorDependencyAnalysis.CopyRow(this.State, arg1, inputSchema, inputTable, outputTable);
					}
					else
					{
						// TODO: This is OVERLY conservative. I should wait until the end of the fixpoint
						// We need to check somehow at the end if the information has not propagated 
						// One option: remenber arg0 and arg1 and check at the end if they have traceables. 
						// If they have (because of SSA) they will be assigned only here
						if (this.numberOfVisits > MAX_ITERATIONS)
						{
							this.State.SetTOP();
							AnalysisStats.AddAnalysisReason(new AnalysisReason(this.method, methodCallStmt, "Could not determine the input or output table"));
						}
						else
							this.validBlock = false;
					}

				}
				else if (methodInvoked.Name.Value == "Reset" && methodInvoked.ContainingType.IsRowType())
				{
					// TODO: Check semantics of Reset. They apply Reset to each column
					// Row.Reset(r) 
					// Now I just look if if is an output column and do nothing.
					var arg = methodCallStmt.Arguments[0];
					var outputTable = TryToGetTable(arg);
				}
				else if ((methodInvoked.Name.Value.Contains("get_") || methodInvoked.Name.Value == "Get")
							&& methodInvoked.Type.IsColumnDataType() && methodInvoked.ContainingType.IsRowType() && !methodInvoked.ContainingType.IsStrictRowType())
				{
					// DIEGODIEGO: This is a case of a column used as a property
					// DIEGODIEGO: Check check
					var arg = methodCallStmt.Arguments[0];
					var columnLiteral = methodInvoked.Name.Value.Substring(4);

					var s = TryToGetSchemaForTable(arg, methodCallStmt);
					if (s != null)
					{
						var column = s.GetColumn(columnLiteral) ?? new Column(columnLiteral, RangeDomain.BOTTOM, null);
						AddColumnTraceable(methodCallStmt, arg, column);
					}
				}

				else if ((methodInvoked.Name.Value.Contains("get_") || methodInvoked.Name.Value == "Get")
					&& methodInvoked.ContainingType.IsColumnDataType())
				{
					var arg = methodCallStmt.Arguments[0];

					var traceables = this.State.GetTraceables(arg);
					UpdatePTAForScopeMethod(methodCallStmt);
					this.State.AssignTraceables(methodCallStmt.Result, traceables);

					CheckFailure(methodCallStmt, traceables);

				}
				else if (methodInvoked.Name.Value == "Load" && methodInvoked.ContainingType.IsRowListType())
				{
					var receiver = methodCallStmt.Arguments[0];
					var arg1 = methodCallStmt.Arguments[1];
					this.State.AddTraceables(receiver, arg1);
					// DIEGODIEGO: Need to add a edge from arg0 to arg1?

					CheckFailure(methodCallStmt, arg1);

				}
				else if (methodInvoked.ContainingType.IsScopeMap())
				{
					if (methodInvoked.Name.Value == "ContainsKey")
					{

					}
					else if (methodInvoked.Name.Value == "get_Item")
					{
						var receiver = methodCallStmt.Arguments[0];
						var arg1 = methodCallStmt.Arguments[1];
						// receiver is of type ScopeMap and should have at least one input column in the traceables
						var traceables = this.State.GetTraceables(receiver);
						var key = (this.equalities.GetValue(arg1) as Constant).Value as string;
						var scopeMapTraceables = traceables.OfType<TraceableColumn>().Where(t => t.TableKind == ProtectedRowKind.Input).Select(t => new TraceableScopeMap(t.Table, t.Column, key));
						UpdatePTAForScopeMethod(methodCallStmt);

						this.State.AssignTraceables(methodCallStmt.Result, scopeMapTraceables);
					}
				}
				else if (methodInvoked.ContainingType.IsStrictRowType())
				{
					// DIEGODIEGO: I should group operations by type and do something better for this case
					UpdateCall(methodCallStmt);
				}
				else if (methodInvoked.ContainingType.IsScopeRuntime()) // .ContainingNamespace=="ScopeRuntime")
				{
					UpdateCall(methodCallStmt);
				}
				else
				{
					result = false;
				}

                //if(result && methodCallStmt.HasResult)
                //{
                //    UpdatePTAForPure(methodCallStmt);
                //}

                return result;

            }

			private void AddColumnTraceable(MethodCallInstruction methodCallStmt, IVariable arg, Column columnLiteral)
			{
				var tableColumns = this.State.GetTraceables(arg).OfType<TraceableTable>()
															.Select(table_i => new TraceableColumn(table_i, columnLiteral));

				UpdatePTAForScopeMethod(methodCallStmt);
				this.State.AssignTraceables(methodCallStmt.Result, tableColumns);

				this.iteratorDependencyAnalysis.InputColumns.AddRange(tableColumns.Where(t => t.TableKind == ProtectedRowKind.Input));
				this.iteratorDependencyAnalysis.OutputColumns.AddRange(tableColumns.Where(t => t.TableKind == ProtectedRowKind.Output));

				CheckFailure(methodCallStmt, tableColumns);
			}

			/// <summary>
			/// Obtain the column referred by a variable
			/// </summary>
			/// <param name="col"></param>
			/// <returns></returns>
			private Column ObtainColumn(IVariable col, Schema schema)
            {
                Column result = result = Column.TOP; 
                var columnLiteral = "";
                if (col.Type.TypeEquals(Types.Instance.PlatformType.SystemString))
                {
                    var columnValue = this.equalities.GetValue(col);
                    if (columnValue is Constant)
                    {
                        columnLiteral = (columnValue as Constant).Value.ToString();
                        result = schema.GetColumn(columnLiteral) ?? new Column(columnLiteral, RangeDomain.BOTTOM, null);
                    }
                }
                else
                {
                    if (scopeData.columnVariable2Literal.ContainsKey(col))
                    {
                        columnLiteral = scopeData.columnVariable2Literal[col];
                        result = schema.GetColumn(columnLiteral) ?? new Column(columnLiteral, RangeDomain.BOTTOM, null);
                    }
                    else
                    {
                        var rangeForColumn = variableRanges.GetValue(col);
                        if (!rangeForColumn.IsBottom)
                        {
                            result = schema.GetColumn(rangeForColumn) ?? new Column(null, rangeForColumn, null);
                        }
                        else
                        {
                            var colValue = this.equalities.GetValue(col);

                            if (colValue is Constant)
                            {
                                var value = colValue as Constant;
                                var r = new RangeDomain((int)value.Value);
                                result = schema.GetColumn(r) ?? new Column(null, r, null);
                            }
                        }
                    }
                }
                return result;
            }

            private bool IsSchemaMethod(IMethodReference methodInvoked)
            {
                if (methodInvoked.ContainingType.ContainingAssembly() != "ScopeRuntime")
                {
                    return false;
                }
                return methodInvoked.Name.Value == "get_Schema"
                    && (methodInvoked.ContainingType.GetName() == "RowSet" 
                    || methodInvoked.ContainingType.GetName() == "Row");
            }

            private bool IsSchemaItemMethod(IMethodReference methodInvoked)
            {
                return methodInvoked.Name.Value == "get_Item"
                    && (methodInvoked.ContainingType.TypeEquals(ScopeTypes.Schema));
            }

            private bool IsIndexOfMethod(IMethodReference methodInvoked)
            {
                if (methodInvoked.ContainingType.ContainingAssembly() != "ScopeRuntime")
                {
                    return false;
                }

                return methodInvoked.Name.Value == "IndexOf" && methodInvoked.ContainingType.TypeEquals(ScopeTypes.Schema);
            }

            private bool IsMethodToInline(IMethodReference methodInvoked, ITypeReference clousureType, bool hasTraceables)
            {
                if(methodInvoked.Name.Value==".ctor")
                {
                    return true;
                }
                var patterns = new string[] { "<>m__Finally", "System.IDisposable.Dispose" };
				var specialMethods = new Tuple<string, string>[] { }; // { Tuple.Create("System.IDisposable", "Dispose") };
                var result = methodInvoked.ContainingType != null 
                    && ( methodInvoked.ContainingType.TypeEquals(clousureType) && patterns.Any(pattern => methodInvoked.Name.Value.StartsWith(pattern))
                         || specialMethods.Any(sm => sm.Item1 == methodInvoked.ContainingType.GetFullName() 
                         && sm.Item2 == methodInvoked.Name.Value));

				if (methodInvoked.ResolvedMethod != null && (MemberHelper.IsGetter(methodInvoked.ResolvedMethod) ||
					MemberHelper.IsSetter(methodInvoked.ResolvedMethod) && hasTraceables))
				{
					return true;
				}
				// DIEGODIEGO: Method we want to force interprocedural analysis
				// A better way would be a white list or a limited scope of intraproc
				if (methodInvoked.Name.Value == "ParseJson" || methodInvoked.Name.Value == "GetAttributeValue")
					return true;
				if (methodInvoked.ContainingType.GetFullName().Contains("Helper"))
					return true;

               return result;
             }

            private Schema TryToGetSchemaForTable(IVariable arg, Instruction methodCallStmt)
            {
                Schema result = null;

                if (scopeData.HasTableForSchemaVar(arg))
                {
                    var tables = scopeData.GetTableFromSchemaMap(arg);
                    var schema = this.iteratorDependencyAnalysis.processToAnalyze.InputSchema;
                    var table = tables.OfType<TraceableTable>().FirstOrDefault();
                    if (table != null)
                    {
                        if (table.TableKind == ProtectedRowKind.Output)
                        {
                            schema = this.iteratorDependencyAnalysis.processToAnalyze.OutputSchema;
                        }
                        result = schema;
                    }
                    else
                    {
                        this.State.SetTOP();
                        AnalysisStats.AddAnalysisReason(new AnalysisReason(this.method, methodCallStmt, "Table not available as traceable argument"));
                    }
                }
                else
                {
                    if (this.State.HasTraceables(arg))
                    {
                        var table = this.State.GetTraceables(arg).OfType<TraceableTable>().FirstOrDefault(); // BUG: what if there are more than one?
                        if (table != null)
                        {
                            var tableType = table.TableKind;
                            Schema s;
                            if (tableType == ProtectedRowKind.Input)
                                s = this.iteratorDependencyAnalysis.processToAnalyze.InputSchema;
                            else
                                s = this.iteratorDependencyAnalysis.processToAnalyze.OutputSchema;
                            return s;
                        }
                        else
                        {
                            this.State.SetTOP();
                            AnalysisStats.AddAnalysisReason(new AnalysisReason(this.method, methodCallStmt, "Table not available as traceable argument"));
                            return result;
                        }
                    }
					// DIEGODIEGODIEGO: We need to check this
					// Check comment about having analyzed enough
					if (this.numberOfVisits > MAX_ITERATIONS)
					{
						this.State.SetTOP();
						AnalysisStats.AddAnalysisReason(new AnalysisReason(this.method, methodCallStmt, "Scope Table mapping not available. Could not get schema"));
					}
					else
						this.validBlock = false;
                }
                return result;
            }

            TraceableTable TryToGetTable(IVariable arg)
            {
                TraceableTable result = null;

                if (scopeData.HasTableForSchemaVar(arg))
                {
                    var tables = scopeData.GetTableFromSchemaMap(arg);
                    var schema = this.iteratorDependencyAnalysis.processToAnalyze.InputSchema;
                    var table = tables.OfType<TraceableTable>().FirstOrDefault();
                    return table;
                }
                else
                {
                    if (this.State.HasTraceables(arg))
                    {
                        var table = this.State.GetTraceables(arg).OfType<TraceableTable>().FirstOrDefault(); // BUG: what if there are more than one?
                        return table;
                    }
                }
                return result;
            }

            /// <summary>
            /// These are method that access columns by name or number 
            /// </summary>
            /// <param name="methodCallStmt"></param>
            /// <param name="methodInvoked"></param>
            /// <param name="callResult"></param>
            /// <returns></returns>
            private bool HandleSchemaRelatedMethod(MethodCallInstruction methodCallStmt, IMethodReference methodInvoked)
            {
                var result = true;
                // this is callResult = arg.Schema(...)
                // we associate arg the table and callResult with the schema
                if (IsSchemaMethod(methodInvoked))
                {
                    var arg = methodCallStmt.Arguments[0];
                    scopeData.UpdateSchemaMap(methodCallStmt.Result, arg, this.State);
                }
                else if (IsSchemaItemMethod(methodInvoked))
                {
                    var arg = methodCallStmt.Arguments[0];

                    var schema = TryToGetSchemaForTable(arg, methodCallStmt);

                    Column column = UpdateColumnData(methodCallStmt, schema);
                }
                // callResult = arg.IndexOf(colunm)
                // we recover the table from arg and associate the column number with the call result
                else if (IsIndexOfMethod(methodInvoked))
                {
                    var arg = methodCallStmt.Arguments[0];

                    var schema = TryToGetSchemaForTable(arg, methodCallStmt);
                    Column column = UpdateColumnData(methodCallStmt, schema);

                    //if (scopeData.HasTableForSchemaVar(arg))
                    //{
                    //    var tables = scopeData.GetTableFromSchemaMap(arg);
                    //    var schema = ScopeProgramAnalysis.ScopeProgramAnalysis.InputSchema;

                    //    var tableKind = tables.OfType<TraceableTable>().FirstOrDefault().TableKind;
                    //    if (tableKind == ProtectedRowKind.Output)
                    //    {
                    //        schema = ScopeProgramAnalysis.ScopeProgramAnalysis.OutputSchema;
                    //    }

                    //    Column column = UpdateColumnData(methodCallStmt, schema);
                    //}
                    //else
                    //{
                    //    this.State.SetTOP();
                    //    AnalysisStats.AddAnalysisReason(new AnalysisReason(this.method, methodCallStmt, "Scope Table mapping not available. Schema passed as parameter?"));
                    //}

                    //this.State.AssignTraceables(methodCallStmt.Result, tables.OfType<TraceableTable>().Select(t => new TraceableColumn(t, column)));
                }
                else
                {
                    result = false;
                }
                return result;
            }

            private Column UpdateColumnData(MethodCallInstruction methodCallStmt, Schema s)
            {
                if (s == null) return Column.TOP;

                var column = ObtainColumn(methodCallStmt.Arguments[1], s);
                scopeData.UpdateColumnLiteralMap(methodCallStmt, column);
                if(column.IsTOP)
                {
                    AnalysisStats.AddAnalysisReason(new AnalysisReason(this.method, methodCallStmt,
                                                    String.Format(CultureInfo.InvariantCulture, "Could not compute a value for the column {0} {1}", methodCallStmt.Arguments[0], methodCallStmt.Arguments[1])));

                }
                return column;
            }

            /// <summary>
            /// Propagates dependencies from uses to defs
            /// </summary>
            /// <param name="instruction"></param>
            private void UpdateUsingDefUsed(Instruction instruction)
            {
                if (instruction.IsConstructorCall())
                {
                    UpdateCtor((MethodCallInstruction)instruction);
                }
                else
                {
                    foreach (var result in instruction.ModifiedVariables)
                    {
                        var traceables = new HashSet<Traceable>();
                        foreach (var arg in instruction.UsedVariables)
                        {
                            var tables = this.State.GetTraceables(arg).Where(t => !(t is Other));
                            traceables.UnionWith(tables);
                        }
                        this.State.AssignTraceables(result, traceables);
                    }
                }
            }

            private void UpdateCall(MethodCallInstruction methodCallStmt, bool updatePTG = true)
            {
                if (methodCallStmt.IsConstructorCall())
                {
                    UpdateCtor(methodCallStmt);
                }
                else
                {
                    foreach (var result in methodCallStmt.ModifiedVariables)
                    {
                        var traceables = new HashSet<Traceable>();
                        foreach (var arg in methodCallStmt.UsedVariables)
                        {
                            // If a paramete is a delegate we try to evaluate it with the parameters available
                            if (methodCallStmt is MethodCallInstruction && arg.Type.IsDelegateType())
                            {
                                var methodCall = methodCallStmt as MethodCallInstruction;
                                traceables.UnionWith(EvaluateDelegate(arg, methodCall));
                            }
                            else
                            {
                                var tables = this.State.GetTraceables(arg).Where(t => !(t is Other));
                                traceables.UnionWith(tables);
                            }
                        }
                        traceables.AddRange(GetCallTraceables(methodCallStmt));
						if (updatePTG)
						{
							// This update make sure that return value and arguments get 
							UpdatePTAForPure(methodCallStmt);
						}
                        this.State.AssignTraceables(result, traceables);
                    }
                }
            }

            private IEnumerable<Traceable>  GetCallTraceables(MethodCallInstruction methodCallStmt)
            {
                var result = new HashSet<Traceable>();
                string argString = String.Join(",", methodCallStmt.Arguments.Select(arg => arg.Type.GetName()).ToList());
                //var argList = new HashSet<Traceable>( methodCallStmt.Arguments.SelectMany(arg => this.State.GetTraceables(arg)));
                //string argString = "["+ String.Join(",", argList) +"]";
                result.Add(new Other(String.Format("{0}({1})", methodCallStmt.Method.Name,argString)));
                //foreach(var arg in methodCallStmt.Arguments)
                //{
                //    result.AddRange(this.State.GetTraceables(arg));
                //}
                return result;
            }

            private void UpdateCtor(MethodCallInstruction constructor)
            {
                PropagateArguments(constructor, constructor.Arguments[0]);
            }
            private void PropagateArguments(MethodCallInstruction methodCall, IVariable result)
            {
                var traceables = new HashSet<Traceable>();
                for(int i=0; i< methodCall.Arguments.Count; i++)
                {
                    var arg = methodCall.Arguments[i];
                    if (arg != result)
                    {
                        // If a paramete is a delegate we try to evaluate it with the parameters available
                        if (arg.Type.IsDelegateType())
                        {
                            traceables.UnionWith(EvaluateDelegate(arg, methodCall));
                        }
                        else
                        {
                            var tables = this.State.GetTraceables(arg);
                            traceables.UnionWith(tables);
                        }
                    }
                }
                this.State.AddTraceables(result, traceables);
           }

            // Evaluate a delegate within a non analyzed method invocation
            // Example result = v.Select(lambda). We don;t analyze Select but we need to do somthing with lambda 
            private IEnumerable<Traceable> EvaluateDelegate(IVariable delegateArgument, MethodCallInstruction methodCall)
            {               
                var traceablesFromDelegate = new HashSet<Traceable>();
                var candidates = this.iteratorDependencyAnalysis.interproceduralManager.ComputeDelegate(delegateArgument, this.currentPTG);
                foreach (var resolvedCallee in candidates.Item1)
                {
                    try
                    {
                        var arguments = new List<IVariable>();
                        //for(int i=0; i<resolvedCallee.Body.Parameters.Count;i++)
                        //{
                        var instance = this.currentPTG.GetTargets(delegateArgument).OfType<DelegateNode>().First().Instance;

                        //var body = MethodBodyProvider.Instance.GetBody(resolvedCallee);

                        var parametersCount = 0;
                        if (!resolvedCallee.IsStatic)
                        {
                            arguments.Add(instance);
                            parametersCount++;
                        }
                        for (int i = 0; i < methodCall.Arguments.Count; i++)
                        {
                            parametersCount++; 
                            arguments.Add(methodCall.Arguments[i]);
                        }
                        var interProcInfo = new InterProceduralCallInfo()
                        {
                            Caller = this.method,
                            Callee = resolvedCallee,
                            CallArguments = arguments,
                            CallLHS = methodCall.Result,
                            CallerState = this.State,
                            CallerPTG = currentPTG,
                            ScopeData = this.scopeData,
                            Instruction = methodCall,
							VariableRanges = this.variableRanges,
                            ProtectedNodes = this.iteratorDependencyAnalysis.protectedNodes
                        };

                        var interProcResult = this.iteratorDependencyAnalysis.interproceduralManager.DoInterProcWithCallee(interProcInfo);

                        this.State = interProcResult.State;
						this.iteratorDependencyAnalysis.InputColumns.AddRange(interProcResult.InputColumns);
						this.iteratorDependencyAnalysis.OutputColumns.AddRange(interProcResult.OutputColumns);
						currentPTG = interProcResult.State.PTG;
                        if(methodCall.HasResult)
                            traceablesFromDelegate.AddRange(this.State.GetTraceables(methodCall.Result));
                    }
                    catch (Exception e)
                    {
                        //Console.WriteLine("Could not analyze delegate {0}", resolvedCallee.ToString());
                        //Console.WriteLine("Exception {0}\n{1}", e.Message, e.StackTrace);
                        AnalysisStats.AddAnalysisReason(new AnalysisReason(this.method, methodCall,
                                    String.Format(CultureInfo.InvariantCulture, "Callee {2} throw exception {0}\n{1}", e.Message, e.StackTrace.ToString(), resolvedCallee.ToString())));
                        AnalysisStats.TotalofFrameworkErrors++;
                        this.State.SetTOP();
                    }
                }
            
                return traceablesFromDelegate;
            }
        }

        public IVariable ReturnVariable { get; private set; }

        private IDictionary<IVariable, IExpression> equalities;
        DataFlowAnalysisResult<SimplePointsToGraph>[] ptgs;
        private ScopeInfo scopeData;
        // private IDictionary<string, IVariable> specialFields;

        private ScopeProcessorInfo processToAnalyze; 

        private INamedTypeDefinition iteratorClass;
        private IMethodDefinition method;

        private InterproceduralManager interproceduralManager;
        public bool InterProceduralAnalysisEnabled { get; private set; }

        public DataFlowAnalysisResult<DependencyPTGDomain>[] Result { get; set; }

        private DependencyPTGDomain initValue;

        private IEnumerable<ProtectedRowNode> protectedNodes;

        private SimplePointsToAnalysis pta;
        private RangeAnalysis rangeAnalysis;

        public ISet<TraceableColumn> InputColumns { get; private set; }
        public ISet<TraceableColumn> OutputColumns { get; private set; }


        public IteratorDependencyAnalysis(ScopeProcessorInfo processToAnalyze, IMethodDefinition method, ControlFlowGraph cfg,
											SimplePointsToAnalysis pta,
                                            IEnumerable<ProtectedRowNode> protectedNodes, 
                                            IDictionary<IVariable, IExpression> equalitiesMap,
                                            InterproceduralManager interprocManager, RangeAnalysis rangeAnalysis) : base(cfg)
        {
            this.processToAnalyze = processToAnalyze;
            this.method = method;
            this.iteratorClass = method.ContainingType as INamedTypeDefinition;
            // this.specialFields = specialFields;
            this.ptgs = pta.Result;
            this.equalities = equalitiesMap;
            this.scopeData = new ScopeInfo();
            this.protectedNodes = protectedNodes;
            this.interproceduralManager = interprocManager;
            this.initValue = null;
            this.ReturnVariable = new LocalVariable(method.Name + "_$RV", method) { Type = Types.Instance.PlatformType.SystemObject };
            this.InterProceduralAnalysisEnabled = AnalysisOptions.DoInterProcAnalysis;
            this.pta = pta;
            this.rangeAnalysis = rangeAnalysis;

            this.InputColumns = new HashSet<TraceableColumn>();
            this.OutputColumns = new HashSet<TraceableColumn>();
        }
        public IteratorDependencyAnalysis(ScopeProcessorInfo processToAnalyze, IMethodDefinition method, ControlFlowGraph cfg, SimplePointsToAnalysis pta,
                                    IEnumerable<ProtectedRowNode> protectedNodes, 
                                    IDictionary<IVariable, IExpression> equalitiesMap,
                                    InterproceduralManager interprocManager,
                                    RangeAnalysis rangeAnalysis,
                                    DependencyPTGDomain initValue,
                                    ScopeInfo scopeData) : this(processToAnalyze, method, cfg, pta, protectedNodes, equalitiesMap, interprocManager, rangeAnalysis) //base(cfg)
        {            
            this.initValue = initValue;
            InitVariablesWithTaint(this.initValue);
            this.scopeData = new ScopeInfo();
            this.scopeData.columnFieldMap = scopeData.columnFieldMap;
        }

        public override DataFlowAnalysisResult<DependencyPTGDomain>[] Analyze()
        {
            this.Result = base.Analyze();
            return this.Result;
        }

        protected override DependencyPTGDomain InitialValue(CFGNode node)
        {
            var depValues = new DependencyPTGDomain(new DependencyDomain(), this.pta.GetInitialValue());
            var currentPTG = depValues.PTG;

            if (this.cfg.Entry.Id == node.Id)
            {
                if (this.initValue != null)
                {
                    return this.initValue;
                }

                //var currentPTG = pta.Result[cfg.Exit.Id].Output;

                IVariable thisVar = null;
                if (!this.method.IsStatic && this.method.Body != null)
                {
                    var body = MethodBodyProvider.Instance.GetBody(method);

                    thisVar = body.Parameters[0];
                    System.Diagnostics.Debug.Assert(thisVar.Name == "this");
                    // currentPTG.Variables.Single(v => v.Name == "this");
                    foreach (var SimplePTGNode in currentPTG.GetTargets(thisVar))
                    {
                        foreach (var target in currentPTG.GetTargets(SimplePTGNode))
                        {
                            var potentialRowNode = target.Value.First() as ParameterNode;
                            //if (target.Key.Type.ToString() == "RowSet" || target.Key.Type.ToString() == "Row")
                            if (protectedNodes.Contains(potentialRowNode))
                            {
                                var traceable = new TraceableTable(new ProtectedRowNode(potentialRowNode, ProtectedRowNode.GetKind(potentialRowNode.Type)));
                                depValues.AddHeapTraceables(SimplePTGNode, target.Key, new HashSet<Traceable>() { traceable } );
                                
                                // depValues.Dependencies.A3_Fields.Add(new Location(SimplePTGNode, target.Key), traceable));
                            }
                        }
                    }
                }
                InitVariablesWithTaint(depValues);
            }
            return depValues;
        }

        private void InitVariablesWithTaint(DependencyPTGDomain depValues)
        {
			foreach (var v in cfg.GetVariables())
            {
                // The framework has problems with  type resolution
                // This is a workaround until the problem is fix
                if (v.Type != null)
                {
					if (!SongTaoDependencyAnalysis.IsScopeType(v.Type) && !v.IsParameter && (!v.Type.IsClassOrStruct() || v.Type.IsString()))
                    {

						var varRange = rangeAnalysis.Result[cfg.Exit.Id].Input.GetValue(v);
						if (!varRange.IsBottom && !varRange.IsTop)
						{
							if(varRange.IsString)
								depValues.AssignTraceables(v, new HashSet<Traceable>() { new Other(varRange.Literal) });
							else
								depValues.AssignTraceables(v, new HashSet<Traceable>() { new Other(varRange.LowerBound.ToString()) });
						}
						else
						{
							depValues.AssignTraceables(v, new HashSet<Traceable>() { new Other(v.Type.GetName()) });
						}
                    }
                }
                else
                {
                    v.Type = Types.Instance.PlatformType.SystemObject;
                    depValues.AssignTraceables(v, new HashSet<Traceable>() { new Other("null") });
                }
            }
        }

        protected override bool Compare(DependencyPTGDomain newState, DependencyPTGDomain oldSTate)
        {
            return newState.LessEqual(oldSTate);
        }

        protected override DependencyPTGDomain Join(DependencyPTGDomain left, DependencyPTGDomain right)
        {
            var result = left.Join(right);
            return result;
        }

        protected override DependencyPTGDomain Copy(DependencyPTGDomain elem)
        {
            return elem.Clone();
        }

		IDictionary<CFGNode,int> numberOfVisits = new Dictionary<CFGNode,int>();

        protected override DependencyPTGDomain Flow(CFGNode node, DependencyPTGDomain input)
        {
            if (input.IsTop)
                return input;

            var oldInput = input.Clone();
			// var currentPTG = pta.Result[node.Id].Output;


			// var predecessorsVisited = node.Predecessors.All(n => visited.Contains(n));

			int count = 0;
			if (!numberOfVisits.TryGetValue(node, out count))
			{
				numberOfVisits[node] = 0;
			}

			// A visitor for the dependency analysis
			var visitor = new MoveNextVisitorForDependencyAnalysis(this, node, oldInput, count);
            visitor.Visit(node);

			numberOfVisits[node] = count + 1;

            return visitor.State;
        }
        public void CopyRow(DependencyPTGDomain state, IVariable arg1,
                                Schema inputSchema, TraceableTable inputTable, TraceableTable outputTable)
        {

            //var inputTable = this.State.GetTraceables(arg0).OfType<TraceableTable>().First(t => t.TableKind == ProtectedRowKind.Input);
            //var outputTable = this.State.GetTraceables(arg1).OfType<TraceableTable>().First(t => t.TableKind == ProtectedRowKind.Output);

            foreach (var column in inputSchema.Columns)
            {
                var traceableInputColumn = new TraceableColumn(inputTable, column);
                var traceableOutputColumn = new TraceableColumn(outputTable, column);

                var outputColumnVar = new TemporalVariable(arg1.Name + "_$" + column.Name, 1, method) { Type = Types.Instance.PlatformType.SystemVoid };
                state.AssignTraceables(outputColumnVar, new Traceable[] { traceableOutputColumn });

                state.AddOutputTraceables(outputColumnVar, new Traceable[] { traceableInputColumn });

				//var traceables = state.Dependencies.ControlTraceables; 
				var traceables = state.Dependencies.ControlVariables.SelectMany(controlVar => state.GetTraceables(controlVar));

				state.AddOutputControlTraceables(outputColumnVar, traceables);

                this.InputColumns.Add(traceableInputColumn);
                this.OutputColumns.Add(traceableOutputColumn);

            }
        }
    }

    internal static class AnalysisOptions
    {
        public static bool DoInterProcAnalysis { get; internal set; }
    }
    #endregion
}
