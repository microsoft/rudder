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

namespace ScopeProgramAnalysis
{
    class Program
    {
        private Host host;
        private IDictionary<string, ClassDefinition> factoryReducerMap;
        private Loader loader;

        public Assembly ScopeGenAssembly { get; private set; }
        public IEnumerable<string> ReferenceFiles { get; private set; }
        public string ClassFilter { get; private set; }
        public string EntryMethod { get; private set; }
        public string ClousureFilter { get; private set; }
        public string MethodUnderAnalysisName { get; private set; }
        public static MethodCFGCache MethodCFGCache { get; private set; }

        public Program(Host host, Loader loader)
        {
            this.host = host;
            this.loader = loader;
            this.factoryReducerMap = new Dictionary<string, ClassDefinition>();
        }

        static void Main(string[] args)
        {
            //const string root = @"C:\Users\t-diga\Source\Repos\ScopeExamples\ILAnalyzer\"; // @"..\..\..";
            //const string input = root + @"\bin\Debug\ILAnalyzer.exe";

            //const string root = @"c:\users\t-diga\source\repos\scopeexamples\metting\";
            
            //const string input = @"D:\MadanExamples\3213e974-d0b7-4825-9fd4-6068890d3327\__ScopeCodeGen__.dll";

            const string input = @"\\research\root\public\mbarnett\Parasail\ExampleWithXML\69FDA6E7DB709175\ScopeMapAccess_4D88E34D25958F3B\__ScopeCodeGen__.dll";

            string[] directories = Path.GetDirectoryName(input).Split(Path.DirectorySeparatorChar);
            var outputPath = Path.Combine(@"D:\Temp\", directories.Last()) + "_" + Path.ChangeExtension(Path.GetFileName(input), ".sarif");

            AnalyzeOneDll(input, outputPath, ScopeMethodKind.Reducer);

            //AnalyzeScopeScript(new string[] { @"D:\ScriptExamples\Files", @"D:\Temp\", "Reducer" } );

            // AnalyzeScopeScript(new string[] { @"D:\MadanExamples\", @"D:\Temp\", "Reducer" });
            
            System.Console.ReadKey();

        }

        enum ScopeMethodKind { Producer, Reducer };

        private static void AnalyzeOneDll(string input, string outputPath, ScopeMethodKind kind)
        {
            var folder = Path.GetDirectoryName(input);
            var referenceFiles = Directory.GetFiles(folder, "*.dll", SearchOption.TopDirectoryOnly).Where(fp => Path.GetFileName(fp).ToLower(CultureInfo.InvariantCulture)!= Path.GetFileName(input).ToLower(CultureInfo.InvariantCulture)).ToList();
            referenceFiles.AddRange(Directory.GetFiles(folder, "*.exe", SearchOption.TopDirectoryOnly));
            AnalyzeDll(input, referenceFiles, outputPath, ScopeMethodKind.Reducer);
        }

        private static void AnalyzeDll(string inputPath, IEnumerable<string> referenceFiles, string outputPath, ScopeMethodKind kind)
        {
            var host = new Host();
            PlatformTypes.Resolve(host);

            var loader = new Loader(host);
            var scopeGenAssembly = loader.LoadAssembly(inputPath);
            loader.SetMainAssembly(inputPath);

            Program.MethodCFGCache = new Backend.Analyses.MethodCFGCache(host);

            // LoadExternalReferences(referenceFiles, loader);
            //loader.LoadCoreAssembly();

            var program = new Program(host, loader);

            program.ScopeGenAssembly = scopeGenAssembly;
            program.ReferenceFiles = referenceFiles;

            program.ClassFilter = "";
            program.ClousureFilter = "<Reduce>d__";
            program.EntryMethod = "Reduce";

            //var classUnderAnalysisPrefix = "<Reduce>d__";

            if (kind == ScopeMethodKind.Reducer)
            {
                program.ClassFilter = "Reducer";
                //classUnderAnalysisPrefix = "<Reduce>d__";
            }
            else
            {
                program.ClassFilter = "Producer";
                program.ClousureFilter = "<Produce>d__";
                program.EntryMethod = "Produce";
                //classUnderAnalysisPrefix = "<Produce>d__";
            }
            program.MethodUnderAnalysisName = "MoveNext";

            var scopeMethodPairs = program.ObtainScopeMethodsToAnalyze();
            var results = new List<Result>();

            if (scopeMethodPairs.Any())
            {
                foreach (var methodPair in scopeMethodPairs)
                {
                    var entryMethodDef = methodPair.Item1;
                    var moveNextMethod = methodPair.Item2;
                    var getEnumMethod= methodPair.Item3;
                    System.Console.WriteLine("Method {0} on class {1}", moveNextMethod.Name, moveNextMethod.ContainingType.FullPathName());
                    var dependencyAnalysis = new SongTaoDependencyAnalysis(host, moveNextMethod, entryMethodDef, getEnumMethod);
                    var depAnalysisResult = dependencyAnalysis.AnalyzeMoveNextMethod();
                    System.Console.WriteLine("Done!");

                    program.ValidateInputSchema(inputPath, moveNextMethod, depAnalysisResult);

                    var escapes = depAnalysisResult.A1_Escaping.Select(traceable => traceable.ToString());

                    if (depAnalysisResult.A4_Ouput.Any())
                    {
                        foreach (var outColum in depAnalysisResult.A4_Ouput.Keys)
                        {
                            var result = new Result();
                            var columnString = depAnalysisResult.A2_Variables[outColum].SingleOrDefault().ToString();
                            var dependsOn = depAnalysisResult.A4_Ouput[outColum].Select(traceable => traceable.ToString());
                            result.SetProperty("column", columnString);
                            result.SetProperty("depends", dependsOn.Union(escapes));
                            results.Add(result);
                        }
                    }
                    else
                    {
                        var result = new Result();
                        result.SetProperty("column", "_ALL_");
                        result.SetProperty("depends", escapes);
                        results.Add(result);
                    }
                    WriteSarifOutput(inputPath, outputPath, results);
                }
            }
            else
            {
                System.Console.WriteLine("No method {0} of type {1} in {2}", program.MethodUnderAnalysisName, program.ClassFilter, inputPath);
            }


            //var candidateClasses = host.Assemblies.SelectMany(a => a.RootNamespace.GetAllTypes().OfType<ClassDefinition>())
            //.Where(c => c.Base != null && c.Base.Name == classFilter);

            //if (candidateClasses.Any())
            //{
            //    var results = new List<Result>();
            //    foreach (var candidateClass in candidateClasses)
            //    {
            //        var assembly = host.Assemblies.Where(a => a.Name == candidateClass.Name);
            //        var candidateClousures = candidateClass.Types.OfType<ClassDefinition>()
            //                        .Where(c => c.Name.StartsWith(clousureFilter));
            //        var methods = candidateClousures.SelectMany(t => t.Members.OfType<MethodDefinition>())
            //                                    .Where(md => md.Body != null
            //                                    && md.Name.Equals(methodUnderAnalysisName));

            //        if (methods.Any())
            //        {
            //            var entryMethodDef = candidateClass.Methods.Where(m => m.Name == entryMethod).Single();
            //            var moveNextMethod = methods.First();
            //            System.Console.WriteLine("Method {0} on class {1}", moveNextMethod.Name, candidateClass.FullPathName());
            //            var dependencyAnalysis = new SongTaoDependencyAnalysis(host, moveNextMethod, entryMethodDef);
            //            var depAnalysisResult = dependencyAnalysis.AnalyzeMoveNextMethod();
            //            System.Console.WriteLine("Done!");

            //            ValidateInputSchema(inputPath, moveNextMethod, depAnalysisResult);

            //            var escapes = depAnalysisResult.A1_Escaping.Select(traceable => traceable.ToString());

            //            if (depAnalysisResult.A4_Ouput.Any())
            //            {
            //                foreach (var outColum in depAnalysisResult.A4_Ouput.Keys)
            //                {
            //                    var result = new Result();
            //                    var columnString = depAnalysisResult.A2_Variables[outColum].SingleOrDefault().ToString();
            //                    var dependsOn = depAnalysisResult.A4_Ouput[outColum].Select(traceable => traceable.ToString());
            //                    result.SetProperty("column", columnString);
            //                    result.SetProperty("depends", dependsOn.Union(escapes));
            //                    results.Add(result);
            //                }
            //            }
            //            else
            //            {
            //                var result = new Result();
            //                result.SetProperty("column", "_ALL_");
            //                result.SetProperty("depends", escapes);
            //                results.Add(result);
            //            }

            //        }
            //        else
            //        {
            //            System.Console.WriteLine("No method {0} on class {1} in {2}", methodUnderAnalysisName, candidateClass.GenericName, inputPath);
            //        }
            //    }
            //    WriteSarifOutput(inputPath, outputPath, results);
            //}
            //else
            //{
            //    System.Console.WriteLine("No {0} class in {1}", kind, inputPath);
            //}
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
                    System.Console.WriteLine("Cannot load {0}", referenceFileName);
                }
            }
        }

