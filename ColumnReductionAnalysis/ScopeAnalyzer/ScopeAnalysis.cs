using System;
using System.Collections.Generic;
using System.Linq;
using Backend;
using Microsoft.Cci;
using Backend.Analysis;
using Backend.ThreeAddressCode.Values;
using Backend.ThreeAddressCode.Instructions;
using ScopeAnalyzer.Analyses;
using ScopeAnalyzer.Interfaces;
using ScopeAnalyzer.Misc;


namespace ScopeAnalyzer
{
    using Assembly = Frontend.Assembly;

    /// <summary>
    /// Class that simply bundles together relevant information about
    /// a method and results of applying ScopeAnalysis on it.
    /// </summary>
    public class ScopeMethodAnalysisResult
    {
        public ScopeEscapeDomain EscapeSummary { get; set; }
        public ConstantPropagationDomain CPropagationSummary { get; set; }
        public ColumnsDomain UsedColumnsSummary { get; set; }

        public ITypeReference ProcessorType
        {
            get { return (Method.ContainingType.Resolve(Host) as INestedTypeDefinition).ContainingTypeDefinition.Resolve(Host); }
        }

        /// <summary>
        /// Number of times a Row column has been accessed by a string name.
        /// </summary>
        public int ColumnStringAccesses { get; set; }

        /// <summary>
        /// Number of times a Row column has been accessed by an integer index.
        /// </summary>
        public int ColumnIndexAccesses { get; set; }


        public bool Failed { get; set; }

        /// <summary>
        /// Tells whether the corresponding methods was considered
        /// relevant for ScopeAnalysis
        /// </summary>
        public bool Interesting { get; set; }

        public bool Unsupported { get; set; }

        public IMethodDefinition Method { get; }

        public IMetadataHost Host { get; }

        public ScopeMethodAnalysisResult (IMethodDefinition m, IMetadataHost h)
        {
            Method = m;
            Host = h;

            Failed = false;
            Interesting = true;
            Unsupported = false;
        } 
    }


    /// <summary>
    /// Main class that performs analysis on Scope processor methods in a given assembly.
    /// The analysis overapproximates the exact columns accessed in a processor method.
    /// </summary>
    public class ScopeAnalysis : MetadataTraverser
    {

        public class MissingScopeMetadataException : Exception
        {
            public MissingScopeMetadataException(string message) : base(message) { }
        }


        IMetadataHost mhost;
        Assembly assembly;
        // We need reference assemblies to get necessary type definitions.
        IEnumerable<Assembly> refAssemblies;

        // We keep track of all possible definitions of Scope types, for soundness reasons.
        List<ITypeDefinition> rowTypes = new List<ITypeDefinition>();
        List<ITypeDefinition> rowsetTypes = new List<ITypeDefinition>();
        List<ITypeDefinition> reducerTypes = new List<ITypeDefinition>();
        List<ITypeDefinition> processorTypes = new List<ITypeDefinition>();
        List<ITypeDefinition> combinerTypes = new List<ITypeDefinition>();
        List<ITypeDefinition> columnTypes = new List<ITypeDefinition>();
        List<ITypeDefinition> schemaTypes = new List<ITypeDefinition>();
        ISourceLocationProvider sourceLocationProvider;

        List<ScopeMethodAnalysisResult> results = new List<ScopeMethodAnalysisResult>();

        // User of the ScopeAnalysis can provide (in constructor) names of the processor methods of interest using this structure.
        // If this structure is null, the analysis will analyze all processors, otherwise it will analyze only ones listed here.
        IEnumerable<string> interestingProcessors;



        public ScopeAnalysis(IMetadataHost host, Assembly assembly, IEnumerable<Assembly> refAssemblies, IEnumerable<string> ips)
        {
            this.mhost = host;
            this.assembly = assembly;
            this.refAssemblies = refAssemblies;
            sourceLocationProvider = assembly.PdbReader;
            interestingProcessors = ips;

            if (interestingProcessors == null)
            {
                Utils.WriteLine("Interesting processors list not provided, continuing without it.");
            }

            LoadTypes();
        }


        public IMetadataHost Host
        {
            get { return mhost; }
        }

        /// <summary>
        /// Returns analysis results for every method in the assembly.
        /// </summary>
        public IEnumerable<ScopeMethodAnalysisResult> Results
        {
            get { return results; }
        }




