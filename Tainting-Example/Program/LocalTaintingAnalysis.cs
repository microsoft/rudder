using Backend.Analyses;
using Backend.Utils;
using Model.ThreeAddressCode.Values;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Backend.Model;
using Model.Types;
using Model.ThreeAddressCode.Visitor;
using Model.ThreeAddressCode.Instructions;
using Model;
using Model.ThreeAddressCode.Expressions;

namespace Program
{
    /// <summary>
    ///  This class perform the Taint analysis over a method
    ///  It propagates the analysis to others callees if needed
    /// </summary>
    class LocalTaintingDFAnalysis : ForwardDataFlowAnalysis<PropagatedInput>
    {
        /// <summary>
        /// This visitor implements the transfer funciton
        /// </summary>
        internal class TranferFuncLocalTaintStateAnalysis : InstructionVisitor
        {
            internal PropagatedInput State { get; }
            private LocalTaintingDFAnalysis dfAnalysis;
            private PointsToGraph ptg;

            internal TranferFuncLocalTaintStateAnalysis(LocalTaintingDFAnalysis dfAnalysis, PropagatedInput state, PointsToGraph ptg)
            {
                this.State = state;
                this.dfAnalysis = dfAnalysis;
                this.ptg = ptg;
            }

            /// <summary>
            ///  A load is either a = b, a = b.f, a = C.f
            /// </summary>
            /// <param name="instruction"></param>
            public override void Visit(LoadInstruction instruction)
            {
                var loadStmt = instruction;
                // a = C.f
                if (loadStmt.Operand is StaticFieldAccess)
                {
                    // TODO: Need to implement this (no big deal)
                }
                // a = b.f
                else if (loadStmt.Operand is InstanceFieldAccess)
                {
                    var fieldAccess = loadStmt.Operand as InstanceFieldAccess;
                    var o = fieldAccess.Instance;
                    var field = fieldAccess.Field;
                    var traceables = new HashSet<Traceable>();

                    // I need to do this because my pt analysis does not keep track of value types (ej: int fields)
                    // Maybe I could add this mapping directly to the PT analysis
                    foreach (var ptgNode in ptg.GetTargets(o))
                    {
                        var loc = new Location(ptgNode, field);
                        if (this.State.LocTraceables.ContainsKey(loc))
                        {
                            traceables.UnionWith(this.State.LocTraceables[loc]);
                        }
                    }
                    this.State.VarTraceables[loadStmt.Result] = traceables;
                }
                else if(loadStmt.Operand is VirtualMethodReference)
                {

                }
                else if (loadStmt.Operand is StaticMethodReference)
                {

                }
                // for a = b or other case is just do default propagation
                else
                {
                    DefaultPropagation(loadStmt);
                }
            }
            /// <summary>
            /// This is a.f = b, C.f = b, etc
            /// </summary>
            /// <param name="instruction"></param>
            public override void Visit(StoreInstruction instruction)
            {
                //  a.f = b  (v is instruction.Operand, o.f is instruction.Result)
                if (instruction.Result is InstanceFieldAccess)
                {
                    var fieldAccess = instruction.Result as InstanceFieldAccess;
                    var o = fieldAccess.Instance;
                    var field = fieldAccess.Field;

                    var traceables = GetTraceablesWithAliases(instruction.Operand);
                    // I need to do this because my pt analysis does not keep track of value types (ej: int fields)
                    // Maybe I could add this mapping directly to the PT analysis
                    foreach (var ptgNode in ptg.GetTargets(o))
                    {
                        this.State.LocTraceables[new Location(ptgNode, field)] = traceables;
                    }
                }
            }
            public override void Visit(ReturnInstruction instruction)
            {
                if(instruction.HasOperand)
                {
                    this.State.VarTraceables[this.dfAnalysis.ptAnalysis.ReturnVariable] = GetTraceablesWithAliases(instruction.Operand);
                }
            }
            public override void Visit(MethodCallInstruction instruction)
            {
                var callee = instruction.Method;

                /// First attempt: super basic stuff.
                if(callee.ContainingType.Name=="SinkClass" && callee.Name=="SinkMethod")
                {
                    ProcessSinkMethod(instruction);
                }
                else if (callee.ContainingType.Name == "SourceClass" && callee.Name == "SourceMethod")
                {
                    ProcessSourceMethod(instruction);
                }
                else
                {
                    var computedCalles = ComputePotentialCallees(instruction);
                    foreach (var resolvedCallee in computedCalles.Item1)
                    {
                        DoInterProcWithCallee(instruction, resolvedCallee);
                    }
                }
            }

