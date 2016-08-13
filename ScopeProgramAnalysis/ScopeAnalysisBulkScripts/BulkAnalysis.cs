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
            AnalyzeScopeScripts(new string[] { @"D:\MadanExamples\", @"D:\Temp\", "Reducer" });
            AnalysisStats.PrintStats(System.Console.Out);
            System.Console.ReadKey();
        }
        public static void AnalyzeScopeScripts(string[] args)
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
                var referencesPath = Directory.GetFiles(folder, "*.dll", SearchOption.TopDirectoryOnly).Where(fp => Path.GetFileName(fp) != inputDllName).ToList();
                referencesPath.AddRange(Directory.GetFiles(folder, "*.exe", SearchOption.TopDirectoryOnly));

                string[] directories = folder.Split(Path.DirectorySeparatorChar);
                var outputPath = Path.Combine(outputFolder, directories.Last()) + "_" + Path.ChangeExtension(Path.GetFileName(dllToAnalyze), ".sarif");

                //var outputPath = Path.Combine(outputFolder, Path.ChangeExtension(Path.GetFileName(dllToAnalyze),".sarif"));

                Program.AnalyzeDll(dllToAnalyze, referencesPath, outputPath, Program.ScopeMethodKind.Reducer);
                System.Console.WriteLine("=========================================================================");
                PointsToGraph.NullNode.Variables.Clear();
                PointsToGraph.NullNode.Targets.Clear();
                PointsToGraph.NullNode.Sources.Clear();
                PointsToGraph.GlobalNode.Variables.Clear();
                PointsToGraph.GlobalNode.Targets.Clear();
                PointsToGraph.GlobalNode.Sources.Clear();
            }
            System.Console.WriteLine("Done!");
        }


    }
}
