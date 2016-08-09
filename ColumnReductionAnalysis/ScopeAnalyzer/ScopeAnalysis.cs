using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Frontend;
using Backend;
using Microsoft.Cci;
using Microsoft.Cci.Immutable;
using Backend.Analysis;
using Backend.Serialization;
using Backend.ThreeAddressCode;
using Backend.ThreeAddressCode.Values;
using Backend.ThreeAddressCode.Instructions;


namespace ScopeAnalyzer
{
    using Assembly = Frontend.Assembly;

    public class ScopeMethodAnalysisResult
    {
        public ScopeEscapeDomain EscapeSummary { get; set; }
        public ConstantPropagationDomain CPropagationSummary { get; set; }

        public bool Failed { get; set; }

        public bool Unsupported { get; set; }

        public IMethodDefinition Method { get; set; }

        public ScopeMethodAnalysisResult (IMethodDefinition m)
        {
            Method = m;
        } 
    }


    class ScopeAnalysis : MetadataTraverser
    {

        public class NotInterestingScopeScript : Exception
        {
            public NotInterestingScopeScript(string message) : base(message) { }
        }


        IMetadataHost mhost;
        Assembly assembly;
        List<Assembly> refAssemblies;
        List<ITypeDefinition> rowTypes = new List<ITypeDefinition>();
        List<ITypeDefinition> rowsetTypes = new List<ITypeDefinition>();
        List<ITypeDefinition> reducerTypes = new List<ITypeDefinition>();
        List<ITypeDefinition> processorTypes = new List<ITypeDefinition>();
        List<ITypeDefinition> columnTypes = new List<ITypeDefinition>();
        ISourceLocationProvider sourceLocationProvider;

        List<ScopeMethodAnalysisResult> results = new List<ScopeMethodAnalysisResult>();

        public ScopeAnalysis(IMetadataHost host, Assembly assembly, List<Assembly> refAssemblies)
        {
            this.mhost = host;
            this.assembly = assembly;
            this.refAssemblies = refAssemblies;
            sourceLocationProvider = assembly.PdbReader;

            LoadTypes();
        }

        
        /// <summary>
        /// Load all necessary Scope types. Jump out if some are missing.
        /// </summary>
        private void LoadTypes()
        {
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
                }

                if (reducerTypes.Any() && processorTypes.Any() && rowsetTypes.Any() && rowTypes.Any() && columnTypes.Any()) continue;
            }

