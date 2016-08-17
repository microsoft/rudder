﻿using Backend.Utils;
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

namespace ScopeProgramAnalysis
{
    public class Program
    {
        private Host host;
        private IDictionary<string, ClassDefinition> factoryReducerMap;
        private Loader loader;
        private InterproceduralManager interprocAnalysisManager;

        public Assembly ScopeGenAssembly { get; private set; }
        public IEnumerable<string> ReferenceFiles { get; private set; }
        public string ClassFilter { get; private set; }
        public string EntryMethod { get; private set; }
        public string ClousureFilter { get; private set; }
        public string MethodUnderAnalysisName { get; private set; }

        public Program(Host host, Loader loader)
        {
            this.host = host;
            this.loader = loader;
            this.factoryReducerMap = new Dictionary<string, ClassDefinition>();
            this.interprocAnalysisManager = new InterproceduralManager(host);
        }

        static void Main(string[] args)
        {
            //const string root = @"c:\users\t-diga\source\repos\scopeexamples\metting\";
            //const string input = root + @"__ScopeCodeGen__.dll";
            //const string input = @"D:\MadanExamples\3213e974-d0b7-4825-9fd4-6068890d3327\__ScopeCodeGen__.dll";

            // Mike example: FileChunker
            //const string input = @"C:\Users\t-diga\Source\Repos\ScopeExamples\ExampleWithXML\69FDA6E7DB709175\ScopeMapAccess_4D88E34D25958F3B\__ScopeCodeGen__.dll";
            //const string input = @"\\research\root\public\mbarnett\Parasail\ExampleWithXML\69FDA6E7DB709175\ScopeMapAccess_4D88E34D25958F3B\__ScopeCodeGen__.dll";

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
            const string input = @"\\madanm2\parasail2\TFS\parasail\ScopeSurvey\AutoDownloader\bin\Debug\8aecff28-5719-4b34-9f9f-cb3135df67d4\__ScopeCodeGen__.dll";

            string[] directories = Path.GetDirectoryName(input).Split(Path.DirectorySeparatorChar);
            var outputPath = Path.Combine(@"D:\Temp\", directories.Last()) + "_" + Path.ChangeExtension(Path.GetFileName(input), ".sarif");

            var logPath = Path.Combine(@"D:\Temp\", "analysis.log");
            var outputStream = File.CreateText(logPath);

            AnalyzeOneDll(input, outputPath, ScopeMethodKind.Reducer, true);

            AnalysisStats.PrintStats(outputStream);
            AnalysisStats.WriteAnalysisReasons(outputStream);
            outputStream.WriteLine("End.");
            outputStream.Flush();

            System.Console.ReadKey();

        }

        public enum ScopeMethodKind { Producer, Reducer };

        private static void AnalyzeOneDll(string input, string outputPath, ScopeMethodKind kind, bool useScopeFactory = true)
        {
            var folder = Path.GetDirectoryName(input);
            var referenceFiles = Directory.GetFiles(folder, "*.dll", SearchOption.TopDirectoryOnly).Where(fp => Path.GetFileName(fp).ToLower(CultureInfo.InvariantCulture) != Path.GetFileName(input).ToLower(CultureInfo.InvariantCulture)).ToList();
            referenceFiles.AddRange(Directory.GetFiles(folder, "*.exe", SearchOption.TopDirectoryOnly));
            AnalyzeDll(input, referenceFiles, outputPath, ScopeMethodKind.Reducer, useScopeFactory);
        }

        public static void AnalyzeDll(string inputPath, IEnumerable<string> referenceFiles, string outputPath, ScopeMethodKind kind, bool useScopeFactory = true, StreamWriter outputStream = null)
        {
            // Determine whether to use Interproc analysis
            AnalysisOptions.DoInterProcAnalysis = false;

            AnalysisStats.TotalNumberFolders++;

            var host = new MyHost();
            PlatformTypes.Resolve(host);

            var loader = new MyLoader(host);
            host.Loader = loader;

            var scopeGenAssembly = loader.LoadMainAssembly(inputPath);
            AnalysisStats.TotalDllsFound++;

            // LoadExternalReferences(referenceFiles, loader);
            //loader.LoadCoreAssembly();

            var program = new Program(host, loader);

            // program.interprocAnalysisManager = new InterproceduralManager(host);
            program.ScopeGenAssembly = scopeGenAssembly;
            program.ReferenceFiles = referenceFiles;

            program.ClassFilter = "Reducer";
            program.ClousureFilter = "<Reduce>d__";
            program.EntryMethod = "Reduce";

            if (kind == ScopeMethodKind.Producer)
            {
                program.ClassFilter = "Producer";
                program.ClousureFilter = "<Produce>d__";
                program.EntryMethod = "Produce";
            }

            program.MethodUnderAnalysisName = "MoveNext";

            IEnumerable<Tuple<MethodDefinition, MethodDefinition, MethodDefinition>> scopeMethodPairs;
            if (useScopeFactory)
            {
                scopeMethodPairs = program.ObtainScopeMethodsToAnalyze();
            }
            else
            {
                scopeMethodPairs = program.ObtainScopeMethodsToAnalyzeFromAssemblyes();
            }
            var results = new List<Result>();

            if (scopeMethodPairs.Any())
            {
                foreach (var methodPair in scopeMethodPairs)
                {
                    var entryMethodDef = methodPair.Item1;
                    var moveNextMethod = methodPair.Item2;
                    var getEnumMethod = methodPair.Item3;
                    System.Console.WriteLine("Method {0} on class {1}", moveNextMethod.Name, moveNextMethod.ContainingType.FullPathName());

                    try
                    {

                        var dependencyAnalysis = new SongTaoDependencyAnalysis(host, program.interprocAnalysisManager, moveNextMethod, entryMethodDef, getEnumMethod);
                        var depAnalysisResult = dependencyAnalysis.AnalyzeMoveNextMethod();
                        System.Console.WriteLine("Done!");

                        program.ValidateInputSchema(inputPath, moveNextMethod, depAnalysisResult);

                        var escapes = depAnalysisResult.Dependencies.A1_Escaping.Select(traceable => traceable.ToString());

                        var inputsReads = new HashSet<Traceable>(depAnalysisResult.Dependencies.A2_Variables.Values.SelectMany(traceables => traceables.Where(t => t.TableKind == ProtectedRowKind.Input)));
                        var outputWrites = new HashSet<Traceable>(depAnalysisResult.Dependencies.A4_Ouput.Values.SelectMany(traceables => traceables.Where(t => t.TableKind == ProtectedRowKind.Output)));

                        var result = new Result();
                        var inputsString = inputsReads.OfType<TraceableColumn>().Select(t => t.ToString());
                        var outputsString = outputWrites.OfType<TraceableColumn>().Select(t => t.ToString());
                        result.SetProperty("Inputs", inputsString);
                        result.SetProperty("Ouputs", outputsString);
                        results.Add(result);

                        if (outputStream != null)
                        {
                            outputStream.WriteLine("Class: [{0}] {1}", moveNextMethod.ContainingType.FullPathName(), moveNextMethod.ToSignatureString());
                            if (depAnalysisResult.IsTop)
                            {
                                outputStream.WriteLine("Analysis returns TOP");
                            }
                            outputStream.WriteLine("Inputs: {0}", String.Join(", ", inputsString));
                            outputStream.WriteLine("Outputs: {0}", String.Join(", ", outputsString));
                        }


                        if (depAnalysisResult.Dependencies.A4_Ouput.Any())
                        {
                            foreach (var outColum in depAnalysisResult.Dependencies.A4_Ouput.Keys)
                            {
                                result = new Result();
                                foreach (var column in depAnalysisResult.Dependencies.A2_Variables[outColum])
                                {
                                    var columnString = column.ToString();
                                    var dependsOn = depAnalysisResult.Dependencies.A4_Ouput[outColum].Select(traceable => traceable.ToString());
                                    result.SetProperty("column", columnString);
                                    result.SetProperty("depends", dependsOn.Union(escapes));
                                    results.Add(result);
                                }
                            }
                        }
                        else
                        {
                            result = new Result();
                            result.SetProperty("column", "_ALL_");
                            result.SetProperty("depends", escapes);
                            results.Add(result);
                        }
                        WriteSarifOutput(inputPath, outputPath, moveNextMethod, results);
                    }
                    catch (Exception e)
                    {
                        System.Console.WriteLine("Could not analyze {0}", inputPath);
                        AnalysisStats.TotalofDepAnalysisErrors++;
                        AnalysisStats.AddAnalysisReason(new AnalysisReason(moveNextMethod.Name.ToString(), moveNextMethod.Body.Instructions[0],
                                        String.Format(CultureInfo.InvariantCulture, "Throw exception {0}", e.StackTrace.ToString())));

                    }
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
                    System.Console.WriteLine("Cannot load {0}:{1}", referenceFileName, e.Message);
                }
            }
        }
        /// <summary>
        /// Analyze the ScopeFactory class to get all the Processor/Reducer classes to analyze
        /// For each one obtain:
        /// 1) entry point method that creates the class with the iterator clousure and populated with some data)
        /// 2) the GetEnumerator method that creates and enumerator and polulated with data
        /// 3) the MoveNextMethod that contains the actual reducer/producer code
        /// </summary>
        /// <returns></returns>
        private IEnumerable<Tuple<MethodDefinition, MethodDefinition, MethodDefinition>> ObtainScopeMethodsToAnalyze()
        {
            var scopeMethodPairsToAnalyze = new HashSet<Tuple<MethodDefinition, MethodDefinition, MethodDefinition>>();

            var operationFactoryClass = this.ScopeGenAssembly.RootNamespace.GetAllTypes().OfType<ClassDefinition>()
                                        .Where(c => c.Name == "__OperatorFactory__" && c.ContainingType != null & c.ContainingType.Name == "___Scope_Generated_Classes___").Single();

            // Hack: use actual ScopeRuntime Types
            var factoryMethods = operationFactoryClass.Methods.Where(m => m.Name.StartsWith("Create_Process_", StringComparison.Ordinal) && m.ReturnType.ToString() == this.ClassFilter);

            // var referencesLoaded = false;

            foreach (var factoryMethdod in factoryMethods)
            {
                var ins = factoryMethdod.Body.Instructions.OfType<Model.Bytecode.CreateObjectInstruction>().Single();

                var reducerClass = ins.Constructor.ContainingType;

                ClassDefinition resolvedEntryClass = null;
                try
                {
                    resolvedEntryClass = host.ResolveReference(reducerClass) as ClassDefinition;

                    if (resolvedEntryClass != null)
                    {
                        var candidateClousures = resolvedEntryClass.Types.OfType<ClassDefinition>()
                                       .Where(c => c.Name.StartsWith(this.ClousureFilter));
                        foreach (var candidateClousure in candidateClousures)
                        {
                            var moveNextMethods = candidateClousure.Methods
                                                        .Where(md => md.Body != null && md.Name.Equals(this.MethodUnderAnalysisName));
                            var getEnumMethods = candidateClousure.Methods
                                                        .Where(m => m.Name == ScopeAnalysisConstants.SCOPE_ROW_ENUMERATOR_METHOD);
                            foreach (var moveNextMethod in moveNextMethods)
                            {
                                AnalysisStats.TotalReducers++;

                                var entryMethod = resolvedEntryClass.Methods.Where(m => m.Name == this.EntryMethod).Single();
                                var getEnumeratorMethod = getEnumMethods.Single();
                                scopeMethodPairsToAnalyze.Add(new Tuple<MethodDefinition, MethodDefinition, MethodDefinition>(entryMethod, moveNextMethod, getEnumeratorMethod));

                                // TODO: Hack for reuse. Needs refactor
                                if (factoryMethdod != null)
                                {
                                    var processID = factoryMethdod.Name.Substring(factoryMethdod.Name.IndexOf("Process_"));
                                    this.factoryReducerMap.Add(processID, entryMethod.ContainingType as ClassDefinition);
                                }
                            }
                        }
                    }
                }
                catch (Exception e)
                {
                    AnalysisStats.TotalofDepAnalysisErrors++;
                    System.Console.WriteLine("Error in Dependency Analysis", e.Message);
                }

            }
            return scopeMethodPairsToAnalyze;
        }

        public IEnumerable<Tuple<MethodDefinition, MethodDefinition, MethodDefinition>> ObtainScopeMethodsToAnalyzeFromAssemblyes()
        {
            var scopeMethodPairsToAnalyze = new HashSet<Tuple<MethodDefinition, MethodDefinition, MethodDefinition>>();

            var candidateClasses = host.Assemblies.SelectMany(a => a.RootNamespace.GetAllTypes().OfType<ClassDefinition>())
                            .Where(c => c.Base != null && c.Base.Name == this.ClassFilter);
            if (candidateClasses.Any())
            {
                var results = new List<Result>();
                foreach (var candidateClass in candidateClasses)
                {
                    var assembly = host.Assemblies.Where(a => a.Name == candidateClass.Name);
                    var candidateClousures = candidateClass.Types.OfType<ClassDefinition>()
                                    .Where(c => c.Name.StartsWith(this.ClousureFilter));
                    foreach (var candidateClousure in candidateClousures)
                    {
                        var methods = candidateClousure.Members.OfType<MethodDefinition>()
                                                .Where(md => md.Body != null
                                                && md.Name.Equals(this.MethodUnderAnalysisName));

                        if (methods.Any())
                        {
                            var entryMethod = candidateClass.Methods.Where(m => m.Name == this.EntryMethod).Single();
                            var moveNextMethod = methods.First();
                            var getEnumMethods = candidateClousure.Methods
                                                        .Where(m => m.Name == ScopeAnalysisConstants.SCOPE_ROW_ENUMERATOR_METHOD);
                            var getEnumeratorMethod = getEnumMethods.First();

                            scopeMethodPairsToAnalyze.Add(new Tuple<MethodDefinition, MethodDefinition, MethodDefinition>(entryMethod, moveNextMethod, getEnumeratorMethod));

                        }
                    }
                }
            }
            return scopeMethodPairsToAnalyze;
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

        private void ValidateInputSchema(string inputPath, MethodDefinition method, DependencyPTGDomain dependencyResults)
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
                    var reducers = operators.Where(op => op.Attribute("id") != null && op.Attribute("id").Value == processId);
                    var inputSchemas = reducers.SelectMany(r => r.Descendants("input").Select(i => i.Attribute("schema")), (r, t) => Tuple.Create(r.Attribute("id"), r.Attribute("className"), t));
                    var outputSchemas = reducers.SelectMany(r => r.Descendants("output").Select(i => i.Attribute("schema")), (r, t) => Tuple.Create(r.Attribute("id"), r.Attribute("className"), t));
                }
            }
        }

        private static void WriteSarifOutput(string inputPath, string outputFilePath, MethodDefinition method, IList<Result> results)
        {
            JsonSerializerSettings settings = new JsonSerializerSettings()
            {
                ContractResolver = SarifContractResolver.Instance,
                Formatting = Formatting.Indented
            };

            var run = new Run();

            // run.StableId = method.ContainingType.FullPathName();
            run.Id = String.Format("[{0}] {1}", method.ContainingType.FullPathName(), method.ToSignatureString());

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
            try
            {
                File.WriteAllText(outputFilePath, sarifText);
            }
            catch (Exception e)
            {
                System.Console.Out.Write("Could not write the file: {0}:{1}", outputFilePath, e.Message);
            }
        }

    }


}