            private Tuple<IEnumerable<MethodDefinition>, IEnumerable<IMethodReference>> ComputePotentialCallees(MethodCallInstruction instruction)
            {
                var resolvedCallees = new HashSet<MethodDefinition>();
                var unresolvedCallees = new HashSet<IMethodReference>();
                var potentalDelegateCall = instruction.Method;

                // If it is a delegate
                if (potentalDelegateCall.Name == "Invoke")
                {
                    var classDef = (potentalDelegateCall.ContainingType.ResolvedType) as ClassDefinition;
                    if (classDef != null && classDef.IsDelegate)
                    {
                        var delegateVar = instruction.Arguments[0];
                        var potentialDelegates = ptg.GetTargets(delegateVar);
                        var resolvedDelegateCallees = potentialDelegates.OfType<DelegateNode>()
                            .Select(d => dfAnalysis.host.FindMethodImplementation(d.Instance.Type as BasicType, d.Method) as MethodDefinition);

                        resolvedCallees.UnionWith(resolvedDelegateCallees.Where(c => c is MethodDefinition));
                        unresolvedCallees.UnionWith(resolvedDelegateCallees.Where(c => !(c is MethodDefinition)));
                    }
                }
                else
                {
                    if (!instruction.Method.IsStatic && instruction.Method.Name != ".ctor")
                    {
                        var receiver = instruction.Arguments[0];
                        var types = ptg.GetTargets(receiver, false).Where(n => n.Kind != PTGNodeKind.Null && n.Type!=null).Select(n => n.Type);
                        var candidateCalless = types.Select(t => this.dfAnalysis.host.FindMethodImplementation(t as IBasicType, instruction.Method));
                        var resolvedInvocations = candidateCalless.Select(c => (this.dfAnalysis.host.ResolveReference(c) as IMethodReference));
                        resolvedCallees.UnionWith(resolvedInvocations.OfType<MethodDefinition>());
                        unresolvedCallees.UnionWith(resolvedInvocations.Where(c => !(c is MethodDefinition)));
                    }
                    else
                    {
                        var candidateCalee = this.dfAnalysis.host.FindMethodImplementation(instruction.Method.ContainingType, instruction.Method);
                        var resolvedCalle = this.dfAnalysis.host.ResolveReference(candidateCalee) as MethodDefinition;
                        if (resolvedCalle != null)
                        {
                            resolvedCallees.Add(resolvedCalle);
                        }
                    }
                }
                return new Tuple<IEnumerable<MethodDefinition>, IEnumerable<IMethodReference>>(resolvedCallees, unresolvedCallees);
            }

            private void ProcessSourceMethod(MethodCallInstruction instruction)
            {
                //var literal = this.dfAnalysis.currentMethod.Body.Instructions.OfType<LoadInstruction>().Where(def => def.Result.Equals(instruction.Arguments[0])).Single().Operand.ToString();
                var literal = this.dfAnalysis.equalities.GetValue(instruction.Arguments[0]);
                var traceable = new Traceable(literal.ToString());
                this.State.VarTraceables.Add(instruction.Result, traceable);
            }

            private void ProcessSinkMethod(MethodCallInstruction instruction)
            {
                var leaking = instruction.Arguments.Where(arg => this.State.VarTraceables.ContainsKey(arg) && this.State.VarTraceables[arg].Any())
                                                    .SelectMany(arg => GetTraceablesWithAliases(arg));
                if (leaking.Any())
                {
                    var methodName = this.dfAnalysis.currentMethod.ContainingType.Name + "." + this.dfAnalysis.currentMethod.Name;
                    var leakInfo = new LeakInfo()
                    {
                        MethodName = methodName,
                        Instruction = instruction,
                        Leaks = leaking
                    };
                    this.State.LeakingIns.Add(leakInfo);
                }
            }

            private void DoInterProcWithCallee(MethodCallInstruction instruction, MethodDefinition resolvedCallee)
            {
                if (resolvedCallee.Body.Instructions.Any())
                {
                    ControlFlowGraph calleeCFG = this.dfAnalysis.methodCache.GetCFG(resolvedCallee);
                    InterproceduralAnalysis(instruction, resolvedCallee, calleeCFG);
                }
            }

