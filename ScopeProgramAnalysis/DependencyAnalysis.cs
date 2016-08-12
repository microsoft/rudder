using System.Collections.Generic;
using Model;
using Model.Types;
using Backend.Analyses;
using Backend.Serialization;
using Backend.Model;
using Backend.Utils;
using Model.ThreeAddressCode.Instructions;
using Model.ThreeAddressCode.Values;
using Model.ThreeAddressCode.Expressions;

namespace ScopeProgramAnalysis
{

    class ScopeAnalysisConstants
    {
        public const string SCOPE_ROW_ENUMERATOR_METHOD = "System.Collections.Generic.IEnumerable<ScopeRuntime.Row>.GetEnumerator";
    }

    class SongTaoDependencyAnalysis
    {
        private Host host;
        private DataFlowAnalysisResult<PointsToGraph>[] ptAnalysisResult;
        private MethodDefinition moveNextMethod;
        private IDictionary<IVariable, IExpression> equalities;
        private MethodDefinition entryMethod;
        // private IDictionary<string,IVariable> specialFields;
        private MethodDefinition getEnumMethod;
        private InterproceduralManager interprocManager;

        public SongTaoDependencyAnalysis(Host host,
                                        InterproceduralManager interprocManager,
                                        MethodDefinition method,
                                        MethodDefinition entryMethod,
                                        MethodDefinition getEnumMethod)
        {
            this.interprocManager = interprocManager;
            this.host = host;
            this.moveNextMethod = method;
            this.entryMethod = entryMethod;
            this.getEnumMethod = getEnumMethod;
            this.equalities = new Dictionary<IVariable, IExpression>();
        }

        public DependencyDomain AnalyzeMoveNextMethod()
        {
            // 1) Analyze the entry method that creates, populates  and return the clousure 
            var cfgEntry = entryMethod.DoAnalysisPhases(host);
            var pointsToEntry = new IteratorPointsToAnalysis(cfgEntry, this.entryMethod); // , this.specialFields);
            var entryResult = pointsToEntry.Analyze();
            var ptgOfEntry = entryResult[cfgEntry.Exit.Id].Output;


            // 2) Call the GetEnumerator that may create a new clousure and polulate it
            var myGetEnumResult = new LocalVariable("$_temp_it") { Type = getEnumMethod.ReturnType };
            ptgOfEntry.Add(myGetEnumResult);
            var ptgAfterEnum = this.interprocManager.PTADoInterProcWithCallee(ptgOfEntry, new List<IVariable> { pointsToEntry.ReturnVariable }, myGetEnumResult, this.getEnumMethod);

            //var specialFields = cfgEntry.ForwardOrder[1].Instructions.OfType<StoreInstruction>()
            //    .Where(st => st.Result is InstanceFieldAccess).Select(st => new KeyValuePair<string,IVariable>((st.Result as InstanceFieldAccess).FieldName,st.Operand) );
            //this.specialFields = specialFields.ToDictionary(item => item.Key, item => item.Value);


            /// Now do MoveNext on the clousure
            var cfg = this.interprocManager.GetCFG(this.moveNextMethod);
            // In general, the variable to bind is going to be pointsToEntry.ReturnVariable which is aliased with "$_temp_it" (myGetEnumResult)
            this.ptAnalysisResult = this.interprocManager.PTABindAndRunInterProcAnalysis(ptgAfterEnum, new List<IVariable> { myGetEnumResult }, this.moveNextMethod, cfg).Result;

            //var pointsTo = new IteratorPointsToAnalysis(cfg, this.moveNextMethod, this.specialFields);
            //this.ptAnalysisResult = pointsTo.Analyze();

            // var pointsTo = new IteratorPointsToAnalysis(cfg, this.moveNextMethod, this.specialFields, ptgOfEntry);

            PropagateExpressions(cfg, this.equalities);
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
            var dependencyAnalysis = new IteratorDependencyAnalysis(this.moveNextMethod, cfg, ptgs, this.equalities, this.interprocManager);
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
        public static void PropagateExpressions(ControlFlowGraph cfg, IDictionary<IVariable, IExpression> equalities)
        {
            foreach (var node in cfg.ForwardOrder)
            {
                PropagateExpressions(node, equalities);
            }
        }

        private static void PropagateExpressions(CFGNode node, IDictionary<IVariable, IExpression> equalities)
        {
            foreach (var instruction in node.Instructions)
            {
                PropagateExpressions(instruction, equalities);
            }
        }

        private static void PropagateExpressions(IInstruction instruction, IDictionary<IVariable, IExpression> equalities)
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