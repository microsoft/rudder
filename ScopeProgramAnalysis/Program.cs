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
    class MyLoader : Loader
    {
        private string assemblyFolder;
        private string assemblyParentFolder;
        private HashSet<IAssemblyReference> failedAssemblies;
        private Assembly mainAssemably;

        public MyLoader(Host host) : base(host)
        {
            this.failedAssemblies = new HashSet<IAssemblyReference>();
        }
        public Assembly LoadMainAssembly(string fileName)
        {
            this.assemblyFolder = Path.GetDirectoryName(fileName);
            this.assemblyParentFolder = Directory.GetParent(Path.GetDirectoryName(fileName)).FullName;
            cciHost.AddLibPath(assemblyFolder);
            cciHost.AddLibPath(assemblyParentFolder);
            this.mainAssemably = base.LoadAssembly(fileName);
            return this.mainAssemably;
        }


        public Assembly TryToLoadReferencedAssembly(IAssemblyReference reference)
        {
            var assembly = this.ourHost.Assemblies.SingleOrDefault(a => a.MatchReference(reference));
            if (assembly == null && !failedAssemblies.Contains(reference))
            {
                try
                {
                    AnalysisStats.TotalDllsFound++;
                    assembly = TryToLoadAssembly(reference.Name);
                }
                catch (Exception e)
                {
                    System.Console.WriteLine("We could not solve this reference: {0}", reference.Name);
                    failedAssemblies.Add(reference);
                    throw e;
                }
            }
            return assembly;
        }

        private Assembly TryToLoadAssembly(string assemblyReferenceName)
        {
            if(assemblyReferenceName=="mscorlib")
            {
                return LoadCoreAssembly();
            }

            var extensions = new string[] { ".dll", ".exe" };
            var referencePath = "";
            foreach (var extension in extensions)
            {
                referencePath = Path.Combine(assemblyFolder, assemblyReferenceName) + extension;
                if (File.Exists(referencePath))
                    break;
                referencePath = Path.Combine(assemblyParentFolder, assemblyReferenceName) + extension;
                if (File.Exists(referencePath))
                    break;
            }
            //var cciAssemblyFromReference = cciHost.LoadUnitFrom(referencePath) as Cci.IModule;
            //// var cciAssemblyFromReference = cciHost.LoadUnit(assemblyReference.AssemblyIdentity) as Cci.IAssembly;
            //return cciAssemblyFromReference;
            return LoadAssembly(referencePath);
        }
        //public Assembly LoadAssemblyAndReferences(string fileName)
        //{
        //    var module = cciHost.LoadUnitFrom(fileName) as Cci.IModule;

        //    if (module == null || module == Cci.Dummy.Module || module == Cci.Dummy.Assembly)
        //        throw new Exception("The input is not a valid CLR module or assembly.");

        //    var pdbFileName = Path.ChangeExtension(fileName, "pdb");
        //    Cci.PdbReader pdbReader = null;

        //    if (File.Exists(pdbFileName))
        //    {
        //        using (var pdbStream = File.OpenRead(pdbFileName))
        //        {
        //            pdbReader = new Cci.PdbReader(pdbStream, cciHost);
        //        }
        //    }
        //    var assembly = this.ExtractAssembly(module, pdbReader);

        //    if (pdbReader != null)
        //    {
        //        pdbReader.Dispose();
        //    }

        //    ourHost.Assemblies.Add(assembly);
        //    this.assemblyFolder = Path.GetDirectoryName(fileName);
        //    this.assemblyParentFolder = Directory.GetParent(Path.GetDirectoryName(fileName)).FullName;
        //    cciHost.AddLibPath(assemblyFolder);
        //    cciHost.AddLibPath(assemblyParentFolder);

        //    foreach (var assemblyReference in module.AssemblyReferences)
        //    {
        //        try
        //        {
        //            Cci.IModule cciAssemblyFromReference = TryToLoadCCIAssembly(assemblyReference);

        //            if (cciAssemblyFromReference == null || cciAssemblyFromReference == Cci.Dummy.Assembly)
        //                throw new Exception("The input is not a valid CLR module or assembly.");

        //            var pdbLocation = cciAssemblyFromReference.DebugInformationLocation;
        //            if (File.Exists(pdbFileName))
        //            {
        //                using (var pdbStream = File.OpenRead(pdbFileName))
        //                {
        //                    pdbReader = new Cci.PdbReader(pdbStream, cciHost);
        //                }
        //            }
        //            var assemblyFromRef = this.ExtractAssembly(cciAssemblyFromReference, pdbReader);
        //            ourHost.Assemblies.Add(assemblyFromRef);
        //            if (pdbReader != null)
        //            {
        //                pdbReader.Dispose();
        //            }
        //        }
        //        catch (Exception e)
        //        {

        //        }
        //    }
        //    return assembly;

        //}


    }

    class MyHost : Host
    {
        public MyLoader Loader { get; set ;}

        public override ITypeDefinition ResolveReference(IBasicType typeToResolve)
        {
            var resolvedType = base.ResolveReference(typeToResolve);
            if (resolvedType == null)
            {
                try
                {
                    Loader.TryToLoadReferencedAssembly(typeToResolve.ContainingAssembly);
                    resolvedType = base.ResolveReference(typeToResolve);
                }
                catch (Exception e)
                {
                    AnalysisStats.DllThatFailedToLoad.Add(typeToResolve.ContainingAssembly.Name);
                    AnalysisStats.TotalDllsFailedToLoad++;
                }

            }
            return resolvedType;
        }
        public override ITypeMemberDefinition ResolveReference(ITypeMemberReference member)
        {
            return base.ResolveReference(member);
        }

    }
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
        public static MethodCFGCache MethodCFGCache { get; private set; }

        public Program(Host host, Loader loader)
        {
            this.host = host;
            this.loader = loader;
            this.factoryReducerMap = new Dictionary<string, ClassDefinition>();
            this.interprocAnalysisManager = new InterproceduralManager(host);
        }

        static void Main(string[] args)
        {
            const string root = @"c:\users\t-diga\source\repos\scopeexamples\metting\";
            // const string input = root+ @"__ScopeCodeGen__.dll";

            //const string input = @"D:\MadanExamples\3213e974-d0b7-4825-9fd4-6068890d3327\__ScopeCodeGen__.dll";

            // Mike example: FileChunker
            const string input = @"C:\Users\t-diga\Source\Repos\ScopeExamples\ExampleWithXML\69FDA6E7DB709175\ScopeMapAccess_4D88E34D25958F3B\__ScopeCodeGen__.dll";
            //const string input = @"\\research\root\public\mbarnett\Parasail\ExampleWithXML\69FDA6E7DB709175\ScopeMapAccess_4D88E34D25958F3B\__ScopeCodeGen__.dll";

            //const string input = @"D:\MadanExamples\137eda33-5443-4217-94a4-35d416fc30a9\__ScopeCodeGen__.dll";


            string[] directories = Path.GetDirectoryName(input).Split(Path.DirectorySeparatorChar);
            var outputPath = Path.Combine(@"D:\Temp\", directories.Last()) + "_" + Path.ChangeExtension(Path.GetFileName(input), ".sarif");

            AnalyzeOneDll(input, outputPath, ScopeMethodKind.Reducer);

            AnalysisStats.PrintStats(System.Console.Out);
            System.Console.ReadKey();

        }

        public enum ScopeMethodKind { Producer, Reducer };

        private static void AnalyzeOneDll(string input, string outputPath, ScopeMethodKind kind)
        {
            var folder = Path.GetDirectoryName(input);
            var referenceFiles = Directory.GetFiles(folder, "*.dll", SearchOption.TopDirectoryOnly).Where(fp => Path.GetFileName(fp).ToLower(CultureInfo.InvariantCulture)!= Path.GetFileName(input).ToLower(CultureInfo.InvariantCulture)).ToList();
            referenceFiles.AddRange(Directory.GetFiles(folder, "*.exe", SearchOption.TopDirectoryOnly));
            AnalyzeDll(input, referenceFiles, outputPath, ScopeMethodKind.Reducer);
        }

        public static void AnalyzeDll(string inputPath, IEnumerable<string> referenceFiles, string outputPath, ScopeMethodKind kind)
        {
            AnalysisStats.TotalNumberFolders++;

            var host = new MyHost();
            PlatformTypes.Resolve(host);
            ScopeTypes.Resolve(host);


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
                    var dependencyAnalysis = new SongTaoDependencyAnalysis(host, program.interprocAnalysisManager, moveNextMethod, entryMethodDef, getEnumMethod);
                    var depAnalysisResult = dependencyAnalysis.AnalyzeMoveNextMethod();
                    System.Console.WriteLine("Done!");

                    program.ValidateInputSchema(inputPath, moveNextMethod, depAnalysisResult);

                    var escapes = depAnalysisResult.A1_Escaping.Select(traceable => traceable.ToString());

                    if (depAnalysisResult.A4_Ouput.Any())
                    {
                        foreach (var outColum in depAnalysisResult.A4_Ouput.Keys)
                        {
                            var result = new Result();
                            foreach(var column in depAnalysisResult.A2_Variables[outColum])
                            {
                                var columnString = column.ToString();
                                var dependsOn = depAnalysisResult.A4_Ouput[outColum].Select(traceable => traceable.ToString());
                                result.SetProperty("column", columnString);
                                result.SetProperty("depends", dependsOn.Union(escapes));
                                results.Add(result);
                            }
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
        /// <summary>
        /// Analyze the ScopeFactory class to get all the Processor/Reducer classes to analyze
        /// For each one obtain:
        /// 1) entry point method that creates the class with the iterator clousure and populated with some data)
        /// 2) the GetEnumerator method that creates and enumerator and polulated with data
        /// 3) the MoveNextMethod that contains the actual reducer/producer code
        /// </summary>
        /// <returns></returns>
        private IEnumerable<Tuple<MethodDefinition, MethodDefinition,MethodDefinition>> ObtainScopeMethodsToAnalyze()
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

                //if (!referencesLoaded)
                //{
                //    LoadExternalReferences(this.ReferenceFiles, loader);
                //    referencesLoaded = true;
                //}
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
                                var processID = factoryMethdod.Name.Substring(factoryMethdod.Name.IndexOf("Process_"));
                                this.factoryReducerMap.Add(processID, entryMethod.ContainingType as ClassDefinition);
                            }
                        }
                    }
                }
                catch(Exception e)
                {
                    AnalysisStats.TotalofDepAnalysisErrors++;
                }
            }
            return scopeMethodPairsToAnalyze;
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
            try {
                File.WriteAllText(outputFilePath, sarifText);
            }
            catch (Exception e)
            {
                System.Console.Out.Write("Could not write the file: {0}", outputFilePath);
            }
        }

    }


}
