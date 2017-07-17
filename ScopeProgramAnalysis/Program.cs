using Backend.Utils;
using Microsoft.CodeAnalysis.Sarif;
using Microsoft.CodeAnalysis.Sarif.Readers;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Backend.Analyses;
using System.Globalization;
using ScopeProgramAnalysis.Framework;
using System.Text.RegularExpressions;
using Microsoft.Cci;
using Backend;
using Backend.Model;
using System.Threading.Tasks;
using System.Diagnostics;

namespace ScopeProgramAnalysis
{
    public class ScopeProgramAnalysis
    {
        private IMetadataHost host;
        private IDictionary<string, ITypeDefinition> factoryReducerMap;
        private MyLoader loader;
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
                    new Regex(@"^___Scope_Generated_Classes___.ScopeCrossApplyProcessor_\d+$", RegexOptions.Compiled),
                    new Regex(@"^___Scope_Generated_Classes___.ScopeProcessorCrossApplyExpressionWrapper_\d+$", RegexOptions.Compiled),
                    new Regex(@"^___Scope_Generated_Classes___.ScopeReducer__\d+$", RegexOptions.Compiled)
                    // ScopeRuntime.
            };

        public ScopeProgramAnalysis(MyLoader loader)
        {
            this.host = loader.Host;
            this.loader = loader;
            this.factoryReducerMap = new Dictionary<string, ITypeDefinition>();
            this.interprocAnalysisManager = new InterproceduralManager(loader);
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
            //input = Path.Combine(madansDirectory, @"01dc7d9c-f0bf-44b3-9228-9d22dda03e5d\__ScopeCodeGen__.dll"); //PassThroughReducer
            //input = Path.Combine(madansDirectory, @"2968e7c3-33a0-4a93-8ac8-81cd105bdbc4\__ScopeCodeGen__.dll"); // WindowsLoginSessionPathComputedColumnProcessor
            //input = Path.Combine(madansDirectory, @"11f04fe1-fa82-4de6-9557-e54a82f88e5a\__ScopeCodeGen__.dll"); // LiveIDStructuredStreamDecompileProcessor
            //input = Path.Combine(madansDirectory, @"2c0e5058-12a9-4fee-a36f-1b036f85aaee\__ScopeCodeGen__.dll"); // TopNReducer
            //input = Path.Combine(madansDirectory, @"30b000af-f6ad-413e-9b27-00f5b63aff1f\__ScopeCodeGen__.dll"); // ConfigurablePassThroughReducer

            const string oneHundredJobsDirectory = @"\\research\root\public\mbarnett\Parasail\First100JobsFromMadan";
            //input = Path.Combine(oneHundredJobsDirectory, @"00e0c351-4bae-4970-989b-92806b1e657c\__ScopeCodeGen__.dll");
            //input = Path.Combine(oneHundredJobsDirectory, @"0b610085-e88d-455c-81ea-90c727bbdf58\__ScopeCodeGen__.dll");
            //input = Path.Combine(oneHundredJobsDirectory, @"0ba011a3-fd85-4f85-92ce-e8a230d33dc3\__ScopeCodeGen__.dll");
            // Times out for unknown reason
            //input = Path.Combine(oneHundredJobsDirectory, @"0cb45fd4-ee48-4091-a95b-6ed802173335\__ScopeCodeGen__.dll");
            // Times out for unknown reason
            //input = Path.Combine(oneHundredJobsDirectory, @"0e86b352-b968-40fd-8377-8b3a5812aa61\__ScopeCodeGen__.dll");
            // No __ScopeCodeGen__ assembly
            //input = Path.Combine(oneHundredJobsDirectory, @"000ef3c1-abb3-4a54-8ea1-60c74139d936\__ScopeCodeGen__.dll");
            // __ScopeCodeGen__ assembly, but no processors found
            //input = Path.Combine(oneHundredJobsDirectory, @"00a41169-9711-4a14-bf02-7d068ad6dded\__ScopeCodeGen__.dll");
            //input = @"C:\dev\Bugs\Parasail\099f4b11-eeeb-4357-87aa-2de336b6eb46\__ScopeCodeGen__.dll";
            //input = @"C:\dev\Parasail\ScopeSurvey\ScopeMapAccess\bin\LocalDebug\7d7e61ab-6687-4e2d-99fc-636bf4eb3e0d\__ScopeCodeGen__.dll";
            //input = @"C:\dev\Parasail\ScopeSurvey\ScopeMapAccess\bin\LocalDebug\79dde6c1-efa3-44e6-a842-8397dea70df4\__ScopeCodeGen__.dll";

            //input = @"C:\dev\Parasail\ScopeSurvey\ScopeMapAccess\bin\LocalDebug\17764c26-9312-4d0a-9ac1-9ae08e0303ee\__ScopeCodeGen__.dll";
            input = @"C:\dev\Parasail\ScopeSurvey\ScopeMapAccess\bin\LocalDebug\75f9adff-7f3e-47f5-b282-99518ee7f8b3\__ScopeCodeGen__.dll";

            string[] directories = Path.GetDirectoryName(input).Split(Path.DirectorySeparatorChar);
            var outputPath = Path.Combine(@"c:\Temp\", directories.Last()) + "_" + Path.ChangeExtension(Path.GetFileName(input), ".sarif");

            var logPath = Path.Combine(@"c:\Temp\", "analysis.log");
            var outputStream = File.CreateText(logPath);

            var log = AnalyzeDll(input, scopeKind, useScopeFactory, false);
            WriteSarifOutput(log, outputPath);


            AnalysisStats.PrintStats(outputStream);
            AnalysisStats.WriteAnalysisReasons(outputStream);
            outputStream.WriteLine("End.");
            outputStream.Flush();

            Console.WriteLine("Finished. Press any key to exit.");
            System.Console.ReadKey();

        }

        public enum ScopeMethodKind { Producer, Reducer, All };

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
            for (int i = 0; i < schemaList.Count(); i++)
            {
                if (schemaList[i].Contains("<") && i < schemaList.Count()-1 && schemaList[i + 1].Contains(">"))
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

        public static SarifLog AnalyzeDll(string inputPath, ScopeMethodKind kind, bool useScopeFactory = true, bool interProc = false, StreamWriter outputStream = null, TimeSpan timeout = default(TimeSpan))
        {
            if (timeout == default(TimeSpan))
            {
                timeout = TimeSpan.FromMinutes(1);
            }
            if (System.Diagnostics.Debugger.IsAttached)
            {
                timeout = TimeSpan.FromMilliseconds(-1);
            }
            var task = Task.Run(() => AnalyzeDll2(inputPath, kind, useScopeFactory, interProc, outputStream));
            if (task.Wait(timeout))
                return task.Result;
            else
            {
                var log = CreateSarifOutput();
                var r = CreateRun(inputPath, "No results", "Timeout", new List<Result>());
                log.Runs.Add(r);
                return log;
            }
        }

        private static SarifLog AnalyzeDll2(string inputPath, ScopeMethodKind kind, bool useScopeFactory = true, bool interProc = false, StreamWriter outputStream = null)
        {
            var log = CreateSarifOutput();
            log.SchemaUri = new Uri("http://step0");

            if (!File.Exists(inputPath))
            {
                var fileName = Path.GetFileName(inputPath);
                var r = CreateRun(inputPath, "No results", "(AnalyzeDLL) File not found: " + fileName, new List<Result>());
                log.Runs.Add(r);
                return log;
            }

            MyLoader loader;
            ScopeProgramAnalysis program;
            IAssembly assembly;
            CreateHostAndProgram(inputPath, interProc, out loader, out program, out assembly);
            var host = loader.Host;

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

            IEnumerable<Tuple<ITypeDefinition, IMethodDefinition, IMethodDefinition, IMethodDefinition, IMethodDefinition>> scopeMethodTuples;
            List<Tuple<ITypeDefinition, string>> errorMessages;
            if (useScopeFactory)
            {
                scopeMethodTuples = program.ObtainScopeMethodsToAnalyze(assembly, out errorMessages);
                if (!scopeMethodTuples.Any())
                {
                    if (outputStream != null)
                        outputStream.WriteLine("Failed to obtain methods from the ScopeFactory. ");
                    //Console.WriteLine("Failed to obtain methods from the ScopeFactory.");

                    //Console.WriteLine("Now trying to find methods in the the assembly");
                    //scopeMethodPairs = program.ObtainScopeMethodsToAnalyzeFromAssemblies();
                }
            }
            else
            {
                scopeMethodTuples = program.ObtainScopeMethodsToAnalyzeFromAssemblies();
                errorMessages = new List<Tuple<ITypeDefinition, string>>();
            }

            if (!scopeMethodTuples.Any() && errorMessages.Count == 0)
            {
                //Console.WriteLine("No processors found in {0}", inputPath);
                var r = CreateRun(inputPath, "No results", "No processors found", new List<Result>());
                log.Runs.Add(r);
                return log;
            }

            foreach (var errorMessage in errorMessages)
            {
                var r = CreateRun(inputPath, errorMessage.Item1 == null ? "No results" : errorMessage.Item1.FullName(), errorMessage.Item2, new List<Result>());
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

            var processorNumber = 0;

            foreach (var methodTuple in scopeMethodTuples)
            {
                processorNumber++;
                AnalysisStats.TotalMethods++;

                log.SchemaUri = new Uri(log.SchemaUri.ToString() + String.Format("/processor{0}", processorNumber));

                var processorClass = methodTuple.Item1;
                var entryMethodDef = methodTuple.Item2;
                var moveNextMethod = methodTuple.Item3;
                var getEnumMethod = methodTuple.Item4;
                var factoryMethod = methodTuple.Item5;
                Console.WriteLine("Method {0} on class {1}", moveNextMethod.Name, moveNextMethod.ContainingType.FullName());

                Schema inputSchema = null;
                Schema outputSchema = null;
                Tuple<Schema, Schema> schemas;
                if (allSchemas.TryGetValue((moveNextMethod.ContainingType as INestedTypeReference).ContainingType.FullName(), out schemas))
                {
                    inputSchema = schemas.Item1;
                    outputSchema = schemas.Item2;
                } else
                {
                    continue; // BUG! Silent failure
                }

                log.SchemaUri = new Uri(log.SchemaUri.ToString() + "/aboutToAnalyze");
                Run run;
                AnalysisReason errorReason;
                
                var ok = AnalyzeProcessor(inputPath, loader, program.interprocAnalysisManager, program.factoryReducerMap, processorClass, entryMethodDef, moveNextMethod, getEnumMethod, factoryMethod, inputSchema, outputSchema, out run, out errorReason);
                if (ok)
                {
                    log.SchemaUri = new Uri(log.SchemaUri.ToString() + "/analyzeOK");

                    log.Runs.Add(run);

                    if (outputStream != null)
                    {
                        outputStream.WriteLine("Class: [{0}] {1}", moveNextMethod.ContainingType.FullName(), moveNextMethod.ToString());

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
                    log.SchemaUri = new Uri(log.SchemaUri.ToString() + "/analyzeNotOK/" + errorReason.Reason);

                    Console.WriteLine("Could not analyze {0}", inputPath);
                    Console.WriteLine("Reason: {0}\n", errorReason.Reason);

                    AnalysisStats.TotalofDepAnalysisErrors++;
                    AnalysisStats.AddAnalysisReason(errorReason);
                }

            }
            var foo = ExtractDependencyStats(log);
            return log;
        }

        public static Run AnalyzeProcessor(Type processorType, string inputSchema, string outputSchema)
        {
            var inputPath = processorType.Assembly.Location;

            MyLoader loader;
            ScopeProgramAnalysis program;
            IAssembly assembly;
            CreateHostAndProgram(inputPath, false, out loader, out program, out assembly);

            var host = loader.Host;

            Run run;
            AnalysisReason errorReason;

            var processorName = processorType.Name;

            var processorClass = assembly
                //.RootNamespace
                .GetAllTypes()
                .OfType<ITypeDefinition>()
                .Where(c => c.GetNameThatMatchesReflectionName() == processorName)
                .SingleOrDefault();
            if (processorClass == null)
            {
                return CreateRun(inputPath, processorName, "Processor class not found", new List<Result>());
            }
            var entryMethod = FindEntryMethod(loader.RuntimeTypes.concurrentProcessor, processorClass);
            if (entryMethod == null)
            {
                return CreateRun(inputPath, processorName, "Entry method not found", new List<Result>());
            }

            var closureName = "<" + entryMethod.Name + ">";
            var containingType = entryMethod.ContainingType.ResolvedType as ITypeDefinition;

            //if(containingType is IGenericTypeInstance)
            //{
            //    containingType = (containingType as IGenericTypeInstance).GenericType.ResolvedType;
            //}

            if (containingType == null)
            {
                return CreateRun(inputPath, processorName, "Containing type of closure type not found", new List<Result>());
            }
            var closureClass = containingType.Members.OfType<ITypeDefinition>().Where(c => c.GetName().StartsWith(closureName)).SingleOrDefault();
            if (closureClass == null)
            {
                return CreateRun(inputPath, processorName, "Closure class not found", new List<Result>());
            }

            var moveNextMethod = closureClass.Methods.Where(m => m.Name.Value == "MoveNext").SingleOrDefault();
            if (moveNextMethod == null) return null;
            if (moveNextMethod == null)
            {
                return CreateRun(inputPath, processorName, "MoveNext method not found", new List<Result>());
            }

            var getEnumMethod = closureClass
                .Methods
                .Where(m => m.Name.Value.StartsWith("System.Collections.Generic.IEnumerable<") && m.Name.Value.EndsWith(">.GetEnumerator"))
                .SingleOrDefault();
            if (getEnumMethod == null) return null;
            if (getEnumMethod == null)
            {
                return CreateRun(inputPath, processorName, "GetEnumerator method not found", new List<Result>());
            }

            var inputColumns = ParseColumns(inputSchema);
            var outputColumns = ParseColumns(outputSchema);
            var i = new Schema(inputColumns);
            var o = new Schema(outputColumns);

            var ok = AnalyzeProcessor(inputPath, loader, program.interprocAnalysisManager, program.factoryReducerMap, processorClass, entryMethod, moveNextMethod, getEnumMethod, null, i, o, out run, out errorReason);
            if (ok)
            {
                return run;
            }
            else
            {
                return CreateRun(inputPath, processorName, errorReason.Reason, new List<Result>());
            }
        }

        /// <summary>
        /// Searches for the Reduce or Process method in a processor.
        /// </summary>
        /// <param name="concurrentProcessorType">This parameter is needed in case <paramref name="c"/> is
        /// a type that implemented ConcurrentProcessor which means the real processor is the type
        /// argument used in the ConcurrentProcessor.</param>
        /// <param name="c">The initial class to begin the search at.</param>
        /// <returns>The processor's method, null if not found</returns>
        private static IMethodDefinition FindEntryMethod(ITypeDefinition concurrentProcessorType, ITypeDefinition c)
        {
            // If c is a subtype of ConcurrentProcessor, then c should really be the first generic argument
            // which is the type of the real processor. Note that this is tricky to find since c might
            // be a non-generic type that is a subtype of another type that in turn is a subtype of
            // ConcurrentProcessor. I.e., c is of type T <: U<A> <: ConcurrentProcessor<A,B,C>
            var found = false;
            var c2 = c;
            IGenericTypeInstanceReference gtir = null;
            while (!found)
            {
                gtir = c2 as IGenericTypeInstanceReference;
                if (gtir != null && TypeHelper.TypesAreEquivalent(gtir.GenericType.ResolvedType, concurrentProcessorType))
                {
                    found = true;
                    break;
                }
                var baseClass = c2.BaseClasses.SingleOrDefault();
                if (baseClass == null) break;
                c2 = baseClass.ResolvedType;
            }
            if (found)
            {
                c = gtir.GenericArguments.ElementAt(0).ResolvedType;
            }

            // First, walk up the inheritance hierarchy until we find out whether this is a processor or a reducer.
            string entryMethodName = null;
            var baseType = c.BaseClasses.SingleOrDefault();
            if (baseType != null)
            {
                var baseClass = baseType.ResolvedType; // need to resolve to get base class
                if (baseClass != null)
                {
                    while (entryMethodName == null)
                    {
                        var fullName = baseClass.FullName();
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
                            if (baseClass.BaseClasses.SingleOrDefault() == null) break; // Object has no base class
                            baseClass = baseClass.BaseClasses.SingleOrDefault().ResolvedType;
                            if (baseClass == null) break;
                        }
                    }
                }
                if (entryMethodName == null) return null;

                // Now, find the entry method (potentially walking up the inheritance hierarchy again, stopping
                // point is not necessarily the same as the class found in the walk above).
                var entryMethod = c.Methods.Where(m => m.Name.Value == entryMethodName).SingleOrDefault();
                var baseClass2 = c.ResolvedType;
                while (entryMethod == null)
                {
                    var baseType2 = baseClass2.BaseClasses.SingleOrDefault();
                    if (baseType2 == null) break;
                    baseClass2 = baseType2.ResolvedType;
                    entryMethod = baseClass2.Methods.Where(m => m.Name.Value == entryMethodName).SingleOrDefault();
                }
                return entryMethod;
            }
            return null;
        }

        private static void CreateHostAndProgram(string inputPath, bool interProc, out MyLoader loader, out ScopeProgramAnalysis program, 
                                                out IAssembly loadedAssembly)
        {
            // Determine whether to use Interproc analysis
            AnalysisOptions.DoInterProcAnalysis = interProc;

            var host = new PeReader.DefaultHost();

            if (inputPath.StartsWith("file:")) // Is there a better way to tell that it is a Uri?
            {
                inputPath = new Uri(inputPath).LocalPath;
            }
            var d = Path.GetDirectoryName(Path.GetFullPath(inputPath));
            if (!Directory.Exists(d))
                throw new InvalidOperationException("Can't find directory from path: " + inputPath);

            loader = new MyLoader(host, d);

            Types.Initialize(host);

            loadedAssembly = loader.LoadMainAssembly(inputPath);

            program = new ScopeProgramAnalysis(loader);
        }

        private static Dictionary<IMethodDefinition, Tuple<DependencyPTGDomain, TimeSpan, ISet<TraceableColumn>, ISet<TraceableColumn>, ScopeAnalyzer.Analyses.ColumnsDomain, TimeSpan>> previousResults = new Dictionary<IMethodDefinition, Tuple<DependencyPTGDomain, TimeSpan, ISet<TraceableColumn>, ISet<TraceableColumn>, ScopeAnalyzer.Analyses.ColumnsDomain, TimeSpan>>();
        private static bool AnalyzeProcessor(
            string inputPath,
            MyLoader loader,
            InterproceduralManager interprocAnalysisManager,
            IDictionary<string, ITypeDefinition> factoryReducerMap,
            ITypeDefinition processorClass,
            IMethodDefinition entryMethodDef,
            IMethodDefinition moveNextMethod,
            IMethodDefinition getEnumMethod,
            IMethodDefinition factoryMethod,
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
                ScopeAnalyzer.Analyses.ColumnsDomain bagOColumnsUsedColumns;
                TimeSpan depAnalysisTime;
                TimeSpan bagOColumnsTime;

                Tuple<DependencyPTGDomain, TimeSpan, ISet<TraceableColumn>, ISet<TraceableColumn>, ScopeAnalyzer.Analyses.ColumnsDomain, TimeSpan> previousResult;
                if (previousResults.TryGetValue(moveNextMethod, out previousResult))
                {
                    depAnalysisResult = previousResult.Item1;
                    depAnalysisTime = previousResult.Item2;
                    inputColumns = previousResult.Item3;
                    outputColumns = previousResult.Item4;
                    bagOColumnsUsedColumns = previousResult.Item5;
                    bagOColumnsTime = previousResult.Item6;

                } else {

                    var dependencyAnalysis = new SongTaoDependencyAnalysis(loader, interprocAnalysisManager, moveNextMethod, entryMethodDef, getEnumMethod);
                    var tup = dependencyAnalysis.AnalyzeMoveNextMethod();
                    depAnalysisResult = tup.Item1;
                    depAnalysisTime = tup.Item2;
                    inputColumns = dependencyAnalysis.InputColumns;
                    outputColumns = dependencyAnalysis.OutputColumns;

                    var a = TypeHelper.GetDefiningUnit(processorClass) as IAssembly;
                    var z = ScopeAnalyzer.ScopeAnalysis.AnalyzeMethodWithBagOColumnsAnalysis(loader.Host, a, Enumerable<IAssembly>.Empty, moveNextMethod);
                    bagOColumnsUsedColumns = z.UsedColumnsSummary ?? ScopeAnalyzer.Analyses.ColumnsDomain.Top;
                    bagOColumnsTime = z.ElapsedTime;

                    previousResults.Add(moveNextMethod, Tuple.Create(depAnalysisResult, depAnalysisTime, inputColumns, outputColumns, bagOColumnsUsedColumns, bagOColumnsTime));
                }
                var producesAnalyzer = new ProducesMethodAnalyzer(loader, processorClass);
                var overApproximatedPassthrough = producesAnalyzer.InferAnnotations(inputSchema);

                var r = CreateResultsAndThenRun(inputPath, processorClass, entryMethodDef, moveNextMethod, factoryMethod, depAnalysisResult, depAnalysisTime, inputSchema, inputColumns, outputColumns, factoryReducerMap, bagOColumnsUsedColumns, bagOColumnsTime);
                runResult = r;

                return true;
            }
            catch (Exception e)
            {
                var id = String.Format("[{0}] {1}", TypeHelper.GetDefiningUnit(processorClass).Name.Value, processorClass.FullName());
                var r = CreateRun(inputPath, id, String.Format(CultureInfo.InvariantCulture, "Thrown exception {0}\n{1}", e.Message, e.StackTrace.ToString()), new List<Result>());
                runResult = r;
                return true;

                //var body = MethodBodyProvider.Instance.GetBody(moveNextMethod);
                //errorReason = new AnalysisReason(moveNextMethod, body.Instructions[0],
                //                String.Format(CultureInfo.InvariantCulture, "Thrown exception {0}\n{1}", e.Message, e.StackTrace.ToString()));
                //return false;
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

        public class DependencyStats
        {
            public int SchemaInputColumnsCount;
            public int SchemaOutputColumnsCount;

            // Diego's analysis
            public long DependencyTime; // in milliseconds
            public List<Tuple<string, string>> PassThroughColumns = new List<Tuple<string, string>>();
            public List<string> UnreadInputs = new List<string>();
            public bool TopHappened;
            public bool OutputHasTop;
            public bool InputHasTop;
            public int ComputedInputColumnsCount;
            public int ComputedOutputColumnsCount;
            public bool Error;
            public string ErrorReason;
            public string DeclaredPassthroughColumns;
            public string DeclaredDependencies;

            // Zvonimir's analysis
            public long UsedColumnTime; // in milliseconds
            public bool UsedColumnTop;
            public string UsedColumnColumns;

            public int NumberUsedColumns { get; internal set; }
            public List<string> UnWrittenOutputs = new List<string>();
            public ISet<string> UnionColumns = new HashSet<string>();
            public bool ZvoTop;
        }

        public static IEnumerable<Tuple<string, DependencyStats>> ExtractDependencyStats(SarifLog log)
        {
            var dependencyStats = new List<Tuple<string, DependencyStats>>();
            foreach (var run in log.Runs)
            {
                var tool = run.Tool.Name;
                if (tool != "ScopeProgramAnalysis") continue;
                var splitId = run.Id.Split('|');

                var processNumber = "0";
                var processorName = "";
                if (splitId.Length == 2)
                {
                    processorName = splitId[0];
                    processNumber = splitId[1];
                }
                else
                {
                    processorName = run.Id;
                }


                if (processorName == "No results")
                {
                    var ret2 = new DependencyStats();
                    ret2.Error = true;
                    ret2.ErrorReason = String.Join(",", run.ToolNotifications.Select(e => e.Message));
                    dependencyStats.Add(Tuple.Create(processorName, ret2));
                    continue;
                }

                var ret = new DependencyStats();

                var visitedColumns = new HashSet<string>();
                var inputColumnsRead = new HashSet<string>();
                if (run.Results.Any())
                {
                    foreach (var result in run.Results)
                    {
                        if (result.Id == "SingleColumn")
                        {
                            var columnProperty = result.GetProperty("column");
                            if (!columnProperty.StartsWith("Col(")) continue;
                            var columnName = columnProperty.Contains(",") ? columnProperty.Split(',')[1].Trim('"', ')') : columnProperty;
                            if (columnName == "_All_")
                            {
                                // ignore this for now because it is more complicated
                                continue;
                            }
                            if (columnName == "_TOP_")
                            {
                                ret.TopHappened = true;
                            }
                            if (visitedColumns.Contains(columnName))
                                continue;

                            visitedColumns.Add(columnName);

                            var dataDependencies = result.GetProperty<List<string>>("data depends");
                            if (dataDependencies.Count == 1)
                            {
                                var inputColumn = dataDependencies[0];
                                if (!inputColumn.StartsWith("Col(Input"))
                                {
                                    // then it is dependent on only one thing, but that thing is not a column.
                                    continue;
                                }
                                if (inputColumn.Contains("TOP"))
                                {
                                    // a pass through column cannot depend on TOP
                                    continue;
                                }
                                // then it is a pass-through column
                                var inputColumnName = inputColumn.Contains(",") ? inputColumn.Split(',')[1].Trim('"', ')') : inputColumn;
                                ret.PassThroughColumns.Add(Tuple.Create(columnName, inputColumnName));
                            }
                        }
                        else if (result.Id == "Summary")
                        {
                            // Do nothing
                            var columnProperty = result.GetProperty<List<string>>("Inputs");
                            var totalInputColumns = columnProperty.Count;
                            ret.InputHasTop = columnProperty.Contains("Col(Input,_TOP_)") || columnProperty.Contains("_TOP_");
                            var inputColumns = columnProperty.Select(x => x.Contains(",") ? x.Split(',')[1].Trim('"', ')') : x);
                            ret.ComputedInputColumnsCount = inputColumns.Count();


                            columnProperty = result.GetProperty<List<string>>("Outputs");
                            var totalOutputColumns = columnProperty.Count;
                            var outputColumns = columnProperty.Select(x => x.Contains(",") ? x.Split(',')[1].Trim('"', ')') : x);

                            ret.OutputHasTop = columnProperty.Contains("Col(Output,_TOP_)") || columnProperty.Contains("_TOP_");
                            ret.ComputedOutputColumnsCount = totalOutputColumns;

                            columnProperty = result.GetProperty<List<string>>("SchemaInputs");
                            ret.SchemaInputColumnsCount = columnProperty.Count;
                            if (!ret.InputHasTop)
                            {
                                ret.UnreadInputs = columnProperty.Where(schemaInput => !inputColumns.Contains(schemaInput)).ToList();
                            }


                            columnProperty = result.GetProperty<List<string>>("SchemaOutputs");
                            ret.SchemaOutputColumnsCount = columnProperty.Count;
                            if (!ret.InputHasTop)
                            {
                                ret.UnWrittenOutputs = columnProperty.Where(schemaOuput => !outputColumns.Contains(schemaOuput)).ToList();
                            }


                            ret.UnionColumns = new HashSet<string>();
                            var schemaOutputs = result.GetProperty<List<string>>("SchemaOutputs").Select(c => c.Contains("[") ? c.Substring(0, c.IndexOf('[')) : c);
                            var schemaInputs = result.GetProperty<List<string>>("SchemaInputs").Select(c => c.Contains("[") ? c.Substring(0, c.IndexOf('[')) : c);
                            ret.UnionColumns.AddRange(schemaInputs);
                            ret.UnionColumns.UnionWith(schemaOutputs);


                            ret.UsedColumnTop = result.GetProperty<bool>("UsedColumnTop");
                            ret.TopHappened |= result.GetProperty<bool>("DependencyAnalysisTop");
                            ret.DeclaredPassthroughColumns = result.GetProperty("DeclaredPassthrough");
                            ret.DeclaredDependencies = result.GetProperty("DeclaredDependency");

                            ret.DependencyTime = result.GetProperty<long>("DependencyAnalysisTime");

                            ret.UsedColumnColumns = result.GetProperty("BagOColumns");
                            int nuo = 0;
                            if (!result.TryGetProperty<int>("BagNOColumns", out nuo))
                            {
                                ret.NumberUsedColumns = ret.UnionColumns.Count;
                                ret.ZvoTop = true;
                            }
                            else
                            {
                                ret.ZvoTop = ret.UsedColumnColumns == "All columns used.";
                                ret.NumberUsedColumns = ret.ZvoTop ? ret.UnionColumns.Count : nuo;

                            }
                            ret.UsedColumnTime = result.GetProperty<long>("BagOColumnsTime");
                        }
                    }
                }
                else
                {
                    if (run.ToolNotifications.Any())
                    {
                        ret.Error = true;
                        ret.ErrorReason = String.Join(",", run.ToolNotifications.Select(e => e.Message));
                    }
                }
                dependencyStats.Add(Tuple.Create(processorName, ret));
            }
            return dependencyStats;
        }


        private static Run CreateResultsAndThenRun(string inputPath, ITypeDefinition processorClass, IMethodDefinition entryMethod, IMethodDefinition moveNextMethod, IMethodDefinition factoryMethod,
            DependencyPTGDomain depAnalysisResult,
            TimeSpan depAnalysiTime,
            Schema inputSchema,
            ISet<TraceableColumn> inputColumns, ISet<TraceableColumn> outputColumns, IDictionary<string, ITypeDefinition> processorMap,
            ScopeAnalyzer.Analyses.ColumnsDomain bagOColumnsUsedColumns,
            TimeSpan bagOColumnsTime
            )
        {
            var results = new List<Result>();

            var escapes = depAnalysisResult.Dependencies.A1_Escaping.Select(traceable => traceable.ToString());

            var inputUses = new HashSet<Traceable>();
            var outputModifies = new HashSet<Traceable>();

                

            string declaredPassthroughString = "";
            string declaredDependencyString = "";
            if (factoryMethod != null)
            {
                try
                {
                    var resultOfProducesMethod = ExecuteProducesMethod(factoryMethod, inputSchema);
                    var declaredPassThroughDictionary = resultOfProducesMethod.Item1;
                    declaredPassthroughString = String.Join("|", declaredPassThroughDictionary.Select(e => e.Key + " <: " + e.Value));
                    var dependenceDictionary = resultOfProducesMethod.Item2;
                    declaredDependencyString = String.Join("|", dependenceDictionary.Select(e => e.Key + " <: " + e.Value));
                } catch (Exception e)
                {
                    declaredPassthroughString = "Exception while trying to execute produces method: " + e.Message;
                }
            } else
            {
                declaredPassthroughString = "Null Factory Method";
            }

            var inputSchemaString = InputSchema.Columns.Select(t => t.ToString());
            var outputSchemaString = OutputSchema.Columns.Select(t => t.ToString());

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

                var inputsString = inputColumns.Select(t => t.ToString());
                var outputsString = outputColumns.Select(t => t.ToString());
                resultSummary.SetProperty("Inputs", inputsString);
                resultSummary.SetProperty("Outputs", outputsString);

                resultSummary.SetProperty("SchemaInputs", inputSchemaString);
                resultSummary.SetProperty("SchemaOutputs", outputSchemaString);

                // Cannot compare the dependency analysis and the used-column analysis.
                // It can be that D <= UC or that UC <= D. (Where <= means the partial order where
                // any result is less-than-or-equal to "top".)
                // So just set a property for each as to whether they returned "top".
                resultSummary.SetProperty("UsedColumnTop", bagOColumnsUsedColumns.IsTop);
                resultSummary.SetProperty("DependencyAnalysisTop", depAnalysisResult.IsTop);

                //// Comparison means that the results are consistent, *not* that they are equal.
                //// In particular, the dependency analysis may be able to return a (non-top) result
                //// when the used-column analysis cannot.
                //if (!bagOColumnsUsedColumns.IsTop && !bagOColumnsUsedColumns.IsBottom)
                //{
                //    var a = bagOColumnsUsedColumns.Elements.Select(e => e.Value.ToString());
                //    var b = inputColumns.Union(outputColumns).Select(tc => tc.Column).Distinct();
                //    var compareResults = Util.SetEqual(a, b, (x, y) => Util.ColumnNameMatches(x, y));
                //    resultSummary.SetProperty("Comparison", compareResults);
                //} else if (bagOColumnsUsedColumns.IsTop)
                //{
                //    // The used column analysis is more conservative than the dependency analysis.
                //    // Top is less-equal-to any other result
                //    resultSummary.SetProperty("Comparison", true);
                //}
                //else
                //{
                //    // Should look into why this might be the case.
                //    resultSummary.SetProperty("Comparison", false);
                //}
                resultSummary.SetProperty("BagOColumns", bagOColumnsUsedColumns.ToString());
                resultSummary.SetProperty("BagNOColumns", bagOColumnsUsedColumns.Count);

                resultSummary.SetProperty("DeclaredPassthrough", declaredPassthroughString);
                resultSummary.SetProperty("DeclaredDependency", declaredDependencyString);

                resultSummary.SetProperty("BagOColumnsTime", (int) bagOColumnsTime.TotalMilliseconds);
                resultSummary.SetProperty("DependencyAnalysisTime", (int) depAnalysiTime.TotalMilliseconds);

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
                resultEmpty.SetProperty("Inputs", new List<string>() { "_TOP_" });
                resultEmpty.SetProperty("Outputs", new List<string>() { "_TOP_" });
                resultEmpty.SetProperty("SchemaInputs", inputSchemaString);
                resultEmpty.SetProperty("SchemaOutputs", outputSchemaString);
                resultEmpty.SetProperty("UsedColumnTop", bagOColumnsUsedColumns.IsTop);
                resultEmpty.SetProperty("DependencyAnalysisTop", false);
                resultEmpty.SetProperty("BagOColumns", bagOColumnsUsedColumns.ToString());
                resultEmpty.SetProperty("BagNOColumns", bagOColumnsUsedColumns.Count);
                resultEmpty.SetProperty("DeclaredPassthrough", declaredPassthroughString);
                resultEmpty.SetProperty("DeclaredDependency", declaredDependencyString);
                resultEmpty.SetProperty("BagOColumnsTime", (int)bagOColumnsTime.TotalMilliseconds);
                resultEmpty.SetProperty("DependencyAnalysisTime", (int)depAnalysiTime.TotalMilliseconds);
                results.Add(resultEmpty);
            }

            var actualProcessorClass = entryMethod.ContainingType;

            var id = String.Format("[{0}] {1}", TypeHelper.GetDefiningUnit(processorClass).Name.Value, processorClass.FullName());

            // Very clumsy way to find the process number and the processor name from the MoveNext method.
            // But it is the process number and processor name that allow us to link these results to the information
            // in the XML file that describes the job.
            // Climb the containing type chain from the MoveNext method until we find the entry in the dictionary whose
            // value matches one of the classes.
            var done = false;
            foreach (var kv in processorMap)
            {
                if (done) break;
                var c = moveNextMethod.ContainingType.ResolvedType;
                while (c != null)
                {
                    if (kv.Value == c)
                    {
                        id = kv.Value.FullName() + "|" + kv.Key;
                        done = true;
                        break;
                    }
                    if (c is INestedTypeDefinition)
                    {
                        c = (c as INestedTypeDefinition).ContainingTypeDefinition;
                    }
                    else
                    {
                        c = null;
                    }
                }
            }

            string actualClassContainingIterator = null;
            if (processorClass != actualProcessorClass)
                actualClassContainingIterator = "Analyzed processsor: " + actualProcessorClass.FullName();

            var r = CreateRun(inputPath, id, actualClassContainingIterator, results);
            return r;
        }

        private static Tuple<Dictionary<string, string>, Dictionary<string, string>> ExecuteProducesMethod(IMethodDefinition factoryMethod, Schema inputSchema)
        {
            var sourceDictionary = new Dictionary<string, string>();
            var dependenceDictionary = new Dictionary<string, string>();

            if (factoryMethod == null) { sourceDictionary.Add("666", "no factoryMethod"); goto L; }
            // Call the factory method to get an instance of the processor.
            var factoryClass = factoryMethod.ContainingType;
            var assembly = System.Reflection.Assembly.LoadFrom(TypeHelper.GetDefiningUnitReference(factoryClass).ResolvedUnit.Location);
            if (assembly == null) { sourceDictionary.Add("666", "no assembly"); goto L; }
            var factoryClass2 = assembly.GetType(TypeHelper.GetTypeName(factoryClass, NameFormattingOptions.UseReflectionStyleForNestedTypeNames));
            if (factoryClass2 == null) { sourceDictionary.Add("666", "no factoryClass2"); goto L; }
            var factoryMethod2 = factoryClass2.GetMethod(factoryMethod.Name.Value);
            if (factoryMethod2 == null) { sourceDictionary.Add("666", "no factoryMethod2" + " (" + factoryMethod.Name.Value + ")"); goto L; }
            object instance = null;
            try
            {
                instance = factoryMethod2.Invoke(null, null);
            }
            catch (System.Reflection.TargetInvocationException e)
            {
                sourceDictionary.Add("666", "At least one missing assembly: " + e.Message); goto L;
            }
            if (instance == null) { sourceDictionary.Add("666", "no instance"); goto L; }
            var producesMethod = factoryMethod2.ReturnType.GetMethod("Produces");
            if (producesMethod == null) { sourceDictionary.Add("666", "no producesMethod"); goto L; }
            // Schema Produces(string[] columns, string[] args, Schema input)
            try
            {
                string[] arg1 = null;
                string[] arg2 = null;
                var inputSchemaAsString = String.Join(",", inputSchema.Columns.Select(e => e.Name + ": " + e.Type));
                var inputSchema2 = new ScopeRuntime.Schema(inputSchemaAsString);
                var specifiedOutputSchema = producesMethod.Invoke(instance, new object[] { arg1, arg2, inputSchema2, });
                if (specifiedOutputSchema == null) { sourceDictionary.Add("666", "no specifiedOutputSchema"); goto L; }
                foreach (var column in ((ScopeRuntime.Schema)specifiedOutputSchema).Columns)
                {
                    if (column.Source != null)
                        sourceDictionary.Add(column.Name, column.Source.Name);
                }
                var allowColumnPruningMethod = factoryMethod2.ReturnType.GetMethod("get_AllowColumnPruning");
                if (allowColumnPruningMethod != null)
                {
                    var columnPruningAllowed = (bool)allowColumnPruningMethod.Invoke(instance, null);
                    if (columnPruningAllowed)
                    {
                        foreach (var column in ((ScopeRuntime.Schema)specifiedOutputSchema).Columns)
                        {
                            if (column.Dependency != null)
                            {
                                dependenceDictionary.Add(column.Name, String.Join("+", column.Dependency.Keys.Select(e => e.Name)));
                            }
                        }
                    }
                }
            }
            catch
            {
                sourceDictionary.Add("666", "exception during Produces");
            }
            L:
            return Tuple.Create(sourceDictionary, dependenceDictionary);
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
        private IEnumerable<Tuple<ITypeDefinition, IMethodDefinition, IMethodDefinition, IMethodDefinition, IMethodDefinition>> ObtainScopeMethodsToAnalyze(IAssembly assembly, out List<Tuple<ITypeDefinition, string>> errorMessages)
        {
            errorMessages = new List<Tuple<ITypeDefinition, string>>();

            var processorsToAnalyze = new HashSet<ITypeDefinition>();

            var scopeMethodTuplesToAnalyze = new HashSet<Tuple<ITypeDefinition, IMethodDefinition, IMethodDefinition, IMethodDefinition, IMethodDefinition>>();

            var operationFactoryClass = assembly.GetAllTypes().OfType<INamedTypeDefinition>()
                                        .Where(c => c.Name.Value == "__OperatorFactory__" && c is INestedTypeDefinition &&  (c as INestedTypeDefinition).ContainingType.GetName() == "___Scope_Generated_Classes___").SingleOrDefault();

            if (operationFactoryClass == null)
                return new HashSet<Tuple<ITypeDefinition, IMethodDefinition, IMethodDefinition, IMethodDefinition, IMethodDefinition>>();

            // Hack: use actual ScopeRuntime Types
            var factoryMethods = operationFactoryClass.Methods.Where(m => m.Name.Value.StartsWith("Create_Process_", StringComparison.Ordinal)
                            /*&& m.ReturnType.ToString() == this.ClassFilter*/);

            // var referencesLoaded = false;
            if (!factoryMethods.Any())
            {
                errorMessages.Add(Tuple.Create((ITypeDefinition)null, "No factory methods found"));
            }

            foreach (var factoryMethod in factoryMethods)
            {
                try
                {
                    var nonNullEntryMethods = factoryMethod.Body.Operations
                        .Where(op => op.OperationCode == OperationCode.Newobj)
                        .Select(e => e.Value)
                        .OfType<IMethodReference>()
                        .Select(e => FindEntryMethod(loader.RuntimeTypes.concurrentProcessor, e.ContainingType.ResolvedType))
                        .Where(e => e != null && !(e is Dummy))
                        ;
                    if (nonNullEntryMethods.Count() != 1)
                    {
                        continue;
                    }
                    var entryMethod = nonNullEntryMethods.First();
                    var reducerClassDefinition = entryMethod.ContainingTypeDefinition;

                    var isCompilerGenerated = compilerGeneretedMethodMatchers.Any(regex => regex.IsMatch(reducerClassDefinition.FullName()));

                    if (isCompilerGenerated)
                        continue;

                    if (processorsToAnalyze.Contains(reducerClassDefinition))
                        continue;

                    processorsToAnalyze.Add(reducerClassDefinition);

                    // Closure classes are always named types. Using the type case means the Name property is defined.
                    var candidateClosures = reducerClassDefinition.NestedTypes.OfType<INamedTypeDefinition>()
                                   .Where(c => this.ClousureFilters.Any(filter => c.Name.Value.StartsWith(filter)));
                    if (!candidateClosures.Any())
                    {
                        errorMessages.Add(Tuple.Create(reducerClassDefinition, "Iterator not found"));
                        continue;
                    }
                    foreach (var candidateClosure in candidateClosures)
                    {
                        var getEnumMethods = candidateClosure.Methods
                                                    .Where(m => m.Name.Value == ScopeAnalysisConstants.SCOPE_ROW_ENUMERATOR_METHOD);
                        var getEnumeratorMethod = getEnumMethods.Single();

                        var moveNextMethods = candidateClosure.Methods
                                                    .Where(md => md.Body != null && md.Name.Value.Equals(this.MethodUnderAnalysisName));
                        foreach (var moveNextMethod in moveNextMethods)
                        {
                            scopeMethodTuplesToAnalyze.Add(Tuple.Create(reducerClassDefinition, entryMethod, moveNextMethod, getEnumeratorMethod, factoryMethod));

                            // TODO: Hack for reuse. Needs refactor
                            if (factoryMethod != null)
                            {
                                var processID = factoryMethod.Name.Value.Substring(factoryMethod.Name.Value.IndexOf("Process_"));
                                this.factoryReducerMap.Add(processID, entryMethod.ContainingType as ITypeDefinition);
                            }
                        }
                    }
                }
                catch (Exception e)
                {
                    AnalysisStats.TotalofDepAnalysisErrors++;
                    Console.WriteLine("Error in Dependency Analysis", e.Message);
                    errorMessages.Add(Tuple.Create((ITypeDefinition)operationFactoryClass, "Exception occurred while looking for processors"));
                }

            }
            return scopeMethodTuplesToAnalyze;
        }

        public IEnumerable<Tuple<ITypeDefinition, IMethodDefinition, IMethodDefinition, IMethodDefinition, IMethodDefinition>> ObtainScopeMethodsToAnalyzeFromAssemblies()
        {
            var scopeMethodTuplesToAnalyze = new HashSet<Tuple<ITypeDefinition, IMethodDefinition, IMethodDefinition, IMethodDefinition, IMethodDefinition>>();

            var alreadyLoadedAssemblies = new List<IAssembly>(host.LoadedUnits.OfType<IAssembly>());
            var candidateClasses = alreadyLoadedAssemblies
                .Where(a => a.Name.Value != "mscorlib")
                .SelectMany(a => a.GetAllTypes().OfType<ITypeDefinition>())
                ;
            if (candidateClasses.Any())
            {
                var results = new List<Result>();
                foreach (var candidateClass in candidateClasses)
                {
                    var isCompilerGenerated = compilerGeneretedMethodMatchers.Any(regex => regex.IsMatch(candidateClass.FullName()));

                    if (isCompilerGenerated)
                        continue;

                    var entryMethod = FindEntryMethod(loader.RuntimeTypes.concurrentProcessor, candidateClass);
                    if (entryMethod == null) continue;

                    var containingType = entryMethod.ContainingType.ResolvedType;
                    if (containingType == null) continue;
                    var candidateClousures = containingType.NestedTypes.OfType<INamedTypeDefinition>()
                                    .Where(c => this.ClousureFilters.Any(filter => c.Name.Value.StartsWith(filter)));
                    foreach (var candidateClousure in candidateClousures)
                    {
                        var methods = candidateClousure.Members.OfType<IMethodDefinition>()
                                                .Where(md => md.Body != null
                                                && md.Name.Value.Equals(this.MethodUnderAnalysisName));

                        if (methods.Any())
                        {
                            var moveNextMethod = methods.First();
                            // BUG: Really should do this by getting the name of the Row type used by the processor, but just a quick hack for now to allow unit testing (which uses a different Row type).
                            // System.Collections.Generic.IEnumerable<FakeRuntime.Row>.GetEnumerator
                            // And really, this should be a type test anyway. The point is to find the explicit interface implementation of IEnumerable<T>.GetEnumerator.
                            var getEnumMethods = candidateClousure.Methods
                                                        .Where(m => m.Name.Value.StartsWith("System.Collections.Generic.IEnumerable<") && m.Name.Value.EndsWith(">.GetEnumerator"));
                            var getEnumeratorMethod = getEnumMethods.First();

                            scopeMethodTuplesToAnalyze.Add(Tuple.Create(candidateClass, entryMethod, moveNextMethod, getEnumeratorMethod, (IMethodDefinition) null));

                        }
                    }
                }
            }
            return scopeMethodTuplesToAnalyze;
        }

        private void ComputeMethodsToAnalyzeForReducerClass(HashSet<Tuple<IMethodDefinition, IMethodDefinition, IMethodDefinition>> scopeMethodPairsToAnalyze,
            IMethodDefinition factoryMethdod, INamedTypeReference reducerClass)
        {
        }

        //private INamedTypeDefinition ResolveClass(INamedTypeReference classToResolve)
        //{
        //    var resolvedClass = host.ResolveReference(classToResolve) as INamedTypeDefinition;
        //    if(resolvedClass == null)
        //    {
        //        try
        //        {
        //            AnalysisStats.TotalDllsFound++;
        //            loader.TryToLoadReferencedAssembly(classToResolve.ContainingAssembly);
        //            resolvedClass = host.ResolveReference(classToResolve) as INamedTypeDefinition;
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
                    var className = kv.Value.FullName();
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
            } else
            {
                throw new FileNotFoundException("Cannot find ScopeVertexDef.xml");
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
            var fileData = FileData.Create(new Uri(fileDataKey, UriKind.RelativeOrAbsolute), false);
            run.Files.Add(fileDataKey, fileData);
            run.ToolNotifications = new List<Notification>();
            if (!String.IsNullOrWhiteSpace(notification))
                run.ToolNotifications.Add(new Notification { Message = notification, });

            run.Results = results;

            return run;
        }
        public static void WriteSarifOutput(SarifLog log, string outputFilePath)
        {
            string sarifText = SarifLogToString(log);
            try
            {
                //if (!File.Exists(outputFilePath))
                //{
                //    File.CreateText(outputFilePath);
                //}
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
