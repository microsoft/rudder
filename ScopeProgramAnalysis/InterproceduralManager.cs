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

        public MethodCFGCache CFGCache { get; set; }
        public InterproceduralManager(Host host)
        {
            this.host = host;
            this.CFGCache = new MethodCFGCache(host);
            this.stackDepth = 0;
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

                var interProcresult =  InterproceduralAnalysis(depDomain, ptg, arguments, result, resolvedCallee, calleeCFG);
                return interProcresult;
            }
            return new Tuple<DependencyDomain, PointsToGraph>(depDomain,ptg);
        }
        /// This does the interprocedural analysis. 
        /// It (currently) does NOT support recursive method invocations
        /// </summary>
        /// <param name="instruction"></param>
        /// <param name="resolvedCallee"></param>
        /// <param name="calleeCFG"></param>
        private Tuple<DependencyDomain, PointsToGraph> InterproceduralAnalysis(DependencyDomain depDomain, PointsToGraph ptg, 
                                             IList<IVariable> arguments, IVariable result, MethodDefinition resolvedCallee,
                                             ControlFlowGraph calleeCFG)
        {
            stackDepth++;
            IteratorPointsToAnalysis pta = this.PTABindAndRunInterProcAnalysis(ptg, arguments, resolvedCallee, calleeCFG);
            IDictionary<IVariable, IExpression> equalities = new Dictionary<IVariable, IExpression>();
            SongTaoDependencyAnalysis.PropagateExpressions(calleeCFG, equalities);

            // Bind Parameters 
            var calleeDepDomain = BindCallerCallee(depDomain, ptg, arguments, resolvedCallee);
            var dependencyAnalysis = new IteratorDependencyAnalysis(resolvedCallee, calleeCFG, pta.Result, equalities, this);
            var depAnalysisResult = dependencyAnalysis.Analyze();
      
            stackDepth--;
            var exitResult =  BindCaleeCalleer(result, arguments , resolvedCallee, calleeCFG, calleeDepDomain, dependencyAnalysis);
            PointsToGraph bindPtg = PTABindCaleeCalleer(result, calleeCFG, pta);

            return new Tuple<DependencyDomain, PointsToGraph>(exitResult, bindPtg);
        }

        private static DependencyDomain BindCallerCallee(DependencyDomain depDomain, PointsToGraph ptg, IList<IVariable> arguments, MethodDefinition resolvedCallee)
        {
            var calleeDepDomain = new DependencyDomain();
            // Bind parameters with arguments 
            for (int i = 0; i < arguments.Count(); i++)
            {
                if (depDomain.A2_Variables.ContainsKey(arguments[i]))
                {
                    calleeDepDomain.A2_Variables[resolvedCallee.Body.Parameters[i]] = depDomain.A2_Variables[arguments[i]];
                }
                if (depDomain.A4_Ouput.ContainsKey(arguments[i]))
                {
                    calleeDepDomain.A4_Ouput[resolvedCallee.Body.Parameters[i]] = depDomain.A2_Variables[arguments[i]];
                }
                calleeDepDomain.A1_Escaping.AddRange(depDomain.A1_Escaping);
                calleeDepDomain.A3_Clousures.UnionWith(depDomain.A3_Clousures);
            }
            return calleeDepDomain;
        }


        private DependencyDomain BindCaleeCalleer(IVariable result, IList<IVariable> arguments,  
                                                    MethodDefinition resolvedCallee,  ControlFlowGraph calleeCFG, 
                                                   DependencyDomain callerDepDomain,  IteratorDependencyAnalysis depAnalysis)
        {
            var exitResult = depAnalysis.Result[calleeCFG.Exit.Id].Output;
            for (int i = 0; i < arguments.Count(); i++)
            {
                if (exitResult.A2_Variables.ContainsKey(resolvedCallee.Body.Parameters[i]))
                {
                    callerDepDomain.A2_Variables.AddRange(arguments[i], exitResult.A2_Variables[resolvedCallee.Body.Parameters[i]]);
                }
                if (exitResult.A4_Ouput.ContainsKey(resolvedCallee.Body.Parameters[i]))
                {
                    callerDepDomain.A4_Ouput.AddRange(arguments[i], exitResult.A4_Ouput[resolvedCallee.Body.Parameters[i]]);
                }
                callerDepDomain.A1_Escaping.AddRange(exitResult.A1_Escaping);
                callerDepDomain.A3_Clousures.UnionWith(exitResult.A3_Clousures);

            }
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



        public Tuple<IEnumerable<MethodDefinition>, IEnumerable<IMethodReference>> ComputePotentialCallees(MethodCallInstruction instruction, PointsToGraph  ptg)
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

    }
}
