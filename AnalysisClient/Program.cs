using ScopeProgramAnalysis;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AnalysisClient
{
    class Program
    {
        public static object ScopeMethodKind { get; private set; }

        static void Main(string[] args)
        {
            var inputDll = args[0];
            var outputPath = args[1];
            var logPath = args[2];

            AnalyzeScopeScript(args);
            AnalysisStats.PrintStats(System.Console.Out);
        }

        public static void AnalyzeScopeScript(string[] args)
        {
            var inputDll = args[0];
            var outputFolder = args[1];
            var tempPath = outputFolder;

            var interproc = false;

            var dllToAnalyze = inputDll;

            var folder = Path.GetDirectoryName(dllToAnalyze);
            string[] directories = folder.Split(Path.DirectorySeparatorChar);

            var logPath = Path.Combine(tempPath, directories.Last()) + "_" + Path.ChangeExtension(Path.GetFileName(dllToAnalyze), ".log");
        
            var outputStream = File.CreateText(logPath);

            //var referencesPath = Directory.GetFiles(folder, "*.dll", SearchOption.TopDirectoryOnly).Where(fp => Path.GetFileName(fp) != inputDllName).ToList();
            //referencesPath.AddRange(Directory.GetFiles(folder, "*.exe", SearchOption.TopDirectoryOnly));

            var outputPath = Path.Combine(outputFolder, directories.Last()) + "_" + Path.ChangeExtension(Path.GetFileName(dllToAnalyze), ".sarif");

            System.Console.WriteLine("=========================================================================");
            System.Console.WriteLine("Folder #{0}", AnalysisStats.TotalNumberFolders);
            System.Console.WriteLine("Analyzing {0}", dllToAnalyze);
            outputStream.WriteLine("Folder #{0}", AnalysisStats.TotalNumberFolders);
            outputStream.WriteLine("===========================================================================");
            outputStream.WriteLine("Analyzing {0}", dllToAnalyze);


            ScopeProgramAnalysis.ScopeProgramAnalysis.AnalyzeDllAndWriteLog(dllToAnalyze, outputPath, 
                ScopeProgramAnalysis.ScopeProgramAnalysis.ScopeMethodKind.All, 
                false, true, interproc, outputStream);

            if (AnalysisStats.AnalysisReasons.Any())
            {
                outputStream.WriteLine("Analysis reasons for {0}", dllToAnalyze);
                AnalysisStats.WriteAnalysisReasons(outputStream);
            }
            outputStream.WriteLine("===========================================================================");
            outputStream.Flush();

            System.Console.WriteLine("=========================================================================");


            AnalysisStats.PrintStats(outputStream);
            outputStream.WriteLine("End.");
            outputStream.Flush();

            System.Console.WriteLine("Done!");
        }

    }
}
