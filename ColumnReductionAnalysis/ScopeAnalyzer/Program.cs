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
using System.Xml.Linq;
using ScopeAnalyzer.Misc;

namespace ScopeAnalyzer
{
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
        public int InterestingMethods;

        public int NotEscapeDummies;
        public int NotCPropagationDummies;
        public int NotColumnDummies;

        public int Mapped;

        public int ColumnsContained;
        public int ColumnsEqual;
        public int ColumnsWarnings;
        public int ColumnsSavings;

        public int ColumnStringAccesses;
        public int ColumnIndexAccesses;

        public ScopeAnalysisStats(int assemblies = 0)
        {
            Assemblies = assemblies;
            AssembliesLoaded = 0;
            Methods = FailedMethods = UnsupportedMethods = InterestingMethods = 0;
            NotEscapeDummies = NotCPropagationDummies = NotColumnDummies = 0;
            Mapped = 0;
            ColumnsContained = ColumnsEqual = ColumnsWarnings = ColumnsSavings = 0;
            ColumnIndexAccesses = ColumnStringAccesses = 0;
        }

        public override string ToString()
        {
            string summary = String.Empty;
            summary += "Assemblies: " + Assemblies + "\n";
            summary += "Assemblies loaded: " + AssembliesLoaded + "\n";
            summary += "\n";
            summary += "Methods: " + Methods + "\n";
            summary += "Methods failed: " + FailedMethods + "\n";
            summary += "\n";
            summary += "Interesting methods (not failed): " + InterestingMethods + "\n";
            summary += "Unsupported feature methods: " + UnsupportedMethods + "\n";
            summary += "\n";
            summary += "Concrete-columns-found methods: " + NotColumnDummies + "\n";
            summary += "\n";
            summary += "Concrete methods successfully mapped: " + Mapped + "\n";
            summary += "Used columns proper subset: " + ColumnsContained + "\n";
            summary += "Used columns equal: " + ColumnsEqual + "\n";
            summary += "Used columns warnings: " + ColumnsWarnings + "\n";
            summary += "Used columns savings: " + ColumnsSavings + "\n";
            summary += "\n";
            summary += "Used columns string accesses: " + ColumnStringAccesses + "\n";
            summary += "Used columns index accesses: " + ColumnIndexAccesses + "\n";
            return summary;
        }
    }



    public static class Program
    {
        public static void Main(string[] args)
        {
            var stats = AnalyzeAssemblies(args);
            Utils.WriteLine(stats.ToString());
            Utils.WriteLine("SUCCESS");
            Utils.OutputClose();
        }


        public static ScopeAnalysisStats AnalyzeAssemblies(string[] args)
        {
            Options options = Options.ParseCommandLineArguments(args);
            if (options.OutputPath != null) Utils.SetOutput(options.OutputPath);

            var vertexDef = LoadVertexDef(options);
            var processorIdMapping = LoadProcessorMapping(options);
           
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

                    // If processor to id mapping and xml with id information are both available, 
                    // then we ask ScopeAnalysis to analyze only those processors mentioned in the mapping.
                    ScopeAnalysis analysis = new ScopeAnalysis(host, mAssembly, assemblies.Item2, 
                                                                processorIdMapping == null || vertexDef == null? null : processorIdMapping.Keys);
                    analysis.Analyze();

                    //Update the stats.
                    UpdateStats(analysis.Results, ref stats, vertexDef, processorIdMapping);

                    Utils.WriteLine("\n====== Done analyzing the assembly  =========\n");
                }
                catch (ScopeAnalysis.MissingScopeMetadataException e)
                {
                    Utils.WriteLine("ASSEMBLY WARNING: " + e.Message);
                }
                catch (Exception e)
                {
                    Utils.WriteLine("ASSEMBLY FAILURE: " + e.Message);
                }
            }
            return stats;
        }

  


        private static void UpdateStats(IEnumerable<ScopeMethodAnalysisResult> results, ref ScopeAnalysisStats stats, 
                                        XElement vDef, Dictionary<string, string> pIdMapping)
        {
            stats.Methods += results.Count();
            stats.FailedMethods += results.Where(r => r.Failed).ToList().Count;

            var interestingResults = results.Where(r => !r.Failed && r.Interesting).ToList();
            stats.InterestingMethods += interestingResults.Count;
            stats.UnsupportedMethods += interestingResults.Where(r => r.Unsupported).ToList().Count;

            stats.NotEscapeDummies += interestingResults.Where(r => !r.EscapeSummary.IsTop).ToList().Count;
            stats.NotCPropagationDummies += interestingResults.Where(r => r.CPropagationSummary != null && !r.CPropagationSummary.IsTop && !r.CPropagationSummary.IsBottom).ToList().Count;

            var concreteResults = interestingResults.Where(r => !r.UsedColumnsSummary.IsTop && !r.UsedColumnsSummary.IsBottom).ToList();
            stats.NotColumnDummies += concreteResults.Count;

            foreach (var result in concreteResults)
            {
                ComputeImprovementStats(result, ref stats, vDef, pIdMapping);
            }
        }


        private static void ComputeImprovementStats(ScopeMethodAnalysisResult result, ref ScopeAnalysisStats stats, 
                                                    XElement vDef, Dictionary<string, string> pIdMapping)
        {
            stats.ColumnIndexAccesses += result.ColumnIndexAccesses;
            stats.ColumnStringAccesses += result.ColumnStringAccesses;

            if (vDef == null || pIdMapping == null)
                return;

            var column = result.UsedColumnsSummary;
            if (column.IsBottom || column.IsTop) return;          

            var pTypeFullName = result.ProcessorType.FullName();
            Utils.WriteLine("Checking column usage for " + pTypeFullName);

            if (!pIdMapping.ContainsKey(pTypeFullName))
            {
                Utils.WriteLine("WARNING: could not match processor mapping: " + pTypeFullName);
                return;
            }

            stats.Mapped += 1;
            try
            {
                var id = pIdMapping[pTypeFullName];
                var operators = vDef.Descendants("operator");
                // Id can appear several times in the xml file since the same reducer can be used multiple times
                // and contained within different Scope vertices.
                var process = operators.Where(op => op.Attribute("id") != null && op.Attribute("id").Value.Equals(id)).ToList().First();

                var input_schema = process.Descendants("input").Single().Attribute("schema").Value.Split(',');
                var inputColumns = new List<string>();
                foreach (var input in input_schema) inputColumns.Add(input.Split(':')[0].Trim());

                var output_schema = process.Descendants("output").Single().Attribute("schema").Value.Split(',');
                var outputColumns = new List<string>();
                foreach (var output in output_schema) outputColumns.Add(output.Split(':')[0].Trim());

                var usedColumns = new HashSet<string>();
                foreach (var c in column.Elements)
                {
                    var val = c.Value;
                    if (val is string)
                    {
                        usedColumns.Add(val as string);
                    }
                    else if (val is int)
                    {
                        int index = Int32.Parse(val.ToString());
                        if (index < inputColumns.Count)
                            usedColumns.Add(inputColumns[index]);
                        if (index < outputColumns.Count)
                            usedColumns.Add(outputColumns[index]);

                        if (index >= inputColumns.Count && index >= outputColumns.Count)
                            Utils.WriteLine("WARNING: index out of schema: " + index);
                    }
                    else
                    {
                        Utils.WriteLine("WARNING: other value type used for indexing besides string and int: " + val);
                        return;
                    }
                }

                var allSchemaColumns = new HashSet<string>(inputColumns.Union(outputColumns));
                if (usedColumns.IsProperSubsetOf(allSchemaColumns))
                {
                    stats.ColumnsContained += 1;
                    stats.ColumnsSavings += (allSchemaColumns.Count - usedColumns.Count);
                    Utils.WriteLine("SAVINGS: used columns subset of defined columns: " + stats.ColumnsSavings);
                }
                else if (allSchemaColumns.SetEquals(usedColumns))
                {
                    stats.ColumnsEqual += 1;
                    Utils.WriteLine("PRECISION: used columns equal to defined columns");
                }
                else
                {
                    Utils.WriteLine("OVERAPPROXIMATION: redundant used columns: " + String.Join(" ", allSchemaColumns));
                    stats.ColumnsWarnings += 1;
                }
            } 
            catch (Exception e)
            {
                Utils.WriteLine(String.Format("ERROR: failed to compute column usage for {0} {1}", pTypeFullName, e.Message));
            }
        }




        [HandleProcessCorruptedStateExceptions]
        public static Tuple<List<Assembly>, List<Assembly>> LoadAssemblies(PeReader.DefaultHost host, Options options)
        {
            // First, load all the reference assemblies.
            var refs = new List<Assembly>();
            foreach (var rassembly in options.ReferenceAssemblies)
            {
                try
                {
                    //TODO: is this a CCI bug?
                    if (rassembly.EndsWith("__ScopeCodeGen__.dll")) continue;

                    var rasm = new Assembly(host);
                    rasm.Load(rassembly);
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

        private static XElement LoadVertexDef(Options options)
        {
            if (File.Exists(options.VertexDefPath))
            {
                try
                {
                    return XElement.Load(options.VertexDefPath);
                }
                catch { }
            }

            Utils.WriteLine(String.Format("WARNING: could not properly load vertex def: {0}", options.VertexDefPath));
            return null;
        }

        private static Dictionary<string, string> LoadProcessorMapping(Options options)
        {
            if (options.ProcessorIdPath == null)
            {
                Utils.WriteLine("No processor to id mapping file available");
                return null;
            }
                
            if (File.Exists(options.ProcessorIdPath))
            {
                try
                {
                    var lines = File.ReadAllLines(options.ProcessorIdPath);
                    var mapping = new Dictionary<string, string>();
                    foreach(var line in lines)
                    {
                        var pair = line.Trim().Split('\t');
                        if (pair.Length != 2)
                            throw new Exception("Processor to id mapping not in correct format!");
                        mapping.Add(pair[0].Trim(), pair[1].Trim());
                    }
                    return mapping;
                }
                catch { }
            }

            Utils.WriteLine(String.Format("WARNING: could not properly load processor to id mapping: {0}", options.ProcessorIdPath));
            return null;
        }
    }
}
