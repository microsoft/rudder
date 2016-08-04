using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Frontend;
using Backend;
using Microsoft.Cci;
using Microsoft.Cci.MutableCodeModel;
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


    class ScopeAnalysis : MetadataRewriter
    {

        public class NotInterestingScopeScript : Exception
        {
            public NotInterestingScopeScript(string message) : base(message) { }
        }


        IMetadataHost mhost;
        Assembly assembly;
        List<Assembly> refAssemblies;
        ITypeDefinition rowType;
        ITypeDefinition rowsetType;
        ITypeDefinition reducerType;
        ITypeDefinition processorType;
        ISourceLocationProvider sourceLocationProvider;

        List<ScopeMethodAnalysisResult> results = new List<ScopeMethodAnalysisResult>();

        public ScopeAnalysis(IMetadataHost host, Assembly assembly, List<Assembly> refAssemblies) : base(host)
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
                    if (type.FullName() == "ScopeRuntime.Reducer") reducerType = type;
                    else if (type.FullName() == "ScopeRuntime.Processor") processorType = type;
                    else if (type.FullName() == "ScopeRuntime.Row") rowType = type;
                    else if (type.FullName() == "ScopeRuntime.RowSet") rowsetType = type;
                }

                if (reducerType != null && processorType != null && rowsetType != null && rowType != null) continue;
            }

            if (reducerType == null || processorType == null || rowsetType == null || rowType == null)
                throw new NotInterestingScopeScript(String.Format("Could not load all necessary Scope types: Reducer:{0}\tProcessor:{1}\tRowSet:{2}\tRow:{3}",
                                                      reducerType != null, processorType != null, rowType != null, rowType != null));
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
            this.Rewrite(assembly.Module);
        }



        public override IMethodDefinition Rewrite(IMethodDefinition methodDefinition)
        {
            var methodResults = new ScopeMethodAnalysisResult(methodDefinition);
            methodResults.Failed = false;
            results.Add(methodResults);

            IMethodDefinition method = methodDefinition;

            Utils.WriteLine("\n--------------------------------------------------\n");
            Utils.WriteLine("Preparing method: " + method.FullName());
            try
            {
                var data = PrepareMethod(methodDefinition);
                var cfg = data.Item1;
                method = data.Item2;

                if (IsProcessor(method))
                {
                    //System.IO.File.WriteAllText(@"mbody-zvonimir.txt", _code);              
                     
                    Utils.WriteLine("Running escape analysis...");
                    var escAnalysis = new NaiveScopeMayEscapeAnalysis(cfg, method, host, rowType, rowsetType);
                    methodResults.EscapeSummary = escAnalysis.Analyze()[cfg.Exit.Id].Output;
                    methodResults.Unsupported = escAnalysis.Unsupported;
                    Utils.WriteLine(methodResults.EscapeSummary.ToString());
                    Utils.WriteLine("Done with escape analysis\n");

                    //Utils.WriteLine("Running constant propagation set analysis...");
                    //var cpsAnalysis = new ConstantPropagationSetAnalysis(cfg, method, host);
                    //methodResults.CPropagationSummary = cpsAnalysis.Analyze()[cfg.Exit.Id].Output;

                    //Utils.WriteLine(methodResults.CPropagationSummary.ToString());
                    //Utils.WriteLine("Done with constant propagation set analysis\n");

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
                Utils.WriteLine(methodDefinition.FullName() + " METHOD FAILURE: " + e.Message);
                Utils.WriteLine(e.StackTrace);
                // Our analysis failed; save this info.
                methodResults.Failed = true;
            }

            return method;
        }


        string _code = String.Empty;
        private Tuple<ControlFlowGraph, IMethodDefinition> PrepareMethod(IMethodDefinition methodDefinition)
        {
            var disassembler = new Disassembler(host, methodDefinition, sourceLocationProvider);
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

            var typeAnalysis = new TypeInferenceAnalysis(cfg);
            typeAnalysis.Analyze();

            var forwardCopyAnalysis = new ForwardCopyPropagationAnalysis(cfg);
            forwardCopyAnalysis.Analyze();
            forwardCopyAnalysis.Transform(methodBody);

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
            return new Tuple<ControlFlowGraph, IMethodDefinition>(cfg, base.Rewrite(methodDefinition));
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
            var rmtype = mtype.Resolve(host);
            // Method needs to be contained in nested Reducer or Processor subclass.
            if (!(rmtype is INestedTypeDefinition)) return false;
            var type = (rmtype as INestedTypeDefinition).ContainingTypeDefinition.Resolve(mhost);
            if (!type.SubtypeOf(reducerType) && !type.SubtypeOf(processorType)) return false;

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

        public class NaiveScopeEscapeInfo : EscapeInformation
        {
            Dictionary<Instruction, ScopeEscapeDomain> info;
            IMetadataHost host;
            ITypeDefinition rowType;
            ITypeDefinition rowsetType;

            public NaiveScopeEscapeInfo(Dictionary<Instruction, ScopeEscapeDomain> results, IMetadataHost h,
                                         ITypeDefinition rowt, ITypeDefinition rowsett)
            {
                info = results;
                host = h;

                rowType = rowt;
                rowsetType = rowsett;
            }

            public bool Escaped(Instruction instruction, IVariable var)
            {
                var domain = info[instruction];
                if (domain.IsTop) return true;

                var rtype = var.Type.Resolve(host);
                if (!NaiveScopeMayEscapeAnalysis.PossiblyRow(rtype, rowType, rowsetType, host)) return true;

                return domain.Escaped(var);
            }

            public bool Escaped(Instruction instruction, IFieldDefinition fdef)
            {
                var domain = info[instruction];
                if (domain.IsTop) return true;

                var rtype = fdef.Type.Resolve(host);
                if (!NaiveScopeMayEscapeAnalysis.PossiblyRow(rtype, rowType, rowsetType, host)) return true;

                return domain.Escaped(fdef);
            }

            public bool Escaped(Instruction instruction, IVariable array, int index)
            {
                return true;
            }

        }

    }
}