        /// <summary>
        /// Load all necessary Scope types. Jump out abruptly if some are missing.
        /// </summary>
        private void LoadTypes()
        {
            // Look for Scope types in the main assembley, but also in the reference assemblies.
            var asms = new HashSet<Assembly>(refAssemblies); asms.Add(assembly);
            foreach (var asm in asms)
            {
                var allTypes = asm.Module.GetAllTypes();
                foreach (var type in allTypes)
                {
                    if (type.FullName() == "ScopeRuntime.Reducer") reducerTypes.Add(type);
                    else if (type.FullName() == "ScopeRuntime.Processor") processorTypes.Add(type);
                    else if (type.FullName() == "ScopeRuntime.Row") rowTypes.Add(type);
                    else if (type.FullName() == "ScopeRuntime.RowSet") rowsetTypes.Add(type);
                    else if (type.FullName() == "ScopeRuntime.ColumnData") columnTypes.Add(type);
                    else if (type.FullName() == "ScopeRuntime.Schema") schemaTypes.Add(type);
                    else if (type.FullName() == "ScopeRuntime.Combiner") combinerTypes.Add(type);
                }
            }

            if (!reducerTypes.Any() || !processorTypes.Any() || !rowsetTypes.Any() || 
                !rowTypes.Any() || !columnTypes.Any() || !schemaTypes.Any() || !combinerTypes.Any())
                throw new MissingScopeMetadataException(
                    String.Format("Could not load all necessary Scope types: Reducer:{0}\tProcessor:{1}\tRowSet:{2}\tRow:{3}\tColumn:{4}\tSchema:{5}\tCombiner:{6}",
                        reducerTypes.Count, processorTypes.Count, rowTypes.Count, rowTypes.Count, columnTypes.Count, schemaTypes.Count, combinerTypes.Count));
        }

        /// <summary>
        /// Entry point method for calling analyses for every method in the main assembly.
        /// </summary>
        public void Analyze()
        {
            base.Traverse(assembly.Module);
        }
 

        /// <summary>
        /// Main method for performing Scope analysis on a method.
        /// </summary>
        /// <param name="methodDefinition"></param>
        public override void TraverseChildren(IMethodDefinition methodDefinition)
        {
            //if (!methodDefinition.FullName().Contains("MMRV2.IndexSelection.DPGenDomainKeyProcessor"))
            //    return;

            //if (!methodDefinition.FullName().Contains("ScopeML.Prediction.CompactModelBuilderReducer") || !methodDefinition.FullName().Contains("MoveNext"))
            //    return;

            var methodResult = new ScopeMethodAnalysisResult(methodDefinition, mhost);
            try
            {
                // We analyze only (interesting) processor methods.
                if (IsInterestingProcessor(methodDefinition))
                {
                    Utils.WriteLine("\n--------------------------------------------------\n");
                    Utils.WriteLine("Found interesting method " + methodDefinition.FullName());

                    // Create CFG and run basic analyses, such as copy-propagation.
                    var cfg = PrepareMethod(methodDefinition);
                    Utils.WriteLine("CFG size " + cfg.Nodes.Count);
                    //System.IO.File.WriteAllText(@"mbody-zvonimir.txt", _code);
                      
                    // Run escape analysis.              
                    var escAnalysis = DoEscapeAnalysis(cfg, methodDefinition, methodResult);
                    methodResult.Unsupported = escAnalysis.Unsupported;

                    // If some row has escaped or the method has unsupported features,
                    // there is no need to analyze the method any further.
                    if (escAnalysis.InterestingRowEscaped || escAnalysis.Unsupported)
                    {
                        if (escAnalysis.InterestingRowEscaped && !escAnalysis.Unsupported) 
                            Utils.WriteLine("A rowish data structure has escaped, no dependency information available.");
                        methodResult.UsedColumnsSummary = ColumnsDomain.Top;                     
                    }
                    else
                    {
                        // Otherwise, do constant-set propagation (CSP) analysis.
                        var cspAnalysis = DoConstantPropagationAnalysis(cfg, methodDefinition, methodResult);
                        var cspInfo = new NaiveScopeConstantsProvider(cspAnalysis.PreResults, mhost);

                        //Finally, do the actual used-columns analysis using results of the previous CSP analysis.
                        var clsAnalysis = DoUsedColumnsAnalysis(cfg, cspInfo, methodResult);
                        methodResult.Unsupported = cspAnalysis.Unsupported | clsAnalysis.Unsupported;
                    }
                  
                    Utils.WriteLine("Method has useful result: " + (!methodResult.UsedColumnsSummary.IsBottom && !methodResult.UsedColumnsSummary.IsTop));
                    Utils.WriteLine("Method has unsupported features: " + methodResult.Unsupported);
                    Utils.WriteLine("\n--------------------------------------------------\n");                  
                } 
                else
                {
                    methodResult.Interesting = false;
                }
            }
            catch (Exception e)
            {
                Utils.WriteLine(String.Format("{0} METHOD FAILURE: {1}", methodDefinition.FullName(), e.Message));
                Utils.WriteLine(e.StackTrace);
                methodResult.Failed = true;
            }

            results.Add(methodResult);
            return;
        }


