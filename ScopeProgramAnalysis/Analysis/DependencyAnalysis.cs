using System.Collections.Generic;
using Backend.Analyses;
using Backend.Model;
using Backend.Utils;
using System.Linq;
using Backend.ThreeAddressCode.Instructions;
using Backend.ThreeAddressCode.Values;
using Backend.ThreeAddressCode.Expressions;
using Microsoft.Cci;
using ScopeProgramAnalysis.Framework;
using System;
using System.Diagnostics;
using RuntimeLoader;

namespace ScopeProgramAnalysis
{

    class ScopeAnalysisConstants
    {
        public const string SCOPE_ROW_ENUMERATOR_METHOD = "System.Collections.Generic.IEnumerable<ScopeRuntime.Row>.GetEnumerator";
    }
    public enum ProtectedRowKind { Unknown, Input, Output, Json };
    public class ProtectedRowNode : ParameterNode
    {
        public ProtectedRowKind RowKind { get; private set; }

        public ProtectedRowNode(ParameterNode n, ProtectedRowKind kind) : base(n.Id, n.Parameter, n.Type)
        {
            this.RowKind = kind;
        }
        public static ProtectedRowKind GetKind(ITypeReference type)
        {
            var result = ProtectedRowKind.Unknown;
            if(type.ResolvedType.Equals(ScopeTypes.Row))
            {
                result = ProtectedRowKind.Output;
            }
            else if (type.ResolvedType.Equals(ScopeTypes.RowSet))
            {
                result = ProtectedRowKind.Input;
            }
            return result;
        }
        public override string ToString()
        {
            var kind = KindToString();
            return kind + ":" + base.ToString();
        }

        public string KindToString()
        {
            string kind = "";
            switch (RowKind)
            {
                case ProtectedRowKind.Input:
                    kind = "Input";
                    break;
                case ProtectedRowKind.Output:
                    kind = "Output";
                    break;
                default:
                    kind = "Unkown";
                    break;
            }
            return kind;
        }
        public override bool Equals(object obj)
        {
            return base.Equals(obj);
        }
        public override int GetHashCode()
        {
            return base.GetHashCode();
        }
    }

    class SongTaoDependencyAnalysis
    {
        private IMetadataHost host;
        private SimplePointsToAnalysis pointsToAnalyzer;
        private ScopeProcessorInfo processToAnalyze;
        private IDictionary<IVariable, IExpression> equalities;
        // private IDictionary<string,IVariable> specialFields;
        private InterproceduralManager interprocManager;
        private ISourceLocationProvider sourceLocationProvider;
        private MyLoader loader;
        private TraceableTable inputTable;
        private TraceableTable outputTable;

        public ISet<TraceableColumn> InputColumns { get; private set; }
        public ISet<TraceableColumn> OutputColumns { get; private set; }


        public SongTaoDependencyAnalysis(MyLoader loader,
                                        InterproceduralManager interprocManager,
                                        ScopeProcessorInfo processToAnalyze)        {
            this.interprocManager = interprocManager;
            this.host = loader.Host;
            this.loader = loader;
            this.equalities = new Dictionary<IVariable, IExpression>();
            this.processToAnalyze = processToAnalyze;
        }

        public Tuple<DependencyPTGDomain, TimeSpan> AnalyzeMoveNextMethod()
        {
            AnalysisStats.extraAnalysisOverHead = new Stopwatch();
            var sw = new Stopwatch();
            sw.Start();

			// The following steps generates a PTG for the MoveNext method that takes into account the context of invocation
			// This is the Producer entry method and the call to GetEnumerator which then is used in the Movenext method

            // 1) Analyze the entry method that creates, populates  and return the clousure 
            var cfgEntry = processToAnalyze.EntryMethod.DoAnalysisPhases(host, sourceLocationProvider);
            var pointsToEntry = new SimplePointsToAnalysis(cfgEntry, processToAnalyze.EntryMethod); 
            var entryResult = pointsToEntry.Analyze();
            var ptgOfEntry = entryResult[cfgEntry.Exit.Id].Output;

            // 2) Call the GetEnumerator that may create a new clousure and polulate it
            var myGetEnumResult = new LocalVariable("$_temp_it") { Type = processToAnalyze.GetIteratorMethod.Type };
            ptgOfEntry.Add(myGetEnumResult);
            var ptgAfterEnum = this.interprocManager.PTAInterProcAnalysis(ptgOfEntry, new List<IVariable> { pointsToEntry.ReturnVariable }, myGetEnumResult, processToAnalyze.GetIteratorMethod);

            // These are the nodes that we want to protect/analyze
            var protectedNodes = ptgOfEntry.Nodes.OfType<ParameterNode>()
                                 .Where(n => IsScopeType(n.Type)).Select(n => new ProtectedRowNode(n, ProtectedRowNode.GetKind(n.Type)));

            // Create tables representing the input and output tables
            this.inputTable = new TraceableTable(protectedNodes.Single(pn => pn.RowKind == ProtectedRowKind.Input));
            this.outputTable = new TraceableTable(protectedNodes.Single(pn => pn.RowKind == ProtectedRowKind.Output));

            // 3) I bing the current PTG with the parameters of MoveNext method on the clousure

            // Well... Inlining is broken we we added the Exceptional control graph. Let's avoid it
            //var cfg = this.moveNextMethod.DoAnalysisPhases(host, this.GetMethodsToInline());

            var cfg = this.interprocManager.GetCFG(processToAnalyze.MoveNextMethod);
            cfg.PropagateExpressions(this.equalities);
            // In general, the variable to bind is going to be pointsToEntry.ReturnVariable which is aliased with "$_temp_it" (myGetEnumResult)
            SimplePointsToGraph calleePTG = InterproceduralManager.PTABindCallerCallee(ptgAfterEnum, new List<IVariable> { myGetEnumResult }, processToAnalyze.MoveNextMethod);
            this.pointsToAnalyzer = new SimplePointsToAnalysis(cfg, processToAnalyze.MoveNextMethod, calleePTG);
                        
           
            // Now I analyze the Movenext method with the proper initialization 
            var result = this.AnalyzeScopeMethod(cfg, pointsToAnalyzer, protectedNodes);

            sw.Stop();
            //return Tuple.Create(result, sw.Elapsed - AnalysisStats.extraAnalysisOverHead.Elapsed);
            return Tuple.Create(result, sw.Elapsed);
        }

