using Backend.Model;
using ScopeProgramAnalysis;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ScopeAnalysisBulkScripts
{
    class BulkAnalysis
    {
        private long maxTime = 0;
        private long totalTime = 0;
        private int totalCanceled = 0;

        static void Main(string[] args)
        {
            var doOnlyPassthrough = false;
            var doAnalysis = !doOnlyPassthrough;

            var rudderPath = @"C:\Users\diegog\Source\Repos\rudder\";
            var analysisClient = Path.Combine(rudderPath, @"AnalysisClient\bin\Debug\AnalysisClient.exe");
            var outputAnalyzer = Path.Combine(rudderPath, @"CompareAnalysisOutput\Compare\bin\Debug\Compare.exe");

            var inputFolder = @"C:\Temp\Scope\First100JobsFromMadan";
                // @"\\madanm2\parasail2\TFS\parasail\ScopeSurvey\AutoDownloader\bin\Debug";
            if (doOnlyPassthrough)
            {
                inputFolder = @"C:\temp\Scope\out";
            }
            //inputFolder = @"\\research\root\public\mbarnett\Parasail\First100JobsFromMadan";
            //var inputFolder = @"D:\Madam3";
            //inputFolder = @"C:\temp\Madam";

            var inputList = @"C:\Temp\Zvo\inputDlls.txt";
            //var inputList = @"C:\Temp\Zvo\sampleDlls.txt";
            var outputFolder = @"C:\Temp\Scope\out";
            //outputFolder = @"C:\Temp\Mike100";
            //outputFolder = @"C:\temp\ZvoList";

            var logPath = outputFolder;

            var bulkAnalysis = new BulkAnalysis();


            //var dllList = bulkAnalysis.LoadListFromFile(inputList);
            IList<string> dllList;
            if(doOnlyPassthrough)
            {
                dllList = bulkAnalysis.LoadSarifFromDirectory(inputFolder);
            }
            else
            {
                dllList = bulkAnalysis.LoadFromDirectory(inputFolder);
            }

            Stopwatch stopWatch = new Stopwatch();
            stopWatch.Start();

            if (doAnalysis)
            {
                bulkAnalysis.ProcessDLLs(dllList, analysisClient, outputFolder, outputFolder);
            }

            stopWatch.Stop();
            TimeSpan ts = stopWatch.Elapsed;

            stopWatch.Reset();
            stopWatch.Start();

            bulkAnalysis.AnalyzeOutput(dllList, outputAnalyzer, outputFolder, doOnlyPassthrough);

            //analysisFolder = @"D:\MadanExamples\";
            ///AnalyzeScopeScripts(new string[] { analysisFolder, @"C:\Temp\", "Reducer" });
            //AnalysisStats.PrintStats(System.Console.Out);
            stopWatch.Stop();
            TimeSpan ts2 = stopWatch.Elapsed;

            var outputStream = File.CreateText(Path.Combine(logPath,"summary.txt"));

            System.Console.WriteLine("Bulk Analysis finished on {0} dlls", dllList.Count);
            System.Console.WriteLine("Total analysis time {0} seconds", ts.Seconds);
            System.Console.WriteLine("Total output procesing time {0} seconds", ts2.Seconds);
            System.Console.WriteLine("Max time {0} seconds", bulkAnalysis.maxTime);

            outputStream.WriteLine("Bulk Analysis finished on {0} dlls", dllList.Count);
            outputStream.WriteLine("Total time {0} seconds", ts.Seconds);
            outputStream.WriteLine("Max time for one analysis {0} ms", bulkAnalysis.maxTime);
            outputStream.WriteLine("Total canceled {0}", bulkAnalysis.totalCanceled);
            outputStream.Close();

            // System.Console.ReadKey();
        }

        private IList<string> LoadListFromFile(string pathToFile)
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
        private IList<string> LoadFromDirectory(string inputFolder)
        {
            const string inputDllName = "__ScopeCodeGen__.dll";
            string[] files = Directory.GetFiles(inputFolder, inputDllName, SearchOption.AllDirectories);
            return files;
        }

        private IList<string> LoadSarifFromDirectory(string inputFolder)
        {
            const string inputDllName = "*.sarif";
            string[] files = Directory.GetFiles(inputFolder, inputDllName, SearchOption.TopDirectoryOnly);
            return files;
        }
        private void ProcessDLLs(IList<string> inputs, string scopeAnalyzerPath, string outputFolder, string logFolder)
        {
            var tasks = new List<Task>();
            
            for (int j = 0; j < inputs.Count; j++)
            {
                //var cts = new CancellationTokenSource();
                //cts.CancelAfter(120000);

                var input = inputs[j];
                var task1 = Task.Run(delegate
                {
                    Stopwatch stopWatch = new Stopwatch();
                    stopWatch.Start();

                    var scopeAnalysisProcess = new Process();
                    scopeAnalysisProcess.StartInfo.FileName = scopeAnalyzerPath;
                    scopeAnalysisProcess.StartInfo.Arguments = String.Format("{0} {1} {2}", input, outputFolder, logFolder);
                    scopeAnalysisProcess.StartInfo.UseShellExecute = false;
                    scopeAnalysisProcess.StartInfo.CreateNoWindow = true;
                    scopeAnalysisProcess.Start();
                    if (!scopeAnalysisProcess.WaitForExit(3 * 60 * 1000))
                    {
                        scopeAnalysisProcess.Kill();
                        totalCanceled++; 
                    }
                    stopWatch.Stop();
                    TimeSpan ts = stopWatch.Elapsed;
                    if (ts.Milliseconds > maxTime)
                        maxTime = ts.Milliseconds;
                    totalTime += ts.Milliseconds;
                });
                tasks.Add(task1);
            }
            Task.WaitAll(tasks.ToArray());


        }

        private void AnalyzeOutput(IList<string> inputs, string outputAnalyzerPath, string outputFolder, bool doOnlyPassthrough)
        {
            var tasks = new List<Task>();
            for (int j = 0; j < inputs.Count; j++)
            {
                var input = inputs[j];

                var task2 = Task.Run(delegate
                {
                    var folder = Path.GetDirectoryName(input);
                    string[] directories = folder.Split(Path.DirectorySeparatorChar);
                    string sarifFilePath;
                    if (doOnlyPassthrough)
                    {
                        sarifFilePath = input;
                    }
                    else
                    {
                        sarifFilePath = Path.Combine(outputFolder, directories.Last()) + "_" + Path.ChangeExtension(Path.GetFileName(input), ".sarif");
                    }
                        
                    var comparerProcess = new Process();
                    comparerProcess.StartInfo.FileName = outputAnalyzerPath;
                    comparerProcess.StartInfo.Arguments = String.Format("{0}", sarifFilePath);
                    comparerProcess.StartInfo.UseShellExecute = false;
                    comparerProcess.StartInfo.CreateNoWindow = true;
                    comparerProcess.StartInfo.RedirectStandardOutput = true;
                    comparerProcess.Start();
                    comparerProcess.WaitForExit(1 * 60 * 1000);
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

                ScopeProgramAnalysis.ScopeProgramAnalysis.AnalyzeDllAndWriteLog(dllToAnalyze, outputPath, ScopeProgramAnalysis.ScopeProgramAnalysis.ScopeMethodKind.All, 
                                                                    true, interProc , outputStream);

                if (AnalysisStats.AnalysisReasons.Any())
                {
                    outputStream.WriteLine("Analysis reasons for {0}", dllToAnalyze);
                    AnalysisStats.WriteAnalysisReasons(outputStream);
                }
                outputStream.WriteLine("===========================================================================");
                outputStream.Flush();

                System.Console.WriteLine("=========================================================================");
                //PointsToGraph.NullNode.Variables.Clear();
                //PointsToGraph.NullNode.Targets.Clear();
                //PointsToGraph.NullNode.Sources.Clear();
                //PointsToGraph.GlobalNode.Variables.Clear();
                //PointsToGraph.GlobalNode.Targets.Clear();
                //PointsToGraph.GlobalNode.Sources.Clear();
                AnalysisStats.AnalysisReasons.Clear();
            }

            AnalysisStats.PrintStats(outputStream);
            outputStream.WriteLine("End.");
            outputStream.Flush();

            System.Console.WriteLine("Done!");
        }


    }
}
