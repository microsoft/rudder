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
using System;

namespace ScopeProgramAnalysis
{
    class SongTaoDependencyAnalysis
    {
        private Host host;
        private DataFlowAnalysisResult<PointsToGraph>[] ptAnalysisResult;
        private MethodDefinition moveNextMethod;
        private IDictionary<IVariable, IExpression> equalities;
        private MethodDefinition entryMethod;
        // private IDictionary<string,IVariable> specialFields;
        private MethodDefinition getEnumMethod;

        public SongTaoDependencyAnalysis(Host host,
                                        MethodDefinition method,
                                        MethodDefinition entryMethod,
                                        MethodDefinition getEnumMethod)
        {
            this.host = host;
            this.moveNextMethod = method;
            this.entryMethod = entryMethod;
            this.getEnumMethod = getEnumMethod;
            this.equalities = new Dictionary<IVariable, IExpression>();
        }

        public DependencyDomain AnalyzeMoveNextMethod()
        {
            var cfgEntry = entryMethod.DoAnalysisPhases(host);
            var pointsToEntry = new IteratorPointsToAnalysis(cfgEntry, this.entryMethod); // , this.specialFields);
            var entryResult = pointsToEntry.Analyze();
            var ptgOfEntry = entryResult[cfgEntry.Exit.Id].Output;

            var myIteratorResult = new LocalVariable("_temp_it") { Type = getEnumMethod.ReturnType };

            IteratorPointsToAnalysis.DoInterProcWithCallee(ptgOfEntry, new List<IVariable> { pointsToEntry.ReturnVariable}, myIteratorResult, this.getEnumMethod);

            //var specialFields = cfgEntry.ForwardOrder[1].Instructions.OfType<StoreInstruction>()
            //    .Where(st => st.Result is InstanceFieldAccess).Select(st => new KeyValuePair<string,IVariable>((st.Result as InstanceFieldAccess).FieldName,st.Operand) );
            //this.specialFields = specialFields.ToDictionary(item => item.Key, item => item.Value);


            var cfg = Program.MethodCFGCache.GetCFG(this.moveNextMethod);
            //Backend.Model.ControlFlowGraph cfg = this.moveNextMethod.DoAnalysisPhases(this.host);

            this.ptAnalysisResult = IteratorPointsToAnalysis.RunInterProcAnalysis(ptgOfEntry, new List<IVariable> { pointsToEntry.ReturnVariable }, this.moveNextMethod, cfg).Result;

            //var pointsTo = new IteratorPointsToAnalysis(cfg, this.moveNextMethod, this.specialFields);
            //this.ptAnalysisResult = pointsTo.Analyze();

            // var pointsTo = new IteratorPointsToAnalysis(cfg, this.moveNextMethod, this.specialFields, ptgOfEntry);

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

            // var dependencyAnalysis = new IteratorDependencyAnalysis(this.moveNextMethod, cfg, ptgs, this.specialFields , this.equalities);
            var dependencyAnalysis = new IteratorDependencyAnalysis(this.moveNextMethod, cfg, ptgs, this.equalities);
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