        /// <summary>
        /// Entry point method for performing escape analysis.
        /// </summary>
        /// <param name="cfg"></param>
        /// <param name="method"></param>
        /// <param name="results"></param>
        /// <returns></returns>
        private NaiveScopeMayEscapeAnalysis DoEscapeAnalysis(ControlFlowGraph cfg, IMethodDefinition method, ScopeMethodAnalysisResult results)
        {
            Utils.WriteLine("Running escape analysis...");
            var escAnalysis = new NaiveScopeMayEscapeAnalysis(cfg, method, mhost, rowTypes, rowsetTypes);
            results.EscapeSummary = escAnalysis.Analyze()[cfg.Exit.Id].Output;
            //Utils.WriteLine(results.EscapeSummary.ToString());
            Utils.WriteLine("Something escaped: " + escAnalysis.InterestingRowEscaped);
            Utils.WriteLine("Done with escape analysis\n");          
            return escAnalysis;
        }

        /// <summary>
        /// Entry point method for performing constant-set propagation analysis.
        /// </summary>
        /// <param name="cfg"></param>
        /// <param name="method"></param>
        /// <param name="results"></param>
        /// <returns></returns>
        private ConstantPropagationSetAnalysis DoConstantPropagationAnalysis(ControlFlowGraph cfg, IMethodDefinition method, ScopeMethodAnalysisResult results)
        {
            Utils.WriteLine("Running constant propagation set analysis...");
            var cpsAnalysis = new ConstantPropagationSetAnalysis(cfg, method, mhost, schemaTypes);
            results.CPropagationSummary = cpsAnalysis.Analyze()[cfg.Exit.Id].Output;
            //Utils.WriteLine(results.CPropagationSummary.ToString());
            Utils.WriteLine("Done with constant propagation set analysis\n");
            return cpsAnalysis;
        }

        /// <summary>
        /// Entry point method for performing used-columns analysis. The analysis expects results of
        /// constant-set propagation analysis.
        /// </summary>
        /// <param name="cfg"></param>
        /// <param name="cspInfo"></param>
        /// <param name="results"></param>
        /// <returns></returns>
        private UsedColumnsAnalysis DoUsedColumnsAnalysis(ControlFlowGraph cfg, ConstantsInfoProvider cspInfo, ScopeMethodAnalysisResult results)
        {
            Utils.WriteLine("Running used columns analysis...");         
            var clsAnalysis = new UsedColumnsAnalysis(mhost, cfg, cspInfo, rowTypes, columnTypes);
            var outcome = clsAnalysis.Analyze();      
            results.UsedColumnsSummary = outcome;

            // We only save statistics about column accesses for methods with useful results.
            if (!results.UsedColumnsSummary.IsTop && !results.UsedColumnsSummary.IsBottom)
            {
                results.ColumnStringAccesses += clsAnalysis.ColumnStringAccesses;
                results.ColumnIndexAccesses += clsAnalysis.ColumnIndexAccesses;
            }

            Utils.WriteLine(results.UsedColumnsSummary.ToString());
            Utils.WriteLine("Done with used columns analysis\n");
            return clsAnalysis;
        }



        //string _code = String.Empty;

        /// <summary>
        /// For a given methodDefinition, create a CFG and run basic analyses such as
        /// stack removal, SSA transformation, live-variables analysis, and copy-propagation.
        /// </summary>
        /// <param name="methodDefinition"></param>
        /// <returns></returns>
        private ControlFlowGraph PrepareMethod(IMethodDefinition methodDefinition)
        {
            var disassembler = new Disassembler(mhost, methodDefinition, sourceLocationProvider);
            var methodBody = disassembler.Execute();

            var cfg = ControlFlowGraph.GenerateNormalControlFlow(methodBody);
            ControlFlowGraph.ComputeDominators(cfg);
            ControlFlowGraph.IdentifyLoops(cfg);

            ControlFlowGraph.ComputeDominatorTree(cfg);
            ControlFlowGraph.ComputeDominanceFrontiers(cfg);

            // Uniquely rename stack variables.
            var splitter = new WebAnalysis(cfg);
            splitter.Analyze();
            splitter.Transform();

            methodBody.UpdateVariables();

            // Infer types for stack variables.
            var typeAnalysis = new TypeInferenceAnalysis(cfg);
            typeAnalysis.Analyze();

            var backwardCopyAnalysis = new BackwardCopyPropagationAnalysis(cfg);
            backwardCopyAnalysis.Analyze();
            backwardCopyAnalysis.Transform(methodBody);

            var lva = new LiveVariablesAnalysis(cfg);
            lva.Analyze();

            var ssa = new StaticSingleAssignmentAnalysis(methodBody, cfg);
            ssa.Transform();
            ssa.Prune(lva);

            methodBody.UpdateVariables();

            //_code = methodBody.ToString();
            return cfg;
        }