            /// <summary> 	Backend.dll!Backend.Analyses.ForwardDataFlowAnalysis<Program.PropagatedInput>.Analyze() Line 86	C#

            /// This does the interprocedural analysis. 
            /// It (currently) does NOT support recursive method invocations
            /// </summary>
            /// <param name="instruction"></param>
            /// <param name="resolvedCallee"></param>
            /// <param name="calleeCFG"></param>
            private void InterproceduralAnalysis(MethodCallInstruction instruction, MethodDefinition resolvedCallee, 
                                                 ControlFlowGraph calleeCFG)
            {
                var bindPtg = ptg.Clone();
                var argParamMap = new Dictionary<IVariable, IVariable>();
                // Bind parameters with arguments in PTA
                for (int i = 0; i < instruction.Arguments.Count(); i++)
                {
                    argParamMap[instruction.Arguments[i]] = resolvedCallee.Body.Parameters[i];
                }
                bindPtg.NewFrame(argParamMap);

                // Bind parameters with arguments in Taint analysis
                for (int i = 0; i < instruction.Arguments.Count(); i++)
                {
                    argParamMap[instruction.Arguments[i]] = resolvedCallee.Body.Parameters[i];
                }

                // Compute PT analysis for callee
                var pta = new PointsToAnalysisWithResults(calleeCFG, resolvedCallee, bindPtg);
                pta.Analyze();

                var callerState = new PropagatedInput();
                // Bind parameters with arguments in Taint analysis
                for (int i = 0; i < instruction.Arguments.Count(); i++)
                {
                    callerState.VarTraceables[resolvedCallee.Body.Parameters[i]] = GetTraceablesWithAliases(instruction.Arguments[i]);
                }

                // Compute Taint analysis for calle using the new pt analysis
                var dfa = new LocalTaintingDFAnalysis(resolvedCallee, 
                                                      calleeCFG, 
                                                      pta, 
                                                      this.dfAnalysis.host, 
                                                      this.dfAnalysis.methodCache, 
                                                      callerState);
                var result = dfa.Analyze();

                var exitResult = result[calleeCFG.Exit.Id].Output;

                // combine with the result of the analyss
                this.State.LeakingIns.UnionWith(exitResult.LeakingIns);
                for (int i = 0; i < instruction.Arguments.Count(); i++)
                {
                    this.State.VarTraceables.AddRange(instruction.Arguments[i], callerState.VarTraceables[resolvedCallee.Body.Parameters[i]]);
                    this.State.LocTraceables.UnionWith(callerState.LocTraceables);
                }


                // Bind again with return values
                // We need to remove unreacheable nodes
                var exitPTG = pta.Result[calleeCFG.Exit.Id].Output;
                if (instruction.HasResult)
                {
                    this.State.VarTraceables.AddRange(instruction.Result, exitResult.VarTraceables[pta.ReturnVariable]);
                    exitPTG.RestoreFrame(pta.ReturnVariable, instruction.Result);
                }
                else
                {
                    exitPTG.RestoreFrame();
                }
            }

            public override void Default(Instruction instruction)
            {
                DefaultPropagation(instruction);
            }

            private void DefaultPropagation(Instruction instruction)
            {
                foreach (var result in instruction.ModifiedVariables)
                {
                    var traceables = new HashSet<Traceable>();
                    foreach (var arg in instruction.UsedVariables)
                    {
                        var vars  = GetTraceablesWithAliases(arg);
                        traceables.UnionWith(vars);

                    }
                    this.State.VarTraceables[result] = traceables;
                }
            }

            /// <summary>
            /// Get all "parameters" for a variable and all it aliases
            /// </summary>
            /// <param name="arg"></param>
            /// <returns></returns>
            private HashSet<Traceable> GetTraceablesWithAliases(IVariable arg)
            {
                var traceables = new HashSet<Traceable>();
                foreach (var argAlias in GetAliases(arg))
                {
                    if (this.State.VarTraceables.ContainsKey(argAlias))
                    {
                        traceables.UnionWith(this.State.VarTraceables[argAlias]);
                    }
                }

                return traceables;
            }
            private ISet<PTGNode> GetPtgNodes(IVariable v)
            {
                var res = new HashSet<PTGNode>();
                if (ptg.Contains(v))
                {
                    res.UnionWith(ptg.GetTargets(v));
                }
                return res;
            }

