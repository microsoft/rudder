using Backend.Analyses;
using Backend.Model;
using Backend.Serialization;
using Backend.Utils;
using Model;
using Model.ThreeAddressCode.Expressions;
using Model.ThreeAddressCode.Instructions;
using Model.ThreeAddressCode.Values;
using Model.Types;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ScopeProgramAnalysis
{
    public class MethodCFGCache
    {
        private IDictionary<MethodDefinition, ControlFlowGraph> methodCFGMap;
        private Host host;

        public MethodCFGCache(Host host)
        {
            this.host = host;
            this.methodCFGMap = new Dictionary<MethodDefinition, ControlFlowGraph>();
        }

        public ControlFlowGraph GetCFG(MethodDefinition method)
        {
            ControlFlowGraph methodCFG = null;
            if (!this.methodCFGMap.ContainsKey(method))
            {
                methodCFG = method.DoAnalysisPhases(this.host);
                this.methodCFGMap[method] = methodCFG;
            }
            else
            {
                methodCFG = this.methodCFGMap[method];
            }
            return methodCFG;
        }
    }

    public struct InterProceduralCallInfo
    {
        public MethodDefinition Caller { get; set; }
        public DependencyPTGDomain CallerState { get; set; }
        public PointsToGraph CallerPTG { get; set; }
        public IList<IVariable> CallArguments { get; set; }
        public IVariable CallLHS { get; set; }
        public MethodDefinition Callee { get; set; }

        public IEnumerable<ProtectedRowNode> ProtectedNodes { get; set; }
        public IInstruction Instruction { get; internal set; }
    }
    public struct InterProceduralReturnInfo
    {
        public InterProceduralReturnInfo(DependencyPTGDomain state)
        {
            this.State = state;
            // this.PTG = ptg;
        }
        public DependencyPTGDomain State { get; set; }
        // public PointsToGraph PTG { get; set; }

    }


    public class InterproceduralManager
    {
        private int stackDepth;
        private Host host;
        private const int MaxStackDepth = 100;
        private Stack<MethodDefinition> callStack;

        public MethodCFGCache CFGCache { get; set; }
        public InterproceduralManager(Host host)
        {
            this.host = host;
            this.CFGCache = new MethodCFGCache(host);
            this.stackDepth = 0;
            this.callStack = new Stack<MethodDefinition>();
        }

        public ControlFlowGraph GetCFG(MethodDefinition method)
        {
            return CFGCache.GetCFG(method);
        }

        public void SetStackDepth(int d)
        {
            this.stackDepth = d;
        }

        public InterProceduralReturnInfo DoInterProcWithCallee(InterProceduralCallInfo callInfo)
        {
            if (callInfo.Callee.Body.Instructions.Any())
            {
                var previousState = callInfo.CallerState;
                ControlFlowGraph calleeCFG = this.GetCFG(callInfo.Callee);

                var interProcresult = InterproceduralAnalysis(callInfo, calleeCFG);
                // For Debugging
                if(interProcresult.State.LessEqual(previousState) && !interProcresult.State.Equals(previousState))
                { }
                return interProcresult;
            }
            return new InterProceduralReturnInfo(callInfo.CallerState);
            
        }

        /// This does the interprocedural analysis. 
        /// It (currently) does NOT support recursive method invocations
        /// </summary>
        /// <param name="instruction"></param>
        /// <param name="resolvedCallee"></param>
        /// <param name="calleeCFG"></param>
        private InterProceduralReturnInfo InterproceduralAnalysis(InterProceduralCallInfo callInfo, ControlFlowGraph calleeCFG)
        {
            if (stackDepth > InterproceduralManager.MaxStackDepth)
                return new InterProceduralReturnInfo(callInfo.CallerState);

            stackDepth++;
            // I currently do not support recursive calls 
            // Will add support for this in the near future
            if(callStack.Contains(callInfo.Callee))
            {
                callInfo.CallerState.Dependencies.IsTop = true;
                AnalysisStats.AddAnalysisReason(new AnalysisReason(callInfo.Caller.Name, callInfo.Instruction, String.Format(CultureInfo.InvariantCulture, "Recursive call to {0}", callInfo.Callee.Name)));
                return new InterProceduralReturnInfo(callInfo.CallerState);
            }

            this.callStack.Push(callInfo.Callee);   
            System.Console.WriteLine("Analyzing Method {0} Stack: {1}", new string(' ',stackDepth*2) + callInfo.Callee.ToSignatureString(), stackDepth);
            // 1) Bind PTG and create a Poinst-to Analysis for the  callee. In pta.Result[node.Exit] is the PTG at exit of the callee
            PointsToGraph calleePTG = PTABindCallerCallee(callInfo.CallerPTG, callInfo.CallArguments, callInfo.Callee);
            IteratorPointsToAnalysis calleePTA = new IteratorPointsToAnalysis(calleeCFG, callInfo.Callee, calleePTG);

            IDictionary<IVariable, IExpression> equalities = new Dictionary<IVariable, IExpression>();
            SongTaoDependencyAnalysis.PropagateExpressions(calleeCFG, equalities);

            // 2) Bind Parameters of the dependency analysis and run
            var calleeDomain = BindCallerCallee(callInfo);
            calleeDomain.PTG = calleePTG;
            var dependencyAnalysis = new IteratorDependencyAnalysis(callInfo.Callee, calleeCFG, calleePTA, callInfo.ProtectedNodes, equalities, this, calleeDomain);
            dependencyAnalysis.Analyze();
            stackDepth--;
            this.callStack.Pop();

            // 3) Bind callee with caller
            // Should I need the PTG of caller and callee?
            //var exitCalleePTG = calleePTA.Result[calleeCFG.Exit.Id].Output;
            var exitCalleePTG = dependencyAnalysis.Result[calleeCFG.Exit.Id].Output.PTG;
            var exitResult = BindCaleeCaller(callInfo, calleeCFG, dependencyAnalysis);

            // Recover the frame of the original Ptg and bind ptg results
            //PointsToGraph bindPtg = PTABindCaleeCalleer(callInfo.CallLHS, calleeCFG, calleePTA);
            PointsToGraph bindPtg = PTABindCaleeCalleer(callInfo.CallLHS, calleeCFG, exitCalleePTG, calleePTA.ReturnVariable);
            exitResult.PTG = bindPtg;

            return new InterProceduralReturnInfo(exitResult);
            //return new InterProceduralReturnInfo(exitResult, bindPtg);
        }

        private DependencyPTGDomain BindCallerCallee(InterProceduralCallInfo callInfo)
        {
            var calleeDepDomain = new DependencyPTGDomain();
            calleeDepDomain.Dependencies.IsTop = callInfo.CallerState.Dependencies.IsTop;
            // Bind parameters with arguments 
            for (int i = 0; i < callInfo.CallArguments.Count(); i++)
            {
                var arg = callInfo.CallArguments[i];
                var param = callInfo.Callee.Body.Parameters[i];

                arg = AdaptIsReference(arg);
                param = AdaptIsReference(param);

                if (callInfo.CallerState.Dependencies.A2_Variables.ContainsKey(arg))
                {
                    calleeDepDomain.Dependencies.A2_Variables[param] = GetTraceablesFromA2_Variables(arg, callInfo.CallerState); 
                                                                        //  callInfo.CallerState.Dependencies.A2_Variables[arg];
                }
                if (callInfo.CallerState.Dependencies.A4_Ouput.ContainsKey(arg))
                {
                    calleeDepDomain.Dependencies.A4_Ouput[param] = callInfo.CallerState.Dependencies.A4_Ouput[arg];
                }
            }
            calleeDepDomain.Dependencies.A1_Escaping = callInfo.CallerState.Dependencies.A1_Escaping;
            calleeDepDomain.Dependencies.A3_Clousures = callInfo.CallerState.Dependencies.A3_Clousures;
            calleeDepDomain.Dependencies.ControlVariables = callInfo.CallerState.Dependencies.ControlVariables;
            //calleeDepDomain.Dependencies.A1_Escaping.UnionWith(callInfo.CallerState.Dependencies.A1_Escaping);
            //calleeDepDomain.Dependencies.A3_Clousures.UnionWith(callInfo.CallerState.Dependencies.A3_Clousures);
            return calleeDepDomain;
        }

        private static IVariable AdaptIsReference(IVariable arg)
        {
            if (arg is Reference)
            {
                arg = (arg as Reference).Value as IVariable;
            }
            else if (arg is Dereference)
            {
                arg = (arg as Dereference).Reference;
            }

            return arg;
        }

        private DependencyPTGDomain BindCaleeCaller(InterProceduralCallInfo callInfo, ControlFlowGraph calleeCFG, IteratorDependencyAnalysis depAnalysis)
        {
            var exitResult = depAnalysis.Result[calleeCFG.Exit.Id].Output;
            for (int i = 0; i < callInfo.CallArguments.Count(); i++)
            {
                var arg = callInfo.CallArguments[i];
                var param = callInfo.Callee.Body.Parameters[i];

                arg = AdaptIsReference(arg);
                param = AdaptIsReference(param);

                callInfo.CallerState.Dependencies.A2_Variables.AddRange(arg, GetTraceablesFromA2_Variables(param,exitResult));

                //if (exitResult.A2_Variables.ContainsKey(param))
                //{
                //    callInfo.CallerState.Dependencies.A2_Variables.AddRange(arg, exitResult.A2_Variables[param]);
                //}
                if (exitResult.Dependencies.A4_Ouput.ContainsKey(param))
                {
                    callInfo.CallerState.Dependencies.A4_Ouput.AddRange(arg, exitResult.Dependencies.A4_Ouput[param]);
                }
            }

            foreach(var outputVar in exitResult.Dependencies.A4_Ouput.Keys)
            {
                var newVar = new LocalVariable(callInfo.Callee.Name + "_" + outputVar.Name);
                newVar.Type = outputVar.Type;

                callInfo.CallerState.Dependencies.A4_Ouput[newVar] = exitResult.Dependencies.A4_Ouput[outputVar];
                callInfo.CallerState.Dependencies.A2_Variables[newVar] = exitResult.Dependencies.A2_Variables[outputVar];
            }

            callInfo.CallerState.Dependencies.A1_Escaping.UnionWith(exitResult.Dependencies.A1_Escaping);
            callInfo.CallerState.Dependencies.A3_Clousures.UnionWith(exitResult.Dependencies.A3_Clousures);

            callInfo.CallerState.Dependencies.IsTop = exitResult.Dependencies.IsTop;

            if (callInfo.CallLHS != null)
            {
                // Need to bind the return value
                if (exitResult.Dependencies.A2_Variables.ContainsKey(depAnalysis.ReturnVariable))
                {
                    callInfo.CallerState.Dependencies.A2_Variables.AddRange(callInfo.CallLHS, exitResult.Dependencies.A2_Variables[depAnalysis.ReturnVariable]);
                }
                if (exitResult.Dependencies.A4_Ouput.ContainsKey(depAnalysis.ReturnVariable))
                {
                    callInfo.CallerState.Dependencies.A4_Ouput.AddRange(callInfo.CallLHS, exitResult.Dependencies.A4_Ouput[depAnalysis.ReturnVariable]);
                }
            }

            return callInfo.CallerState;

        }

        #region Interprocedural analysis for the points-to analysis
        /// This does the interprocedural analysis. 
        /// It (currently) does NOT support recursive method invocations
        /// </summary>
        /// <param name="instruction"></param>
        /// <param name="resolvedCallee"></param>
        /// <param name="calleeCFG"></param>
        public PointsToGraph PTAInterProcAnalysis(PointsToGraph ptg, IList<IVariable> arguments, IVariable result, MethodDefinition resolvedCallee)
        {
            if (resolvedCallee.Body.Instructions.Any())
            {
                ControlFlowGraph calleeCFG = this.GetCFG(resolvedCallee);
                DGMLSerializer.Serialize(calleeCFG);
                stackDepth++;
                IteratorPointsToAnalysis pta = this.PTABindAndRunInterProcAnalysis(ptg, arguments, resolvedCallee, calleeCFG);
                stackDepth--;

                return PTABindCaleeCalleer(result, calleeCFG, pta);
            }
            return ptg;
        }

        private PointsToGraph PTABindCaleeCalleer(IVariable result, ControlFlowGraph calleeCFG, IteratorPointsToAnalysis pta)
        {
            var exitPTG = pta.Result[calleeCFG.Exit.Id].Output;
            if (result != null)
            {
                exitPTG.RestoreFrame(pta.ReturnVariable, result);
            }
            else
            {
                exitPTG.RestoreFrame();
            }
            return exitPTG;
        }

        private PointsToGraph PTABindCaleeCalleer(IVariable result, ControlFlowGraph calleeCFG, PointsToGraph calleePTG, IVariable rv)
        {
            var exitPTG = calleePTG;
            if (result != null)
            {
                exitPTG.RestoreFrame(rv, result);
            }
            else
            {
                exitPTG.RestoreFrame();
            }
            return exitPTG;
        }


        public IteratorPointsToAnalysis PTABindAndRunInterProcAnalysis(PointsToGraph ptg, IList<IVariable> arguments, MethodDefinition resolvedCallee, ControlFlowGraph calleeCFG)
        {
            PointsToGraph bindPtg = PTABindCallerCallee(ptg, arguments, resolvedCallee);

            // Compute PT analysis for callee
            var pta = new IteratorPointsToAnalysis(calleeCFG, resolvedCallee, bindPtg);
            pta.Analyze();
            return pta;
        }

        public static PointsToGraph PTABindCallerCallee(PointsToGraph ptg, IList<IVariable> arguments, MethodDefinition resolvedCallee)
        {
            var bindPtg = ptg.Clone();
            var argParamMap = new Dictionary<IVariable, IVariable>();
            // Bind parameters with arguments in PTA
            for (int i = 0; i < arguments.Count(); i++)
            {
                argParamMap[arguments[i]] = resolvedCallee.Body.Parameters[i];
            }
            bindPtg.NewFrame(argParamMap);
            return bindPtg;
        }
        #endregion


        public Tuple<IEnumerable<MethodDefinition>, IEnumerable<IMethodReference>> ComputePotentialCallees(MethodCallInstruction instruction, PointsToGraph ptg)
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
                        .Select(d => host.FindMethodImplementation(d.Instance.Type as BasicType, d.Method) as MethodDefinition);

                    resolvedCallees.UnionWith(resolvedDelegateCallees.Where(c => c is MethodDefinition));
                    unresolvedCallees.UnionWith(resolvedDelegateCallees.Where(c => !(c is MethodDefinition)));
                }
            }
            else
            {
                if (!instruction.Method.IsStatic && instruction.Method.Name != ".ctor")
                {
                    var receiver = instruction.Arguments[0];
                    var types = ptg.GetTargets(receiver, false).Where(n => n.Kind != PTGNodeKind.Null && n.Type != null).Select(n => n.Type);
                    var candidateCalless = types.Select(t => host.FindMethodImplementation(t as IBasicType, instruction.Method));
                    var resolvedInvocations = candidateCalless.Select(c => (host.ResolveReference(c) as IMethodReference));
                    resolvedCallees.UnionWith(resolvedInvocations.OfType<MethodDefinition>());
                    unresolvedCallees.UnionWith(resolvedInvocations.Where(c => !(c is MethodDefinition)));
                }
                else
                {
                    var candidateCalee = host.FindMethodImplementation(instruction.Method.ContainingType, instruction.Method);
                    var resolvedCalle = host.ResolveReference(candidateCalee) as MethodDefinition;
                    if (resolvedCalle != null)
                    {
                        resolvedCallees.Add(resolvedCalle);
                    }
                }
            }
            return new Tuple<IEnumerable<MethodDefinition>, IEnumerable<IMethodReference>>(resolvedCallees, unresolvedCallees);
        }
        private HashSet<Traceable> GetTraceablesFromA2_Variables(IVariable arg, DependencyPTGDomain depDomain)
        {
            var union = new HashSet<Traceable>();
            foreach (var argAlias in depDomain.PTG.GetAliases(arg))
            {
                if (depDomain.Dependencies.A2_Variables.ContainsKey(argAlias))
                {
                    union.UnionWith(depDomain.Dependencies.A2_Variables[argAlias]);
                }
            }
            return union;
        }
    }
}