            if (!reducerTypes.Any() || !processorTypes.Any() || !rowsetTypes.Any() || !rowTypes.Any() || !columnTypes.Any())
                throw new NotInterestingScopeScript(String.Format("Could not load all necessary Scope types: Reducer:{0}\tProcessor:{1}\tRowSet:{2}\tRow:{3}\tColumn:{4}",
                                                      reducerTypes.Count, processorTypes.Count, rowTypes.Count, rowTypes.Count, columnTypes.Count));
        }


        public IMetadataHost Host
        {
            get { return mhost; }
        }
        
        
        /// <summary>
        /// Returns results for methods that (1) failed to be analyzed due to some bug or (2)
        /// were successfully analyzed. Not interesting methods are not returned.
        /// </summary>
        public IEnumerable<ScopeMethodAnalysisResult> Results
        {
            get { return results; }
        }


        public void Analyze()
        {
            base.Traverse(assembly.Module);
        }
 

        public override void TraverseChildren(IMethodDefinition methodDefinition)
        {
            var methodResults = new ScopeMethodAnalysisResult(methodDefinition);
            methodResults.Failed = false;
            results.Add(methodResults);

            //if (!methodDefinition.FullName().Contains("___Scope_Generated_Classes___.Row_84A97FF629CF2AE9.Serializ"))
            //{
            //    return methodDefinition;
            //}

            //if (!methodDefinition.FullName().Contains("MoveNext()"))
            //{
            //    return;
            //}

            Utils.WriteLine("\n--------------------------------------------------\n");
            Utils.WriteLine("Preparing method: " + methodDefinition.FullName());

            try
            {
                var cfg = PrepareMethod(methodDefinition);

                if (IsProcessor(methodDefinition))
                {
                    //System.IO.File.WriteAllText(@"mbody-zvonimir.txt", _code);              

                    var escAnalysis = DoEscapeAnalysis(cfg, methodDefinition, methodResults);
                    var cspAnalysis = DoConstantPropagationAnalysis(cfg, methodDefinition, methodResults);

                    var escInfo = new NaiveScopeEscapeInfoProvider(escAnalysis.PostResults, mhost, rowTypes, rowsetTypes);
                    var cspInfo = new NaiveScopeConstantsProvider(cspAnalysis.PreResults, mhost);


                    Utils.WriteLine("Running used columns anaysis...");
                    var clsAnalysis = new UsedColumnsAnalysis(mhost, cfg, escInfo, cspInfo, rowTypes, columnTypes);
                    var outcome = clsAnalysis.Analyze();
                    Utils.WriteLine("Used columns results:\n" + outcome.ToString());

                    Utils.WriteLine("Method has unsupported features: " + escAnalysis.Unsupported);
                } 
                else
                {
                    Utils.WriteLine("Not an interesting method.");
                    // Not an interesting method, so we don't need to keep her results.
                    results.Remove(methodResults);
                }
            }
            catch (ScopeAnalysis.NotInterestingScopeScript e)
            {
                Utils.WriteLine("METHOD WARNING: " + e.Message);
                // Not an interesting method, so we don't need to keep her results.
                results.Remove(methodResults);
            }
            catch (Exception e)
            {
                Utils.WriteLine("METHOD FAILURE: " + e.Message);
                Utils.WriteLine(e.StackTrace);
                // Our analysis failed; save this info.
                methodResults.Failed = true;
            }

            return;
        }


        private NaiveScopeMayEscapeAnalysis DoEscapeAnalysis(ControlFlowGraph cfg, IMethodDefinition method, ScopeMethodAnalysisResult results)
        {
            Utils.WriteLine("Running escape analysis...");
            var escAnalysis = new NaiveScopeMayEscapeAnalysis(cfg, method, mhost, rowTypes, rowsetTypes);
            results.EscapeSummary = escAnalysis.Analyze()[cfg.Exit.Id].Output;
            results.Unsupported = escAnalysis.Unsupported;
            Utils.WriteLine(results.EscapeSummary.ToString());
            Utils.WriteLine("Done with escape analysis\n");
            return escAnalysis;
        }

        private ConstantPropagationSetAnalysis DoConstantPropagationAnalysis(ControlFlowGraph cfg, IMethodDefinition method, ScopeMethodAnalysisResult results)
        {
            Utils.WriteLine("Running constant propagation set analysis...");
            var cpsAnalysis = new ConstantPropagationSetAnalysis(cfg, method, mhost);
            results.CPropagationSummary = cpsAnalysis.Analyze()[cfg.Exit.Id].Output;

            Utils.WriteLine(results.CPropagationSummary.ToString());
            Utils.WriteLine("Done with constant propagation set analysis\n");
            return cpsAnalysis;
        }





        string _code = String.Empty;
        private ControlFlowGraph PrepareMethod(IMethodDefinition methodDefinition)
        {
            var disassembler = new Disassembler(mhost, methodDefinition, sourceLocationProvider);
            var methodBody = disassembler.Execute();

            var cfg = ControlFlowGraph.GenerateNormalControlFlow(methodBody);
            ControlFlowGraph.ComputeDominators(cfg);
            ControlFlowGraph.IdentifyLoops(cfg);

            ControlFlowGraph.ComputeDominatorTree(cfg);
            ControlFlowGraph.ComputeDominanceFrontiers(cfg);

            var splitter = new WebAnalysis(cfg);
            splitter.Analyze();
            splitter.Transform();

            methodBody.UpdateVariables();
            //System.IO.File.WriteAllText(@"mbody-zvonimir.txt", methodBody.ToString());
            var typeAnalysis = new TypeInferenceAnalysis(cfg);
            typeAnalysis.Analyze();

            //var forwardCopyAnalysis = new ForwardCopyPropagationAnalysis(cfg);
            //forwardCopyAnalysis.Analyze();
            //forwardCopyAnalysis.Transform(methodBody);

            var backwardCopyAnalysis = new BackwardCopyPropagationAnalysis(cfg);
            backwardCopyAnalysis.Analyze();
            backwardCopyAnalysis.Transform(methodBody);

            //var pointsTo = new PointsToAnalysis(cfg);
            //var result = pointsTo.Analyze();

            var lva = new LiveVariablesAnalysis(cfg);
            lva.Analyze();

            var ssa = new StaticSingleAssignmentAnalysis(methodBody, cfg);
            ssa.Transform();
            ssa.Prune(lva);

            methodBody.UpdateVariables();

            //var dot = DOTSerializer.Serialize(cfg);
            //var dgml = DGMLSerializer.Serialize(cfg);
            _code = methodBody.ToString();
            //return new Tuple<ControlFlowGraph, IMethodDefinition>(cfg, methodBody);
            return cfg;
        }

    

        /// <summary>
        /// Check if a method is a processor (reducer) method.
        /// </summary>
        /// <param name="method"></param>
        /// <returns></returns>
        private bool IsProcessor(IMethodDefinition method)
        {
            // Bunch of checks to make sure that the method we are analyzing is suitable for our analysis.

            // Containing type of a method needs to be a non-anonymous reference type.
            if (!(method.ContainingType is INamedTypeReference)) return false;
            var mtype = method.ContainingType as INamedTypeReference;
            if (mtype.IsEnum || mtype.IsValueType) return false;

            // Its definition must be available.
            var rmtype = mtype.Resolve(mhost);
            // Method needs to be contained in nested Reducer or Processor subclass.
            if (!(rmtype is INestedTypeDefinition)) return false;
            var type = (rmtype as INestedTypeDefinition).ContainingTypeDefinition.Resolve(mhost);
            if (reducerTypes.All(rt => !type.SubtypeOf(rt)) && processorTypes.All(pt => !type.SubtypeOf(pt))) return false;
            // We are currently focusing on MoveNext methods only.
            if (!method.FullName().EndsWith(".MoveNext()")) return false;
           
            // This is just a sanity check. TODO: too specific? What if compiler changes?
            var name = method.ContainingType.Name();           
            if (!name.StartsWith("<Reduce>") && !name.StartsWith("<Process>")) return false;

            //CVS specific, only for initial developement.
            //var fname = method.ContainingType.FullName();
            //if (!fname.Contains("CVBase")) return false;

            return true;
        }

        private class NaiveScopeEscapeInfoProvider : EscapeInfoProvider
        {
            Dictionary<Instruction, ScopeEscapeDomain> info;
            IMetadataHost host;
            List<ITypeDefinition> rowType;
            List<ITypeDefinition> rowsetType;

            public NaiveScopeEscapeInfoProvider(Dictionary<Instruction, ScopeEscapeDomain> results, IMetadataHost h,
                                         List<ITypeDefinition> rowt, List<ITypeDefinition> rowsett)
            {
                info = results;
                host = h;

                rowType = rowt;
                rowsetType = rowsett;
            }

            public bool Escaped(Instruction instruction, IVariable var)
            {
                var rtype = var.Type.Resolve(host);
                if (!NaiveScopeMayEscapeAnalysis.PossiblyRow(rtype, rowType, rowsetType, host)) return true;

                var domain = info[instruction];
                if (domain.IsTop) return true;
                return domain.Escaped(var);
            }

            public bool Escaped(Instruction instruction, IFieldReference field)
            {
                var rtype = field.Type.Resolve(host);
                if (!NaiveScopeMayEscapeAnalysis.PossiblyRow(rtype, rowType, rowsetType, host)) return true;

                var domain = info[instruction];
                if (domain.IsTop) return true;
                return domain.Escaped(field);
            }

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

            public IEnumerable<Constant> GetConstants(Instruction instruction, IFieldReference field)
            {
                if (!ConstantPropagationSetAnalysis.IsConstantType(field.Type, host)) return null;

                var domain = info[instruction];
                var fieldDomain = domain.Constants(field);
                if (fieldDomain.IsTop) return null;
                if (fieldDomain.IsBottom) return new HashSet<Constant>();

                return new HashSet<Constant>(fieldDomain.Elements);
            }

            public IEnumerable<Constant> GetConstants(Instruction instruction, IVariable array, int index)
            {
                return null;
            }

        }

    }
}
