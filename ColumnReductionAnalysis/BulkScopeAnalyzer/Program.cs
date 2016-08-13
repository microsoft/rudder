using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using Microsoft.Cci;
using Frontend;
using ScopeAnalyzer;


namespace BulkScopeAnalyzer
{
    class Program
    {
        static void Main(string[] args)
        {
            DoParallel();
            //DoSequential();
        }

        static void DoParallel()
        {
            //string mainFolder = @"C:\Users\t-zpavli\Desktop\scope benchmarks\real examples";
            string mainFolder = @"\\madanm2\parasail2\TFS\\parasail\ScopeSurvey\AutoDownloader\bin\Debug";
            string libPath = @"\\madanm2\parasail2\TFS\parasail\ScopeSurvey\AutoDownloader\bin\Debug";
            string scopeAnalyzer = @"C:\Users\t-zpavli\Desktop\dfa-analysis\zvonimir\analysis-net\ScopeAnalyzer\bin\\Debug\ScopeAnalyzer.exe";
            string outputPrefix = @"C:\Users\t-zpavli\Desktop\test output\";

            var subdirs = SubDirectoryPaths(mainFolder);
            var dlls = new List<string>();
            foreach (var subdir in subdirs)
            {
                var libs = LibraryPaths(subdir);
                dlls.AddRange(libs);
                CreateProcessorIdMapping(subdir, libs, Utils.PROCESSOR_ID_MAPPING_NAME);              
            }

            Console.WriteLine(String.Format("Analyzing {0} Scope projects with {1} dlls\n", subdirs.Length, dlls.Count));
            for (int i = 0; i < dlls.Count; i++)
            {
                if (i >= 100 && i % 100 == 0)
                {
                    Console.WriteLine("Giving myself 5 minutes...");
                    Thread.Sleep(300000);
                }

                Console.WriteLine("Spawning process " + (i + 1));
                string output = outputPrefix + String.Format("output_{0}.txt", (i + 1));
                string input = dlls[i];

                Process process = new Process();
                process.StartInfo.FileName = scopeAnalyzer;
                process.StartInfo.Arguments = String.Format("/assembly:\"{0}\" /libs:\"{1}\" /output:\"{2}\"", input, libPath, output);             
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.CreateNoWindow = true;
                process.Start();              
            }            
        }


     


        private static string[] SubDirectoryPaths(string dir)
        {
            return Directory.GetDirectories(dir);
        }

        private static List<string> LibraryPaths(string dir)
        {
            var dlls = new List<string>();
            try
            {
                dlls = Utils.CollectAssemblies(dir);
            }
            catch (Exception e)
            {
                Console.WriteLine("WARNING: failed to get dll paths " + e.Message);
            }
            return dlls;
        }


        private static void CreateProcessorIdMapping(string subdir, IEnumerable<string> libs, string outpath)
        {
            var mainDll = subdir + "\\" + Utils.MAIN_DLL_NAME;
            if (!libs.Contains(mainDll))
            {
                Console.WriteLine("Did not find main scope dll: " + subdir);
                return;
            }

            // delete existing processor id mapping file
            outpath = subdir + "\\" + outpath;
            File.Delete(outpath);
            
            Assembly asm;
            try
            {
                var host = new PeReader.DefaultHost();
                asm = new Assembly(host);
                asm.Load(mainDll);

                var mappingAnalyzer = new ProcessorMappingAnalyzer(asm);
                mappingAnalyzer.Analyze();
                var results = mappingAnalyzer.ProcessorIdMapping;
                var summary = String.Empty;
                foreach(var k in results.Keys)
                {
                    summary += k + "\t" + results[k] + "\n";
                }

                using (StreamWriter file = new StreamWriter(outpath))
                {
                    file.Write(summary);
                }
                Console.WriteLine("Successfully extracted processor to id mapping for the main dll: " + subdir);
            }
            catch
            {
                Console.WriteLine("WARNING: failed to extract processor to id mapping the main dll: " + subdir);
                return;
            }
        }


        #region deprecated

        //static void DoSequential()
        //{
        //    Stopwatch stopWatch = new Stopwatch();
        //    stopWatch.Start();

        //    string mainFolder = @"C:\Users\t-zpavli\Desktop\scope benchmarks\real examples";
        //    //string mainFolder = @"\\MADANM2\parasail\ScopeSurvey\ScopeMapAccess\bin\LocalDebug";
        //    var subdirs = SubDirectoryPaths(mainFolder);

        //    //string libPath = "C:\\Users\\t-zpavli\\Desktop\\libs";
        //    string libPath = @"\\MADANM2\\parasail\\ScopeSurvey\\ScopeMapAccess\\bin\\LocalDebug";
        //    string outPath = "bulk-trace-output";

        //    ScopeAnalyzer.Utils.SetOutput(outPath);
        //    ScopeAnalyzer.Utils.WriteLine("Analyzing " + subdirs.Count() + " Scope projects.\n");

        //    var cumulativeStats = new ScopeAnalyzer.ScopeAnalysisStats();
        //    foreach (var subdir in subdirs)
        //    {
        //        ScopeAnalyzer.Utils.WriteLine("**************** ANALYZING " + subdir + " * ***************");

        //        try
        //        {
        //            var stats = ScopeAnalyzer.Program.AnalyzeAssemblies(new string[] { subdir, libPath });
        //            ScopeAnalyzer.Utils.WriteLine("\nLocal stats:");
        //            ScopeAnalyzer.Program.PrintScopeAnalysisStats(stats);

        //            cumulativeStats.Assemblies += stats.Assemblies;
        //            cumulativeStats.AssembliesLoaded += stats.AssembliesLoaded;
        //            cumulativeStats.FailedMethods += stats.FailedMethods;
        //            cumulativeStats.Methods += stats.Methods;
        //            cumulativeStats.UnsupportedMethods += stats.UnsupportedMethods;
        //            cumulativeStats.InterestingMethods += stats.InterestingMethods;
        //            cumulativeStats.NotCPropagationDummies += stats.InterestingMethods;
        //            cumulativeStats.NotEscapeDummies += stats.NotEscapeDummies;
        //            cumulativeStats.NotColumnDummies += stats.NotColumnDummies;
        //        }
        //        catch (Exception e)
        //        {
        //            ScopeAnalyzer.Utils.WriteLine("TOP LEVEL ERROR: " + e.Message);
        //        }
        //    }

        //    ScopeAnalyzer.Utils.WriteLine("\n\nGLOBAL STATS:");
        //    ScopeAnalyzer.Program.PrintScopeAnalysisStats(cumulativeStats);

        //    var ts = stopWatch.Elapsed;
        //    string elapsedTime = String.Format("{0:00}:{1:00}:{2:00}.{3:00}",
        //        ts.Hours, ts.Minutes, ts.Seconds,
        //        ts.Milliseconds / 10);
        //    ScopeAnalyzer.Utils.WriteLine("\nTotal time: " + elapsedTime);
        //}
        #endregion
    }
}