        /// <summary>
        /// Check if a method is a processor (reducer) method. Also, include the check
        /// for method being declared interesting by the class user.
        /// </summary>
        /// <param name="method"></param>
        /// <returns></returns>
        private bool IsInterestingProcessor(IMethodDefinition method)
        {
            // Containing type of a method needs to be a non-anonymous reference type.
            if (!(method.ContainingType is INamedTypeReference))
                return false;
            var mtype = method.ContainingType as INamedTypeReference;
            if (mtype.IsEnum || mtype.IsValueType)
                return false;

            // Its definition must be available.
            var rmtype = mtype.Resolve(mhost);
            // Method needs to be contained in nested Reducer or Processor subclass.
            if (!(rmtype is INestedTypeDefinition))
                return false;
            var type = (rmtype as INestedTypeDefinition).ContainingTypeDefinition.Resolve(mhost);

            //TODO: push this check up later. Here currently to test CCI types for robustness.
            if (interestingProcessors != null && !interestingProcessors.Contains(type.FullName()))
                return false;
            
            // We are interested in processors, reducers, and combiners.
            if (reducerTypes.All(rt => !type.SubtypeOf(rt, mhost)) && 
                processorTypes.All(pt => !type.SubtypeOf(pt, mhost)) &&
                combinerTypes.All(ct => !type.SubtypeOf(ct, mhost)))
                return false;

            // We are currently focusing on MoveNext methods only.
            if (!method.FullName().EndsWith(".MoveNext()"))
                return false;

            return true;
        }


        /*
         * The following two classes are simple implementations of escape and constant propagation
         * results interfaces, using the results of the actual analysis used by ScopeAnalysis.
         */

        private class NaiveScopeEscapeInfoProvider : EscapeInfoProvider
        {
            Dictionary<Instruction, ScopeEscapeDomain> info;
            NaiveScopeMayEscapeAnalysis analysis;

            public NaiveScopeEscapeInfoProvider(NaiveScopeMayEscapeAnalysis ans)
            {
                analysis = ans;
                info = analysis.PostResults;           
            }

            public bool Escaped(Instruction instruction, IVariable var)
            {
                if (!analysis.PossiblyRow(var.Type)) return true;

                var domain = info[instruction];
                if (domain.IsTop) return true;
                return domain.Escaped(var);
            }

            public bool Escaped(Instruction instruction, IFieldAccess field)
            {
                if (!analysis.PossiblyRow(field.Type)) return true;

                var domain = info[instruction];
                if (domain.IsTop) return true;
                return domain.Escaped(field);
            }

            // We currently don't have any escape analysis for array accesses.
            public bool Escaped(Instruction instruction, IVariable array, int index)
            {
                return true;
            }
        }

        private class NaiveScopeConstantsProvider : ConstantsInfoProvider
        {
            Dictionary<Instruction, ConstantPropagationDomain> info;
            IMetadataHost host;

            public NaiveScopeConstantsProvider(Dictionary<Instruction, ConstantPropagationDomain> results, IMetadataHost h)
            {
                info = results;
                host = h;
            }

            public IEnumerable<Constant> GetConstants(Instruction instruction, IVariable var)
            {
                if (!ConstantPropagationSetAnalysis.IsConstantType(var.Type, host)) return null;

                var domain = info[instruction];
                var varDomain = domain.Constants(var);
                if (varDomain.IsTop) return null;
                if (varDomain.IsBottom) return new HashSet<Constant>();

                return new HashSet<Constant>(varDomain.Elements);
            }

            public IEnumerable<Constant> GetConstants(Instruction instruction, IFieldAccess field)
            {
                if (!ConstantPropagationSetAnalysis.IsConstantType(field.Type, host)) return null;

                var domain = info[instruction];
                var fieldDomain = domain.Constants(field);
                if (fieldDomain.IsTop) return null;
                if (fieldDomain.IsBottom) return new HashSet<Constant>();

                return new HashSet<Constant>(fieldDomain.Elements);
            }

            // We currently don't have any constant-set propagation analysis for array accesses.
            public IEnumerable<Constant> GetConstants(Instruction instruction, IVariable array, int index)
            {
                return null;
            }
        }

    }
}
