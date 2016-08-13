using Backend.Analyses;
using Backend.Model;
using Backend.Utils;
using Model;
using Model.ThreeAddressCode.Expressions;
using Model.ThreeAddressCode.Instructions;
using Model.ThreeAddressCode.Values;
using Model.Types;
using System;
using System.Collections.Generic;
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

        public Tuple<DependencyDomain, PointsToGraph> DoInterProcWithCallee(DependencyDomain depDomain, PointsToGraph ptg, IList<IVariable> arguments,
                                                      IVariable result, MethodDefinition resolvedCallee)
        {
            if (resolvedCallee.Body.Instructions.Any())
            {
                ControlFlowGraph calleeCFG = this.GetCFG(resolvedCallee);

                var interProcresult = InterproceduralAnalysis(depDomain, ptg, arguments, result, resolvedCallee, calleeCFG);
                return interProcresult;
            }
            return new Tuple<DependencyDomain, PointsToGraph>(depDomain, ptg);
        }
        /// This does the interprocedural analysis. 
        /// It (currently) does NOT support recursive method invocations
        /// </summary>
        /// <param name="instruction"></param>
        /// <param name="resolvedCallee"></param>
        /// <param name="calleeCFG"></param>
        private Tuple<DependencyDomain, PointsToGraph> InterproceduralAnalysis(DependencyDomain depDomain, PointsToGraph callerPTG,
                                             IList<IVariable> arguments, IVariable methodResult, MethodDefinition resolvedCallee,
                                             ControlFlowGraph calleeCFG)
        {
            if (stackDepth > InterproceduralManager.MaxStackDepth)
                return new Tuple<DependencyDomain, PointsToGraph>(depDomain, callerPTG);

            stackDepth++;
            this.callStack.Push(resolvedCallee);   
            System.Console.WriteLine("Analyzing Method {0} Stack: {1}", new string(' ',stackDepth*2) + resolvedCallee.ToSignatureString(), stackDepth);
            // 1) Bind PTG and call PT analysis on callee. In pta.Result[node.Exit] is the PTG at exit of the callee

            IteratorPointsToAnalysis pta = this.PTABindAndRunInterProcAnalysis(callerPTG, arguments, resolvedCallee, calleeCFG);
            IDictionary<IVariable, IExpression> equalities = new Dictionary<IVariable, IExpression>();
            SongTaoDependencyAnalysis.PropagateExpressions(calleeCFG, equalities);


            // 2) Bind Parameters of the dependency analysis and run
            var calleeDepDomain = BindCallerCallee(depDomain, callerPTG, arguments, resolvedCallee);
            var dependencyAnalysis = new IteratorDependencyAnalysis(resolvedCallee, calleeCFG, pta.Result, equalities, this, calleeDepDomain);
            dependencyAnalysis.Analyze();
            stackDepth--;
            this.callStack.Pop();

            // 3) Bind callee with caller
            // Should I need the PTG of caller and callee?
            var exitCalleePTG = pta.Result[calleeCFG.Exit.Id].Output;
            var exitResult = BindCaleeCaller(methodResult, arguments, resolvedCallee, calleeCFG, calleeDepDomain, dependencyAnalysis);

            // Recover the frame of the original Ptg and bind ptg results
            PointsToGraph bindPtg = PTABindCaleeCalleer(methodResult, calleeCFG, pta);

            return new Tuple<DependencyDomain, PointsToGraph>(exitResult, bindPtg);
        }

        private static DependencyDomain BindCallerCallee(DependencyDomain depDomain, PointsToGraph ptg,
                                                         IList<IVariable> arguments, MethodDefinition resolvedCallee)
        {
            var calleeDepDomain = new DependencyDomain();
            calleeDepDomain.IsTop = depDomain.IsTop;
            // Bind parameters with arguments 
            for (int i = 0; i < arguments.Count(); i++)
            {
                var arg = arguments[i];
                var param = resolvedCallee.Body.Parameters[i];

                arg = AdaptIsReference(arg);
                param = AdaptIsReference(param);

                if (depDomain.A2_Variables.ContainsKey(arg))
                {
                    calleeDepDomain.A2_Variables[param] = depDomain.A2_Variables[arg];
                }
                if (depDomain.A4_Ouput.ContainsKey(arg))
                {
                    calleeDepDomain.A4_Ouput[param] = depDomain.A4_Ouput[arg];
                }
            }
            // calleeDepDomain.A3_Clousures = depDomain.A3_Clousures;
            calleeDepDomain.A1_Escaping.UnionWith(depDomain.A1_Escaping);
            calleeDepDomain.A3_Clousures.UnionWith(depDomain.A3_Clousures);
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

        private DependencyDomain BindCaleeCaller(IVariable result, IList<IVariable> arguments,
                                                    MethodDefinition resolvedCallee, ControlFlowGraph calleeCFG,
                                                   DependencyDomain callerDepDomain, IteratorDependencyAnalysis depAnalysis)
        {
            var exitResult = depAnalysis.Result[calleeCFG.Exit.Id].Output;
            for (int i = 0; i < arguments.Count(); i++)
            {
                var arg = arguments[i];
                var param = resolvedCallee.Body.Parameters[i];

                arg = AdaptIsReference(arg);
                param = AdaptIsReference(param);

                if (exitResult.A2_Variables.ContainsKey(param))
                {
                    callerDepDomain.A2_Variables.AddRange(arg, exitResult.A2_Variables[param]);
                }
                if (exitResult.A4_Ouput.ContainsKey(param))
                {
                    callerDepDomain.A4_Ouput.AddRange(arg, exitResult.A4_Ouput[param]);
                }
            }
            callerDepDomain.A1_Escaping.UnionWith(exitResult.A1_Escaping);
            callerDepDomain.A3_Clousures.UnionWith(exitResult.A3_Clousures);

            callerDepDomain.IsTop = exitResult.IsTop;
            // callerDepDomain.A3_Clousures = exitResult.A3_Clousures;

            if (result != null)
            {
                // Need to bind the return value
                if (exitResult.A2_Variables.ContainsKey(depAnalysis.ReturnVariable))
                {
                    callerDepDomain.A2_Variables.AddRange(result, exitResult.A2_Variables[depAnalysis.ReturnVariable]);
                }
                if (exitResult.A4_Ouput.ContainsKey(depAnalysis.ReturnVariable))
                {
                    callerDepDomain.A4_Ouput.AddRange(result, exitResult.A4_Ouput[depAnalysis.ReturnVariable]);
                }
            }

            return callerDepDomain;

        }

        public PointsToGraph PTADoInterProcWithCallee(PointsToGraph ptg, IList<IVariable> arguments, IVariable result, MethodDefinition resolvedCallee)
        {
            if (resolvedCallee.Body.Instructions.Any())
            {
                ControlFlowGraph calleeCFG = this.GetCFG(resolvedCallee);

                return PTAInterproceduralAnalysis(ptg, arguments, result, resolvedCallee, calleeCFG);

            }
            return ptg;
        }

        /// This does the interprocedural analysis. 
        /// It (currently) does NOT support recursive method invocations
        /// </summary>
        /// <param name="instruction"></param>
        /// <param name="resolvedCallee"></param>
        /// <param name="calleeCFG"></param>
        private PointsToGraph PTAInterproceduralAnalysis(PointsToGraph ptg, IList<IVariable> arguments, IVariable result, MethodDefinition resolvedCallee,
                                             ControlFlowGraph calleeCFG)
        {
            stackDepth++;
            IteratorPointsToAnalysis pta = this.PTABindAndRunInterProcAnalysis(ptg, arguments, resolvedCallee, calleeCFG);
            stackDepth--;

            return PTABindCaleeCalleer(result, calleeCFG, pta);

        }

        private PointsToGraph PTABindCaleeCalleer(IVariable result, ControlFlowGraph calleeCFG, IteratorPointsToAnalysis pta)
        {
            var exitPTG = pta.Result[calleeCFG.Exit.Id].Output;
            if (result != null)
            {
                //this.State.VarTraceables.AddRange(instruction.Result, exitResult.VarTraceables[pta.ReturnVariable]);
                exitPTG.RestoreFrame(pta.ReturnVariable, result);
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

        private static PointsToGraph PTABindCallerCallee(PointsToGraph ptg, IList<IVariable> arguments, MethodDefinition resolvedCallee)
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
        private HashSet<Traceable> GetTraceablesFromA2_Variables(IVariable arg, DependencyDomain depDomain, PointsToGraph ptg)
        {
            var union = new HashSet<Traceable>();
            foreach (var argAlias in ptg.GetAliases(arg))
            {
                if (depDomain.A2_Variables.ContainsKey(argAlias))
                {
                    union.UnionWith(depDomain.A2_Variables[argAlias]);
                }
            }
            return union;
        }
    }
}