        private IEnumerable<Tuple<MethodDefinition, MethodDefinition,MethodDefinition>> ObtainScopeMethodsToAnalyze()
        {
            var scopeMethodPairsToAnalyze = new HashSet<Tuple<MethodDefinition, MethodDefinition, MethodDefinition>>();

            var operationFactoryClass = this.ScopeGenAssembly.RootNamespace.GetAllTypes().OfType<ClassDefinition>()
                                        .Where(c => c.Name == "__OperatorFactory__" && c.ContainingType != null & c.ContainingType.Name == "___Scope_Generated_Classes___").Single();

            // Hack: use actual ScopeRuntime Types
            var factoryMethods = operationFactoryClass.Methods.Where(m => m.Name.StartsWith("Create_Process_", StringComparison.Ordinal) && m.ReturnType.ToString() == this.ClassFilter);

            var referencesLoaded = false;

            foreach (var factoryMethdod in factoryMethods)
            {
                var ins = factoryMethdod.Body.Instructions.OfType<Model.Bytecode.CreateObjectInstruction>().Single();
                var reducerClass = ins.Constructor.ContainingType;

                //if (!referencesLoaded)
                //{
                //    LoadExternalReferences(this.ReferenceFiles, loader);
                //    referencesLoaded = true;
                //}
                ClassDefinition resolvedEntryClass = ResolveClass(reducerClass);
                if (resolvedEntryClass != null)
                {
                    var candidateClousures = resolvedEntryClass.Types.OfType<ClassDefinition>()
                                   .Where(c => c.Name.StartsWith(this.ClousureFilter, StringComparison.Ordinal));
                    foreach (var candidateClousure in candidateClousures)
                    {
                        var moveNextMethods = candidateClousure.Methods
                                                    .Where(md => md.Body != null
                                                    && md.Name.Equals(this.MethodUnderAnalysisName));
                        var getEnumMethods = candidateClousure.Methods
                                                    .Where(m => m.Name == "System.Collections.Generic.IEnumerable<ScopeRuntime.Row>.GetEnumerator");
                        foreach (var moveNextMethod in moveNextMethods)
                        {
                            //var moveNextMethod = methods.Single();
                            var entryMethod = resolvedEntryClass.Methods.Where(m => m.Name == this.EntryMethod).Single();
                            var getEnumeratorMethod = getEnumMethods.Single();
                            scopeMethodPairsToAnalyze.Add(new Tuple<MethodDefinition, MethodDefinition, MethodDefinition>(entryMethod, moveNextMethod, getEnumeratorMethod));
                            var processID = factoryMethdod.Name.Substring(factoryMethdod.Name.IndexOf("Process_", StringComparison.Ordinal));
                            this.factoryReducerMap.Add(processID, entryMethod.ContainingType as ClassDefinition);
                        }
                    }
                }
                else
                { }
            }
            return scopeMethodPairsToAnalyze;
        }

