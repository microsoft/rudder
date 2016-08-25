using Backend.Model;
using ScopeProgramAnalysis;
using System;
using System.Collections.Generic;
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
            var analysisFolder = @"\\madanm2\parasail2\TFS\parasail\ScopeSurvey\AutoDownloader\bin\Debug";
            analysisFolder = @"D:\MadanExamples\";
            AnalyzeScopeScripts(new string[] { analysisFolder, @"C:\Temp\", "Reducer" });
            AnalysisStats.PrintStats(System.Console.Out);
            System.Console.ReadKey();
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
                var referencesPath = Directory.GetFiles(folder, "*.dll", SearchOption.TopDirectoryOnly).Where(fp => Path.GetFileName(fp) != inputDllName).ToList();
                referencesPath.AddRange(Directory.GetFiles(folder, "*.exe", SearchOption.TopDirectoryOnly));

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
