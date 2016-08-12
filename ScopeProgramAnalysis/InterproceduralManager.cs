using Backend.Analyses;
using Backend.Model;
using Backend.Utils;
using Model;
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

    class InterproceduralManager
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

        public PointsToGraph DoInterProcWithCallee(PointsToGraph ptg, IList<IVariable> arguments, IVariable result, MethodDefinition resolvedCallee)
        {
            if (resolvedCallee.Body.Instructions.Any())
            {
                ControlFlowGraph calleeCFG = this.GetCFG(resolvedCallee);
                
                return InterproceduralAnalysis(ptg, arguments, result, resolvedCallee, calleeCFG);
                
            }
            return ptg;
        }

        /// This does the interprocedural analysis. 
        /// It (currently) does NOT support recursive method invocations
        /// </summary>
        /// <param name="instruction"></param>
        /// <param name="resolvedCallee"></param>
        /// <param name="calleeCFG"></param>
        private PointsToGraph InterproceduralAnalysis(PointsToGraph ptg, IList<IVariable> arguments, IVariable result, MethodDefinition resolvedCallee,
                                             ControlFlowGraph calleeCFG)
        {
            stackDepth++;
            IteratorPointsToAnalysis pta = this.BindAndRunInterProcPTAAnalysis(ptg, arguments, resolvedCallee, calleeCFG);
            stackDepth--;

            return BindCaleeCalleer(result, calleeCFG, pta);

        }

        private PointsToGraph BindCaleeCalleer(IVariable result, ControlFlowGraph calleeCFG, IteratorPointsToAnalysis pta)
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

        public IteratorPointsToAnalysis BindAndRunInterProcPTAAnalysis(PointsToGraph ptg, IList<IVariable> arguments, MethodDefinition resolvedCallee, ControlFlowGraph calleeCFG)
        {
            PointsToGraph bindPtg = BindCallerCallee(ptg, arguments, resolvedCallee);

            // Compute PT analysis for callee
            var pta = new IteratorPointsToAnalysis(calleeCFG, resolvedCallee, bindPtg);
            pta.Analyze();
            return pta;
        }

        private static PointsToGraph BindCallerCallee(PointsToGraph ptg, IList<IVariable> arguments, MethodDefinition resolvedCallee)
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
    }
}
