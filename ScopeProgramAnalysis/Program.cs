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

namespace ScopeProgramAnalysis
{
    class Program
    {
        private Host host;

        public Program(Host host)
        {
            this.host = host;
        }

        static void Main(string[] args)
        {
            //const string root = @"C:\Users\t-diga\Source\Repos\ScopeExamples\ILAnalyzer\"; // @"..\..\..";
            //const string input = root + @"\bin\Debug\ILAnalyzer.exe";

            const string root = @"c:\users\t-diga\source\repos\scopeexamples\metting\";
            const string input = root + @"\__scopecodegen__.dll";

            //string[] directories = Path.GetDirectoryName(input).Split(Path.DirectorySeparatorChar);
            //var outputPath = Path.Combine(@"D:\Temp\", directories.Last()) + "_" + Path.ChangeExtension(Path.GetFileName(input), ".sarif");

            //AnalyzeDll(input, outputPath, ScopeMethodKind.Reducer);

            AnalyzeScopeScript(new string[] { @"D:\ScriptExamples\Files", @"D:\Temp\", "Reducer" } );

            System.Console.ReadKey();

        }

        enum ScopeMethodKind { Producer, Reducer };

        private static void AnalyzeDll(string inputPath, string outputPath, ScopeMethodKind kind)
        {
            var host = new Host();
            PlatformTypes.Resolve(host);
            //host.Assemblies.Add(assembly);

            var loader = new Loader(host);
            loader.LoadAssembly(inputPath);

            // loader.LoadCoreAssembly();

            var program = new Program(host);

            var classFilter = "";
            var clousureFilter = "<Reduce>d__";
            var entryMethod = "Reduce";
            var classUnderAnalysis = "<Reduce>d__";

            if (kind == ScopeMethodKind.Reducer)
            {
                classFilter = "Reducer";
                classUnderAnalysis = "<Reduce>d__";
            }
            else
            {
                classFilter = "Producer";
                clousureFilter = "<Produce>d__";
                entryMethod = "Produce";
                classUnderAnalysis = "<Produce>d__";
            }
            var methodUnderAnalysis = "MoveNext";


            var candidateClasses = host.Assemblies.SelectMany(a => a.RootNamespace.GetAllTypes().OfType<ClassDefinition>())
                            .Where(c => c.Base != null && c.Base.Name == classFilter);
            //foreach (var t in types)
            //{
            //    System.Console.WriteLine("{0}", t.FullPathName());
            //}

            if (candidateClasses.Any())
            {
                var results = new List<Result>();
                foreach (var candidateClass in candidateClasses)
                {
                    var assembly = host.Assemblies.Where(a => a.Name == candidateClass.Name);
                    var candidateClousures = candidateClass.Types.OfType<ClassDefinition>()
                                    .Where(c => c.Name.StartsWith(clousureFilter));
                    var methods = candidateClousures.SelectMany(t => t.Members.OfType<MethodDefinition>())
                                                .Where(md => md.Body != null
                                                && md.Name.Equals(methodUnderAnalysis));

                    if (methods.Any())
                    {
                        var entryMethodDef = candidateClass.Methods.Where(m => m.Name == entryMethod).Single();
                        var moveNextMethod = methods.First();
                        System.Console.WriteLine("Method {0} on class {1}", moveNextMethod.Name, candidateClass.FullPathName());
                        var dependencyAnalysis = new SongTaoDependencyAnalysis(host, moveNextMethod, entryMethodDef);
                        var depAnalysisResult = dependencyAnalysis.AnalyzeMoveNextMethod();
                        System.Console.WriteLine("Done!");

                        ValidateInputSchema(inputPath, moveNextMethod, depAnalysisResult);

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

                    }
                    else
                    {
                        System.Console.WriteLine("No method {0} on class {1} in {2}", methodUnderAnalysis, candidateClass.GenericName, inputPath);
                    }
                }
                WriteSarifOutput(inputPath, outputPath, results);
            }
            else
            {
                System.Console.WriteLine("No {0} class in {1}", kind, inputPath);
            }
        }

        private static void ValidateInputSchema(string inputPath, MethodDefinition method, Backend.Analyses.DependencyDomain dependencyResults)
        {
            var inputDirectory = Path.GetDirectoryName(inputPath);
            var xmlFile = Path.Combine(inputDirectory, "ScopeVertexDef.xml");
            XElement x = XElement.Load(xmlFile);
            var operators = x.Descendants("operator");
            var reducers = operators.Where(op => op.Attribute("className") != null && op.Attribute("className").Value.StartsWith("ScopeReducer"));

            var inputSchemas = reducers.SelectMany(r => r.Descendants("input").Select(i => i.Attribute("schema")), (r, t) => Tuple.Create(r.Attribute("id"), r.Attribute("className"), t));
            var outputSchemas = reducers.SelectMany(r => r.Descendants("output").Select(i => i.Attribute("schema")), (r, t) => Tuple.Create(r.Attribute("id"), r.Attribute("className"), t));
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
            string[] files = Directory.GetFiles(inputFolder, "__ScopeCodeGen__.dll", SearchOption.AllDirectories);
            foreach (var dllToAnalyze in files)
            {
                System.Console.WriteLine("Analyzing {0}", dllToAnalyze);
                string[] directories = Path.GetDirectoryName(dllToAnalyze).Split(Path.DirectorySeparatorChar);
                var outputPath = Path.Combine(outputFolder, directories.Last()) + "_" + Path.ChangeExtension(Path.GetFileName(dllToAnalyze), ".sarif");
                //var outputPath = Path.Combine(outputFolder, Path.ChangeExtension(Path.GetFileName(dllToAnalyze),".sarif"));
                AnalyzeDll(dllToAnalyze, outputPath, ScopeMethodKind.Reducer);
            }
            System.Console.WriteLine("Done!");
            System.Console.ReadKey();


        }
    }


}
