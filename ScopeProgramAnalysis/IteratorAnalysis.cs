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
            return oth!= null && oth.TableName.Equals(this.TableName);
        }
        public override int GetHashCode()
        {
            return TableName.GetHashCode();
        }
        public override string ToString()
        {
            return TableName;
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

    public class ColumnDomain
    {
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Security", "CA2104:DoNotDeclareReadOnlyMutableReferenceTypes")]
        public static readonly ColumnDomain TOP = new ColumnDomain(-2) { ColumnName = "__TOP__", IsTOP = true };
        public static readonly ColumnDomain ALL = new ColumnDomain(-3) { ColumnName = "__ALL__", IsTOP = false};
        public string ColumnName { get; private set; }
        public int ColumnPosition { get; private set; }
        public bool IsString { get; private set; }
        public bool IsTOP { get; private set; }

        public ColumnDomain(string columnName)
        {
            this.ColumnName = columnName;
            this.IsString = true;
            this.ColumnPosition = -1;
            IsTOP = columnName == "_TOP_";
            if (IsTOP)
            {
                this.ColumnPosition = -2;
            }
        }
        public ColumnDomain(int columnPosition)
        {
            this.ColumnName = "_TOP_";
            this.IsString = false;
            this.ColumnPosition = columnPosition;
            IsTOP = columnPosition == -2;
        }
        public override string ToString()
        {
            if (IsTOP)
                return "_TOP_";
            if (IsString)
            {
                return ColumnName;
            }
            else
            {
                return ColumnPosition.ToString(CultureInfo.InvariantCulture);
            }
        }
        public override bool Equals(object obj)
        {
            var oth = obj as ColumnDomain;

            return oth.IsString==this.IsString && oth.IsTOP == oth.IsTOP 
                    && oth.ColumnName==this.ColumnName 
                    && oth.ColumnPosition==this.ColumnPosition;
        }
        public override int GetHashCode()
        {
            if (IsString)
            {
                return this.ColumnName.GetHashCode();
            }
            return this.ColumnPosition.GetHashCode();
        }
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
        internal class ScopeInfo
        {
            //internal IDictionary<IVariable, IExpression> schemaMap = new Dictionary<IVariable, IExpression>();
            internal MapSet<IVariable, string> schemaTableMap = new MapSet<IVariable, string>();
            internal IDictionary<IVariable, string> columnMap = new Dictionary<IVariable, string>();
            internal IDictionary<IFieldReference, string> columnFieldMap = new Dictionary<IFieldReference, string>();
            internal IDictionary<IFieldReference, IVariable> schemaFieldMap = new Dictionary<IFieldReference, IVariable>();
            // Maybe a map for IEpression to IVariable?
            internal IVariable row = null;
            //internal IVariable rowEnum = null;

            internal ScopeInfo()
            {
                //schemaMap = new Dictionary<IVariable, IExpression>();
                columnMap = new Dictionary<IVariable, string>();
                //row = null;
                //rowEnum = null;
            }
            internal void UpdateSchemaMap(IVariable callResult, IVariable arg, PointsToGraph currentPTG)
            {
                var nodes = currentPTG.GetTargets(arg, false);
                if (nodes.Any())
                {
                    var tables = nodes.Where(n => n.Type is IBasicType && (n.Type as IBasicType).GetFullName() != "").SelectMany(n => n.Sources.Select(kv => kv.Key.Name));
                    this.schemaTableMap[callResult] = new HashSet<string>(tables);
                }
            }
            internal IEnumerable<string> GetTableFromSchemaMap(IVariable arg)
            {
                return this.schemaTableMap[arg];
            }
            internal void UpdateColumnMap(MethodCallInstruction methodCallStmt, ColumnDomain columnLiteral)
            {
                columnMap[methodCallStmt.Result] = columnLiteral.ColumnName;
            }

            internal void ProcessLoadField(LoadInstruction loadStmt, InstanceFieldAccess fieldAccess)
            {
                if (this.columnFieldMap.ContainsKey(fieldAccess.Field))
                {
                    this.columnMap[loadStmt.Result] = this.columnFieldMap[fieldAccess.Field];
                }

                if (fieldAccess.Instance.Name == "this" && this.schemaFieldMap.ContainsKey(fieldAccess.Field))
                {
                    var recoveredVar = this.schemaFieldMap[fieldAccess.Field];
                    this.schemaTableMap[loadStmt.Result] = this.schemaTableMap[recoveredVar];
                }
            }
            internal void ProcessStoreField(StoreInstruction instruction, InstanceFieldAccess fieldAccess)
            {
                // This is to connect the column field with the literal
                // Do I need this?
                if (this.columnMap.ContainsKey(instruction.Operand))
                {
                    var columnLiteral = this.columnMap[instruction.Operand];
                    this.columnFieldMap[fieldAccess.Field] = columnLiteral;
                }

                if (this.schemaTableMap.ContainsKey(instruction.Operand))
                {
                    this.schemaFieldMap[fieldAccess.Field] = instruction.Operand;
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

                if ( ((IBasicType)instanceType).Name == this.iteratorDependencyAnalysis.iteratorClass.Name) 
                    // && !fieldAccess.FieldName.StartsWith.Contains("<>1__state"))
                {
                    return true;
                }
                var isClousureField = this.iteratorDependencyAnalysis.iteratorClass.Name == field.ContainingType.Name;
                var isReducerField = this.iteratorDependencyAnalysis.iteratorClass.ContainingType != null
                                        && this.iteratorDependencyAnalysis.iteratorClass.ContainingType.Name == field.ContainingType.Name;
                if(isClousureField || isReducerField)
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
                var res = new HashSet<IVariable>() { v } ;
                foreach (var ptgNode in currentPTG.GetTargets(v, false)) // GetPtgNodes(v))
                {
                    res.UnionWith(ptgNode.Variables);
                }
                return res;
            }
            public override void Visit(LoadInstruction instruction)
            {
                visitorPTA.Visit(instruction);

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
                        if (SongTaoDependencyAnalysis.IsScopeType(referencedValue.Type))
                        {
                            var isHandled = HandleLoadWithOperand(loadStmt, referencedValue);
                            if (!isHandled)
                            {
                                this.State.SetTOP();
                                AnalysisStats.AddAnalysisReason(new AnalysisReason(this.method.Name, loadStmt, "Load Reference not Supported"));
                            }
                        }
                    }
                    else if (operand is Dereference)
                    {
                        var reference = (operand as Dereference).Reference;
                        if (SongTaoDependencyAnalysis.IsScopeType(reference.Type))
                        {
                            var isHandled = HandleLoadWithOperand(loadStmt, reference);
                            if (!isHandled)
                            {
                                this.State.SetTOP();
                                AnalysisStats.AddAnalysisReason(new AnalysisReason(this.method.Name, loadStmt, "Load Dereference not Supported"));
                            }
                        }
                    }
                    else if (operand is IndirectMethodCallExpression)
                    {
                        AnalysisStats.AddAnalysisReason(new AnalysisReason(this.method.Name, loadStmt, "Indirect method invocation not Supported"));
                        this.State.SetTOP();
                    }
                    else if (operand is StaticMethodReference || loadStmt.Operand is VirtualMethodReference)
                    {
                        // Now handled by the PT Analysis
                    }
                    else
                    {
                        AnalysisStats.AddAnalysisReason(new AnalysisReason(this.method.Name, loadStmt, "Unsupported load"));
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
                    var o = fieldAccess.Instance;
                    var field = fieldAccess.Field;

                    ProcessLoad(loadStmt, fieldAccess);

                    // TODO: Filter for columns only
                    scopeData.ProcessLoadField(loadStmt, fieldAccess);

                }
                else if (operand is ArrayElementAccess)
                {
                    var arrayAccess = operand as ArrayElementAccess;
                    var baseArray = arrayAccess.Array;

                    // TODO: Add dependencies in indices
                    // var indices = arrayAccess.Indices;
                    var union1 = new HashSet<Traceable>();
                    // a2:= [v <- a2[o] U a3[loc(o.f)] if loc(o.f) is CF
                    // TODO: Check this. I think it is too conservative to add a2[o]
                    // this is a2[o]
                    union1 = GetTraceablesFromA2_Variables(baseArray);

                    foreach (var ptgNode in currentPTG.GetTargets(baseArray))
                    {
                        // TODO: I need to provide a BasicType. I need the base of the array 
                        // Currenly I use the method containing type
                        var fakeField = new FieldReference("[]", arrayAccess.Type, method.ContainingType);
                        //fakeField.ContainingType = PlatformTypes.Object;
                        var loc = new Location(ptgNode, fakeField);
                        if (this.State.Dependencies.A3_Clousures.ContainsKey(loc))
                        {
                            union1.UnionWith(this.State.Dependencies.A3_Clousures[loc]);
                        }
                    }
                    this.State.Dependencies.A2_Variables[loadStmt.Result] = union1;
                }
                else if (operand is ArrayLengthAccess)
                {
                    UpdateUsingDefUsed(loadStmt);
                }
                else if (operand is IVariable)
                {
                    var v = operand as IVariable;
                    this.State.Dependencies.A2_Variables[loadStmt.Result] = GetTraceablesFromA2_Variables(v);
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
                var union1 = new HashSet<Traceable>();
                // a2:= [v <- a2[o] U a3[loc(o.f)] if loc(o.f) is CF
                // TODO: Check this. I think it is too conservative to add a2[o]
                // this is a2[o]
                union1 = GetTraceablesFromA2_Variables(fieldAccess.Instance);
                if (ISClousureField(fieldAccess.Instance, fieldAccess.Field))
                {
                    // this is a[loc(o.f)]
                    foreach (var ptgNode in currentPTG.GetTargets(fieldAccess.Instance))
                    {
                        var loc = new Location(ptgNode, fieldAccess.Field);
                        if (this.State.Dependencies.A3_Clousures.ContainsKey(loc))
                        {
                            union1.UnionWith(this.State.Dependencies.A3_Clousures[loc]);
                        }
                    }
                }
                this.State.Dependencies.A2_Variables[loadStmt.Result] = union1;
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
                    var union1 = new HashSet<Traceable>();
                    // a2:= [v <- a3[loc(o.f)] if loc(o.f) is CF
                    // if (ISClousureField(PointsToGraph.GlobalNode.Variables.Single(), fieldAccess.Field))
                    {
                        // this is a[loc(C.f)]
                        var loc = new Location(PointsToGraph.GlobalNode, fieldAccess.Field);
                        if (this.State.Dependencies.A3_Clousures.ContainsKey(loc))
                        {
                            union1.UnionWith(this.State.Dependencies.A3_Clousures[loc]);
                        }

                    }
                    this.State.Dependencies.A2_Variables[loadStmt.Result] = union1;
                }
                else
                { }
                // Static fields now supported
                //else
                //{
                //    if (!fieldAccess.Field.ContainingType.Equals(PlatformTypes.String))
                //    {
                //        AnalysisStats.AddAnalysisReason(new AnalysisReason(this.method.Name, loadStmt, "Static store instruction not Supported"));
                //        this.State.IsTop = true;
                //    }
                //}

            }


            public override void Visit(StoreInstruction instruction)
            {
                visitorPTA.Visit(instruction);

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
                            this.State.Dependencies.A2_Variables.AddRange(v,  GetTraceablesFromA2_Variables(instruction.Operand));
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
                            this.State.Dependencies.A2_Variables.AddRange(reference, GetTraceablesFromA2_Variables(instruction.Operand));
                        }
                    }
                    else
                    {
                        AnalysisStats.AddAnalysisReason(new AnalysisReason(this.method.Name, instruction, "Unsupported Store"));
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
                        var union = GetTraceablesFromA2_Variables(instruction.Operand);
                        foreach (var ptgNode in currentPTG.GetTargets(o))
                        {
                            this.State.Dependencies.A3_Clousures[new Location(ptgNode, field)] = union;
                        }
                    }
                    scopeData.ProcessStoreField(instruction, fieldAccess);

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
                    var union = GetTraceablesFromA2_Variables(instruction.Operand);
                    foreach (var ptgNode in currentPTG.GetTargets(baseArray))
                    {
                        // TODO: I need to provide a BasicType. I need the base of the array 
                        // Currenly I use the method containing type
                        var fakeField = new FieldReference("[]", arrayAccess.Type, method.ContainingType);
                        //fakeField.ContainingType = PlatformTypes.Object;
                        var loc = new Location(ptgNode, fakeField);
                        this.State.Dependencies.A3_Clousures[new Location(ptgNode, fakeField)] = union;
                    }
                }
                else if (instructionResult is StaticFieldAccess)
                {
                    var field = (instructionResult as StaticFieldAccess).Field;
                    var union = GetTraceablesFromA2_Variables(instruction.Operand);
                    this.State.Dependencies.A3_Clousures[new Location(PointsToGraph.GlobalNode, field)] = union;

                    this.State.Dependencies.A1_Escaping.UnionWith(GetTraceablesFromA2_Variables(instruction.Operand));
                }
                else
                {
                    result = false;
                }
                return result;
            }


            public override void Visit(ConditionalBranchInstruction instruction)
            {
                visitorPTA.Visit(instruction);

                this.State.Dependencies.ControlVariables.UnionWith(instruction.UsedVariables.Where( v => GetTraceablesFromA2_Variables(v).Any()));

            }
            public override void Visit(ReturnInstruction instruction)
            {
                visitorPTA.Visit(instruction);

                if (instruction.HasOperand)
                {
                    var rv = this.iteratorDependencyAnalysis.ReturnVariable;
                    this.State.Dependencies.A2_Variables.AddRange(this.iteratorDependencyAnalysis.ReturnVariable, GetTraceablesFromA2_Variables(rv));
                }
            }
            public override void Visit(MethodCallInstruction instruction)
            {
                var methodCallStmt = instruction;
                var methodInvoked = methodCallStmt.Method;
                var callResult = methodCallStmt.Result;

                // We are analyzing instructions of the form this.table.Schema.IndexOf("columnLiteral")
                // to maintain a mapping between column numbers and literals 
                var isSchemaMethod = AnalyzeSchemaRelatedMethod(methodCallStmt, methodInvoked);
                if (!isSchemaMethod)
                {
                    var isScopeRowMethod = AnalyzeScopeRowMethods(methodCallStmt, methodInvoked);
                    if (!isScopeRowMethod)
                    {
                        var isCollectionMethod = AnalyzeCollectionMethods(methodCallStmt, methodInvoked);
                        if(!isCollectionMethod)
                        {
                            // Pure Methods
                            if(IsPureMethod(methodCallStmt))
                            {
                                UpdatePTAForPure(methodCallStmt);
                                UpdateUsingDefUsed(methodCallStmt);
                            }
                            else
                            {
                                // I first check in the calle may a input/output row
                                var argRootNodes = methodCallStmt.Arguments.SelectMany(arg => currentPTG.GetTargets(arg, false));
                                var escaping = currentPTG.ReachableNodes(argRootNodes).Intersect(this.iteratorDependencyAnalysis.protectedNodes).Any();
                                if (escaping)
                                {
                                    if (this.iteratorDependencyAnalysis.InterProceduralAnalysisEnabled || IsMethodToInline(methodInvoked))
                                    {
                                        // This updates the Dep Domain and the PTG
                                        var computedCalles = this.iteratorDependencyAnalysis.interproceduralManager.ComputePotentialCallees(instruction, currentPTG);
                                        AnalyzeResolvedCallees(instruction, methodCallStmt, computedCalles.Item1);

                                        // If there are unresolved calles
                                        if (computedCalles.Item2.Any())
                                        {
                                            HandleNoAnalyzableMethod(instruction, methodCallStmt);
                                        }
                                    }
                                    else
                                    {
                                        HandleNoAnalyzableMethod(instruction, methodCallStmt);
                                    }
                                }
                                else
                                {
                                    // I should at least update the Poinst-to graph
                                    // or make the parameters escape
                                    foreach(var escapingNode in argRootNodes.Where(n => n.Kind!=PTGNodeKind.Null))
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
                    var returnNode = new PTGNode(new PTGID(new MethodContex(this.method), (int)instruction.Offset), instruction.Result.Type);

                    foreach (var result in instruction.ModifiedVariables)
                    {
                        var allNodes = new HashSet<PTGNode>();
                        foreach (var arg in instruction.UsedVariables)
                        {
                            var nodes = this.currentPTG.GetTargets(arg, false);
                            allNodes.UnionWith(nodes);
                        }

                        currentPTG.RemoveEdges(result);
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
                        AnalysisStats.TotalofFrameworkErrors++;
                        HandleNoAnalyzableMethod(instruction, methodCallStmt);
                    }
                }
            }

            private void HandleNoAnalyzableMethod(MethodCallInstruction instruction, MethodCallInstruction methodCallStmt)
            {
                UpdatePTAForPure(methodCallStmt);
                UpdateUsingDefUsed(methodCallStmt);
                // I already know that the are argument escaping (because I only invoke this method in that case
                //var argRootNodes = methodCallStmt.Arguments.SelectMany(arg => ptg.GetTargets(arg, false));
                //var escaping = ptg.ReachableNodes(argRootNodes).Intersect(this.iteratorDependencyAnalysis.protectedNodes).Any();
                //if(escaping)
                //{
                    this.State.Dependencies.A1_Escaping.UnionWith(methodCallStmt.Arguments.SelectMany(arg => GetTraceablesFromA2_Variables(arg)));
                    AnalysisStats.AddAnalysisReason(new AnalysisReason(this.method.Name, instruction, 
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
                if (containingType is BasicType && metodCallStmt.Method.Name==".ctor")
                {
                    return true;
                }
                if (containingType.TypeKind == TypeKind.ValueType)
                {
                    return true;
                }
                return result;
            }

            public override void Visit(PhiInstruction instruction)
            {
                visitorPTA.Visit(instruction);

                UpdateUsingDefUsed(instruction);
            }

            /// <summary>
            /// Default treatment of statement using Def/Use information
            /// TODO: Check for soundness
            /// </summary>
            /// <param name="instruction"></param>
            public override void Default(Instruction instruction)
            {
                visitorPTA.Visit(instruction);

                UpdateUsingDefUsed(instruction);
                // base.Default(instruction);
            }

            /// <summary>
            /// Special treatment for collection methdod: some are pure, other only modify the receiver
            /// </summary>
            /// <param name="methodCallStmt"></param>
            /// <param name="methodInvoked"></param>
            /// <returns></returns>
            private bool AnalyzeCollectionMethods(MethodCallInstruction methodCallStmt, IMethodReference methodInvoked)
            {
                var pureCollectionMethods = new HashSet<String>() { "Contains", "ContainsKey", "get_Item", "Count", "get_Count" };
                var pureEnumerationMethods = new HashSet<String>() { "Select", "Where", "Any", "Count", "GroupBy"};
                 

                var result = true;
                if (methodInvoked.Name == "Any") //  && methodInvoked.ContainingType.FullName == "Enumerable")
                {
                    var arg = methodCallStmt.Arguments[0];
                    var tablesCounters = GetTraceablesFromA2_Variables(arg).OfType<TraceableTable>()
                                        .Select(table_i => new TraceableCounter(table_i));
                    var any = GetTraceablesFromA2_Variables(arg).Any();
                    UpdateUsingDefUsed(methodCallStmt);
                }
                else if(methodInvoked.IsPure() || pureEnumerationMethods.Contains(methodInvoked.Name)) // && methodInvoked.ContainingType.FullName.Contains("Enumerable"))
                {
                    UpdateUsingDefUsed(methodCallStmt);
                }
                else if(pureCollectionMethods.Contains(methodInvoked.Name) &&  TypeHelper.IsContainer(methodInvoked.ContainingType))
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
                    this.State.Dependencies.A2_Variables.AddRange(arg0, new HashSet<Traceable>(GetTraceablesFromA2_Variables(arg1)));
                }
                else if (methodInvoked.Name == "get_Current" 
                    && (methodInvoked.ContainingType.Name == "IEnumerator"))
                {
                    var arg = methodCallStmt.Arguments[0];
                    AssignTraceables(arg, methodCallStmt.Result);
                }
                else if (methodInvoked.Name == "MoveNext"
                    && (methodInvoked.ContainingType.Name == "IEnumerator"))
                {
                    var arg = methodCallStmt.Arguments[0];
                    AssignTraceables(arg, methodCallStmt.Result);
                }
                else if (methodInvoked.Name == "GetEnumerator"
                    && (methodInvoked.ContainingType.Name == "IEnumerable"))
                {
                    var arg = methodCallStmt.Arguments[0];
                    AssignTraceables(arg, methodCallStmt.Result);
                }
                else
                {
                    result = false;
                }
                return result;
            }

            private void AssignTraceables(IVariable source, IVariable destination)
            {
                HashSet<Traceable> union = GetTraceablesFromA2_Variables(source);
                this.State.Dependencies.A2_Variables[destination] = union; 
            }
            private void AddTraceables(IVariable source, IVariable destination)
            {
                HashSet<Traceable> union = GetTraceablesFromA2_Variables(source);
                this.State.Dependencies.A2_Variables.Add(destination, union);
            }

            private bool  AnalyzeScopeRowMethods(MethodCallInstruction methodCallStmt, IMethodReference methodInvoked)
            {
                var result = true;
                if(methodInvoked.ContainingType.ContainingAssembly.Name!="ScopeRuntime")
                {
                    return false;
                }
                // This is when you get rows
                // a2 = a2[v<- a[arg_0]] 
                if (methodInvoked.Name == "get_Rows" && methodInvoked.ContainingType.Name == "RowSet")
                {
                    var arg = methodCallStmt.Arguments[0];
                    AssignTraceables(arg, methodCallStmt.Result);

                    // TODO: I don't know I need this
                    scopeData.UpdateSchemaMap(methodCallStmt.Result, arg, currentPTG);
                }
                // This is when you get enumerator (same as get rows)
                // a2 = a2[v <- a[arg_0]] 
                else if (methodInvoked.Name == "GetEnumerator" && methodInvoked.ContainingType.GenericName== "IEnumerable<Row>")
                {
                    var arg = methodCallStmt.Arguments[0];

                    // a2[ v = a2[arg[0]]] 
                    AssignTraceables(arg, methodCallStmt.Result);
                    
                    // TODO: Do I need this?
                    var rows = equalities.GetValue(arg) as MethodCallExpression;
                    scopeData.UpdateSchemaMap(methodCallStmt.Result, rows.Arguments[0], currentPTG);
                    
                    // scopeData.schemaMap[methodCallStmt.Result] = inputTable;
                }
                // v = arg.Current
                // a2 := a2[v <- Table(i)] if Table(i) in a2[arg]
                else if (methodInvoked.Name == "get_Current" 
                    && ( methodInvoked.ContainingType.GenericName == "IEnumerator<Row>")
                         || methodInvoked.ContainingType.GenericName == "IEnumerator<ScopeMapUsage>")
                {
                    var arg = methodCallStmt.Arguments[0];
                    AssignTraceables(arg, methodCallStmt.Result);
                }
                // v = arg.Current
                // a2 := a2[v <- Table(i)] if Table(i) in a2[arg]
                else if (methodInvoked.Name == "MoveNext" && methodInvoked.ContainingType.GenericName == "IEnumerator")
                {
                    var arg = methodCallStmt.Arguments[0];
                    var tablesCounters = GetTraceablesFromA2_Variables(arg).OfType<TraceableTable>()
                                        .Select(table_i => new TraceableCounter(table_i));
                    this.State.Dependencies.A2_Variables[methodCallStmt.Result] = new HashSet<Traceable>(tablesCounters);
                }
                // v = arg.getItem(col)
                // a2 := a2[v <- Col(i, col)] if Table(i) in a2[arg]
                else if (methodInvoked.Name == "get_Item" && methodInvoked.ContainingType.GenericName== "Row")
                {
                    var arg = methodCallStmt.Arguments[0];
                    var col = methodCallStmt.Arguments[1];
                    var columnLiteral = ObtainColumn(col);

                    var tableColumns = GetTraceablesFromA2_Variables(arg).OfType<TraceableTable>()
                                        .Select(table_i => new TraceableColumn(table_i, columnLiteral));

                    this.State.Dependencies.A2_Variables[methodCallStmt.Result] = new HashSet<Traceable>(tableColumns); ;

                    scopeData.UpdateSchemaMap(methodCallStmt.Result, arg, currentPTG);
                }
                // arg.Set(arg1)
                // a4 := a4[arg0 <- a4[arg0] U a2[arg1]] 
                else if (methodInvoked.Name == "Set" && methodInvoked.ContainingType.Name == "ColumnData")
                {
                    var arg0 = methodCallStmt.Arguments[0];
                    var arg1 = methodCallStmt.Arguments[1];


                    var tables = GetTraceablesFromA2_Variables(arg1);
                    this.State.Dependencies.A4_Ouput.AddRange(arg0, tables);

                    //
                    var traceables = this.State.Dependencies.ControlVariables.SelectMany(controlVar => GetTraceablesFromA2_Variables(controlVar));
                    this.State.Dependencies.A4_Ouput.AddRange(arg0, traceables);
                }
                // arg.Copy(arg1)
                // a4 := a4[arg1 <- a4[arg1] U a2[arg0]] 
                else if (methodInvoked.Name == "CopyTo" && methodInvoked.ContainingType.Name == "Row")
                {
                    // TODO: This is a pass-through!
                    var arg0 = methodCallStmt.Arguments[0];
                    var arg1 = methodCallStmt.Arguments[1];

                    var tables = GetTraceablesFromA2_Variables(arg0);
                    var column = ColumnDomain.ALL;
                    this.State.Dependencies.A4_Ouput.AddRange(arg1, tables.OfType<TraceableTable>().Select( t => new TraceableColumn(t, column)));
                    //
                    var traceables = this.State.Dependencies.ControlVariables.SelectMany(controlVar => GetTraceablesFromA2_Variables(controlVar));
                    this.State.Dependencies.A4_Ouput.AddRange(arg1, traceables);
                }
                else if ((methodInvoked.Name == "get_String" || methodInvoked.Name == "Get") && methodInvoked.ContainingType.Name == "ColumnData")
                {
                    var arg = methodCallStmt.Arguments[0];
                    this.State.Dependencies.A2_Variables[methodCallStmt.Result] = new HashSet<Traceable>(GetTraceablesFromA2_Variables(arg)); ;
                }
                else if (methodInvoked.Name == "Load" && methodInvoked.ContainingType.Name == "RowList")
                {
                    var receiver = methodCallStmt.Arguments[0];
                    var arg1 = methodCallStmt.Arguments[1];
                    this.State.Dependencies.A2_Variables[receiver] = new HashSet<Traceable>(GetTraceablesFromA2_Variables(arg1)); 
                }
                else if(methodInvoked.ContainingType.ContainingNamespace=="ScopeRuntime")
                {
                    this.UpdateUsingDefUsed(methodCallStmt);
                }
                else
                {
                    result = false;
                }
                return result;

            }

            //private void UpdateSchemaMap(IVariable callResult, IVariable arg)
            //{
            //    var inputTable = equalities.GetValue(arg);

            //    scopeData.schemaMap[callResult] = inputTable;
            //    UpdateTableSchemaMap(callResult, arg);
            //}

            //private void UpdateSchemaMap(IVariable callResult, IVariable arg)
            //{
            //    var nodes = currentPTG.GetTargets(arg, false);
            //    if (nodes.Any())
            //    {
            //        var tables = nodes.Where(n => n.Type is IBasicType && (n.Type as IBasicType).GetFullName() != "").SelectMany(n => n.Sources.Select(kv => kv.Key.Name));
            //        scopeData.schemaTableMap[callResult] = new HashSet<string>(tables);
            //    }
            //}

 
            private ColumnDomain ObtainColumn(IVariable col)
            {
                ColumnDomain result = result = ColumnDomain.TOP; 
                var columnLiteral = "";
                if (col.Type.ToString() == "String")
                {
                    var columnValue = this.equalities.GetValue(col);
                    if (columnValue is Constant)
                    {
                        columnLiteral = columnValue.ToString();
                        result = new ColumnDomain(columnLiteral);
                    }
                }
                else
                {
                    if (scopeData.columnMap.ContainsKey(col))
                    {
                        columnLiteral = scopeData.columnMap[col];
                        result = new ColumnDomain(columnLiteral);
                    }
                    else
                    {
                        var colValue = this.equalities.GetValue(col);
                        if(colValue is Constant)
                        {
                            var value = colValue as Constant;
                            result = new ColumnDomain((int)value.Value);
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

            private bool IsMethodToInline(IMethodReference methodInvoked)
            { 
               string pattern = "<>m__Finally";
               return methodInvoked.Name.StartsWith(pattern);
             }

            /// <summary>
            /// These are method that access columns by name or number 
            /// </summary>
            /// <param name="methodCallStmt"></param>
            /// <param name="methodInvoked"></param>
            /// <param name="callResult"></param>
            /// <returns></returns>
            private bool AnalyzeSchemaRelatedMethod(MethodCallInstruction methodCallStmt, IMethodReference methodInvoked)
            {
                var result = true;
                // this is callResult = arg.Schema(...)
                // we associate arg the table and callResult with the schema
                if (IsSchemaMethod(methodInvoked))
                {
                    var arg = methodCallStmt.Arguments[0];
                    //var table = equalities.GetValue(arg);
                    //scopeData.schemaMap[callResult] = table;
                    scopeData.UpdateSchemaMap(methodCallStmt.Result, arg, currentPTG);
                }
                // callResult = arg.IndexOf(colunm)
                // we recover the table from arg and associate the column number with the call result
                else if (IsIndexOfMethod(methodInvoked))
                {
                    IEnumerable<string> table;
                    ColumnDomain columnLiteral;
                    UpdateColumnData(methodCallStmt, out table, out columnLiteral);
                    this.State.Dependencies.A2_Variables.AddRange(methodCallStmt.Result, table.OfType<TraceableTable>().Select(t => new TraceableColumn(t, columnLiteral)));
                }
                else
                {
                    result = false;
                }
                return result;
            }

            private void UpdateColumnData(MethodCallInstruction methodCallStmt, out IEnumerable<string> table, out ColumnDomain columnn)
            {
                var arg = methodCallStmt.Arguments[0];
                table = scopeData.GetTableFromSchemaMap(arg);
                columnn = ObtainColumn(methodCallStmt.Arguments[1]);
                scopeData.UpdateColumnMap(methodCallStmt, columnn);
                if(columnn.IsTOP)
                {
                    AnalysisStats.AddAnalysisReason(new AnalysisReason(this.method.ToString(), methodCallStmt,
                                                    String.Format(CultureInfo.InvariantCulture, "Could not compute a value for the column {0} {1}", methodCallStmt.Arguments[0], methodCallStmt.Arguments[1])));

                }
            }

            /// <summary>
            /// Get all "traceacbles" for a variable and all it aliases
            /// </summary>
            /// <param name="arg"></param>
            /// <returns></returns>
            private HashSet<Traceable> GetTraceablesFromA2_Variables(IVariable arg)
            {
                var union = new HashSet<Traceable>();
                foreach (var argAlias in GetAliases(arg))
                {
                    if (this.State.Dependencies.A2_Variables.ContainsKey(argAlias))
                    {
                        union.UnionWith(this.State.Dependencies.A2_Variables[argAlias]);
                    }
                }

                return union;
            }

            private void UpdateUsingDefUsed(Instruction instruction)
            {
                foreach (var result in instruction.ModifiedVariables)
                {
                    var union = new HashSet<Traceable>();
                    foreach (var arg in instruction.UsedVariables)
                    {
                        var tables = GetTraceablesFromA2_Variables(arg);
                        union.UnionWith(tables);

                    }
                    this.State.Dependencies.A2_Variables[result] = union;
                }
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

        public IteratorDependencyAnalysis(MethodDefinition method , ControlFlowGraph cfg, IteratorPointsToAnalysis pta,
                                            IEnumerable<ProtectedRowNode> protectedNodes, 
                                            IDictionary<IVariable, IExpression> equalitiesMap,
                                            InterproceduralManager interprocManager) : base(cfg)
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
        }
        public IteratorDependencyAnalysis(MethodDefinition method, ControlFlowGraph cfg, IteratorPointsToAnalysis pta,
                                    IEnumerable<ProtectedRowNode> protectedNodes, 
                                    IDictionary<IVariable, IExpression> equalitiesMap,
                                    InterproceduralManager interprocManager,
                                    DependencyPTGDomain initValue) : this(method, cfg, pta, protectedNodes, equalitiesMap, interprocManager) //base(cfg)
        {            
            this.initValue = initValue;
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
                            var potentialRowNode = target.Value.First();
                            //if (target.Key.Type.ToString() == "RowSet" || target.Key.Type.ToString() == "Row")
                            if (protectedNodes.Contains(potentialRowNode))
                            {
                                depValues.Dependencies.A3_Clousures.Add(new Location(ptgNode, target.Key),  
                                                                        new TraceableTable(new ProtectedRowNode(potentialRowNode, ProtectedRowNode.GetKind(potentialRowNode.Type))));
                            }
                        }
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
