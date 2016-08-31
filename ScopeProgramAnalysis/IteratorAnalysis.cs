using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Backend.Model;
using Model.ThreeAddressCode.Instructions;
using Model.ThreeAddressCode.Visitor;
using Model.ThreeAddressCode.Values;
using Model.ThreeAddressCode.Expressions;
using Backend.Utils;
using Model.Types;
using Model;
using System.Globalization;
using ScopeProgramAnalysis;
using static Backend.Analyses.IteratorPointsToAnalysis;
using ScopeProgramAnalysis.Framework;

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
            return String.Format("{0}({1})", this.Name, this.Position);
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


    public class Location // : PTGNode
    {
        PTGNode ptgNode = null;
        public IFieldReference Field { get; set; }

        public Location(PTGNode node, IFieldReference f) 
        {
            this.ptgNode = node;
            this.Field = f;
        }

        public Location(IFieldReference f) 
        {
            this.ptgNode = SimplePointsToGraph.GlobalNode;
            this.Field = f;
        }
        public override bool Equals(object obj)
        {
            var oth = obj as Location;
            return oth!=null && oth.ptgNode.Equals(this.ptgNode)
                && oth.Field.Equals(this.Field);
        }
        public override int GetHashCode()
        {
            return ptgNode.GetHashCode() + Field.GetHashCode();
        }
        public override string ToString()
        {
            return "[" + ptgNode.ToString() +"."+  Field.ToString() + "]";
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
        private PTGNode ptgNode = null;
        public AbstractObject(PTGNode ptgNode)
        {
            //this.variable = variable;

        }
        public string Name
        {
            get
            {
                return String.Join(",", ptgNode.Variables);

            }
        }
        public override bool Equals(object obj)
        {
            var oth = obj as AbstractObject;
            return oth!=null && oth.ptgNode.Equals(this.ptgNode);
        }
        public override int GetHashCode()
        {
            return ptgNode.GetHashCode();
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
                columnVariable2Literal[methodCallStmt.Result] = columnLiteral.ToString();
            }

            internal void PropagateLoad(LoadInstruction loadStmt, InstanceFieldAccess fieldAccess)
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
        }

        internal class MoveNextVisitorForDependencyAnalysis : InstructionVisitor
        {
            private IDictionary<IVariable, IExpression> equalities;
            private IteratorDependencyAnalysis iteratorDependencyAnalysis;

            private ScopeInfo scopeData;
            internal DependencyPTGDomain State { get; private set; }
            private SimplePointsToGraph currentPTG;
            private CFGNode cfgNode;
            private MethodDefinition method;
            private PTAVisitor visitorPTA;
            private VariableRangeDomain variableRanges;

            public MoveNextVisitorForDependencyAnalysis(IteratorDependencyAnalysis iteratorDependencyAnalysis, PTAVisitor visitorPTA,
                                   CFGNode cfgNode,  IDictionary<IVariable, IExpression> equalities, 
                                   ScopeInfo scopeData, SimplePointsToGraph ptg, DependencyPTGDomain oldInput)
            {
                this.iteratorDependencyAnalysis = iteratorDependencyAnalysis;
                this.equalities = equalities;
                this.scopeData = scopeData;
                this.State = oldInput;
                this.currentPTG = ptg;
                this.cfgNode = cfgNode;
                this.method = iteratorDependencyAnalysis.method;
                this.visitorPTA = visitorPTA;
                this.variableRanges = this.iteratorDependencyAnalysis.rangeAnalysis.Result[cfgNode.Id].Output;
            }

            private bool IsClousureType(IVariable instance)
            {
                return instance.Type.Equals(this.iteratorDependencyAnalysis.iteratorClass);
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
                if(instanceType is PointerType)
                {
                    instanceType= (instanceType as PointerType).TargetType;
                }

                if ( instanceType.Equals(this.iteratorDependencyAnalysis.iteratorClass)) 
                {
                    return true;
                }
                var isClousureField = this.iteratorDependencyAnalysis.iteratorClass.Equals(field.ContainingType);

                bool instanceIsCompilerGeneratedSibblingClass = false;
                var typeAsClass = (instance.Type as IBasicType);
                if (typeAsClass != null && typeAsClass.ResolvedType != null)
                {
                    var typeAsClassResolved = (typeAsClass.ResolvedType as ClassDefinition);
                    instanceIsCompilerGeneratedSibblingClass = MyTypesHelper.IsCompiledGeneratedClass(typeAsClassResolved)
                                            && typeAsClassResolved.ContainingType != null 
                                            && this.iteratorDependencyAnalysis.iteratorClass.ContainingType != null
                                            && typeAsClassResolved.ContainingType.Equals(this.iteratorDependencyAnalysis.iteratorClass.ContainingType);
                }
                var isReducerField = this.iteratorDependencyAnalysis.iteratorClass.ContainingType != null
                    && this.iteratorDependencyAnalysis.iteratorClass.ContainingType.Equals(field.ContainingType);

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
            //private ISet<PTGNode> GetPtgNodes(IVariable v)
            //{
            //    var res = new HashSet<PTGNode>();
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

            public override void Visit(LoadInstruction instruction)
            {
                instruction.Accept(visitorPTA);

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
                    scopeData.PropagateLoad(loadStmt, fieldAccess);
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

                    //foreach (var ptgNode in currentPTG.GetTargets(baseArray))
                    //{
                    //    // TODO: I need to provide a BasicType. I need the base of the array 
                    //    // Currenly I use the method containing type
                    //    var fakeField = new FieldReference("[]", arrayAccess.Type, method.ContainingType);
                    //    var loc = new Location(ptgNode, fakeField);
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
                    this.State.AssignTraceables(loadStmt.Result, new Traceable[] { new Other(constant.Type.ToString()) });
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

                //if (SongTaoDependencyAnalysis.IsScopeType(fieldAccess.Instance.Type))
                //if (ISClousureField(fieldAccess.Instance, fieldAccess.Field))
                //{
                    
                    // this is a[loc(o.f)]
                    var nodes = currentPTG.GetTargets(fieldAccess.Instance);
                    if (nodes.Any())
                    {
                        // TODO: SHould I only consider the clousure fields?
                        traceables.UnionWith(this.State.GetHeapTraceables(fieldAccess.Instance, fieldAccess.Field));
                        //if(IsClousureType(fieldAccess.Instance))
                        {
                            traceables.AddRange(this.State.GetTraceables(fieldAccess.Instance));
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

            private bool MaybeProctectedNode(PTGNode node)
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
                // TODO: Move to IsClousureField()
                var isClousureField =  this.iteratorDependencyAnalysis.iteratorClass.Name == fieldAccess.Field.ContainingType.Name;
                var isReducerField = this.iteratorDependencyAnalysis.iteratorClass.ContainingType!=null 
                                        && this.iteratorDependencyAnalysis.iteratorClass.ContainingType.Name == fieldAccess.Field.ContainingType.Name;
                // TODO: Hack. I need to check for private fields and properly model 
                if (ISClousureField(IteratorPointsToAnalysis.GlobalVariable, fieldAccess.Field))
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
                    //if (ISClousureField(fieldAccess.Instance, fieldAccess.Field))
                    {
                        var arg = instruction.Operand;
                        var inputTable = equalities.GetValue(arg);

                        // a3 := a3[loc(o.f) <- a2[v]] 
                        // union = a2[v]
                        var OK = this.State.AddHeapTraceables(o, field, instruction.Operand);

                        //var traceables = this.State.GetTraceables(instruction.Operand);
                        //var nodes = currentPTG.GetTargets(o);
                        //if (nodes.Any())
                        //{
                        //    foreach (var ptgNode in nodes)
                        //    {
                        //        var location = new Location(ptgNode, field);
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

                    //foreach (var ptgNode in currentPTG.GetTargets(baseArray))
                    //{
                    //    // TODO: I need to provide a BasicType. I need the base of the array 
                    //    // Currenly I use the method containing type
                    //    var fakeField = new FieldReference("[]", arrayAccess.Type, method.ContainingType);
                    //    //fakeField.ContainingType = PlatformTypes.Object;
                    //    var loc = new Location(ptgNode, fakeField);
                    //    this.State.Dependencies.A3_Fields[new Location(ptgNode, fakeField)] = traceables;
                    //}
                }
                else if (instructionResult is StaticFieldAccess)
                {
                    var field = (instructionResult as StaticFieldAccess).Field;
                    var traceables = this.State.GetTraceables(instruction.Operand);

                    this.State.AddHeapTraceables(SimplePointsToGraph.GlobalNode, field, traceables);

                    //this.State.Dependencies.A3_Fields[new Location(SimplePointsToGraph.GlobalNode, field)] = traceables;

                    this.State.Dependencies.A1_Escaping.UnionWith(traceables);
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

                this.State.Dependencies.ControlVariables.UnionWith(instruction.UsedVariables.Where( v => this.State.GetTraceables(v).Any()));
            }
            public override void Visit(ReturnInstruction instruction)
            {
                instruction.Accept(visitorPTA);

                if (instruction.HasOperand)
                {
                    var rv = this.iteratorDependencyAnalysis.ReturnVariable;
                    this.State.CopyTraceables(this.iteratorDependencyAnalysis.ReturnVariable, rv);
                }
            }
            public override void Visit(CreateObjectInstruction instruction)
            {
                var traceables = new HashSet<Traceable>();
                traceables.Add(new Other(instruction.AllocationType.ToString()));
                instruction.Accept(visitorPTA);
                this.State.AssignTraceables(instruction.Result, traceables);
                
            }
            public override void Visit(MethodCallInstruction instruction)
            {
                instruction.Accept(visitorPTA);

                var methodCallStmt = instruction;
                var methodInvoked = methodCallStmt.Method;
                var callResult = methodCallStmt.Result;

                // We are analyzing instructions of the form this.table.Schema.IndexOf("columnLiteral")
                // to maintain a mapping between column numbers and literals 
                var isSchemaMethod = HandleSchemaRelatedMethod(methodCallStmt, methodInvoked);
                if (!isSchemaMethod)
                {
                    var isScopeRowMethod = HandleScopeRowMethods(methodCallStmt, methodInvoked);
                    if (!isScopeRowMethod)
                    {
                        var isCollectionMethod = HandleCollectionMethod(methodCallStmt, methodInvoked);
                        if(!isCollectionMethod)
                        {
                            // Pure Methods
                            if(IsPureMethod(methodCallStmt))
                            {
                                UpdateCall(methodCallStmt);
                            }
                            else
                            {
                                // I first check in the calle may a input/output row
                                var argRootNodes = methodCallStmt.Arguments.SelectMany(arg => currentPTG.GetTargets(arg, false))
                                                    .Where(n => n!=SimplePointsToGraph.NullNode);

                                // If it is a method within the same class it will be able to acesss all the fields 
                                // I also see that compiler generated methods (like lambbas should also access)
                                var isInternalClassInvocation = methodInvoked.ContainingType.SameType(this.iteratorDependencyAnalysis.iteratorClass);
                                var isCompiledGeneratedLambda = this.method.ContainingType.IsCompilerGenerated() 
                                                                  && this.method.ContainingType.ContainingType!=null &&
                                                                  methodInvoked.ContainingType.SameType(this.iteratorDependencyAnalysis.iteratorClass.ContainingType);

                                Predicate<Tuple<PTGNode, IFieldReference>> fieldFilter = (nodeField => isInternalClassInvocation || isCompiledGeneratedLambda
                                                    || !nodeField.Item2.ContainingType.SameType(this.iteratorDependencyAnalysis.iteratorClass));
                                var reachableNodes = currentPTG.ReachableNodes(argRootNodes, fieldFilter);

                                var escaping = reachableNodes.Intersect(this.iteratorDependencyAnalysis.protectedNodes).Any();


                                if (escaping)
                                {
                                    var isMethodToInline = IsMethodToInline(methodInvoked, this.iteratorDependencyAnalysis.iteratorClass);

                                    if (this.iteratorDependencyAnalysis.InterProceduralAnalysisEnabled || isMethodToInline )
                                    {

                                        // For the demo I'll skip this methods that do anything important
                                        if (isMethodToInline && !methodInvoked.IsConstructor())
                                        { }
                                        else
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
                                    foreach (var escapingNode in argRootNodes.Where(n => n.Kind!=PTGNodeKind.Null))
                                    {
                                        var escapingField = new FieldReference("escape", PlatformTypes.Object, this.method.ContainingType);
                                        currentPTG.PointsTo(SimplePointsToGraph.GlobalNode, escapingField, escapingNode);
                                    }
                                }
                            }
                        }
                    }
                }
            }

            /// <summary>
            /// Updates the points-to graph using only the info from parameter
            /// TODO: We should actually follow the ideas of our IWACO paper...
            /// </summary>
            /// <param name="instruction"></param>
            private void UpdatePTAForPure(MethodCallInstruction instruction)
            {
                if (instruction.HasResult && instruction.Result.Type.IsClassOrStruct())
                {
                    var returnNode = new PTGNode(new PTGID(new MethodContex(this.method), (int)instruction.Offset), instruction.Result.Type, PTGNodeKind.Object);

                    foreach (var result in instruction.ModifiedVariables)
                    {
                        var allNodes = new HashSet<PTGNode>();
                        foreach (var arg in instruction.UsedVariables)
                        {
                            var nodes = this.currentPTG.GetTargets(arg, false);
                            allNodes.UnionWith(nodes);
                        }

                        currentPTG.RemoveRootEdges(result);
                        currentPTG.PointsTo(result, returnNode);
                        var returnField = new FieldReference("$return", PlatformTypes.Object, this.method.ContainingType);
                        foreach (var ptgNode in allNodes)
                        {
                            if (TypeHelper.TypesAreAssignmentCompatible(ptgNode.Type, instruction.Result.Type))
                            {
                                currentPTG.PointsTo(returnNode, returnField, ptgNode);
                            }
                            else
                            { }
                        }
                    }

                }
            }

            private void AnalyzeResolvedCallees(MethodCallInstruction instruction, MethodCallInstruction methodCallStmt, IEnumerable<MethodDefinition> calles)
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
                                ProtectedNodes = this.iteratorDependencyAnalysis.protectedNodes
                            };

                            var interProcResult = this.iteratorDependencyAnalysis.interproceduralManager.DoInterProcWithCallee(interProcInfo);
                            callStates.Add(interProcResult.State);

                            this.State = interProcResult.State;
                            currentPTG = interProcResult.State.PTG;

                        }
                        catch (Exception e)
                        {
                            System.Console.WriteLine("Could not analyze {0}", resolvedCallee.ToSignatureString());
                            System.Console.WriteLine("Exception {0}\n{1}", e.Message, e.StackTrace);
                            AnalysisStats.AddAnalysisReason(new AnalysisReason(this.method, methodCallStmt,
                                        String.Format(CultureInfo.InvariantCulture, "Callee {2} throw exception {0}\n{1}", e.Message, e.StackTrace.ToString(), resolvedCallee.ToSignatureString())));
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
                this.State.Dependencies.A1_Escaping.UnionWith(methodCallStmt.Arguments.SelectMany(arg => this.State.GetTraceables(arg)));
                    AnalysisStats.AddAnalysisReason(new AnalysisReason(this.method, methodCallStmt, 
                                                    String.Format(CultureInfo.InvariantCulture, "Invocation to {0} not analyzed with argument potentially reaching the columns", methodCallStmt.Method)));
                    this.State.SetTOP();
                // }
            }

            private bool IsPureMethod(MethodCallInstruction metodCallStmt)
            {
                var result = false;

                if(metodCallStmt.Method.IsPure())
                {
                    return true;
                }

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
                return result;
            }

            public override void Visit(PhiInstruction instruction)
            {
                instruction.Accept(visitorPTA);

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
                    //traceables.UnionWith(tables);
                }
                this.State.AssignTraceables(instruction.Result, traceables);
            }

            public override void Visit(ConvertInstruction instruction)
            {
                var traceables = this.State.GetTraceables(instruction.Operand);
                instruction.Accept(visitorPTA);

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
                var pureEnumerationMethods = new HashSet<String>() { "Select", "Where", "Any", "Count", "GroupBy", "Max", "Min", "First" };
                 
                var result = true;
                // For constructors of collections we create an small summary for the PTA
                if(methodInvoked.IsConstructor() && methodInvoked.ContainingType.IsCollection())
                {
                    var arg = methodCallStmt.Arguments[0];
                    this.iteratorDependencyAnalysis.pta.CreateSummaryForCollection(this.State.PTG, methodCallStmt.Offset, arg);

                }
                // For GetEnum we need to create an object iterator that points-to the colecction
                if (methodInvoked.Name == "GetEnumerator"
                    && (methodInvoked.ContainingType.IsIEnumerable() || methodInvoked.ContainingType.IsEnumerable()))
                {
                     var arg = methodCallStmt.Arguments[0];
                    var traceables = this.State.GetTraceables(arg);
                    // This method makes method.Result point to the collections automatically getting the traceables from there
                    this.iteratorDependencyAnalysis.pta.ProcessGetEnum(this.State.PTG, methodCallStmt.Offset, arg, methodCallStmt.Result);
                }
                // For Current we need to obtain one item from the collection
                else if (methodInvoked.Name == "get_Current"  
                    && (methodInvoked.ContainingType.IsIEnumerator() || methodInvoked.ContainingType.IsEnumerator()))
                {
                    var arg = methodCallStmt.Arguments[0];
                    var traceables = this.State.GetTraceables(arg);
                    // This method makes method.Result point to the collections item, so automatically getting the traceables from there
                    this.iteratorDependencyAnalysis.pta.ProcessGetCurrent(this.State.PTG, methodCallStmt.Offset, arg, methodCallStmt.Result);
                }
                // set_Item add an element to the colecction using a fake field "$item"
                else if (methodInvoked.Name == "set_Item" && (methodInvoked.ContainingType.IsCollection() || methodInvoked.ContainingType.IsDictionary() || methodInvoked.ContainingType.IsSet()))
                {
                    var traceables = this.State.GetTraceables(methodCallStmt.Arguments[2]);

                    PropagateArguments(methodCallStmt, methodCallStmt.Arguments[0]);
                    var itemField = this.iteratorDependencyAnalysis.pta.AddItemforCollection(this.State.PTG, methodCallStmt.Offset, methodCallStmt.Arguments[0], methodCallStmt.Arguments[2]);
                    this.State.AddHeapTraceables(methodCallStmt.Arguments[0], itemField, traceables);
                    // Notice that we add the traceables to the receiver object (arg0.Add(args...))
                    this.State.AddTraceables(methodCallStmt.Arguments[0], traceables);
                }
                // for Add we need to add an element the collection using a fake field "$item"
                else if (methodInvoked.Name == "Add" && (methodInvoked.ContainingType.IsCollection() || methodInvoked.ContainingType.IsDictionary() || methodInvoked.ContainingType.IsSet()))
                {
                    PropagateArguments(methodCallStmt, methodCallStmt.Arguments[0]);
                    if (methodInvoked.ContainingType.IsDictionary())
                    {
                        var itemField = this.iteratorDependencyAnalysis.pta.AddItemforCollection(this.State.PTG, methodCallStmt.Offset, methodCallStmt.Arguments[0], methodCallStmt.Arguments[2]);
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
                else if (methodInvoked.Name == "get_Item"  && (methodInvoked.ContainingType.IsCollection() || methodInvoked.ContainingType.IsDictionary() || methodInvoked.ContainingType.IsSet()))
                {
                    if (methodInvoked.ContainingType.IsDictionary())
                    {
                        var itemField = this.iteratorDependencyAnalysis.pta.GetItemforCollection(this.State.PTG, methodCallStmt.Offset, methodCallStmt.Arguments[0], methodCallStmt.Result);
                        this.State.AssignTraceables(methodCallStmt.Result, this.State.GetHeapTraceables(methodCallStmt.Arguments[0], itemField));
                        // this.State.AddTraceables(methodCallStmt.Result, this.State.GetTraceables(methodCallStmt.Arguments[0]));
                    }
                    else
                    {
                        // For the case of a list or other colections we treat it as an unknowm call (but pure)
                        UpdateCall(methodCallStmt);
                    }
                }
                // For movenext we treated as an unknowm call (but pure, even it modified the it)
                else if (methodInvoked.Name == "MoveNext"
                    && methodInvoked.ContainingType.IsIEnumerator())
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
                else if (methodInvoked.Name == "Any") //  && methodInvoked.ContainingType.FullName == "Enumerable")
                {
                    UpdateCall(methodCallStmt);
                }
                // Check for a predefined set of pure methods and we just propagate the arguments to the return value (and update the PT graph)
                else if(pureCollectionMethods.Contains(methodInvoked.Name) && methodInvoked.ContainingType.IsCollection())
                {
                    UpdateCall(methodCallStmt);
                }
                else if(methodInvoked.IsPure() || pureEnumerationMethods.Contains(methodInvoked.Name) && methodInvoked.ContainingType.IsEnumerable())
                {
                    UpdateCall(methodCallStmt);
                }
                else if(pureCollectionMethods.Contains(methodInvoked.Name) && methodInvoked.IsContainerMethod())
                {
                    UpdateCall(methodCallStmt);
                }
                else if (pureCollectionMethods.Contains(methodInvoked.Name) && methodInvoked.ContainingType.IsSet())
                {
                    UpdateCall(methodCallStmt);
                }
                else if (pureCollectionMethods.Contains(methodInvoked.Name) && methodInvoked.ContainingType.IsDictionary())
                {
                    UpdateCall(methodCallStmt);
                }
                else
                {
                    result = false;
                }
                    
                return result;
            }

            private bool  HandleScopeRowMethods(MethodCallInstruction methodCallStmt, IMethodReference methodInvoked)
            {
                var result = true;
                // This is when you get rows
                // a2 = a2[v<- a[arg_0]] 
                if (methodInvoked.Name == "get_Rows" && methodInvoked.ContainingType.IsRowSetType()) 
                {
                    var arg = methodCallStmt.Arguments[0];

                    var traceables = this.State.GetTraceables(arg);
                    UpdatePTAForPure(methodCallStmt);
                    this.State.AssignTraceables(methodCallStmt.Result, traceables);

                    // TODO: I don't know I need this
                    scopeData.UpdateSchemaMap(methodCallStmt.Result, arg, this.State);
                }
                // This is when you get enumerator (same as get rows)
                // a2 = a2[v <- a[arg_0]] 
                else if (methodInvoked.Name == "GetEnumerator"  
                    && ( methodInvoked.ContainingType.IsIEnumerableRow()
                       || methodInvoked.ContainingType.IsIEnumerableScopeMapUsage()))
                {
                    var arg = methodCallStmt.Arguments[0];
                    var traceables = this.State.GetTraceables(arg);
                    UpdatePTAForPure(methodCallStmt);
                    this.State.AssignTraceables(methodCallStmt.Result, traceables);
                }
                // v = arg.Current
                // a2 := a2[v <- Table(i)] if Table(i) in a2[arg]
                else if (methodInvoked.Name == "get_Current" 
                    && ( methodInvoked.ContainingType.IsIEnumeratorRow()
                         || methodInvoked.ContainingType.IsIEnumeratorScopeMapUsage()))
                {
                    var arg = methodCallStmt.Arguments[0];
                    var traceables = this.State.GetTraceables(arg);
                    UpdatePTAForPure(methodCallStmt);
                    this.State.AssignTraceables(methodCallStmt.Result, traceables);
                }
                // v = arg.Current
                // a2 := a2[v <- Table(i)] if Table(i) in a2[arg]
                else if (methodInvoked.Name == "MoveNext" && methodInvoked.ContainingType.IsIEnumerator())
                {
                    var arg = methodCallStmt.Arguments[0];
                    var tablesCounters = this.State.GetTraceables(arg).OfType<TraceableTable>()
                                        .Select(table_i => new TraceableCounter(table_i));

                    UpdatePTAForPure(methodCallStmt);
                    this.State.AssignTraceables(methodCallStmt.Result, tablesCounters);
                }
                // v = arg.getItem(col)
                // a2 := a2[v <- Col(i, col)] if Table(i) in a2[arg]
                else if (methodInvoked.Name == "get_Item" && methodInvoked.ContainingType.IsRowType())
                {
                    var arg = methodCallStmt.Arguments[0];
                    var col = methodCallStmt.Arguments[1];

                    var tableType = this.State.GetTraceables(arg).OfType<TraceableTable>()
                        .Select(t => t.TableKind).FirstOrDefault(); // BUG: what if there are more than one?
                    Schema s;
                    if (tableType == ProtectedRowKind.Input)
                        s = ScopeProgramAnalysis.ScopeProgramAnalysis.InputSchema;
                    else
                        s = ScopeProgramAnalysis.ScopeProgramAnalysis.OutputSchema;

                    //var columnLiteral = ObtainColumn(col, s);
                    var columnLiteral = UpdateColumnData(methodCallStmt, s); //  ObtainColumn(col, s);

                    var tableColumns = this.State.GetTraceables(arg).OfType<TraceableTable>()
                                        .Select(table_i => new TraceableColumn(table_i, columnLiteral));

                    UpdatePTAForPure(methodCallStmt);
                    this.State.AssignTraceables(methodCallStmt.Result, tableColumns);

                    this.iteratorDependencyAnalysis.InputColumns.AddRange(tableColumns.Where(t => t.TableKind==ProtectedRowKind.Input));
                    this.iteratorDependencyAnalysis.OutputColumns.AddRange(tableColumns.Where(t => t.TableKind == ProtectedRowKind.Output));

                    //scopeData.UpdateSchemaMap(methodCallStmt.Result, arg, this.State);
                }
                // arg.Set(arg1)
                // a4 := a4[arg0 <- a4[arg0] U a2[arg1]] 
                else if (methodInvoked.Name == "Set" && methodInvoked.ContainingType.IsColumnDataType())
//                                        && methodInvoked.ContainingType.ContainingAssembly.Name == "ScopeRuntime")
                {
                    var arg0 = methodCallStmt.Arguments[0];
                    var arg1 = methodCallStmt.Arguments[1];

                    var traceables = this.State.GetTraceables(arg1);
                    UpdatePTAForPure(methodCallStmt);
                    this.State.AddOutputTraceables(arg0, traceables);

                    var controlTraceables = this.State.Dependencies.ControlVariables.SelectMany(controlVar => this.State.GetTraceables(controlVar));
                    this.State.AddOutputControlTraceables(arg0, controlTraceables);
                }
                // arg.Copy(arg1)
                // a4 := a4[arg1 <- a4[arg1] U a2[arg0]] 
                else if (methodInvoked.Name == "CopyTo" && methodInvoked.ContainingType.IsRowType())
                {
                    // TODO: This is a pass-through!
                    var arg0 = methodCallStmt.Arguments[0];
                    var arg1 = methodCallStmt.Arguments[1];

                    var inputSchema = ScopeProgramAnalysis.ScopeProgramAnalysis.InputSchema;

                    var inputTable = this.State.GetTraceables(arg0).OfType<TraceableTable>().First(t => t.TableKind == ProtectedRowKind.Input);
                    var outputTable = this.State.GetTraceables(arg1).OfType<TraceableTable>().Single(t => t.TableKind == ProtectedRowKind.Output);

                    foreach(var column in inputSchema.Columns)
                    {
                        var traceableInputColumn = new TraceableColumn(inputTable, column);
                        var traceableOutputColumn = new TraceableColumn(outputTable, column);

                        var outputColumnVar = new TemporalVariable(arg1.Name + "_$"+ column.Name, 1) { Type = PlatformTypes.Void };
                        this.State.AssignTraceables(outputColumnVar, new Traceable[] { traceableOutputColumn } );

                        this.State.AddOutputTraceables(outputColumnVar, new Traceable[] { traceableInputColumn } );

                        var traceables = this.State.Dependencies.ControlVariables.SelectMany(controlVar => this.State.GetTraceables(controlVar));
                        this.State.AddOutputControlTraceables(outputColumnVar, traceables);

                    }

                    //var tables = this.State.GetTraceables(arg0);
                    //var allColumns = Column.ALL;

                    //// Create a fake column for the output table
                    //var allColumnsVar = new TemporalVariable(arg1.Name + "_$all", 1) {Type = PlatformTypes.Void };

                    //var outputTable = this.State.GetTraceables(arg1).OfType<TraceableTable>().Single(t => t.TableKind == ProtectedRowKind.Output);
                    //this.State.AssignTraceables(allColumnsVar, new Traceable[] { new TraceableColumn(outputTable, allColumns) });
                    //arg1 = allColumnsVar;
                    //this.State.AddOutputTraceables(arg1, tables.OfType<TraceableTable>().Select( t => new TraceableColumn(t, allColumns)));
                    ////
                    //var traceables = this.State.Dependencies.ControlVariables.SelectMany(controlVar => this.State.GetTraceables(controlVar));
                    //this.State.AddOutputControlTraceables(arg1, traceables);
                }
                else if ((methodInvoked.Name.Contains("get_") || methodInvoked.Name=="Get") && methodInvoked.ContainingType.IsColumnDataType())
                {
                    var arg = methodCallStmt.Arguments[0];

                    var traceables = this.State.GetTraceables(arg);
                    UpdatePTAForPure(methodCallStmt);
                    this.State.AssignTraceables(methodCallStmt.Result, traceables);
                }
                else if (methodInvoked.Name == "Load" && methodInvoked.ContainingType.IsRowListType()) 
                {
                    var receiver = methodCallStmt.Arguments[0];
                    var arg1 = methodCallStmt.Arguments[1];
                    this.State.AddTraceables(receiver, arg1);
                }
                else if(methodInvoked.ContainingType.IsScopeRuntime()) // .ContainingNamespace=="ScopeRuntime")
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

            /// <summary>
            /// Obtain the column referred by a variable
            /// </summary>
            /// <param name="col"></param>
                /// <returns></returns>
            private Column ObtainColumn(IVariable col, Schema schema)
            {
                Column result = result = Column.TOP; 
                var columnLiteral = "";
                if (col.Type.Equals(PlatformTypes.String))
                {
                    var columnValue = this.equalities.GetValue(col);
                    if (columnValue is Constant)
                    {
                        columnLiteral = (columnValue as Constant).Value.ToString();
                        result = schema.GetColumn(columnLiteral) ?? new Column(columnLiteral, RangeDomain.BOTTOM, "string");
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
                if (methodInvoked.ContainingType.ContainingAssembly.Name != "ScopeRuntime")
                {
                    return false;
                }
                return methodInvoked.Name == "get_Schema"
                    && (methodInvoked.ContainingType.Name == "RowSet" || methodInvoked.ContainingType.Name == "Row");
            }

            private bool IsSchemaItemMethod(IMethodReference methodInvoked)
            {
                if (methodInvoked.ContainingType.ContainingAssembly.Name != "ScopeRuntime")
                {
                    return false;
                }
                return methodInvoked.Name == "get_Item"
                    && (methodInvoked.ContainingType.Name == "Schema");
            }

            private bool IsIndexOfMethod(IMethodReference methodInvoked)
            {
                if (methodInvoked.ContainingType.ContainingAssembly.Name != "ScopeRuntime")
                {
                    return false;
                }

                return methodInvoked.Name == "IndexOf" && methodInvoked.ContainingType.Name == "Schema";
            }

            private bool IsMethodToInline(IMethodReference methodInvoked, IType clousureType)
            {
                if(methodInvoked.Name==".ctor")
                {
                    return true;
                }
                var patterns = new string[] { "<>m__Finally", "System.IDisposable.Dispose" };
                var specialMethods = new Tuple<string, string>[] { }; //  { Tuple.Create("IDisposable", "Dispose") };
                var result = methodInvoked.ContainingType != null 
                    && ( methodInvoked.ContainingType.Equals(clousureType) && patterns.Any(pattern => methodInvoked.Name.StartsWith(pattern))
                         || specialMethods.Any(sm => sm.Item1 == methodInvoked.ContainingType.Name && sm.Item2 == methodInvoked.Name));
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
                    var schema = ScopeProgramAnalysis.ScopeProgramAnalysis.InputSchema;
                    var tableKind = this.State.GetTraceables(methodCallStmt.Arguments[0]).OfType<TraceableTable>().FirstOrDefault().TableKind;
                    if(tableKind == ProtectedRowKind.Output)
                    {
                        schema = ScopeProgramAnalysis.ScopeProgramAnalysis.OutputSchema;
                    }

                    Column column = UpdateColumnData(methodCallStmt, schema);

                    //var columnn = ObtainColumn(methodCallStmt.Arguments[1], schema);
                    //scopeData.UpdateColumnLiteralMap(methodCallStmt, columnn);
                    //scopeData.UpdateSchemaMap(methodCallStmt.Result, arg, this.State);
                }
                // callResult = arg.IndexOf(colunm)
                // we recover the table from arg and associate the column number with the call result
                else if (IsIndexOfMethod(methodInvoked))
                {
                    var arg = methodCallStmt.Arguments[0];

                    if (scopeData.HasTableForSchemaVar(arg))
                    {
                        var tables = scopeData.GetTableFromSchemaMap(arg);
                        var tableType = this.State.GetTraceables(arg).OfType<TraceableTable>()
                            .Select(t => t.TableKind).FirstOrDefault(); // BUG: what if there are more than one?

                        var schema = ScopeProgramAnalysis.ScopeProgramAnalysis.InputSchema;

                        var tableKind = this.State.GetTraceables(methodCallStmt.Arguments[0]).OfType<TraceableTable>().FirstOrDefault().TableKind;
                        if (tableKind == ProtectedRowKind.Output)
                        {
                            schema = ScopeProgramAnalysis.ScopeProgramAnalysis.OutputSchema;
                        }

                        Column column = UpdateColumnData(methodCallStmt, schema);
                    }
                    else
                    {
                        this.State.SetTOP();
                        AnalysisStats.AddAnalysisReason(new AnalysisReason(this.method, methodCallStmt, "Scope Table mapping not available. Schema passed as parameter?"));
                    }

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

            private void UpdateCall(MethodCallInstruction methodCallStmt)
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
                        UpdatePTAForPure(methodCallStmt);
                        this.State.AssignTraceables(result, traceables);
                    }
                }
            }

            private IEnumerable<Traceable>  GetCallTraceables(MethodCallInstruction methodCallStmt)
            {
                var result = new HashSet<Traceable>();
                //string argString = String.Join(",", methodCallStmt.Arguments.Select(arg => arg.Type.ToString()).ToList());
                string argString = String.Join(",",  methodCallStmt.Arguments.Select(arg => "["+String.Join(",", this.State.GetTraceables(arg))+"]").ToList());
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
                        var parametersCount = resolvedCallee.Body.Parameters.Count;
                        if (!resolvedCallee.IsStatic)
                        {
                            arguments.Add(instance);
                            parametersCount--;
                        }
                        for (int i = 0; i < parametersCount; i++)
                            arguments.Add(methodCall.Arguments[i]);
                        //}
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
                            ProtectedNodes = this.iteratorDependencyAnalysis.protectedNodes
                        };

                        var interProcResult = this.iteratorDependencyAnalysis.interproceduralManager.DoInterProcWithCallee(interProcInfo);

                        this.State = interProcResult.State;
                        currentPTG = interProcResult.State.PTG;
                        if(methodCall.HasResult)
                            traceablesFromDelegate.AddRange(this.State.GetTraceables(methodCall.Result));
                    }
                    catch (Exception e)
                    {
                        System.Console.WriteLine("Could not analyze delegate {0}", resolvedCallee.ToSignatureString());
                        System.Console.WriteLine("Exception {0}\n{1}", e.Message, e.StackTrace);
                        AnalysisStats.AddAnalysisReason(new AnalysisReason(this.method, methodCall,
                                    String.Format(CultureInfo.InvariantCulture, "Callee {2} throw exception {0}\n{1}", e.Message, e.StackTrace.ToString(), resolvedCallee.ToSignatureString())));
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
        private ITypeDefinition iteratorClass;
        private MethodDefinition method;

        private InterproceduralManager interproceduralManager;
        public bool InterProceduralAnalysisEnabled { get; private set; }

        public DataFlowAnalysisResult<DependencyPTGDomain>[] Result { get; set; }

        private DependencyPTGDomain initValue;

        private IEnumerable<ProtectedRowNode> protectedNodes;

        private IteratorPointsToAnalysis pta;
        private RangeAnalysis rangeAnalysis;

        public ISet<TraceableColumn> InputColumns { get; private set; }
        public ISet<TraceableColumn> OutputColumns { get; private set; }


        public IteratorDependencyAnalysis(MethodDefinition method , ControlFlowGraph cfg, IteratorPointsToAnalysis pta,
                                            IEnumerable<ProtectedRowNode> protectedNodes, 
                                            IDictionary<IVariable, IExpression> equalitiesMap,
                                            InterproceduralManager interprocManager, RangeAnalysis rangeAnalysis) : base(cfg)
        {
            this.method = method;
            this.iteratorClass = method.ContainingType;
            // this.specialFields = specialFields;
            this.ptgs = pta.Result;
            this.equalities = equalitiesMap;
            this.scopeData = new ScopeInfo();
            this.protectedNodes = protectedNodes;
            this.interproceduralManager = interprocManager;
            this.initValue = null;
            this.ReturnVariable = new LocalVariable(method.Name + "_$RV") { Type = PlatformTypes.Object };
            this.InterProceduralAnalysisEnabled = AnalysisOptions.DoInterProcAnalysis;
            this.pta = pta;
            this.rangeAnalysis = rangeAnalysis;

            this.InputColumns = new HashSet<TraceableColumn>();
            this.OutputColumns = new HashSet<TraceableColumn>();
        }
        public IteratorDependencyAnalysis(MethodDefinition method, ControlFlowGraph cfg, IteratorPointsToAnalysis pta,
                                    IEnumerable<ProtectedRowNode> protectedNodes, 
                                    IDictionary<IVariable, IExpression> equalitiesMap,
                                    InterproceduralManager interprocManager,
                                    RangeAnalysis rangeAnalysis,
                                    DependencyPTGDomain initValue,
                                    ScopeInfo scopeData) : this(method, cfg, pta, protectedNodes, equalitiesMap, interprocManager, rangeAnalysis) //base(cfg)
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
                    thisVar = this.method.Body.Parameters[0];
                    System.Diagnostics.Debug.Assert(thisVar.Name == "this");
                    // currentPTG.Variables.Single(v => v.Name == "this");
                    foreach (var ptgNode in currentPTG.GetTargets(thisVar))
                    {
                        foreach (var target in currentPTG.GetTargets(ptgNode))
                        {
                            var potentialRowNode = target.Value.First() as ParameterNode;
                            //if (target.Key.Type.ToString() == "RowSet" || target.Key.Type.ToString() == "Row")
                            if (protectedNodes.Contains(potentialRowNode))
                            {
                                var traceable = new TraceableTable(new ProtectedRowNode(potentialRowNode, ProtectedRowNode.GetKind(potentialRowNode.Type)));
                                depValues.AddHeapTraceables(ptgNode, target.Key, new HashSet<Traceable>() { traceable } );
                                
                                // depValues.Dependencies.A3_Fields.Add(new Location(ptgNode, target.Key), traceable));
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
                if (!SongTaoDependencyAnalysis.IsScopeType(v.Type) && !v.IsParameter && !v.Type.IsClassOrStruct())
                {
                    depValues.AssignTraceables(v, new HashSet<Traceable>() { new Other(v.Type.ToString()) });
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

        protected override DependencyPTGDomain Flow(CFGNode node, DependencyPTGDomain input)
        {
            if (input.IsTop)
                return input;

            var oldInput = input.Clone();
            // var currentPTG = pta.Result[node.Id].Output;

            // A visitor for the points-to graph
            var visitorPTA = new PTAVisitor(oldInput.PTG, this.pta);

            // A visitor for the dependency analysis
            // TODO: Since PTG is now part of the state I no longer need to send it as parameters
            var visitor = new MoveNextVisitorForDependencyAnalysis(this, visitorPTA, node, this.equalities, this.scopeData, oldInput.PTG, oldInput);
            visitor.Visit(node);

            return visitor.State;
        }
    }

    internal static class AnalysisOptions
    {
        public static bool DoInterProcAnalysis { get; internal set; }
    }
    #endregion
}
