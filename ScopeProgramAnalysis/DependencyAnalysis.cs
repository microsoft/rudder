using System.Collections.Generic;
using System.Linq;
using Model;
using Model.Types;
using Backend.Analyses;
using Backend.Serialization;
using Backend.Transformations;
using Backend.Model;
using Backend.Utils;
using Model.ThreeAddressCode.Instructions;
using Model.ThreeAddressCode.Values;
using Model.ThreeAddressCode.Expressions;

namespace ScopeProgramAnalysis
{
    class SongTaoDependencyAnalysis
    {
        private Host host;
        private DataFlowAnalysisResult<PointsToGraph>[] ptAnalysisResult;
        private MethodDefinition moveNextMethod;
        private IDictionary<IVariable, IExpression> equalities;
        private MethodDefinition entryMethod;
        private IList<InstanceFieldAccess> specialFields; 

        public SongTaoDependencyAnalysis(Host host,
                                        MethodDefinition method,
                                        MethodDefinition entryMethod)
        {
            this.host = host;
            this.moveNextMethod = method;
            this.entryMethod = entryMethod;
            this.equalities = new Dictionary<IVariable, IExpression>();
        }

        public DependencyDomain AnalyzeMoveNextMethod()
        {
            var cfgEntry = DoAnalysisPhases(entryMethod, host, false);

            var especialFields = cfgEntry.ForwardOrder[1].Instructions.OfType<StoreInstruction>()
                .Where(st => st.Result is InstanceFieldAccess).Select(st => st.Result as InstanceFieldAccess);
            this.specialFields = especialFields.ToList();

            Backend.Model.ControlFlowGraph cfg = DoAnalysisPhases(this.moveNextMethod, this.host);

            var pointsTo = new SimplerPointsToAnalysis(cfg, this.moveNextMethod);
            this.ptAnalysisResult = pointsTo.Analyze();

            this.PropagateExpressions(cfg);
            var result = this.AnalyzeScopeMethods(cfg, ptAnalysisResult);

            //var sorted_nodes = cfg.ForwardOrder;
            //var ptgExit = ptAnalysisResult[cfg.Exit.Id].Output;
            //ptgExit.RemoveTemporalVariables();
            //ptgExit.RemoveDerivedVariables();
            //var ptgDGML = DGMLSerializer.Serialize(ptgExit);

            var dgml = DGMLSerializer.Serialize(cfg);
            return result;
        }

        DependencyDomain AnalyzeScopeMethods(ControlFlowGraph cfg, DataFlowAnalysisResult<PointsToGraph>[] ptgs)
        {

            var iteratorAnalysis = new IteratorStateAnalysis(cfg, ptgs, this.equalities);
            var result = iteratorAnalysis.Analyze();

            var dependencyAnalysis = new IteratorDependencyAnalysis(this.moveNextMethod, cfg, ptgs, this.specialFields , this.equalities);
            var resultDepAnalysis = dependencyAnalysis.Analyze();

            var node = cfg.Exit;
            System.Console.Out.WriteLine("At {0}\nBefore {1}\nAfter {2}\n", node.Id, resultDepAnalysis[node.Id].Input, resultDepAnalysis[node.Id].Output);

            //foreach (var node in cfg.ForwardOrder)
            //{
            //    System.Console.Out.WriteLine("At {0}\nBefore {1}\nAfter {2}\n", node.Id, resultDepAnalysis[node.Id].Input, resultDepAnalysis[node.Id].Output);
            //    //System.Console.Out.WriteLine(String.Join(Environment.NewLine, node.Instructions));
            //}
            return resultDepAnalysis[node.Id].Output;
        }

        private static Backend.Model.ControlFlowGraph DoAnalysisPhases(MethodDefinition method, Host host, bool inline = false)
        {
            var disassembler = new Disassembler(method);
            var methodBody = disassembler.Execute();
            method.Body = methodBody;

            if (inline)
            {
                DoInlining(method, host, methodBody);
            }

            var cfAnalysis = new ControlFlowAnalysis(method.Body);
            var cfg = cfAnalysis.GenerateNormalControlFlow();

            var domAnalysis = new DominanceAnalysis(cfg);
            domAnalysis.Analyze();
            domAnalysis.GenerateDominanceTree();

            var loopAnalysis = new NaturalLoopAnalysis(cfg);
            loopAnalysis.Analyze();

            var domFrontierAnalysis = new DominanceFrontierAnalysis(cfg);
            domFrontierAnalysis.Analyze();

            var splitter = new WebAnalysis(cfg);
            splitter.Analyze();
            splitter.Transform();

            methodBody.UpdateVariables();


            var analysis = new TypeInferenceAnalysis(cfg);
            analysis.Analyze();

            var copyProgapagtion = new ForwardCopyPropagationAnalysis(cfg);
            copyProgapagtion.Analyze();
            copyProgapagtion.Transform(methodBody);

            var backwardCopyProgapagtion = new BackwardCopyPropagationAnalysis(cfg);
            backwardCopyProgapagtion.Analyze();
            backwardCopyProgapagtion.Transform(methodBody);

            var liveVariables = new LiveVariablesAnalysis(cfg);
            var resultLiveVar = liveVariables.Analyze();


            var ssa = new StaticSingleAssignment(methodBody, cfg);
            ssa.Transform();
            ssa.Prune(liveVariables);
            methodBody.UpdateVariables();

            method.Body = methodBody;

            var dgml = DGMLSerializer.Serialize(cfg);
            return cfg;
        }

        private static void DoInlining(MethodDefinition method, Host host, MethodBody methodBody)
        {
            var methodCalls = methodBody.Instructions.OfType<MethodCallInstruction>().ToList();
            foreach (var methodCall in methodCalls)
            {
                var calleeM = host.ResolveReference(methodCall.Method);
                var callee = calleeM as MethodDefinition;
                if (callee != null)
                {
                    // var calleeCFG = DoAnalysisPhases(callee, host);
                    var disassemblerCallee = new Disassembler(callee);
                    var methodBodyCallee = disassemblerCallee.Execute();
                    callee.Body = methodBodyCallee;
                    methodBody.Inline(methodCall, callee.Body);
                }
            }

            methodBody.UpdateVariables();

            method.Body = methodBody;
        }

        #region Methods to Compute a sort of propagation of Equalities (should be moved to extensions or utils)
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


}