        private IFieldReference GetCurrentFieldFromClausure()
        {
            IFieldReference currentField = null;
            var clousureClass = processToAnalyze.MoveNextMethod.ContainingTypeDefinition;
            currentField = clousureClass.Fields.Where(f => f.Name.Value == @"<>2__current").SingleOrDefault();
            return currentField;
        }

        public IEnumerable<TraceableTable> GetTraceablesFromYieldReturn(DependencyPTGDomain state)
        {
            var thisVariable = state.PTG.Roots.Where(v => v.Name == "this").Single();
            var currentField = GetCurrentFieldFromClausure();
            var traceables = state.GetHeapTraceables(thisVariable, currentField).OfType<TraceableTable>();
            return traceables;
        }


        private IEnumerable<IMethodReference> GetMethodsToInline()
        {
            var pattern = "<>m__Finally";
            var methodRefs = this.processToAnalyze.MoveNextMethod.GetMethodsInvoked();
            return methodRefs.Where(m => m.Name.Value.StartsWith(pattern));
        }
        /// <summary>
        /// Analize the MoveNext method
        /// </summary>
        /// <param name="cfg"></param>
        /// <param name="pointsToAnalyzer"></param>
        /// <param name="protectedNodes"></param>
        /// <returns></returns>
        DependencyPTGDomain AnalyzeScopeMethod(ControlFlowGraph cfg, SimplePointsToAnalysis pointsToAnalyzer,
                                           IEnumerable<ProtectedRowNode> protectedNodes)
        {
            var nodeEntry = cfg.Entry.Successors.First();
            var nodeExit = cfg.Exit;
            nodeExit.Successors.Add(nodeEntry);
            nodeEntry.Predecessors.Add(nodeExit);

            var rangeAnalysis = new RangeAnalysis(cfg);
            var ranges = rangeAnalysis.Analyze();
            var exitRange = ranges[cfg.Exit.Id];
            
            var dependencyAnalysis = new IteratorDependencyAnalysis(this.processToAnalyze, this.processToAnalyze.MoveNextMethod, cfg, pointsToAnalyzer, protectedNodes ,this.equalities, this.interprocManager, rangeAnalysis);
            var resultDepAnalysis = dependencyAnalysis.Analyze();

            //dependencyAnalysis.SetPreviousResult(resultDepAnalysis);

            //resultDepAnalysis = dependencyAnalysis.Analyze();

            var node = cfg.Exit;
            //System.Console.Out.WriteLine("At {0}\nBefore {1}\nAfter {2}\n", node.Id, resultDepAnalysis[node.Id].Input, resultDepAnalysis[node.Id].Output);

            this.InputColumns = dependencyAnalysis.InputColumns;
            this.OutputColumns = dependencyAnalysis.OutputColumns;

            var result = resultDepAnalysis[node.Id].Output;

            if (!this.OutputColumns.Any())
            {
                var thisVariable = result.PTG.Roots.Where(v => v.Name == "this").Single();
                var currentTraceables = this.GetTraceablesFromYieldReturn(result).OfType<TraceableTable>();
                if (currentTraceables.Any(t => t.TableKind == ProtectedRowKind.Input))
                {
                    var inputSchema = processToAnalyze.InputSchema;

                    dependencyAnalysis.CopyRow(result,thisVariable, inputSchema, inputTable, outputTable);
                }
            }


            return result;
        }


        public static bool IsScopeType(ITypeReference type)
        {
            string[] scopeTypes = new[] { "RowList", "RowSet", "Row", "IEnumerable<Row>", "IEnumerator<Row>" };
            string[] scopeUsageTypes = new[] { "ScopeMapUsage",  "IEnumerable<ScopeMapUsage>", "IEnumerator<ScopeMapUsage>" };
            var basicType = type as INamedTypeReference;
            if (basicType == null)
            {
                return false;
            }
            if (ScopeTypes.Contains(basicType))
            {
                return true;
            }
            //if ( scopeUsageTypes.Contains(basicType.Name))
            //{
            //    return true;
            //}

            return false;
        }
				    
    }
}