        private ClassDefinition ResolveClass(IBasicType reducerClass)
        {
            var resolvedClass = host.ResolveReference(reducerClass) as ClassDefinition;
            if(resolvedClass == null)
            {
                loader.TryToLoadReferencedAssembly(reducerClass.ContainingAssembly);
                resolvedClass = host.ResolveReference(reducerClass) as ClassDefinition;
            }
            return resolvedClass;
        }

        private void ValidateInputSchema(string inputPath, MethodDefinition method, Backend.Analyses.DependencyDomain dependencyResults)
        {
            var inputDirectory = Path.GetDirectoryName(inputPath);
            var xmlFile = Path.Combine(inputDirectory, "ScopeVertexDef.xml");
            if (File.Exists(xmlFile))
            {
                XElement x = XElement.Load(xmlFile);
                var operators = x.Descendants("operator");
                foreach (var processId in this.factoryReducerMap.Keys)
                {
                    //var reducers = operators.Where(op => op.Attribute("className") != null && op.Attribute("className").Value.StartsWith("ScopeReducer"));
                    var reducers = operators.Where(op => op.Attribute("id") != null && op.Attribute("id").Value==processId);
                    var inputSchemas = reducers.SelectMany(r => r.Descendants("input").Select(i => i.Attribute("schema")), (r, t) => Tuple.Create(r.Attribute("id"), r.Attribute("className"), t));
                    var outputSchemas = reducers.SelectMany(r => r.Descendants("output").Select(i => i.Attribute("schema")), (r, t) => Tuple.Create(r.Attribute("id"), r.Attribute("className"), t));
                }
            }
        }