            private ISet<IVariable> GetAliases(IVariable v)
            {
                var res = new HashSet<IVariable>() { v };
                foreach (var ptgNode in GetPtgNodes(v))
                {
                    res.UnionWith(ptgNode.Variables);
                }
                return res;
            }



        }
        private PointsToAnalysisWithResults ptAnalysis;
        private MethodCFGCache methodCache;
        private MethodDefinition currentMethod;
        private IList<IVariable> currentMethodParameters;
        private PropagatedInput entryValue;
        private Host host;
        
        public LocalTaintingDFAnalysis(MethodDefinition method, 
                                       ControlFlowGraph cfg, 
                                       PointsToAnalysisWithResults pta,
                                       Host host,
                                       MethodCFGCache methodCache) : base(cfg)
        {
            this.ptAnalysis = pta;
            this.host = host;
            this.currentMethod = method;
            this.methodCache= methodCache;
            this.currentMethodParameters = currentMethod.Body.Parameters;
            this.entryValue = EntryValue();
            this.PropagateExpressions(cfg);
        }

        public LocalTaintingDFAnalysis(MethodDefinition method, ControlFlowGraph cfg,
                                        PointsToAnalysisWithResults pta,
                                        Host host,
                                        MethodCFGCache methodCache,
                                        PropagatedInput entryValue) : base(cfg)
        {
            this.ptAnalysis = pta;
            this.host = host;
            this.currentMethod = method;
            this.methodCache = methodCache;
            this.currentMethodParameters = currentMethod.Body.Parameters;
            this.entryValue = entryValue;
            this.PropagateExpressions(cfg);
        }

        protected override bool Compare(PropagatedInput left, PropagatedInput right)
        {
            return left.LessEqual(right);
        }

        protected override PropagatedInput Flow(CFGNode node, PropagatedInput input)
        {
            var currentPTG = this.ptAnalysis.Result[node.Id].Output;
            var inputCopy = input.Clone();
            var transfer = new TranferFuncLocalTaintStateAnalysis(this, inputCopy, currentPTG);
            transfer.Visit(node);
            return transfer.State;
        }

        protected override PropagatedInput InitialValue(CFGNode node)
        {
            if(node.Id == cfg.Entry.Id)
            {
                return entryValue;
            }
            var result = new PropagatedInput();
            return result;
        }
        protected PropagatedInput EntryValue()
        {
            var result = new PropagatedInput();
            return result;
        }

        protected override PropagatedInput Join(PropagatedInput left, PropagatedInput right)
        {
            return left.Join(right);
        }

        #region Methods to Compute a sort of propagation of Equalities (should be moved to extensions or utils)
        private IDictionary<IVariable, IExpression> equalities = new Dictionary<IVariable, IExpression>();
        private void PropagateExpressions(ControlFlowGraph cfg)
        {
            foreach (var node in cfg.ForwardOrder)
            {
                this.PropagateExpressions(node);
            }
        }

        private void PropagateExpressions(CFGNode node)
        {
            foreach (var instruction in node.Instructions)
            {
                this.PropagateExpressions(instruction);
            }
        }

        private void PropagateExpressions(IInstruction instruction)
        {
            var definition = instruction as DefinitionInstruction;

            if (definition != null && definition.HasResult)
            {
                var expr = definition.ToExpression().ReplaceVariables(equalities);
                equalities.Add(definition.Result, expr);
            }
        }
        #endregion

    }

    class Traceable 
    {

        string Name { get; set; }
        public Traceable(string name)
        {
            this.Name = name;
        }

        public override bool Equals(object obj)
        {
            var traceable = obj as Traceable;
            return traceable!=null && traceable.Name==this.Name;
        }
        public override int GetHashCode()
        {
            return Name.GetHashCode();
        }
        public override string ToString()
        {
            return String.Format("Source:{0}", Name.ToString());
        }
    }

    struct LeakInfo
    {
        public string MethodName { get; set; }
        public IInstruction Instruction { get; set; }
        public IEnumerable<Traceable> Leaks { get; set; }
        public override string ToString()
        {
            return String.Format("{0}:{1} = [{2}]", MethodName, Instruction.ToString(), String.Join(",",Leaks));
        }

    } 

