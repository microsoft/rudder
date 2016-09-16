using Backend.Utils;
using CCIProvider;
using Microsoft.CodeAnalysis.Sarif;
using Microsoft.CodeAnalysis.Sarif.Readers;
using Model;
using Model.Types;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using Backend.Analyses;
using System.Globalization;
using ScopeProgramAnalysis.Framework;
using System.Text.RegularExpressions;

namespace ScopeProgramAnalysis
{
    public class ScopeProgramAnalysis
    {
        private Host host;
        private IDictionary<string, ClassDefinition> factoryReducerMap;
        private Loader loader;
        private InterproceduralManager interprocAnalysisManager;
        public static Schema InputSchema;
        public static Schema OutputSchema;

        public IEnumerable<string> ReferenceFiles { get; private set; }
        public HashSet<string> ClassFilters { get; private set; }
        public HashSet<string> EntryMethods { get; private set; }
        public HashSet<string> ClousureFilters { get; private set; }
        public string MethodUnderAnalysisName { get; private set; }

        private Regex[] compilerGeneretedMethodMatchers = new Regex[]
            {
                    new Regex(@"^___Scope_Generated_Classes___.ScopeFilterTransformer_\d+$", RegexOptions.Compiled),
                    new Regex(@"^___Scope_Generated_Classes___.ScopeGrouper_\d+$", RegexOptions.Compiled),
                    new Regex(@"^___Scope_Generated_Classes___.ScopeProcessorCrossApplyExpressionWrapper_\d+$", RegexOptions.Compiled),
                    new Regex(@"^___Scope_Generated_Classes___.ScopeOptimizedClass_\d+$", RegexOptions.Compiled),
                    new Regex(@"^___Scope_Generated_Classes___.ScopeTransformer_\d+$", RegexOptions.Compiled),
                    new Regex(@"^___Scope_Generated_Classes___.ScopeGrouper_\d+$", RegexOptions.Compiled),
                    new Regex(@"^___Scope_Generated_Classes___.ScopeProcessorCrossApplyExpressionWrapper_\d+$", RegexOptions.Compiled),
                    new Regex(@"^___Scope_Generated_Classes___.ScopeReducer__\d+$", RegexOptions.Compiled)
                    // ScopeRuntime.
            };

        public ScopeProgramAnalysis(Host host, Loader loader)
        {
            this.host = host;
            this.loader = loader;
            this.factoryReducerMap = new Dictionary<string, ClassDefinition>();
            this.interprocAnalysisManager = new InterproceduralManager(host);
        }

