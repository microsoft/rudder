// Copyright (c) Edgardo Zoppi.  All Rights Reserved.  Licensed under the MIT License.  See License.txt in the project root for license information.

using System;
using System.IO;
using System.Runtime.ExceptionServices;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Cci;
using Frontend;
using Backend;

namespace ScopeAnalyzer
{
    /// <summary>
    /// Utilities for double-printing and similar.
    /// </summary>
    public static class Utils
    {
        public static StreamWriter Output;

        public static void WriteLine(string message)
        {
            Console.WriteLine(message);
            if (Output != Console.Out)
            {
                Output.WriteLine(message);
                Output.Flush();
            }
        }

        public static void SetOutput(string path)
        {
            var fs = new FileStream(path, FileMode.Create);
            Output = new StreamWriter(fs);
        }

    }

    /// <summary>
    /// Struct that saves basic statistics about Scope analysis.
    /// </summary>
    public struct ScopeAnalysisStats
    {
        public int Assemblies;
        public int AssembliesLoaded;
        public int Methods;
        public int FailedMethods;
        public int UnsupportedMethods;
        public int NotEscapeDummies;

        public ScopeAnalysisStats(int assemblies = 0, int assembliesLoaded = 0, int methods = 0, int failedMethods = 0,
                                    int methodsWithExceptions = 0, int notEscapeDummies = 0)
        {
            Assemblies = assemblies;
            AssembliesLoaded = assembliesLoaded;
            Methods = methods;
            FailedMethods = failedMethods;
            UnsupportedMethods = methodsWithExceptions;
            NotEscapeDummies = notEscapeDummies;
        }
    }



    public static class Program
    {
        public static void Main(string[] args)
        {
            var stats = AnalyzeAssemblies(args);
            PrintScopeAnalysisStats(stats);
        }


        public static ScopeAnalysisStats AnalyzeAssemblies(string[] args)
        {
            Options options;
            try
            {
                options = Options.ParseCommandLineArguments(args);
                //hack
                if (options.OutputPath != null) Utils.SetOutput(options.OutputPath);
            }
            catch (Exception e)
            {
                Utils.WriteLine("Problems with parsing the command line arguments:");
                Utils.WriteLine(e.ToString());
                throw new ParsingOptionsException(e.Message);
            }

            var host = new PeReader.DefaultHost();
            var assemblies = LoadAssemblies(host, options);
            var stats = new ScopeAnalysisStats();
            stats.Assemblies = options.Assemblies.Count;

            var mainAssemblies = assemblies.Item1;
            stats.AssembliesLoaded = mainAssemblies.Count;
            foreach (var mAssembly in mainAssemblies)
            {
                try
                {
                    Utils.WriteLine("\n====== Analyzing assembly: " + mAssembly.FileName + " =========\n");

                    ScopeAnalysis analysis = new ScopeAnalysis(host, mAssembly, assemblies.Item2);
                    analysis.Analyze();
                    var results = analysis.Results;

                    // Save the stats.
                    stats.Methods += results.Count();
                    var goodMethods = results.Where(r => !r.Failed).ToList();
                    stats.FailedMethods += (results.Count() - goodMethods.Count);
                    stats.UnsupportedMethods += goodMethods.Where(r => r.Unsupported).ToList().Count;
                    stats.NotEscapeDummies += goodMethods.Where(r => !r.EscapeSummary.IsTop).ToList().Count;

                    Utils.WriteLine("\n====== Done analyzing the assembly  =========\n");
                }
                catch (ScopeAnalysis.NotInterestingScopeScript e)
                {
                    Utils.WriteLine("ASSEMBLY WARNING: " + e.Message);
                }
                catch (Exception e)
                {
                    Utils.WriteLine("ASSEMBLY FAILURE: " + e.Message);
                    //Utils.WriteLine(e.StackTrace);
                }
            }
            return stats;
        }

        [HandleProcessCorruptedStateExceptions]
        private static Tuple<List<Assembly>, List<Assembly>> LoadAssemblies(PeReader.DefaultHost host, Options options)
        {
            // First, load all the reference assemblies.
            var refs = new List<Assembly>();
            foreach (var rassembly in options.ReferenceAssemblies)
            {
                try
                {
                    //HAAAAACK
                    //TODO: is this a CCI bug?
                    if (rassembly.EndsWith("__ScopeCodeGen__.dll")) continue;

                    var rasm = new Assembly(host);
                    rasm.Load(rassembly, false);
                    refs.Add(rasm);
                    Utils.WriteLine("Successfully loaded reference assembly: " + rassembly);
                }
                catch (AccessViolationException e)
                {
                    Utils.WriteLine("Warning: perhaps this is a library with unmanaged code?");
                }

                catch (Exception e)
                {
                    Utils.WriteLine(String.Format("Warning: failed to load reference assembly {0} ({1})", rassembly, e.Message));
                }
            }

            // Now, load the main assemblies.
            var assemblies = new List<Assembly>();
            foreach (var assembly in options.Assemblies)
            {
                try
                {
                    var asm = new Assembly(host);
                    asm.Load(assembly);
                    assemblies.Add(asm);
                    Utils.WriteLine("Successfully loaded main assembly: " + assembly);
                }
                catch (AccessViolationException e)
                {
                    Utils.WriteLine("Warning: perhaps this is a library with unmanaged code?");
                }
                catch (Exception e)
                {
                    Utils.WriteLine(String.Format("LOAD FAILURE: failed to load main assembly {0} ({1})", assembly, e.Message));
                } 
            }

            Types.Initialize(host);

            return new Tuple<List<Assembly>, List<Assembly>>(assemblies, refs);
        }


        public static void PrintScopeAnalysisStats(ScopeAnalysisStats stats)
        {
            Utils.WriteLine("Assemblies: " + stats.Assemblies);
            Utils.WriteLine("Assemblies loaded: " + stats.AssembliesLoaded);
            Utils.WriteLine("");
            Utils.WriteLine("Methods failed: " + stats.FailedMethods);
            Utils.WriteLine("Interesting methods (not failed): " + (stats.Methods - stats.FailedMethods));           
            Utils.WriteLine("");
            Utils.WriteLine("Unsupported feature: " + stats.UnsupportedMethods);
            Utils.WriteLine("Not escape dummies: " + stats.NotEscapeDummies);
        }

    }
}