        class PropagatedInput
    {
        // This maintains the set of propagated parameters 
        public MapSet<IVariable, Traceable> VarTraceables{ get; set; }
        // This, combined with the pt analysis, allow us to keep track of field dereferences
        public MapSet<Location, Traceable> LocTraceables{ get; set; }
        // The set of leaked invocations (in general, method invocations)
        public ISet<LeakInfo> LeakingIns { get; set; }

        public PropagatedInput()
        {
            VarTraceables = new MapSet<IVariable, Traceable>();
            LocTraceables = new MapSet<Location, Traceable>();
            LeakingIns = new HashSet<LeakInfo>();
        }

        public PropagatedInput Clone()
        {
            var result = new PropagatedInput();
            result.VarTraceables = new MapSet<IVariable, Traceable>(this.VarTraceables);
            result.LocTraceables = new MapSet<Location, Traceable>(this.LocTraceables);
            result.LeakingIns = new HashSet<LeakInfo>(this.LeakingIns);
            return result;

        }
        public PropagatedInput Join(PropagatedInput right)
        {
            var result = new PropagatedInput();
            result.VarTraceables = new MapSet<IVariable, Traceable>(this.VarTraceables);
            result.LocTraceables = new MapSet<Location, Traceable>(this.LocTraceables);
            result.LeakingIns = new HashSet<LeakInfo>(this.LeakingIns);

            result.VarTraceables.UnionWith(right.VarTraceables);
            result.LocTraceables.UnionWith(right.LocTraceables);
            result.LeakingIns.UnionWith(right.LeakingIns);

            return result;
        }
        public bool LessEqual(PropagatedInput right)
        {
            // TODO: FIX!!
            return this.Equals(right);
        }
        public override bool Equals(object obj)
        {
            var other = obj as PropagatedInput;
            return other!=null 
                && other.VarTraceables.Equals(this.VarTraceables) 
                && other.LocTraceables.Equals(this.LocTraceables)
                && other.LeakingIns.SetEquals(this.LeakingIns);
        }

        public override int GetHashCode()
        {
            return VarTraceables.GetHashCode() + LocTraceables.GetHashCode() + LeakingIns.GetHashCode();
        }
        public override string ToString()
        {
            var result = "";
            result += "VarPar=\n";
            foreach (var var in this.VarTraceables.Keys)
            {
                result += String.Format("{0}:[{1}]\n", var, ToString(VarTraceables[var]));
            }
            result += "LocPar=\n";
            foreach (var loc in this.LocTraceables.Keys)
            {
                result += String.Format("{0}:[{1}]\n", loc, ToString(LocTraceables[loc]));
            }

            result += "Leaks=\n";
            foreach (var ins in this.LeakingIns)
            {
                result += String.Format("{0}\n", ins.ToString());
            }

            return result;
        }
        private string ToString(ISet<Traceable> set)
        {
            var result = String.Join(",", set.Select(e => e.ToString()));
            return result;
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
            this.ptgNode = new NullNode();
            this.Field = f;
        }
        public override bool Equals(object obj)
        {
            var oth = obj as Location;
            return oth != null && oth.ptgNode.Equals(this.ptgNode)
                && oth.Field.Name.Equals(this.Field.Name);
        }
        public override int GetHashCode()
        {
            return ptgNode.GetHashCode() + Field.Name.GetHashCode();
        }
        public override string ToString()
        {
            return "[" + ptgNode.ToString() + "." + Field.ToString() + "]";
        }
    }

    // Just a hack to be able to access the PTA results (Result is private in the original analysis)
    public class PointsToAnalysisWithResults : PointsToAnalysis
    {
        protected PointsToGraph initPTG;
        public  DataFlowAnalysisResult<PointsToGraph>[] Result { get; protected set; }
        public PointsToAnalysisWithResults(ControlFlowGraph cfg, MethodDefinition method) : base(cfg, method)
        {
        }
        public PointsToAnalysisWithResults(ControlFlowGraph cfg, MethodDefinition method, PointsToGraph initPTG) : base(cfg, method)
        {
            this.initialGraph.Union(initPTG);
            this.initPTG = this.initialGraph; // initPTG;
        }

        public override DataFlowAnalysisResult<PointsToGraph>[] Analyze()
        {
            Result = base.Analyze();
            return Result;
        }
        protected override PointsToGraph InitialValue(CFGNode node)
        {
            if(this.cfg.Entry.Id==node.Id && this.initPTG!=null)
            {
                return this.initPTG;
            }
            return base.InitialValue(node);
        }
    }
}