        private static void WriteSarifOutput(string inputPath, string outputFilePath, IList<Result> results)
        {
            JsonSerializerSettings settings = new JsonSerializerSettings()
            {
                ContractResolver = SarifContractResolver.Instance,
                Formatting = Formatting.Indented
            };

            var run = new Run();
            run.Tool = Tool.CreateFromAssemblyData();
            run.Files = new Dictionary<string, FileData>();
            var fileDataKey = UriHelper.MakeValidUri(inputPath);
            var fileData = FileData.Create(new Uri(fileDataKey), false);
            run.Files.Add(fileDataKey, fileData);

            run.Results = results;
            SarifLog log = new SarifLog()
            {
                Runs = new[] { run }
            };

            var sarifText = JsonConvert.SerializeObject(log, settings);
            File.WriteAllText(outputFilePath, sarifText);
        }

        public static void AnalyzeScopeScript(string[] args)
        {
            var inputFolder = args[0];
            var outputFolder = args[1];
            var kind = args[2];
            const string inputDllName = "__ScopeCodeGen__.dll";
            string[] files = Directory.GetFiles(inputFolder, inputDllName, SearchOption.AllDirectories);
            foreach (var dllToAnalyze in files)
            {
                System.Console.WriteLine("=========================================================================");
                System.Console.WriteLine("Analyzing {0}", dllToAnalyze);
                var folder = Path.GetDirectoryName(dllToAnalyze);
                var referencesPath = Directory.GetFiles(folder, "*.dll", SearchOption.TopDirectoryOnly).Where( fp => Path.GetFileName(fp)!= inputDllName).ToList();
                referencesPath.AddRange(Directory.GetFiles(folder, "*.exe", SearchOption.TopDirectoryOnly));

                string[] directories = folder.Split(Path.DirectorySeparatorChar);
                var outputPath = Path.Combine(outputFolder, directories.Last()) + "_" + Path.ChangeExtension(Path.GetFileName(dllToAnalyze), ".sarif");
                
                //var outputPath = Path.Combine(outputFolder, Path.ChangeExtension(Path.GetFileName(dllToAnalyze),".sarif"));

                AnalyzeDll(dllToAnalyze, referencesPath, outputPath, ScopeMethodKind.Reducer);
                System.Console.WriteLine("=========================================================================");
            }
            System.Console.WriteLine("Done!");
            System.Console.ReadKey();


        }
    }


}
