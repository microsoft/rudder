using Backend.Model;
using ScopeProgramAnalysis;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ScopeAnalysisBulkScripts
{
    class BulkAnalysis
    {
        static void Main(string[] args)
        {
            var analysisClient = @"C:\Users\t-diga\Source\Repos\rudder-github\AnalysisClient\bin\Debug\AnalysisClient.exe";
            var outputAnalyzer = @"C:\Users\t-diga\Source\Repos\rudder-github\CompareAnalysisOutput\Compare\bin\Debug\Compare.exe";

            var inputList = @"C:\Temp\Zvo\sampleDlls.txt";
            var outputFolder = @"C:\Temp\Demo";

            var dllList = LoadListFromFile(inputList);

            ProcessDLLs(dllList, analysisClient, outputFolder, outputFolder);

            AnalyzeOutput(dllList, outputAnalyzer, outputFolder);

            //var analysisFolder = @"\\madanm2\parasail2\TFS\parasail\ScopeSurvey\AutoDownloader\bin\Debug";
            //analysisFolder = @"D:\MadanExamples\";
            ///AnalyzeScopeScripts(new string[] { analysisFolder, @"C:\Temp\", "Reducer" });
            AnalysisStats.PrintStats(System.Console.Out);
            System.Console.WriteLine("Bulk Analysis finished");
            // System.Console.ReadKey();
        }

        private static IList<string> LoadListFromFile(string pathToFile)
        {
            var dllList = new List<string>();
            TextReader tr;
            tr = File.OpenText(pathToFile);
            string dll;
            dll = tr.ReadLine();
            while (dll != null)
            {
                dllList.Add(dll);
                dll = tr.ReadLine();
            }
            return dllList;
        }
        private static IList<string> LoadFromDirectory(string inputFolder)
        {
            const string inputDllName = "__ScopeCodeGen__.dll";
            string[] files = Directory.GetFiles(inputFolder, inputDllName, SearchOption.AllDirectories);
            return files;
        }

        private static void ProcessDLLs(IList<string> inputs, string scopeAnalyzerPath, string outputFolder, string logFolder)
        {
            var tasks = new List<Task>();
            for (int j = 0; j < inputs.Count; j++)
            {
                var input = inputs[j];
                var task1 = Task.Run(delegate
                {
                    var scopeAnalysisProcess = new Process();
                    scopeAnalysisProcess.StartInfo.FileName = scopeAnalyzerPath;
                    scopeAnalysisProcess.StartInfo.Arguments = String.Format("{0} {1} {2}", input, outputFolder, logFolder);
                    scopeAnalysisProcess.StartInfo.UseShellExecute = false;
                    scopeAnalysisProcess.StartInfo.CreateNoWindow = true;
                    scopeAnalysisProcess.Start();
                    scopeAnalysisProcess.WaitForExit();
                });
                tasks.Add(task1);
            }
            Task.WaitAll(tasks.ToArray());


        }

        private static void AnalyzeOutput(IList<string> inputs, string outputAnalyzerPath, string outputFolder)
        {
            var tasks = new List<Task>();
            for (int j = 0; j < inputs.Count; j++)
            {
                var input = inputs[j];

                var task2 = Task.Run(delegate
                {
                    var folder = Path.GetDirectoryName(input);
                    string[] directories = folder.Split(Path.DirectorySeparatorChar);
                    var sarifFilePath = Path.Combine(outputFolder, directories.Last()) + "_" + Path.ChangeExtension(Path.GetFileName(input), ".sarif");

                    var comparerProcess = new Process();
                    comparerProcess.StartInfo.FileName = outputAnalyzerPath;
                    comparerProcess.StartInfo.Arguments = String.Format("{0}", sarifFilePath);
                    comparerProcess.StartInfo.UseShellExecute = false;
                    comparerProcess.StartInfo.CreateNoWindow = true;
                    comparerProcess.StartInfo.RedirectStandardOutput = true;
                    comparerProcess.Start();
                    comparerProcess.WaitForExit();
                    string output = comparerProcess.StandardOutput.ReadToEnd();

                    var outputPath = Path.Combine(outputFolder, directories.Last()) + "_" + Path.ChangeExtension(Path.GetFileName(input), ".passthrough");
                    using (var outputFile = File.CreateText(outputPath))
                    {
                        outputFile.WriteLine(output);
                    }

                });
                tasks.Add(task2);
            }
            Task.WaitAll(tasks.ToArray());
        }

        public static void AnalyzeScopeScripts(string[] args)
        {
            var logPath = Path.Combine(@"C:\Temp\", "analysis.log");
            var outputStream = File.CreateText(logPath);
            var interProc = false;


            var inputFolder = args[0];
            var outputFolder = args[1];
            var kind = args[2];
            const string inputDllName = "__ScopeCodeGen__.dll";
            string[] files = Directory.GetFiles(inputFolder, inputDllName, SearchOption.AllDirectories);
            foreach (var dllToAnalyze in files)
            {
                System.Console.WriteLine("=========================================================================");
                System.Console.WriteLine("Folder #{0}", AnalysisStats.TotalNumberFolders);
                System.Console.WriteLine("Analyzing {0}", dllToAnalyze);
                outputStream.WriteLine("Folder #{0}", AnalysisStats.TotalNumberFolders);
                outputStream.WriteLine("===========================================================================");
                outputStream.WriteLine("Analyzing {0}", dllToAnalyze);

                var folder = Path.GetDirectoryName(dllToAnalyze);
                //var referencesPath = Directory.GetFiles(folder, "*.dll", SearchOption.TopDirectoryOnly).Where(fp => Path.GetFileName(fp) != inputDllName).ToList();
                //referencesPath.AddRange(Directory.GetFiles(folder, "*.exe", SearchOption.TopDirectoryOnly));
                string[] directories = folder.Split(Path.DirectorySeparatorChar);
                var outputPath = Path.Combine(outputFolder, directories.Last()) + "_" + Path.ChangeExtension(Path.GetFileName(dllToAnalyze), ".sarif");

                //var outputPath = Path.Combine(outputFolder, Path.ChangeExtension(Path.GetFileName(dllToAnalyze),".sarif"));

                ScopeProgramAnalysis.ScopeProgramAnalysis.AnalyzeDll(dllToAnalyze, outputPath, ScopeProgramAnalysis.ScopeProgramAnalysis.ScopeMethodKind.All, 
                                                                    true, interProc , outputStream);

                if (AnalysisStats.AnalysisReasons.Any())
                {
                    outputStream.WriteLine("Analysis reasons for {0}", dllToAnalyze);
                    AnalysisStats.WriteAnalysisReasons(outputStream);
                }
                outputStream.WriteLine("===========================================================================");
                outputStream.Flush();

                System.Console.WriteLine("=========================================================================");
                PointsToGraph.NullNode.Variables.Clear();
                PointsToGraph.NullNode.Targets.Clear();
                PointsToGraph.NullNode.Sources.Clear();
                PointsToGraph.GlobalNode.Variables.Clear();
                PointsToGraph.GlobalNode.Targets.Clear();
                PointsToGraph.GlobalNode.Sources.Clear();
                AnalysisStats.AnalysisReasons.Clear();
            }

            AnalysisStats.PrintStats(outputStream);
            outputStream.WriteLine("End.");
            outputStream.Flush();

            System.Console.WriteLine("Done!");
        }


    }
}