        static void Main(string[] args)
        {
            var useScopeFactory = true;
            var scopeKind = ScopeMethodKind.All;

            string input;

            //const string root = @"c:\users\t-diga\source\repos\scopeexamples\metting\";
            //const string input = root + @"__ScopeCodeGen__.dll";
            //scopeKind = ScopeMethodKind.Reducer;
            //const string input = @"D:\MadanExamples\3213e974-d0b7-4825-9fd4-6068890d3327\__ScopeCodeGen__.dll";

            // Mike example: FileChunker
            //const string input = @"C:\Users\t-diga\Source\Repos\ScopeExamples\ExampleWithXML\69FDA6E7DB709175\ScopeMapAccess_4D88E34D25958F3B\__ScopeCodeGen__.dll";
            //const string input = @"\\research\root\public\mbarnett\Parasail\ExampleWithXML\69FDA6E7DB709175\ScopeMapAccess_4D88E34D25958F3B\__ScopeCodeGen__.dll";
            //const string input = @"C:\Users\t-diga\Source\Repos\ScopeExamples\\ExampleWithXML\ILAnalyzer.exe";
            //useScopeFactory = false;

            // const string input = @"D:\MadanExamples\13c04344-e910-4828-8eae-bc49925b4c9b\__ScopeCodeGen__.dll";
            //const string input = @"D:\MadanExamples\15444206-b209-437e-b23b-2d916f18cd35\__ScopeCodeGen__.dll";
            // const string input = @"D:\MadanExamples\208afef3-4cae-428c-a7a2-75ea7350b1ea\__ScopeCodeGen__.dll";
            //const string input = @"D:\MadanExamples\9e5dad20-19f4-4a4d-8b95-319fd2e047f8\__ScopeCodeGen__.dll";
            //const string input = @"D:\MadanExamples\0f0d828a-5a11-4750-83e6-4e2294c51e5a\__ScopeCodeGen__.dll";

            // This one gave me out of memory...
            //const string input = @"D:\MadanExamples\901f84e4-be76-49fe-8fc1-5508a8b561a6\__ScopeCodeGen__.dll";

            //const string input = @"\\madanm2\parasail2\TFS\parasail\ScopeSurvey\AutoDownloader\bin\Debug\03374553-725d-4d47-9400-90a9d168a658\__ScopeCodeGen__.dll";

            //const string input = @"D:\MadanExamples\3d2b4d2c-42b4-45c3-be19-71c1266ae835\__ScopeCodeGen__.dll";
            // const string input  = @" D:\MadanExamples\0061a95f-fbe7-4b0d-9878-c7fea686bec6\__ScopeCodeGen__.dll";
            // const string input = @"D:\MadanExamples\01c085ee-9e42-418d-b0e8-a94ee1a0d76b\__ScopeCodeGen__.dll";
            // const string input = @"\\madanm2\parasail2\TFS\parasail\ScopeSurvey\AutoDownloader\bin\Debug\49208328-24d1-42fb-8fa4-f74ba84760d3\__ScopeCodeGen__.dll";
            //const string input = @"\\madanm2\parasail2\TFS\parasail\ScopeSurvey\AutoDownloader\bin\Debug\8aecff28-5719-4b34-9f9f-cb3135df67d4\__ScopeCodeGen__.dll";
            //const string input = @"\\madanm2\parasail2\TFS\parasail\ScopeSurvey\AutoDownloader\bin\Debug\018c2f92-f63d-4790-a843-40a1b0e0e58a\__ScopeCodeGen__.dll";

            // From Zvonimir's PDF summary:
            const string zvonimirDirectory = @"\\research\root\public\mbarnett\Parasail\InterestingScopeProjects";
            //const string zvonimirDirectory = @"const string input = @"\\madanm2\parasail2\TFS\parasail\ScopeSurvey\AutoDownloader\bin\Debug";
            // Example 1 
            //string input = Path.Combine(zvonimirDirectory, @"0003cc74-a571-4638-af03-77775c5542c6\__ScopeCodeGen__.dll");
            // Example 2
            //string input = Path.Combine(zvonimirDirectory, @"0ce5ea59-dec8-4f6f-be08-0e0746e12515\CdpLogCache.Scopelib.dll");
            // Example 3
            //input = Path.Combine(zvonimirDirectory, @"10c15390-ea74-4b20-b87e-3f3992a130c0\__ScopeCodeGen__.dll");
            // Example 4
            //input = Path.Combine(zvonimirDirectory, @"2407f5f1-0930-4ce5-88d3-e288a86e54ca\__ScopeCodeGen__.dll");
            // Example 5
            //input = Path.Combine(zvonimirDirectory, @"3b9f1ec4-0ad8-4bde-879b-65c92d109159\__ScopeCodeGen__.dll");

            //const string input = @"\\madanm2\parasail2\TFS\parasail\ScopeSurvey\AutoDownloader\bin\Debug\0003cc74-a571-4638-af03-77775c5542c6\__ScopeCodeGen__.dll";
            //const string input = @"\\madanm2\parasail2\TFS\parasail\ScopeSurvey\AutoDownloader\bin\Debug\10c15390-ea74-4b20-b87e-3f3992a130c0\__ScopeCodeGen__.dll";
            //const string input = @"\\madanm2\parasail2\TFS\parasail\ScopeSurvey\AutoDownloader\bin\Debug\018c2f92-f63d-4790-a843-40a1b0e0e58a\__ScopeCodeGen__.dll";
            //const string input = @"\\madanm2\parasail2\TFS\parasail\ScopeSurvey\AutoDownloader\bin\Debug\0ab0de7e-6110-4cd4-8c30-6e72c013c2f0\__ScopeCodeGen__.dll";

            // Mike's example: 
            //input = @"\\research\root\public\mbarnett\Parasail\Diego\SimpleProcessors_9E4B4B56B06EFFD2\__ScopeCodeGen__.dll";

            //const string input = @"\\madanm2\parasail2\TFS\parasail\ScopeSurvey\AutoDownloader\bin\Debug\02e7c1bd-42ab-4f5b-8506-d6c49e562790\__ScopeCodeGen__.dll";

            // Loop
            //const string 
            //input = @"\\madanm2\parasail2\TFS\parasail\ScopeSurvey\AutoDownloader\bin\Debug\02e7c1bd-42ab-4f5b-8506-d6c49e562790\__ScopeCodeGen__.dll";

            // const string input = @"\\madanm2\parasail2\TFS\parasail\ScopeSurvey\AutoDownloader\bin\Debug\__ScopeCodeGen__.dll";

            //const string input = @"\\madanm2\parasail2\TFS\parasail\ScopeSurvey\AutoDownloader\bin\Debug\018c2f92-f63d-4790-a843-40a1b0e0e58a\__ScopeCodeGen__.dll";
            //input = @"\\madanm2\parasail2\TFS\parasail\ScopeSurvey\AutoDownloader\bin\Debug\98a9c4a1-6996-4c34-8d33-f7dd140ffbf9\__ScopeCodeGen__.dll";

            //input = @"\\madanm2\parasail2\TFS\parasail\ScopeSurvey\AutoDownloader\bin\Debug\d9b320ac-a1ff-415a-93e3-0d47d3d949ad\__ScopeCodeGen__.dll";

            // input = @"\\madanm2\parasail2\TFS\parasail\ScopeSurvey\AutoDownloader\bin\Debug\69dc12be-aacd-48a5-a776-e2766178a343\Microsoft.Bing.Platform.Inferences.Offline.SignalsCooking.dll";

            //input = @"\\madanm2\parasail2\TFS\parasail\ScopeSurvey\AutoDownloader\bin\Debug\6a02b587-21b6-4b4d-84c5-4caaebc9d5ad\__ScopeCodeGen__.dll";

            //input = @"\\madanm2\parasail2\TFS\parasail\ScopeSurvey\AutoDownloader\bin\Debug\0c3e04fe-75ec-59a8-a3e6-a85aecfe5476\__ScopeCodeGen__.dll";

            //input = @"\\madanm2\parasail2\TFS\parasail\ScopeSurvey\AutoDownloader\bin\Debug\00f41d12-10e8-4aa4-b54d-1c275bd99550\__ScopeCodeGen__.dll";

            // This one show me the problem in the topological order
            //input = @"\\madanm2\parasail2\TFS\parasail\ScopeSurvey\AutoDownloader\bin\Debug\4554b01e-829b-4e37-b818-688b074b00bf\__ScopeCodeGen__.dll";

            // This one is complaining about a missing schema
            //input = @"\\research\root\public\mbarnett\Parasail\First100JobsFromMadan\00e0c351-4bae-4970-989b-92806b1e657c\__ScopeCodeGen__.dll";
            // This one fails with changes of type edgardo did:
            //input = @"\\research\root\public\mbarnett\Parasail\First100JobsFromMadan\0b5243a6-cf68-4c35-8b45-8ce0e0162e14\__ScopeCodeGen__.dll";

            // Could not find column
            //input = @" \\research\root\public\mbarnett\Parasail\First100JobsFromMadan\0c92351b-f81e-4da1-91c3-930c7778fac6\__ScopeCodeGen__.dll";

            // Failed to find Schema
            //input = @"\\research\root\public\mbarnett\Parasail\First100JobsFromMadan\0c92351b-f81e-4da1-91c3-930c7778fac6\__ScopeCodeGen__.dll";

            // Has 27 passthrough out of 31 out
            //input = @"\\madanm2\parasail2\TFS\parasail\ScopeSurvey\AutoDownloader\bin\Debug\02c4581e-781a-4798-8875-162b4d740b5f\__ScopeCodeGen__.dll";

            // Can not find methods in the factory
            //input = @"\\madanm2\parasail2\TFS\parasail\ScopeSurvey\AutoDownloader\bin\Debug\0e4ca5d2-3478-431f-a4ad-f0b256780daf\__ScopeCodeGen__.dll";

            const string madansDirectory = @"\\madanm2\parasail2\TFS\parasail\ScopeSurvey\AutoDownloader\bin\Debug";
            input = Path.Combine(madansDirectory, @"01dc7d9c-f0bf-44b3-9228-9d22dda03e5d\__ScopeCodeGen__.dll"); //PassThroughReducer
            //input = Path.Combine(madansDirectory, @"2968e7c3-33a0-4a93-8ac8-81cd105bdbc4\__ScopeCodeGen__.dll"); // WindowsLoginSessionPathComputedColumnProcessor
            //input = Path.Combine(madansDirectory, @"11f04fe1-fa82-4de6-9557-e54a82f88e5a\__ScopeCodeGen__.dll"); // LiveIDStructuredStreamDecompileProcessor
            //input = Path.Combine(madansDirectory, @"2c0e5058-12a9-4fee-a36f-1b036f85aaee\__ScopeCodeGen__.dll"); // TopNReducer
            //input = Path.Combine(madansDirectory, @"30b000af-f6ad-413e-9b27-00f5b63aff1f\__ScopeCodeGen__.dll"); // ConfigurablePassThroughReducer

            string[] directories = Path.GetDirectoryName(input).Split(Path.DirectorySeparatorChar);
            var outputPath = Path.Combine(@"c:\Temp\", directories.Last()) + "_" + Path.ChangeExtension(Path.GetFileName(input), ".sarif");

            var logPath = Path.Combine(@"c:\Temp\", "analysis.log");
            var outputStream = File.CreateText(logPath);

            var log = AnalyzeOneDll(input, scopeKind, useScopeFactory);
            WriteSarifOutput(log, outputPath);


            AnalysisStats.PrintStats(outputStream);
            AnalysisStats.WriteAnalysisReasons(outputStream);
            outputStream.WriteLine("End.");
            outputStream.Flush();

            Console.WriteLine("Finished. Press any key to exit.");
            System.Console.ReadKey();

        }

        public enum ScopeMethodKind { Producer, Reducer, All };

        private static SarifLog AnalyzeOneDll(string input, ScopeMethodKind kind, bool useScopeFactory = true, bool interProcAnalysis = false)
        {
            //var folder = Path.GetDirectoryName(input);
            //var referenceFiles = Directory.GetFiles(folder, "*.dll", SearchOption.TopDirectoryOnly).Where(fp => Path.GetFileName(fp).ToLower(CultureInfo.InvariantCulture) != Path.GetFileName(input).ToLower(CultureInfo.InvariantCulture)).ToList();
            //referenceFiles.AddRange(Directory.GetFiles(folder, "*.exe", SearchOption.TopDirectoryOnly));
            return AnalyzeDll(input, kind, useScopeFactory, interProcAnalysis);
        }

        private void ComputeColumns(string xmlFile, string processNumber)
        {
            XElement x = XElement.Load(xmlFile);
            var processor = x
                .Descendants("operator")
                .Where(op => op.Attribute("id") != null && op.Attribute("id").Value == processNumber)
                .FirstOrDefault()
                ;

            Console.Write("Processor: {0}. ", processor.Attribute("className").Value);

            var inputSchema = ParseColumns(processor.Descendants("input").FirstOrDefault().Attribute("schema").Value);
            
        }

        private static IEnumerable<Column> ParseColumns(string schema)
        {
            // schema looks like: "JobGUID:string,SubmitTime:DateTime?,NewColumn:string"
            var schemaList = schema.Split(',');
            for(int i=0;i<schemaList.Count(); i++)
            {
                if(schemaList[i].Contains("<") && i<schemaList.Count() && schemaList[i+1].Contains(">"))
                {
                    schemaList[i] += schemaList[i + 1];
                    schemaList[i + 1] = "";
                    i++;
                }
            }
            return schemaList.Where( elem => !String.IsNullOrEmpty(elem)).Select((c, i) => { var a = c.Split(':'); return new Column(a[0].Trim(' '), new RangeDomain(i), a[1].Trim(' ')); });

            //return schema
            //    .Split(',')
            //    .Select((c, i) => { var a = c.Split(':'); return new Column(a[0], new RangeDomain(i), a[1]); });
        }


        public static SarifLog AnalyzeDll(string inputPath, ScopeMethodKind kind,

                                       bool useScopeFactory = true, bool interProc = false, StreamWriter outputStream = null)
        {
            MyHost host;
            ScopeProgramAnalysis program;
            Assembly assembly;
            CreateHostAndProgram(inputPath, interProc, out host, out program, out assembly);
            AnalysisStats.TotalNumberFolders++;
            AnalysisStats.TotalDllsFound++;

            program.ClassFilters = new HashSet<string>();
            program.ClousureFilters = new HashSet<string>();

            program.EntryMethods = new HashSet<string>();

            if (kind == ScopeMethodKind.Reducer || kind == ScopeMethodKind.All)
            {
                program.ClassFilters.Add("Reducer");
                program.ClousureFilters.Add("<Reduce>d__");
                program.EntryMethods.Add("Reduce");
            }
            if (kind == ScopeMethodKind.Producer || kind == ScopeMethodKind.All)
            {
                program.ClassFilters.Add("Processor");
                program.ClousureFilters.Add("<Process>d__");
                program.EntryMethods.Add("Process");
                //program.ClassFilter = "Producer";
                //program.ClousureFilter = "<Produce>d__";
                //program.EntryMethod = "Produce";
            }

            program.MethodUnderAnalysisName = "MoveNext";

            IEnumerable<Tuple<ClassDefinition, MethodDefinition, MethodDefinition, MethodDefinition>> scopeMethodTuples;
            List<Tuple<ClassDefinition, string>> errorMessages;
            if (useScopeFactory)
            {
                scopeMethodTuples = program.ObtainScopeMethodsToAnalyze(assembly, out errorMessages);
                if (!scopeMethodTuples.Any())
                {
                    if (outputStream != null)
                        outputStream.WriteLine("Failed to obtain methods from the ScopeFactory. ");
                    System.Console.WriteLine("Failed to obtain methods from the ScopeFactory.");

                    //System.Console.WriteLine("Now trying to find methods in the the assembly");
                    //scopeMethodPairs = program.ObtainScopeMethodsToAnalyzeFromAssemblies();
                }
            }
            else
            {
                scopeMethodTuples = program.ObtainScopeMethodsToAnalyzeFromAssemblies();
                errorMessages = new List<Tuple<ClassDefinition, string>>();
            }

            if (!scopeMethodTuples.Any() && errorMessages.Count == 0)
            {
                System.Console.WriteLine("No processors found in {0}", inputPath);
                return null;
            }

            var log = CreateSarifOutput();

            foreach (var errorMessage in errorMessages)
            {
                var r = CreateRun(inputPath, errorMessage.Item1.GetFullName(), errorMessage.Item2, new List<Result>());
                log.Runs.Add(r);
            }

            IReadOnlyDictionary<string, Tuple<Schema, Schema>> allSchemas;
            if (useScopeFactory)
            {
                allSchemas = program.ReadSchemasFromXML(inputPath);
            }
            else
            {
                allSchemas = program.ReadSchemasFromXML2(inputPath);
            }

            foreach (var methodTuple in scopeMethodTuples)
            {
                AnalysisStats.TotalMethods++;

                var processorClass = methodTuple.Item1;
                var entryMethodDef = methodTuple.Item2;
                var moveNextMethod = methodTuple.Item3;
                var getEnumMethod = methodTuple.Item4;
                System.Console.WriteLine("Method {0} on class {1}", moveNextMethod.Name, moveNextMethod.ContainingType.FullPathName());

                Schema inputSchema = null;
                Schema outputSchema = null;
                Tuple<Schema, Schema> schemas;
                if (allSchemas.TryGetValue(moveNextMethod.ContainingType.ContainingType.Name, out schemas))
                {
                    inputSchema = schemas.Item1;
                    outputSchema = schemas.Item2;
                }

                Run run;
                AnalysisReason errorReason;
                var ok = AnalyzeProcessor(inputPath, host, program.interprocAnalysisManager, program.factoryReducerMap, processorClass, entryMethodDef, moveNextMethod, getEnumMethod, inputSchema, outputSchema, out run, out errorReason);
                if (ok)
                {
                    log.Runs.Add(run);

                    if (outputStream != null)
                    {
                        outputStream.WriteLine("Class: [{0}] {1}", moveNextMethod.ContainingType.FullPathName(), moveNextMethod.ToSignatureString());

                        var resultSummary = run.Results.Where(r => r.Id == "Summary").FirstOrDefault();
                        if (resultSummary != null) // BUG? What to do if it is null?
                        {
                            outputStream.WriteLine("Inputs: {0}", String.Join(", ", resultSummary.GetProperty("Inputs")));
                            outputStream.WriteLine("Outputs: {0}", String.Join(", ", resultSummary.GetProperty("Outputs")));
                        }
                    }

                }
                else
                {
                    System.Console.WriteLine("Could not analyze {0}", inputPath);
                    System.Console.WriteLine("Reason: {0}\n", errorReason.Reason);

                    AnalysisStats.TotalofDepAnalysisErrors++;
                    AnalysisStats.AddAnalysisReason(errorReason);
                }

            }
            return log;
        }

        public static Run AnalyzeProcessor(Type processorType, string inputSchema, string outputSchema)
        {
            var inputPath = processorType.Assembly.Location;

            MyHost host;
            ScopeProgramAnalysis program;
            Assembly assembly;
            CreateHostAndProgram(inputPath, false, out host, out program, out assembly);
            Run run;
            AnalysisReason errorReason;

            var processorName = processorType.Name;

            var processorClass = assembly
                .RootNamespace
                .GetAllTypes()
                .OfType<ClassDefinition>()
                .Where(c => c.Name == processorName)
                .SingleOrDefault();
            if (processorClass == null) return null;


            var entryMethod = FindEntryMethod(host, processorClass);
            if (entryMethod == null) return null;

            var closureName = "<" + entryMethod.Name + ">";
            var containingType = entryMethod.ContainingType as ClassDefinition;
            if (containingType == null) return null;
            var closureClass = containingType.Members.OfType<ClassDefinition>().Where(c => c.Name.StartsWith(closureName)).SingleOrDefault();
            if (closureClass == null) return null;

            var moveNextMethod = closureClass.Methods.Where(m => m.Name == "MoveNext").SingleOrDefault();
            if (moveNextMethod == null) return null;

            var getEnumMethod = closureClass
                .Methods
                .Where(m => m.Name.StartsWith("System.Collections.Generic.IEnumerable<") && m.Name.EndsWith(">.GetEnumerator"))
                .SingleOrDefault();
            if (getEnumMethod == null) return null;

            var inputColumns = ParseColumns(inputSchema);
            var outputColumns = ParseColumns(outputSchema);
            var i = new Schema(inputColumns);
            var o = new Schema(outputColumns);

            var ok = AnalyzeProcessor(inputPath, host, program.interprocAnalysisManager, program.factoryReducerMap, processorClass, entryMethod, moveNextMethod, getEnumMethod, i, o, out run, out errorReason);
            if (ok) return run;
            else return null;
        }

        /// <summary>
        /// Searches for the Reduce or Process method in a processor.
        /// </summary>
        /// <param name="host">Used to resolve the base class references as the inheritance hierarchy is searched.</param>
        /// <param name="c">The initial class to begin the search at.</param>
        /// <returns>The processor's method, null if not found</returns>
        private static MethodDefinition FindEntryMethod(Host host, ClassDefinition c)
        {
            // First, walk up the inheritance hierarchy until we find out whether this is a processor or a reducer.
            string entryMethodName = null;
            ClassDefinition baseClass = host.ResolveReference(c.Base) as ClassDefinition;
            while (entryMethodName == null)
            {
                var fullName = baseClass.GetFullName();
                if (fullName == "ScopeRuntime.Processor")
                {
                    entryMethodName = "Process";
                    break;
                }
                else if (fullName == "ScopeRuntime.Reducer")
                {
                    entryMethodName = "Reduce";
                    break;
                }
                else
                {
                    if (baseClass.Base == null) break; // Object has no base class
                    baseClass = host.ResolveReference(baseClass.Base) as ClassDefinition;
                    if (baseClass == null) break;
                }
            }
            if (entryMethodName == null) return null;

            // Now, find the entry method (potentially walking up the inheritance hierarchy again, stopping
            // point is not necessarily the same as the class found in the walk above).
            var entryMethod = c.Methods.Where(m => m.Name == entryMethodName).SingleOrDefault();
            while (entryMethod == null)
            {
                c = host.ResolveReference(c.Base) as ClassDefinition;
                if (c == null) break;
                entryMethod = c.Methods.Where(m => m.Name == entryMethodName).SingleOrDefault();
            }
            return entryMethod;
        }

        private static void CreateHostAndProgram(string inputPath, bool interProc, out MyHost host, out ScopeProgramAnalysis program, out Assembly loadedAssembly)
        {
            // Determine whether to use Interproc analysis
            AnalysisOptions.DoInterProcAnalysis = interProc;

            host = new MyHost();
            PlatformTypes.Resolve(host);

            var loader = new MyLoader(host);
            host.Loader = loader;

            loadedAssembly = loader.LoadMainAssembly(inputPath);

            loader.LoadCoreAssembly();

            program = new ScopeProgramAnalysis(host, loader);

        }

        private static Dictionary<MethodDefinition, Tuple<DependencyPTGDomain, ISet<TraceableColumn>, ISet<TraceableColumn>>> previousResults = new Dictionary<MethodDefinition, Tuple<DependencyPTGDomain, ISet<TraceableColumn>, ISet<TraceableColumn>>>();
        private static bool AnalyzeProcessor(
            string inputPath,
            MyHost host,
            InterproceduralManager interprocAnalysisManager,
            IDictionary<string, ClassDefinition> factoryReducerMap,
            ClassDefinition processorClass,
            MethodDefinition entryMethodDef,
            MethodDefinition moveNextMethod,
            MethodDefinition getEnumMethod,
            Schema inputSchema,
            Schema outputSchema,
            out Run runResult,
            out AnalysisReason errorReason)
        {
            runResult = null;
            errorReason = default(AnalysisReason);

            try
            {

                // BUG: Get rid of these static fields
                InputSchema = inputSchema;
                OutputSchema = outputSchema;

                DependencyPTGDomain depAnalysisResult;
                ISet<TraceableColumn> inputColumns;
                ISet<TraceableColumn> outputColumns;

                Tuple<DependencyPTGDomain, ISet<TraceableColumn>, ISet<TraceableColumn>> previousResult;
                if (previousResults.TryGetValue(moveNextMethod, out previousResult))
                {
                    depAnalysisResult = previousResult.Item1;
                    inputColumns = previousResult.Item2;
                    outputColumns = previousResult.Item3;
                } else {
                    var dependencyAnalysis = new SongTaoDependencyAnalysis(host, interprocAnalysisManager, moveNextMethod, entryMethodDef, getEnumMethod);
                    depAnalysisResult = dependencyAnalysis.AnalyzeMoveNextMethod();
                    inputColumns = dependencyAnalysis.InputColumns;
                    outputColumns = dependencyAnalysis.OutputColumns;
                    previousResults.Add(moveNextMethod, Tuple.Create(depAnalysisResult, inputColumns, outputColumns));
                }

                var r = CreateResultsAndThenRun(inputPath, processorClass, entryMethodDef, moveNextMethod, depAnalysisResult, inputColumns, outputColumns, factoryReducerMap);
                runResult = r;

                return true;
            }
            catch (Exception e)
            {
                errorReason = new AnalysisReason(moveNextMethod, moveNextMethod.Body.Instructions[0],
                                String.Format(CultureInfo.InvariantCulture, "Thrown exception {0}\n{1}", e.Message, e.StackTrace.ToString()));
                return false;
            }
            finally
            {
                InputSchema = null;
                OutputSchema = null;
            }
        }

        public static void AnalyzeDllAndWriteLog(string inputPath, string outputPath, ScopeMethodKind kind,
                              bool useScopeFactory = true, bool interProc = false, StreamWriter outputStream = null)
        {
            var log = AnalyzeDll(inputPath, ScopeMethodKind.All, useScopeFactory, interProc, outputStream);
            WriteSarifOutput(log, outputPath);
        }


        private static Run CreateResultsAndThenRun(string inputPath, ClassDefinition processorClass, MethodDefinition entryMethod, MethodDefinition moveNextMethod, DependencyPTGDomain depAnalysisResult,
            ISet<TraceableColumn> inputColumns, ISet<TraceableColumn> outputColumns, IDictionary<string, ClassDefinition> processorMap)
        {
            var results = new List<Result>();

            var escapes = depAnalysisResult.Dependencies.A1_Escaping.Select(traceable => traceable.ToString());

            var inputUses = new HashSet<Traceable>();
            var outputModifies = new HashSet<Traceable>();

            if (!depAnalysisResult.IsTop)
            {
                if (depAnalysisResult.Dependencies.A4_Ouput.Any())
                {
                    var outColumnMap = new MapSet<TraceableColumn, Traceable>();
                    var outColumnControlMap = new MapSet<TraceableColumn, Traceable>();
                    foreach (var outColum in depAnalysisResult.Dependencies.A4_Ouput.Keys)
                    {
                        var outColumns = depAnalysisResult.GetTraceables(outColum).OfType<TraceableColumn>()
                                                                 .Where(t => t.TableKind == ProtectedRowKind.Output);
                        foreach (var column in outColumns)
                        {
                            if (!outColumnMap.ContainsKey(column))
                            {
                                outColumnMap.AddRange(column, depAnalysisResult.Dependencies.A4_Ouput[outColum]);
                            }
                            else
                            {
                                outColumnMap.AddRange(column, outColumnMap[column].Union(depAnalysisResult.Dependencies.A4_Ouput[outColum]));
                            }
                            if (!outColumnControlMap.ContainsKey(column))
                            {
                                outColumnControlMap.AddRange(column, depAnalysisResult.Dependencies.A4_Ouput_Control[outColum]);
                            }
                            else
                            {
                                outColumnControlMap.AddRange(column, outColumnControlMap[column].Union(depAnalysisResult.Dependencies.A4_Ouput_Control[outColum]));
                            }
                        }
                    }

                    foreach (var entryOutput in outColumnMap)
                    {
                        var result = new Result();
                        result.Id = "SingleColumn";
                        var column = entryOutput.Key;
                        var columnString = column.ToString();
                        var dependsOn = entryOutput.Value;
                        var controlDepends = new HashSet<Traceable>();

                        result.SetProperty("column", columnString);
                        result.SetProperty("data depends", dependsOn.Select(traceable => traceable.ToString()));

                        if (outColumnControlMap.ContainsKey(column))
                        {
                            var controlDependsOn = outColumnControlMap[column];
                            result.SetProperty("control depends", controlDependsOn.Select(traceable => traceable.ToString()));
                        }
                        else
                        {
                            result.SetProperty("control depends", new string[] { });
                        }
                        result.SetProperty("escapes", escapes);
                        results.Add(result);

                        inputUses.AddRange(dependsOn.Where(t => t.TableKind == ProtectedRowKind.Input));
                        outputModifies.Add(column);
                    }


                    //foreach (var outColum in depAnalysisResult.Dependencies.A4_Ouput.Keys)
                    //{
                    //    //var outColumns = depAnalysisResult.Dependencies.A2_Variables[outColum].OfType<TraceableColumn>()
                    //    //                                            .Where(t => t.TableKind == ProtectedRowKind.Output);
                    //    var outColumns = depAnalysisResult.GetTraceables(outColum).OfType<TraceableColumn>()
                    //                                                .Where(t => t.TableKind == ProtectedRowKind.Output);

                    //    foreach (var column in outColumns)
                    //    {
                    //        var result = new Result();
                    //        result.Id = "SingleColumn";
                    //        var columnString = column.ToString();
                    //        var dependsOn = depAnalysisResult.Dependencies.A4_Ouput[outColum];

                    //        //dependsOn.AddRange(traceables);
                    //        result.SetProperty("column", columnString);
                    //        result.SetProperty("data depends", dependsOn.Select(traceable => traceable.ToString()));
                    //        if (depAnalysisResult.Dependencies.A4_Ouput_Control.ContainsKey(outColum))
                    //        {
                    //            var controlDependsOn = depAnalysisResult.Dependencies.A4_Ouput_Control[outColum];
                    //            result.SetProperty("control depends", controlDependsOn.Where(t => !(t is Other)).Select(traceable => traceable.ToString()));
                    //            inputUses.AddRange(controlDependsOn.Where(t => t.TableKind == ProtectedRowKind.Input));
                    //        }
                    //        else
                    //        {
                    //            result.SetProperty("control depends", new string[] { });
                    //        }
                    //        result.SetProperty("escapes", escapes);
                    //        results.Add(result);

                    //        inputUses.AddRange(dependsOn.Where(t => t.TableKind == ProtectedRowKind.Input));
                    //        outputModifies.Add(column);
                    //    }
                    //}
                }
                else
                {
                    var result = new Result();
                    result.Id = "SingleColumn";
                    result.SetProperty("column", "_EMPTY_");
                    result.SetProperty("escapes", escapes);
                    results.Add(result);

                }
                var resultSummary = new Result();
                resultSummary.Id = "Summary";

                //var inputsString = inputUses.OfType<TraceableColumn>().Select(t => t.ToString());
                //var outputsString = outputModifies.OfType<TraceableColumn>().Select(t => t.ToString());
                //result2.SetProperty("Inputs", inputsString);
                //result2.SetProperty("Outputs", outputsString);

                var inputsString = inputColumns.Select(t => t.ToString());
                var outputsString = outputColumns.Select(t => t.ToString());
                resultSummary.SetProperty("Inputs", inputsString);
                resultSummary.SetProperty("Outputs", outputsString);

                var inputSchemaString = InputSchema.Columns.Select(t => t.ToString());
                var outputSchemaString = OutputSchema.Columns.Select(t => t.ToString());

                resultSummary.SetProperty("SchemaInputs", inputSchemaString);
                resultSummary.SetProperty("SchemaOutputs", outputSchemaString);
                results.Add(resultSummary);
            }
            else
            {
                var result = new Result();
                result.Id = "SingleColumn";
                result.SetProperty("column", "_TOP_");
                result.SetProperty("depends", "_TOP_");
                results.Add(result);
                var resultEmpty = new Result();
                resultEmpty.Id = "Summary";
                resultEmpty.SetProperty("Inputs", "_TOP_");
                resultEmpty.SetProperty("Outputs", "_TOP_");
                resultEmpty.SetProperty("SchemaInputs", "_TOP_");
                resultEmpty.SetProperty("SchemaOutputs", "_TOP_");
                results.Add(resultEmpty);
            }

            var actualProcessorClass = entryMethod.ContainingType;

            var id = String.Format("[{0}] {1}", processorClass.ContainingAssembly.Name, processorClass.GetFullName());

            // Very clumsy way to find the process number and the processor name from the MoveNext method.
            // But it is the process number and processor name that allow us to link these results to the information
            // in the XML file that describes the job.
            // Climb the containing type chain from the MoveNext method until we find the entry in the dictionary whose
            // value matches one of the classes.
            var done = false;
            foreach (var kv in processorMap)
            {
                if (done) break;
                var c = moveNextMethod.ContainingType as ClassDefinition;
                while (c != null)
                {
                    if (kv.Value == c)
                    {
                        id = kv.Value.Name + "|" + kv.Key;
                        done = true;
                        break;
                    }
                    c = c.ContainingType as ClassDefinition;
                }
            }

            string actualClassContainingIterator = null;
            if (processorClass != actualProcessorClass)
                actualClassContainingIterator = "Analyzed processsor: " + actualProcessorClass.GetFullName();

            var r = CreateRun(inputPath, id, actualClassContainingIterator, results);
            return r;
        }

        private static void LoadExternalReferences(IEnumerable<string> referenceFiles, Loader loader)
        {
            foreach (var referenceFileName in referenceFiles)
            {
                try
                {
                    loader.LoadAssembly(referenceFileName);
                }
                catch (Exception e)
                {
                    System.Console.WriteLine("Cannot load {0}:{1}", referenceFileName, e.Message);
                }
            }
        }
        /// <summary>
        /// Analyze the ScopeFactory class to get all the Processor/Reducer classes to analyze
        /// For each one obtain:
        /// 1) The class that the factory creates an instance of.
        /// 2) entry point method that creates the class with the iterator clousure and populated with some data)
        /// 3) the GetEnumerator method that creates and enumerator and polulated with data
        /// 4) the MoveNextMethod that contains the actual reducer/producer code
        /// </summary>
        /// <returns></returns>
        private IEnumerable<Tuple<ClassDefinition, MethodDefinition, MethodDefinition, MethodDefinition>> ObtainScopeMethodsToAnalyze(Assembly assembly, out List<Tuple<ClassDefinition, string>> errorMessages)
        {
            errorMessages = new List<Tuple<ClassDefinition, string>>();

            var processorsToAnalyze = new HashSet<ClassDefinition>();

            var scopeMethodTuplesToAnalyze = new HashSet<Tuple<ClassDefinition, MethodDefinition, MethodDefinition, MethodDefinition>>();

            var operationFactoryClass = assembly.RootNamespace.GetAllTypes().OfType<ClassDefinition>()
                                        .Where(c => c.Name == "__OperatorFactory__" && c.ContainingType != null && c.ContainingType.Name == "___Scope_Generated_Classes___").SingleOrDefault();

            if (operationFactoryClass == null)
                return new HashSet<Tuple<ClassDefinition, MethodDefinition, MethodDefinition, MethodDefinition>>();

            // Hack: use actual ScopeRuntime Types
            var factoryMethods = operationFactoryClass.Methods.Where(m => m.Name.StartsWith("Create_Process_", StringComparison.Ordinal)
                            /*&& m.ReturnType.ToString() == this.ClassFilter*/);

            // var referencesLoaded = false;

            foreach (var factoryMethod in factoryMethods)
            {
                var ins = factoryMethod.Body.Instructions.OfType<Model.Bytecode.CreateObjectInstruction>().Single();

                var reducerClass = ins.Constructor.ContainingType;

                var isCompilerGenerated = compilerGeneretedMethodMatchers.Any(regex => regex.IsMatch(reducerClass.GetFullName()));

                if (isCompilerGenerated)
                    continue;

                ClassDefinition reducerClassDefinition = null;
                try
                {
                    reducerClassDefinition = host.ResolveReference(reducerClass) as ClassDefinition;

                    if (reducerClassDefinition != null)
                    {
                        var entryMethod = FindEntryMethod(host, reducerClassDefinition);
                        if (entryMethod == null)
                        {
                            errorMessages.Add(Tuple.Create(reducerClassDefinition, "Entry method not found"));
                            continue;
                        }
                        var containingType = entryMethod.ContainingType as ClassDefinition;
                        if (containingType == null)
                        {
                            continue;
                        }

                        if (processorsToAnalyze.Contains(containingType))
                            continue;

                        processorsToAnalyze.Add(containingType);

                        var candidateClosures = containingType.Types.OfType<ClassDefinition>()
                                       .Where(c => this.ClousureFilters.Any(filter => c.Name.StartsWith(filter)));
                        if (!candidateClosures.Any())
                        {
                            errorMessages.Add(Tuple.Create(reducerClassDefinition, "Iterator not found"));
                            continue;
                        }
                        foreach (var candidateClosure in candidateClosures)
                        {
                            var getEnumMethods = candidateClosure.Methods
                                                        .Where(m => m.Name == ScopeAnalysisConstants.SCOPE_ROW_ENUMERATOR_METHOD);
                            var getEnumeratorMethod = getEnumMethods.Single();

                            var moveNextMethods = candidateClosure.Methods
                                                        .Where(md => md.Body != null && md.Name.Equals(this.MethodUnderAnalysisName));
                            foreach (var moveNextMethod in moveNextMethods)
                            {
                                scopeMethodTuplesToAnalyze.Add(Tuple.Create(reducerClassDefinition, entryMethod, moveNextMethod, getEnumeratorMethod));

                                // TODO: Hack for reuse. Needs refactor
                                if (factoryMethod != null)
                                {
                                    var processID = factoryMethod.Name.Substring(factoryMethod.Name.IndexOf("Process_"));
                                    this.factoryReducerMap.Add(processID, entryMethod.ContainingType as ClassDefinition);
                                }
                            }
                        }
                    }
                    else
                    {
                        AnalysisStats.TotalMethodsNotFound++;
                    }
                }
                catch (Exception e)
                {
                    AnalysisStats.TotalofDepAnalysisErrors++;
                    System.Console.WriteLine("Error in Dependency Analysis", e.Message);
                }

            }
            return scopeMethodTuplesToAnalyze;
        }

        public IEnumerable<Tuple<ClassDefinition, MethodDefinition, MethodDefinition, MethodDefinition>> ObtainScopeMethodsToAnalyzeFromAssemblies()
        {
            var scopeMethodTuplesToAnalyze = new HashSet<Tuple<ClassDefinition, MethodDefinition, MethodDefinition, MethodDefinition>>();

            var candidateClasses = host
                .Assemblies
                .Where(a => a.Name != "mscorlib")
                .SelectMany(a => a.RootNamespace.GetAllTypes().OfType<ClassDefinition>())
                ;
            if (candidateClasses.Any())
            {
                var results = new List<Result>();
                foreach (var candidateClass in candidateClasses)
                {
                    var isCompilerGenerated = compilerGeneretedMethodMatchers.Any(regex => regex.IsMatch(candidateClass.GetFullName()));

                    if (isCompilerGenerated)
                        continue;

                    var entryMethod = FindEntryMethod(host, candidateClass);
                    if (entryMethod == null) continue;

                    var containingType = entryMethod.ContainingType as ClassDefinition;
                    if (containingType == null) continue;
                    var candidateClousures = containingType.Types.OfType<ClassDefinition>()
                                    .Where(c => this.ClousureFilters.Any(filter => c.Name.StartsWith(filter)));
                    foreach (var candidateClousure in candidateClousures)
                    {
                        var methods = candidateClousure.Members.OfType<MethodDefinition>()
                                                .Where(md => md.Body != null
                                                && md.Name.Equals(this.MethodUnderAnalysisName));

                        if (methods.Any())
                        {
                            var moveNextMethod = methods.First();
                            // BUG: Really should do this by getting the name of the Row type used by the processor, but just a quick hack for now to allow unit testing (which uses a different Row type).
                            // System.Collections.Generic.IEnumerable<FakeRuntime.Row>.GetEnumerator
                            // And really, this should be a type test anyway. The point is to find the explicit interface implementation of IEnumerable<T>.GetEnumerator.
                            var getEnumMethods = candidateClousure.Methods
                                                        .Where(m => m.Name.StartsWith("System.Collections.Generic.IEnumerable<") && m.Name.EndsWith(">.GetEnumerator"));
                            var getEnumeratorMethod = getEnumMethods.First();

                            scopeMethodTuplesToAnalyze.Add(Tuple.Create(candidateClass, entryMethod, moveNextMethod, getEnumeratorMethod));

                        }
                    }
                }
            }
            return scopeMethodTuplesToAnalyze;
        }

        private void ComputeMethodsToAnalyzeForReducerClass(HashSet<Tuple<MethodDefinition, MethodDefinition, MethodDefinition>> scopeMethodPairsToAnalyze,
            MethodDefinition factoryMethdod, IBasicType reducerClass)
        {
        }

        //private ClassDefinition ResolveClass(IBasicType classToResolve)
        //{
        //    var resolvedClass = host.ResolveReference(classToResolve) as ClassDefinition;
        //    if(resolvedClass == null)
        //    {
        //        try
        //        {
        //            AnalysisStats.TotalDllsFound++;
        //            loader.TryToLoadReferencedAssembly(classToResolve.ContainingAssembly);
        //            resolvedClass = host.ResolveReference(classToResolve) as ClassDefinition;
        //        }
        //        catch (Exception e)
        //        {
        //            AnalysisStats.DllThatFailedToLoad.Add(classToResolve.ContainingAssembly.Name);
        //            AnalysisStats.TotalDllsFailedToLoad++;
        //        }

        //    }
        //    return resolvedClass;
        //}

        private IReadOnlyDictionary<string, Tuple<Schema, Schema>>
            ReadSchemasFromXML(string inputPath)
        {
            var d = new Dictionary<string, Tuple<Schema, Schema>>();
            var inputDirectory = Path.GetDirectoryName(inputPath);
            var xmlFile = Path.Combine(inputDirectory, "ScopeVertexDef.xml");
            if (File.Exists(xmlFile))
            {
                XElement x = XElement.Load(xmlFile);
                var operators = x.Descendants("operator");
                foreach (var kv in this.factoryReducerMap)
                {
                    var processId = kv.Key;
                    var className = kv.Value.Name;
                    var processors = operators.Where(op => op.Attribute("id") != null && op.Attribute("id").Value == processId);
                    var inputSchemas = processors.SelectMany(processor => processor.Descendants("input").Select(i => i.Attribute("schema")), (processor, schema) => Tuple.Create(processor.Attribute("id"), schema));
                    var outputSchemas = processors.SelectMany(processor => processor.Descendants("output").Select(i => i.Attribute("schema")), (processor, schema) => Tuple.Create(processor.Attribute("id"), schema));
                    var inputSchema = inputSchemas.FirstOrDefault();
                    var outputSchema = outputSchemas.FirstOrDefault();
                    if (inputSchema == null || outputSchema == null) continue; // BUG? Silent failure okay?
                    if (inputSchema.Item1 != outputSchema.Item1) continue; // silent failure okay?
                    var inputColumns = ParseColumns(inputSchema.Item2.Value);
                    var outputColumns = ParseColumns(outputSchema.Item2.Value);
                    d.Add(className, Tuple.Create(new Schema(inputColumns), new Schema(outputColumns)));
                }
            }
            return d;
        }

        private IReadOnlyDictionary<string, Tuple<Schema, Schema>>
            ReadSchemasFromXML2(string inputPath)
        {
            var d = new Dictionary<string, Tuple<Schema, Schema>>();
            var inputDirectory = Path.GetDirectoryName(inputPath);
            var xmlFile = Path.Combine(inputDirectory, "Schema.xml");
            if (File.Exists(xmlFile))
            {
                XElement x = XElement.Load(xmlFile);
                var operators = x.Descendants("operator");
                foreach (var op in operators)
                {
                    var inputSchema = op.Descendants("input").First().Attribute("schema").Value;
                    var outputSchema = op.Descendants("output").First().Attribute("schema").Value;
                    var inputColumns = ParseColumns(inputSchema);
                    var outputColumns = ParseColumns(outputSchema);
                    d.Add(op.Attribute("className").Value, Tuple.Create(new Schema(inputColumns), new Schema(outputColumns)));
                }
            }
            return d;
        }

        private static SarifLog CreateSarifOutput()
        {
            JsonSerializerSettings settings = new JsonSerializerSettings()
            {
                ContractResolver = SarifContractResolver.Instance,
                Formatting = Formatting.Indented
            };

            SarifLog log = new SarifLog()
            {
                Runs = new List<Run>()
            };
            return log;
        }

        private static Run CreateRun(string inputPath, string id, string notification, IList<Result> results)
        {
            var run = new Run();
            // run.StableId = method.ContainingType.FullPathName();
            //run.Id = String.Format("[{0}] {1}", method.ContainingType.FullPathName(), method.ToSignatureString());
            run.Id = id;
            run.Tool = Tool.CreateFromAssemblyData();
            run.Tool.Name = "ScopeProgramAnalysis";
            run.Files = new Dictionary<string, FileData>();
            var fileDataKey = UriHelper.MakeValidUri(inputPath);
            var fileData = FileData.Create(new Uri(fileDataKey), false);
            run.Files.Add(fileDataKey, fileData);
            if (!String.IsNullOrWhiteSpace(notification))
                run.ToolNotifications = new List<Notification>() { new Notification { Message = notification, }, };

            run.Results = results;

            return run;
        }
        public static void WriteSarifOutput(SarifLog log, string outputFilePath)
        {
            string sarifText = SarifLogToString(log);
            try
            {
                File.WriteAllText(outputFilePath, sarifText);
            }
            catch (Exception e)
            {
                System.Console.Out.Write("Could not write the file: {0}:{1}", outputFilePath, e.Message);
            }
        }

        public static string SarifLogToString(SarifLog log)
        {
            JsonSerializerSettings settings = new JsonSerializerSettings()
            {
                ContractResolver = SarifContractResolver.Instance,
                Formatting = Formatting.Indented
            };

            var sarifText = JsonConvert.SerializeObject(log, settings);
            return sarifText;
        }
        public static string SarifRunToString(Run run)
        {
            JsonSerializerSettings settings = new JsonSerializerSettings()
            {
                ContractResolver = SarifContractResolver.Instance,
                Formatting = Formatting.Indented
            };

            var sarifText = JsonConvert.SerializeObject(run, settings);
            return sarifText;
        }
    }

}
