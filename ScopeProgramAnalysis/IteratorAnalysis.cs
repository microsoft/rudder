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


    public class ColumnName : ColumnDomain
    {
        public string Name { get; private set; }
        public ColumnName(string columnName) 
        {
            this.Name = columnName;
        }
        public override string ToString()
        {
            if (IsTOP || IsAll) return base.ToString();
            else
            {
                return Name.ToString(CultureInfo.InvariantCulture);
            }
        }
        public override bool Equals(object obj)
        {
            var oth = obj as ColumnName;

            return oth!=null && oth.Name == this.Name && base.Equals(oth);
        }
        public override int GetHashCode()
        {
            return this.Name.GetHashCode() + base.GetHashCode();
        }
    }

    public class ColumnPosition : ColumnDomain
    {
        public RangeDomain Range{ get; private set; }

        public int Position {  get { return Range.LowerBound; } }
        public ColumnPosition(int position )
        {
            this.Range = new RangeDomain(position, position);
        }
        public ColumnPosition(RangeDomain range) 
        {
            this.Range = range;
        }
        public override string ToString()
        {
            if (IsTOP || IsAll) return base.ToString();
            else
            {
                return Range.ToString(); //  String.Format(CultureInfo.InvariantCulture,"[{0}..{1}]", Range.Start, Range.End);
            }
        }
        public override bool Equals(object obj)
        {
            var oth = obj as ColumnPosition;

            return oth != null && oth.Range.Equals(this.Range) && base.Equals(oth);
        }
        public override int GetHashCode()
        {
            return this.Range.GetHashCode();
        }
    }



    public class ColumnDomain
    {
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Security", "CA2104:DoNotDeclareReadOnlyMutableReferenceTypes")]
        public static readonly ColumnDomain TOP = new ColumnDomain() { IsTOP = true };
        public static readonly ColumnDomain ALL = new ColumnPosition(RangeAnalysis.TOP); //  ColumnPosition (-3) { ColumnName = "__ALL__", IsTOP = false};
        //public string ColumnName { get; private set; }
        //public int ColumnPosition { get; private set; }
        //public bool IsString { get; private set; }
        public virtual bool IsTOP { get; private set; }
        public virtual bool IsAll { get { return this == ALL; } }



        public ColumnDomain()
        {

        }

        public override string ToString()
        {
            if (IsTOP)
                return "_TOP_";
            if (IsAll)
                return "_All_";
            return "";
        }
        public override bool Equals(object obj)
        {
            var oth = obj as ColumnDomain;

            return this.IsTOP == oth.IsTOP && this.IsAll == oth.IsAll;
        }
        public override int GetHashCode()
        {
            return this.IsTOP?1:0;
        }

        //public ColumnDomain(string columnName)
        //{
        //    this.ColumnName = columnName;
        //    this.IsString = true;
        //    this.ColumnPosition = -1;
        //    IsTOP = columnName == "_TOP_";
        //    if (IsTOP)
        //    {
        //        this.ColumnPosition = -2;
        //    }
        //}
        //public ColumnDomain(int columnPosition)
        //{
        //    this.ColumnName = "_TOP_";
        //    this.IsString = false;
        //    this.ColumnPosition = columnPosition;
        //    IsTOP = columnPosition == -2;
        //}
        //public override string ToString()
        //{
        //    if (IsTOP)
        //        return "_TOP_";
        //    if (IsAll)
        //        return "_All_";
        //    if (IsString)
        //    {
        //        return ColumnName;
        //    }
        //    else
        //    {
        //        return ColumnPosition.ToString(CultureInfo.InvariantCulture);
        //    }
        //}
        //public override bool Equals(object obj)
        //{
        //    var oth = obj as ColumnDomain;

        //    return oth.IsString==this.IsString && oth.IsTOP == oth.IsTOP 
        //            && oth.ColumnName==this.ColumnName 
        //            && oth.ColumnPosition==this.ColumnPosition;
        //}
        //public override int GetHashCode()
        //{
        //    if (IsString)
        //    {
        //        return this.ColumnName.GetHashCode();
        //    }
        //    return this.ColumnPosition.GetHashCode();
        //}
    }


    //public class TraceableColumnNumber: Traceable
    //{
    //    public int Column { get; private set; }
    //    public TraceableColumnNumber(string name, int column): base(name)
    //    {
    //        this.Column = column;
    //    }
    //    public override string ToString()
    //    {
    //        return String.Format(CultureInfo.InvariantCulture, "Col({0},{1})", TableName, Column);
    //    }
    //    public override bool Equals(object obj)
    //    {
    //        var oth = obj as TraceableColumnName;
    //        return oth != null && oth.Column.Equals(this.Column) && base.Equals(oth);
    //    }
    //    public override int GetHashCode()
    //    {
    //        return base.GetHashCode() + Column.GetHashCode();
    //    }
    //}
    //public class TraceableColumnName: Traceable
    //{
    //    public string Column { get; private set; }
    //    public TraceableColumnName(string name, string column): base(name)
    //    {
    //        this.Column = column;
    //    }
    //    public override string ToString()
    //    {
    //        return String.Format(CultureInfo.InvariantCulture, "Col({0},{1})", TableName, Column);
    //    }
    //    public override bool Equals(object obj)
    //    {
    //        var oth = obj as TraceableColumnName;
    //        return oth != null && oth.Column.Equals(this.Column) && base.Equals(oth);
    //    }
    //    public override int GetHashCode()
    //    {
    //        return base.GetHashCode()+Column.GetHashCode();
    //    }
    //}

    public class TraceableColumn : Traceable
    {
        public TraceableTable Table { get; private set; }
        public ColumnDomain Column { get; private set; }
        public TraceableColumn(TraceableTable table,  ColumnDomain column) : base(table)
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
            this.ptgNode = PointsToGraph.GlobalNode;
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
            internal void UpdateColumnMap(MethodCallInstruction methodCallStmt, ColumnDomain columnLiteral)
            {
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
            private DependencyPTGDomain oldInput;
            private ScopeInfo scopeData;
            internal DependencyPTGDomain State { get; private set; }
            private PointsToGraph currentPTG;
            private CFGNode cfgNode;
            private MethodDefinition method;
            private PTAVisitor visitorPTA;
            private VariableRangeDomain variableRanges;

            public MoveNextVisitorForDependencyAnalysis(IteratorDependencyAnalysis iteratorDependencyAnalysis, PTAVisitor visitorPTA,
                                   CFGNode cfgNode,  IDictionary<IVariable, IExpression> equalities, 
                                   ScopeInfo scopeData, PointsToGraph ptg, DependencyPTGDomain oldInput)
            {
                this.iteratorDependencyAnalysis = iteratorDependencyAnalysis;
                this.equalities = equalities;
                this.scopeData = scopeData;
                this.oldInput = oldInput;
                this.State = oldInput;
                this.currentPTG = ptg;
                this.cfgNode = cfgNode;
                this.method = iteratorDependencyAnalysis.method;
                this.visitorPTA = visitorPTA;
                this.variableRanges = this.iteratorDependencyAnalysis.rangeAnalysis.Result[cfgNode.Id].Output;
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
                    // && !fieldAccess.FieldName.StartsWith.Contains("<>1__state"))
                {
                    return true;
                }
                var isClousureField = this.iteratorDependencyAnalysis.iteratorClass.Equals(field.ContainingType);

                // TODO: need to read an attribute of something

                bool isCompilerGenerated = false;
                var typeAsClass = (instance.Type as IBasicType);
                if (typeAsClass != null && typeAsClass.ResolvedType != null)
                {
                    var typeAsClassResolved = (typeAsClass.ResolvedType as ClassDefinition);
                    isCompilerGenerated = typeAsClassResolved.ContainingType!=null &&
                                          this.iteratorDependencyAnalysis.iteratorClass.ContainingType != null
                                        && typeAsClassResolved.ContainingType.Equals(this.iteratorDependencyAnalysis.iteratorClass.ContainingType);
                }
                var isReducerField = this.iteratorDependencyAnalysis.iteratorClass.ContainingType != null
                    && this.iteratorDependencyAnalysis.iteratorClass.ContainingType.Equals(field.ContainingType);


                if(isClousureField || isReducerField || isCompilerGenerated)
                {
                    return true;
                }

                return false;
            }

            private ISet<ISymbolicValue> GetSymbolicValues(IVariable v)
            {
                if(v.Type.TypeKind == TypeKind.ValueType)
                {
                    return new HashSet<ISymbolicValue>() { new EscalarVariable(v) } ;
                }
                var res = new HashSet<ISymbolicValue>();
                if(currentPTG.Contains(v))
                {
                    res.UnionWith(currentPTG.GetTargets(v).Select( ptg => new AbstractObject(ptg) ));
                }
                return res;
            }
            private ISet<PTGNode> GetPtgNodes(IVariable v)
            {
                var res = new HashSet<PTGNode>();
                if (currentPTG.Contains(v))
                {
                    res.UnionWith(currentPTG.GetTargets(v));
                }
                return res;
            }

            private ISet<IVariable> GetAliases(IVariable v)
            {
                return currentPTG.GetAliases(v);
            }
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

                    foreach (var ptgNode in currentPTG.GetTargets(baseArray))
                    {
                        // TODO: I need to provide a BasicType. I need the base of the array 
                        // Currenly I use the method containing type
                        var fakeField = new FieldReference("[]", arrayAccess.Type, method.ContainingType);
                        //fakeField.ContainingType = PlatformTypes.Object;
                        var loc = new Location(ptgNode, fakeField);
                        if (this.State.Dependencies.A3_Clousures.ContainsKey(loc))
                        {
                            traceables.UnionWith(this.State.Dependencies.A3_Clousures[loc]);
                        }
                    }
                    this.State.AssignTraceables(loadStmt.Result, traceables);
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
                { }
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
                if (SongTaoDependencyAnalysis.IsScopeType(fieldAccess.Instance.Type))
                {
                    traceables.AddRange(this.State.GetTraceables(fieldAccess.Instance));
                }

                    //if (IsProctectedAccess(fieldAccess.Instance, fieldAccess.Field) || fieldAccess.Field.Type.IsValueType())
                if (ISClousureField(fieldAccess.Instance, fieldAccess.Field))
                {
                    // this is a[loc(o.f)]
                    foreach (var ptgNode in currentPTG.GetTargets(fieldAccess.Instance))
                    {
                        var loc = new Location(ptgNode, fieldAccess.Field);
                        //if(fieldAccess.Field.Type.IsValueType() || fieldAccess.Type==PlatformTypes.String)
                        //{ }
                        if (this.State.Dependencies.A3_Clousures.ContainsKey(loc))
                        {
                            //if (!IsProctectedAccess(fieldAccess.Instance, fieldAccess.Field))
                            //{ }

                            traceables.UnionWith(this.State.Dependencies.A3_Clousures[loc]);
                        }
                    }
                }
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
                if (ISClousureField(PointsToGraph.GlobalNode.Variables.Single(), fieldAccess.Field))
                //    if (isClousureField || isReducerField)
                {
                    var traceables = new HashSet<Traceable>();
                    // a2:= [v <- a3[loc(o.f)] if loc(o.f) is CF
                    // if (ISClousureField(PointsToGraph.GlobalNode.Variables.Single(), fieldAccess.Field))
                    {
                        // this is a[loc(C.f)]
                        var loc = new Location(PointsToGraph.GlobalNode, fieldAccess.Field);
                        if (this.State.Dependencies.A3_Clousures.ContainsKey(loc))
                        {
                            traceables.UnionWith(this.State.Dependencies.A3_Clousures[loc]);
                        }

                    }
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
                    if (ISClousureField(fieldAccess.Instance, fieldAccess.Field))
                    {
                        var arg = instruction.Operand;
                        var inputTable = equalities.GetValue(arg);

                        // a3 := a3[loc(o.f) <- a2[v]] 
                        // union = a2[v]
                        var traceables = this.State.GetTraceables(instruction.Operand);
                        foreach (var ptgNode in currentPTG.GetTargets(o))
                        {
                            this.State.Dependencies.A3_Clousures[new Location(ptgNode, field)] = traceables;
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
                    foreach (var ptgNode in currentPTG.GetTargets(baseArray))
                    {
                        // TODO: I need to provide a BasicType. I need the base of the array 
                        // Currenly I use the method containing type
                        var fakeField = new FieldReference("[]", arrayAccess.Type, method.ContainingType);
                        //fakeField.ContainingType = PlatformTypes.Object;
                        var loc = new Location(ptgNode, fakeField);
                        this.State.Dependencies.A3_Clousures[new Location(ptgNode, fakeField)] = traceables;
                    }
                }
                else if (instructionResult is StaticFieldAccess)
                {
                    var field = (instructionResult as StaticFieldAccess).Field;
                    var traceables = this.State.GetTraceables(instruction.Operand);
                    this.State.Dependencies.A3_Clousures[new Location(PointsToGraph.GlobalNode, field)] = traceables;

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
                instruction.Accept(visitorPTA);
                //if(!instruction.AllocationType.IsDelegateType())
                //{
                //    Default(instruction);
                //}
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
                                UpdateUsingDefUsed(methodCallStmt);
                                UpdatePTAForPure(methodCallStmt);
                            }
                            else
                            {
                                // I first check in the calle may a input/output row
                                var argRootNodes = methodCallStmt.Arguments.SelectMany(arg => currentPTG.GetTargets(arg, false))
                                                    .Where(n => n!=PointsToGraph.NullNode);

                                // If it is a method within the same class it will be able to acesss all the fiealds 
                                var isInternalClassInvocation = methodInvoked.ContainingType.Equals(this.iteratorDependencyAnalysis.iteratorClass);

                                Predicate<Tuple<PTGNode, IFieldReference>> fieldFilter = (nf => isInternalClassInvocation ||
                                                   nf.Item2.ContainingType != this.iteratorDependencyAnalysis.iteratorClass);
                                argRootNodes = currentPTG.ReachableNodes(argRootNodes, fieldFilter);
                                var escaping = argRootNodes.Intersect(this.iteratorDependencyAnalysis.protectedNodes).Any();
                                if (escaping)
                                {
                                    if (this.iteratorDependencyAnalysis.InterProceduralAnalysisEnabled 
                                        || IsMethodToInline(methodInvoked, this.iteratorDependencyAnalysis.iteratorClass))
                                    {
                                        // This updates the Dep Domain and the PTG
                                        var computedCalles = this.iteratorDependencyAnalysis.interproceduralManager.ComputePotentialCallees(instruction, currentPTG);
                                        AnalyzeResolvedCallees(instruction, methodCallStmt, computedCalles.Item1);

                                        // If there are unresolved calles
                                        if (computedCalles.Item2.Any())
                                        {
                                            HandleNoAnalyzableMethod(methodCallStmt);
                                        }
                                    }
                                    else
                                    {
                                        HandleNoAnalyzableMethod(methodCallStmt);
                                    }
                                }
                                else
                                {
                                    UpdateUsingDefUsed(methodCallStmt);

                                    // I should at least update the Poinst-to graph
                                    // or make the parameters escape
                                    foreach (var escapingNode in argRootNodes.Where(n => n.Kind!=PTGNodeKind.Null))
                                    {
                                        var escapingField = new FieldReference("escape", PlatformTypes.Object, this.method.ContainingType);
                                        currentPTG.PointsTo(PointsToGraph.GlobalNode, escapingField, escapingNode);
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
                if (instruction.Result != null && !instruction.Result.Type.IsValueType())
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
                            currentPTG.PointsTo(returnNode, returnField , ptgNode);
                        }
                    }
                }
            }

            private void AnalyzeResolvedCallees(MethodCallInstruction instruction, MethodCallInstruction methodCallStmt, IEnumerable<MethodDefinition> calles)
            {
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
            }

            private void HandleNoAnalyzableMethod(MethodCallInstruction methodCallStmt)
            {
                UpdatePTAForPure(methodCallStmt);
                UpdateUsingDefUsed(methodCallStmt);
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
                if(containingType.Name=="String")
                {
                    return true;
                }
                if (containingType.Name == "Tuple")
                {
                    return true;
                }
                //if (containingType is BasicType && metodCallStmt.Method.Name==".ctor")
                //{
                //    return true;
                //}
                if (containingType.TypeKind == TypeKind.ValueType)
                {
                    return true;
                }
                return result;
            }

            public override void Visit(PhiInstruction instruction)
            {
                instruction.Accept(visitorPTA);

                UpdateUsingDefUsed(instruction);
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
                var pureCollectionMethods = new HashSet<String>() { "Contains", "ContainsKey", "get_Item", "Count", "get_Count", "First" };
                var pureEnumerationMethods = new HashSet<String>() { "Select", "Where", "Any", "Count", "GroupBy", "Max", "Min"};
                 

                var result = true;
                if (methodInvoked.Name == "Any") //  && methodInvoked.ContainingType.FullName == "Enumerable")
                {
                    //var arg = methodCallStmt.Arguments[0];
                    //var tablesCounters = this.State.GetTraceables(arg).OfType<TraceableTable>()
                    //                    .Select(table_i => new TraceableCounter(table_i));
                    //var any = tablesCounters.Any();
                    UpdateUsingDefUsed(methodCallStmt);
                }
                // Check for a predefined set of pure methods
                if(pureCollectionMethods.Contains(methodInvoked.Name) && methodInvoked.IsCollectionMethod())
                {
                    UpdateUsingDefUsed(methodCallStmt);
                }
                else if(methodInvoked.IsPure() || pureEnumerationMethods.Contains(methodInvoked.Name) && methodInvoked.IsEnumerableMethod())
                {
                    UpdateUsingDefUsed(methodCallStmt);
                }
                else if(pureCollectionMethods.Contains(methodInvoked.Name) && methodInvoked.IsContainerMethod())
                {
                    UpdateUsingDefUsed(methodCallStmt);
                }
                else if (pureCollectionMethods.Contains(methodInvoked.Name) && methodInvoked.ContainingType.Name.Contains("Set"))
                {
                    UpdateUsingDefUsed(methodCallStmt);
                }
                else if (pureCollectionMethods.Contains(methodInvoked.Name) && methodInvoked.ContainingType.Name.Contains("SortedDictionary"))
                {
                    UpdateUsingDefUsed(methodCallStmt);
                }

                else if (methodInvoked.Name == "Add" && methodInvoked.ContainingType.GenericName.Contains("Set"))
                {
                    var arg0 = methodCallStmt.Arguments[0];
                    var arg1 = methodCallStmt.Arguments[1];
                    this.State.AddTraceables(arg0, arg1);
                }
                else if (methodInvoked.Name == "get_Current" 
                    && (methodInvoked.ContainingType.Name == "IEnumerator"))
                {
                    var arg = methodCallStmt.Arguments[0];
                    this.State.CopyTraceables(methodCallStmt.Result, arg);
                }
                else if (methodInvoked.Name == "MoveNext"
                    && (methodInvoked.ContainingType.Name == "IEnumerator"))
                {
                    var arg = methodCallStmt.Arguments[0];
                    this.State.CopyTraceables(methodCallStmt.Result, arg);
                }
                else if (methodInvoked.Name == "GetEnumerator"
                    && (methodInvoked.ContainingType.Name == "IEnumerable"))
                {
                    var arg = methodCallStmt.Arguments[0];
                    this.State.CopyTraceables(methodCallStmt.Result, arg);
                }
                else
                {
                    result = false;
                }
                if (result && methodCallStmt.HasResult)
                {
                    UpdatePTAForPure(methodCallStmt);
                    //var node = new PTGNode(new PTGID(new MethodContex(this.method), (int)methodCallStmt.Offset), methodCallStmt.Result.Type);
                    //this.State.PTG.PointsTo(methodCallStmt.Result, node);
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
                    this.State.CopyTraceables(methodCallStmt.Result, arg);

                    // TODO: I don't know I need this
                    scopeData.UpdateSchemaMap(methodCallStmt.Result, arg, this.State);
                }
                // This is when you get enumerator (same as get rows)
                // a2 = a2[v <- a[arg_0]] 
                else if (methodInvoked.Name == "GetEnumerator" 
                    && (methodInvoked.ContainingType.GenericName== "IEnumerable<Row>"
                       || methodInvoked.ContainingType.GenericName == "IEnumerable<ScopeMapUsage>"))
                {
                    var arg = methodCallStmt.Arguments[0];

                    // a2[ v = a2[arg[0]]] 
                    this.State.CopyTraceables(methodCallStmt.Result, arg);
                }
                // v = arg.Current
                // a2 := a2[v <- Table(i)] if Table(i) in a2[arg]
                else if (methodInvoked.Name == "get_Current" 
                    && ( methodInvoked.ContainingType.GenericName == "IEnumerator<Row>")
                         || methodInvoked.ContainingType.GenericName == "IEnumerator<ScopeMapUsage>")
                {
                    var arg = methodCallStmt.Arguments[0];
                    this.State.CopyTraceables(methodCallStmt.Result, arg);
                }
                // v = arg.Current
                // a2 := a2[v <- Table(i)] if Table(i) in a2[arg]
                else if (methodInvoked.Name == "MoveNext" && methodInvoked.ContainingType.Name == "IEnumerator")
                {
                    var arg = methodCallStmt.Arguments[0];
                    var tablesCounters = this.State.GetTraceables(arg).OfType<TraceableTable>()
                                        .Select(table_i => new TraceableCounter(table_i));
                    this.State.AssignTraceables(methodCallStmt.Result, tablesCounters);
                }
                // v = arg.getItem(col)
                // a2 := a2[v <- Col(i, col)] if Table(i) in a2[arg]
                else if (methodInvoked.Name == "get_Item" && methodInvoked.ContainingType.IsRowType())
//                                        && methodInvoked.ContainingType.ContainingAssembly.Name == "ScopeRuntime")

                {
                    var arg = methodCallStmt.Arguments[0];
                    var col = methodCallStmt.Arguments[1];
                    var columnLiteral = ObtainColumn(col);

                    var tableColumns = this.State.GetTraceables(arg).OfType<TraceableTable>()
                                        .Select(table_i => new TraceableColumn(table_i, columnLiteral));
                    this.State.AssignTraceables(methodCallStmt.Result, tableColumns);

                    //scopeData.UpdateSchemaMap(methodCallStmt.Result, arg, this.State);
                }
                // arg.Set(arg1)
                // a4 := a4[arg0 <- a4[arg0] U a2[arg1]] 
                else if (methodInvoked.Name == "Set" && methodInvoked.ContainingType.IsColumnDataType())
//                                        && methodInvoked.ContainingType.ContainingAssembly.Name == "ScopeRuntime")
                {
                    var arg0 = methodCallStmt.Arguments[0];
                    var arg1 = methodCallStmt.Arguments[1];

                    this.State.AddOutputTraceables(arg0, arg1);

                    var traceables = this.State.Dependencies.ControlVariables.SelectMany(controlVar => this.State.GetTraceables(controlVar));
                    this.State.AddOutputControlTraceables(arg0, traceables);
                }
                // arg.Copy(arg1)
                // a4 := a4[arg1 <- a4[arg1] U a2[arg0]] 
                else if (methodInvoked.Name == "CopyTo" && methodInvoked.ContainingType.IsRowType())
//                                        && methodInvoked.ContainingType.ContainingAssembly.Name == "ScopeRuntime")
                {
                    // TODO: This is a pass-through!
                    var arg0 = methodCallStmt.Arguments[0];
                    var arg1 = methodCallStmt.Arguments[1];

                    var tables = this.State.GetTraceables(arg0);
                    var allColumns = ColumnDomain.ALL;

                    // Create a fake column for the output table
                    var allColumnsVar = new TemporalVariable(arg1.Name + "_$all", 1);
                    var outputTable = this.State.GetTraceables(arg1).OfType<TraceableTable>().Single();
                    this.State.AddTraceables(allColumnsVar, new Traceable[] { new TraceableColumn(outputTable, allColumns) } );

                    arg1 = allColumnsVar;
                    this.State.AddOutputTraceables(arg1, tables.OfType<TraceableTable>().Select( t => new TraceableColumn(t, allColumns)));
                    //
                    var traceables = this.State.Dependencies.ControlVariables.SelectMany(controlVar => this.State.GetTraceables(controlVar));
                    this.State.AddOutputControlTraceables(arg1, traceables);
                }
                else if ((methodInvoked.Name == "get_String" || methodInvoked.Name == "Get") && methodInvoked.ContainingType.IsColumnDataType())
//                                        && methodInvoked.ContainingType.ContainingAssembly.Name == "ScopeRuntime")
                {
                    var arg = methodCallStmt.Arguments[0];
                    this.State.CopyTraceables(methodCallStmt.Result, arg);
                }
                else if (methodInvoked.Name == "Load" && methodInvoked.ContainingType.IsRowListType()) 
//                                        && methodInvoked.ContainingType.ContainingAssembly.Name == "ScopeRuntime")

                {
                    var receiver = methodCallStmt.Arguments[0];
                    var arg1 = methodCallStmt.Arguments[1];
                    this.State.AddTraceables(receiver, arg1);
                }
                else if(methodInvoked.ContainingType.ContainingNamespace=="ScopeRuntime")
                {
                    this.UpdateUsingDefUsed(methodCallStmt);
                }
                else
                {
                    result = false;
                }

                if(result && methodCallStmt.HasResult)
                {
                    UpdatePTAForPure(methodCallStmt);
                    //var node = new PTGNode(new PTGID(new MethodContex(this.method), (int)methodCallStmt.Offset), methodCallStmt.Result.Type);
                    //this.State.PTG.PointsTo(methodCallStmt.Result, node);
                }

                return result;

            }

            /// <summary>
            /// Obtain the column referred by a variable
            /// </summary>
            /// <param name="col"></param>
                /// <returns></returns>
            private ColumnDomain ObtainColumn(IVariable col)
            {
                ColumnDomain result = result = ColumnDomain.TOP; 
                var columnLiteral = "";
                if (col.Type.Equals(PlatformTypes.String))
                {
                    var columnValue = this.equalities.GetValue(col);
                    if (columnValue is Constant)
                    {
                        columnLiteral = columnValue.ToString();
                        result = new ColumnName(columnLiteral);
                    }
                }
                else
                {
                    if (scopeData.columnVariable2Literal.ContainsKey(col))
                    {
                        columnLiteral = scopeData.columnVariable2Literal[col];
                        result = new ColumnName(columnLiteral);
                    }
                    else
                    {
                        var colValue = this.equalities.GetValue(col);
                        var rangeForColumn = variableRanges.GetValue(col);
                        if(!rangeForColumn.IsBottom)
                        {
                            result = new ColumnPosition(rangeForColumn);
                        }
                        else
                        if(colValue is Constant)
                        {
                            var value = colValue as Constant;
                            result = new ColumnPosition((int)value.Value);
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
                // callResult = arg.IndexOf(colunm)
                // we recover the table from arg and associate the column number with the call result
                else if (IsIndexOfMethod(methodInvoked))
                {
                    var arg = methodCallStmt.Arguments[0];
                    var tables= scopeData.GetTableFromSchemaMap(arg);
                    ColumnDomain column = UpdateColumnData(methodCallStmt);
                    //this.State.AssignTraceables(methodCallStmt.Result, tables.OfType<TraceableTable>().Select(t => new TraceableColumn(t, column)));
                }
                else
                {
                    result = false;
                }
                return result;
            }

            private ColumnDomain UpdateColumnData(MethodCallInstruction methodCallStmt)
            {
                var columnn = ObtainColumn(methodCallStmt.Arguments[1]);
                scopeData.UpdateColumnMap(methodCallStmt, columnn);
                if(columnn.IsTOP)
                {
                    AnalysisStats.AddAnalysisReason(new AnalysisReason(this.method, methodCallStmt,
                                                    String.Format(CultureInfo.InvariantCulture, "Could not compute a value for the column {0} {1}", methodCallStmt.Arguments[0], methodCallStmt.Arguments[1])));

                }
                return columnn;
            }

            /// <summary>
            /// Propagates dependencies from uses to defs
            /// </summary>
            /// <param name="instruction"></param>
            private void UpdateUsingDefUsed(Instruction instruction)
            {
                foreach (var result in instruction.ModifiedVariables)
                {
                    var traceables = new HashSet<Traceable>();
                    foreach (var arg in instruction.UsedVariables)
                    {
                        // If a paramete is a delegate we try to evaluate it with the parameters available
                        if(instruction is MethodCallInstruction && arg.Type.IsDelegateType())
                        {
                            var methodCall = instruction as MethodCallInstruction;
                            traceables.UnionWith(EvaluateDelegate(arg, methodCall));
                        }
                        else
                        {
                            var tables = this.State.GetTraceables(arg);
                            traceables.UnionWith(tables);
                        }
                    }
                    this.State.AssignTraceables(result, traceables);
                }
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
                        arguments.Add(instance);
                        arguments.Add(methodCall.Arguments[0]);
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
        DataFlowAnalysisResult<PointsToGraph>[] ptgs;
        private ScopeInfo scopeData;
        // private IDictionary<string, IVariable> specialFields;
        private ITypeDefinition iteratorClass;
        private MethodDefinition method;

        private InterproceduralManager interproceduralManager;
        public bool InterProceduralAnalysisEnabled { get; private set; }

        public DataFlowAnalysisResult<DependencyPTGDomain>[] Result { get; private set; }

        private DependencyPTGDomain initValue;

        private IEnumerable<ProtectedRowNode> protectedNodes;

        private IteratorPointsToAnalysis pta;
        private RangeAnalysis rangeAnalysis;

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
            this.ReturnVariable = new LocalVariable(method.Name+"_$RV");
            this.ReturnVariable.Type = PlatformTypes.Object;
            this.InterProceduralAnalysisEnabled = AnalysisOptions.DoInterProcAnalysis;
            this.pta = pta;
            this.rangeAnalysis = rangeAnalysis;
        }
        public IteratorDependencyAnalysis(MethodDefinition method, ControlFlowGraph cfg, IteratorPointsToAnalysis pta,
                                    IEnumerable<ProtectedRowNode> protectedNodes, 
                                    IDictionary<IVariable, IExpression> equalitiesMap,
                                    InterproceduralManager interprocManager,
                                    RangeAnalysis rangeAnalysis,
                                    DependencyPTGDomain initValue,
                                    ScopeInfo scopeData) : this(method, cfg, pta, protectedNodes, 
                                                                          equalitiesMap, interprocManager, rangeAnalysis) //base(cfg)
        {            
            this.initValue = initValue;
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
                if(this.initValue != null)
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
                        foreach (var target in ptgNode.Targets)
                        {
                            var potentialRowNode = target.Value.First() as ParameterNode;
                            //if (target.Key.Type.ToString() == "RowSet" || target.Key.Type.ToString() == "Row")
                            if (protectedNodes.Contains(potentialRowNode))
                            {
                                depValues.Dependencies.A3_Clousures.Add(new Location(ptgNode, target.Key),  
                                                                        new TraceableTable(new ProtectedRowNode(potentialRowNode, ProtectedRowNode.GetKind(potentialRowNode.Type))));
                            }
                        }
                    }
                }
                foreach(var v in cfg.GetVariables())
                {
                    if(!SongTaoDependencyAnalysis.IsScopeType(v.Type))
                    {
                        depValues.AssignTraceables(v, new HashSet<Traceable>() { new Other(v.Type.ToString()) });
                    }
                }
            }
            return depValues;
        }

        protected override bool Compare(DependencyPTGDomain newState, DependencyPTGDomain oldSTate)
        {
            return newState.LessEqual(oldSTate);
        }

        protected override DependencyPTGDomain Join(DependencyPTGDomain left, DependencyPTGDomain right)
        {
            return left.Join(right);
        }

        protected override DependencyPTGDomain Flow(CFGNode node, DependencyPTGDomain input)
        {
            if (input.IsTop)
                return input;

            var oldInput = input;
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
