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
            if (Directory.Exists(args[0]))
            {
                AnalyzeAllJobsInDirectory(args);
                return;
            }

            var inputDll = args[0];
            var outputPath = args[1];

            AnalyzeScopeScript(args);
            //            AnalysisStats.PrintStats(System.Console.Out);
        }

        public static void AnalyzeAllJobsInDirectory(string[] args)
        {
            var inputDir = args[0];
            // enumerate all GUIDs in this directory
            var inputDlls = from dir in Directory.EnumerateDirectories(inputDir)
                            let scopeCodeGenFile = Path.Combine(dir, "__ScopeCodeGen__.dll")
                            where File.Exists(scopeCodeGenFile)
                            select scopeCodeGenFile;

            Parallel.ForEach(inputDlls, new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount },
                inputDll =>
                {
                    var process = new Process();
                    process.StartInfo = new ProcessStartInfo(Process.GetCurrentProcess().MainModule.FileName, String.Join(" ", new string[] { inputDll, args[1] }));
                    process.StartInfo.UseShellExecute = false;
                    process.StartInfo.RedirectStandardError = true;
                    process.StartInfo.RedirectStandardOutput = true;
                    process.StartInfo.CreateNoWindow = true;
                    process.Start();
                    if(!process.WaitForExit((int)(System.TimeSpan.FromMinutes(10).TotalMilliseconds)))
                    {
                        System.Console.WriteLine("{0} timed out", inputDll);
                        process.Kill();
                    }
                }
                );
        }

        public static void AnalyzeScopeScript(string[] args)
        {
            Console.Error.WriteLine("Analyzing {0}", args[0]);
            var inputDll = args[0];
            var outputFolder = args[1];

            var tempPath = outputFolder;

            var dllToAnalyze = inputDll;

            var folder = Path.GetDirectoryName(dllToAnalyze);
            string[] directories = folder.Split(Path.DirectorySeparatorChar);

            var outputSummaryFile = Path.Combine(tempPath, "summary.txt");

            //var logPath = Path.Combine(tempPath, directories.Last()) + "_" + Path.ChangeExtension(Path.GetFileName(dllToAnalyze), ".log");

            //var outputStream = File.CreateText(logPath);

            var outputPath = Path.Combine(outputFolder, directories.Last()) + "_" + Path.ChangeExtension(Path.GetFileName(dllToAnalyze), ".sarif");

            var output = ScopeProgramAnalysis.ScopeProgramAnalysis.AnalyzeDll(inputDll, ScopeProgramAnalysis.ScopeProgramAnalysis.ScopeMethodKind.All, false);

            List<string> ret = new List<string>();

            if (output == null)
            {
                ret.Add(String.Join("\t", new string[] {
                    inputDll,
                    ScopeProgramAnalysis.AnalysisStats.StatsAsString(),
                    "__ERROR__" }));
            }
            else
            {
                ScopeProgramAnalysis.ScopeProgramAnalysis.WriteSarifOutput(output, outputPath);
                var depStream = ScopeProgramAnalysis.ScopeProgramAnalysis.ExtractDependencyStats(output);
                foreach (var x in depStream)
                {
                    var processorName = x.Item1;
                    ScopeProgramAnalysis.ScopeProgramAnalysis.DependencyStats stats = x.Item2;
                    ret.Add(String.Join("\t", new string[] {
                        inputDll,
                        ScopeProgramAnalysis.AnalysisStats.StatsAsString(),
                        processorName,
                        stats.InputHasTop.ToString(),
                        stats.OutputHasTop.ToString(),
                        stats.TopHappened.ToString(),
                        stats.PassThroughColumns.Count.ToString(),
                        String.Join(",",stats.PassThroughColumns),
                        stats.UnreadInputs.Count.ToString(),
                        String.Join(",", stats.UnreadInputs)
                    }));
                }
            }
            var retStr = String.Join("\r\n", ret) + "\r\n";
            using (var mutex = new System.Threading.Mutex(false, "ScopeDependencyAnalysis_OutputSummaryMutex"))
            {
                mutex.WaitOne();
                File.AppendAllText(outputSummaryFile, retStr);
                mutex.ReleaseMutex();
            }
        }
    }
}
