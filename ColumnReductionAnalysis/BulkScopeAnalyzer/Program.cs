using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;

namespace BulkScopeAnalyzer
{
    class Program
    {
        static void Main(string[] args)
        {
            Stopwatch stopWatch = new Stopwatch();
            stopWatch.Start();

            string mainFolder = @"C:\Users\t-zpavli\Desktop\scope benchmarks\real examples";
            //string mainFolder = @"\\MADANM2\parasail\ScopeSurvey\ScopeMapAccess\bin\LocalDebug";
            var subdirs = SubDirectoryPaths(mainFolder);
            string libPath = "C:\\Users\\t-zpavli\\Desktop\\libs";
            string outPath = "bulk-trace-output";

            ScopeAnalyzer.Utils.SetOutput(outPath);
           
            var cumulativeStats = new ScopeAnalyzer.ScopeAnalysisStats();
            foreach (var subdir in subdirs)
            {
                ScopeAnalyzer.Utils.WriteLine("**************** ANALYZING " + subdir + " * ***************");

                try
                {
                    var stats = ScopeAnalyzer.Program.AnalyzeAssemblies(new string[] { subdir, libPath});
                    ScopeAnalyzer.Utils.WriteLine("\nLOCAL STATS:");
                    ScopeAnalyzer.Program.PrintScopeAnalysisStats(stats);

                    cumulativeStats.Assemblies += stats.Assemblies;
                    cumulativeStats.AssembliesLoaded += stats.AssembliesLoaded;
                    cumulativeStats.FailedMethods += stats.FailedMethods;
                    cumulativeStats.Methods += stats.Methods;
                    cumulativeStats.MethodsWithExceptions += stats.MethodsWithExceptions;
                    cumulativeStats.NotEscapeDummies += stats.NotEscapeDummies;
                }
                catch (Exception e)
                {
                    ScopeAnalyzer.Utils.WriteLine("TOP LEVEL ERROR: " + e.Message);
                }        
            }

            ScopeAnalyzer.Utils.WriteLine("\nGLOBAL STATS:");
            ScopeAnalyzer.Program.PrintScopeAnalysisStats(cumulativeStats);

            var ts = stopWatch.Elapsed;
            string elapsedTime = String.Format("{0:00}:{1:00}:{2:00}.{3:00}",
                ts.Hours, ts.Minutes, ts.Seconds,
                ts.Milliseconds / 10);
            ScopeAnalyzer.Utils.WriteLine("\nTotal time: " + elapsedTime);
        }


        private static string[] SubDirectoryPaths(string dir)
        {
            return Directory.GetDirectories(dir);
        }
    }
}
