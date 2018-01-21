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
using static ScopeProgramAnalysis.ScopeProgramAnalysis;

namespace ScopeProgramAnalysis
{
    public class Program
    {
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
            var task = Task.Run(() => ScopeProgramAnalysis.AnalyzeDll2(inputPath, kind, useScopeFactory, interProc, outputStream));
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

     
        public static Run AnalyzeProcessor(Type processorType, string inputSchema, string outputSchema)
        {
            var inputPath = processorType.Assembly.Location;

            MyLoader loader;
            ScopeProgramAnalysis scopeProgramAnalyzer;
            IAssembly assembly;
            CreateHostAndProgram(inputPath, false, out loader, out scopeProgramAnalyzer, out assembly);

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

            var ok = scopeProgramAnalyzer.AnalyzeProcessor(inputPath, loader, processorClass, entryMethod, moveNextMethod, getEnumMethod, null, i, o, out run, out errorReason);
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

        private static Tuple<Dictionary<string, string>, Dictionary<string, string>> ExecuteProducesMethod(ITypeDefinition processorClass, IMethodDefinition factoryMethod, Schema inputSchema)
        {
            var sourceDictionary = new Dictionary<string, string>();
            var dependenceDictionary = new Dictionary<string, string>();

            var processorAssembly = System.Reflection.Assembly.LoadFrom(TypeHelper.GetDefiningUnitReference(processorClass).ResolvedUnit.Location);
            if (processorAssembly == null) { sourceDictionary.Add("666", "no processorAssembly"); goto L; }
            var processorClass2 = processorAssembly.GetType(TypeHelper.GetTypeName(processorClass, NameFormattingOptions.UseReflectionStyleForNestedTypeNames));
            if (processorClass2 == null) { sourceDictionary.Add("666", "no processorClass2"); goto L; }
            var finalizeMethod = processorClass2.GetMethod("Finalize", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            if (finalizeMethod != null) { sourceDictionary.Add("666", "Finalize() method found for processor: " + TypeHelper.GetTypeName(processorClass)); goto L; }


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
