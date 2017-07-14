using Microsoft.CodeAnalysis.Sarif;
using Microsoft.CodeAnalysis.Sarif.Readers;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace Compare
{
    class Program
    {
        static int Main(string[] args)
        {
            if (args.Length < 1 || args[0].Contains("?"))
            {
                Console.WriteLine("Usage: Compare.exe <sarif file> <xml file>");
                return -1;
            }
            if (args.Length == 2)
            {
                CompareSarifAndXML(args[0], args[1]);
            }
            if (args.Length == 1)
            {
                ComputePassThroughColumns(args[0]);
            }

            return 0; // success
        }

        struct Stats
        {
            public int TotalOutputColumns;
            public int PassThrough;

            public int TotalInputColumns { get; internal set; }
            public int TotalSchemaInputs { get; internal set; }
            public int TotalSchemaOutputs { get; internal set; }
            public long ZvonimirAnalysisTime { get; internal set; }
            public long DepencyAnalysysTime { get; internal set; }
        }

        static int ComputePassThroughColumns(string sarifFile)
        {
            Dictionary<string, Stats> analysisStats = new Dictionary<string, Stats>();

            if (!File.Exists(sarifFile))
            {
                Console.WriteLine("Error: Sarif file not found: {0}", sarifFile);
                return -1;
            }

            try
            {
                string logContents = File.ReadAllText(sarifFile);

                var settings = new JsonSerializerSettings()
                {
                    ContractResolver = SarifContractResolver.Instance
                };

                SarifLog log = JsonConvert.DeserializeObject<SarifLog>(logContents, settings);


                foreach (var run in log.Runs)
                {
                    var tool = run.Tool.Name;
                    if (tool != "ScopeProgramAnalysis") continue;
                    var splitId = run.Id.Split('|');

                    var processNumber = "0";
                    var processorName = "";
                    if (splitId.Length == 2)
                    {
                        processorName = splitId[0];
                        processNumber = splitId[1];
                    }
                    else
                    {
                        processorName = run.Id;
                    }

                    Console.WriteLine("Processor '{0}'+ Number '{1}'", processorName, processNumber);

                    var visitedColumns = new HashSet<string>();

                    var passThroughColumns = new List<Tuple<string, string>>();
                    var topHappened = false;
                    var totalColumns = 0;
                    foreach (var result in run.Results)
                    {
                        if (result.Id == "SingleColumn")
                        {
                            var columnProperty = result.GetProperty("column");
                            if (!columnProperty.StartsWith("Col(")) continue;
                            var columnName = columnProperty.Split(',')[1].Trim('"', ')');
                            if (columnName == "_All_")
                            {
                                // ignore this for now because it is more complicated
                                continue;
                            }
                            if (columnName == "_TOP_")
                            {
                                topHappened = true;
                            }
                            if (visitedColumns.Contains(columnName))
                                continue;

                            visitedColumns.Add(columnName);

                            var dataDependencies = result.GetProperty<List<string>>("data depends");
                            if (dataDependencies.Count == 1)
                            {
                                var inputColumn = dataDependencies[0];
                                if (!inputColumn.StartsWith("Col("))
                                {
                                    // then it is dependent on only one thing, but that thing is not a column.
                                    continue;
                                }
                                // then it is a pass-through column
                                var inputColumnName = inputColumn.Split(',')[1].Trim('"', ')');
                                passThroughColumns.Add(Tuple.Create(columnName, inputColumnName));
                            }
                        }
                        else if (result.Id == "Summary")
                        {
                            // Do nothing
                            var columnProperty = result.GetProperty<List<string>>("Inputs");
                            var totalInputColumns = columnProperty.Count;
                            var inputHasTop = columnProperty.Contains("Col(Input,_TOP_)");

                            columnProperty = result.GetProperty<List<string>>("Outputs");
                            var totalOutputColumns = columnProperty.Count;
                            var outputHasTop = columnProperty.Contains("Col(Output,_TOP_)");

                            columnProperty = result.GetProperty<List<string>>("SchemaInputs");
                            var columnsSchemaInput = columnProperty.Count;

                            columnProperty = result.GetProperty<List<string>>("SchemaOutputs");
                            var columnsSchemaOutput = columnProperty.Count;

                            var zvoTime = result.GetProperty<int>("BagOColumnsTime");
                            var depAnalysisTime = result.GetProperty<int>("DependencyAnalysisTime");


                            analysisStats.Add(processorName, new Stats() {
                                        PassThrough = !topHappened? passThroughColumns.Count: -1,
                                        TotalOutputColumns = !outputHasTop ? totalOutputColumns :-1,
                                        TotalInputColumns = !inputHasTop ? totalInputColumns:-1,
                                        TotalSchemaInputs = columnsSchemaInput,
                                        TotalSchemaOutputs =  columnsSchemaOutput,
                                        ZvonimirAnalysisTime = zvoTime,
                                        DepencyAnalysysTime = depAnalysisTime
                            });
                        }
                    }
                    if (passThroughColumns.Count == 0)
                    {
                        Console.WriteLine("No pass through columns");
                    }
                    else
                    {
                        if (topHappened)
                        {
                            Console.WriteLine("Had to give up: there was an unknown column that was written to.");
                        }
                        else
                        {
                            foreach (var t in passThroughColumns)
                            {
                                Console.WriteLine("Passthrough: {0} depends on {1}", t.Item1, t.Item2);
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("Error: exception occurred ({0}, {1}: {2}", sarifFile, e.Message);
                return -1;
            }

            Console.WriteLine("===============");
            Console.WriteLine("=== Summary ===");
            foreach(var entry in analysisStats)
            {
                var data = entry.Value;
                Console.WriteLine("{0}+ {1}+ {2}+ {3}+ {4}+ {5} +{6} +{7}", entry.Key, data.PassThrough, 
                                data.TotalOutputColumns, data.TotalInputColumns, 
                                data.TotalSchemaOutputs, data.TotalSchemaInputs,
                                data.ZvonimirAnalysisTime, data.DepencyAnalysysTime);
            }

            return 0; // success
        }
        static int CompareSarifAndXML(string sarifFile, string xmlFile)
        {
            if (!File.Exists(sarifFile))
            {
                Console.WriteLine("Error: Sarif file not found: {0}", sarifFile);
                return -1;
            }

            if (!File.Exists(xmlFile))
            {
                Console.WriteLine("Error: XML file not found: {0}", xmlFile);
                return -1;
            }

            try
            {
                string logContents = File.ReadAllText(sarifFile);

                var settings = new JsonSerializerSettings()
                {
                    ContractResolver = SarifContractResolver.Instance
                };

                SarifLog log = JsonConvert.DeserializeObject<SarifLog>(logContents, settings);

                XElement x = XElement.Load(xmlFile);

                foreach (var run in log.Runs)
                {
                    var tool = run.Tool.Name;
                    if (tool != "ScopeProgramAnalysis") continue;
                    var splitId = run.Id.Split('|');
                    if (splitId.Length != 2) continue;
                    var processorName = splitId[0];
                    var processNumber = splitId[1];

                    var processor = x
                        .Descendants("operator")
                        .Where(op => op.Attribute("id") != null && op.Attribute("id").Value == processNumber)
                        .FirstOrDefault()
                        ;

                    Console.Write("Processor: {0}. ", processor.Attribute("className").Value);

                    var inputSchema = ParseColumns(processor.Descendants("input").FirstOrDefault().Attribute("schema").Value);
                    var outputSchema = ParseColumns(processor.Descendants("output").FirstOrDefault().Attribute("schema").Value);

                    foreach (var result in run.Results)
                    {
                        if (result.Id == "SingleColumn")
                        {
                            foreach (var propertyName in result.PropertyNames)
                            {
                                if (propertyName == "column")
                                {
                                    var columnName = result.GetProperty(propertyName);
                                }
                                else
                                {
                                    var property = result.GetProperty<List<string>>(propertyName);
                                }
                            }
                        }
                        else if (result.Id == "Summary")
                        {
                            // then this is the summary of the inputs and outputs for this processor
                            var inputs = result.GetProperty<List<string>>("Inputs");
                            var outputs = result.GetProperty<List<string>>("Outputs");
                            if (inputs.Any(i => i.Equals("Col(Input,_All_)")))
                            {
                                // then there was a CopyTo call in the processor that copied the input to the output
                                Console.WriteLine("All input columns were read.");
                            }
                            else
                            {
                                var inputColumnsRead = inputs.Select(i => i.Split(',')[1].Trim('"', ')'));
                                if (inputColumnsRead.Count() != inputSchema.Count())
                                {
                                    Console.WriteLine("Only some input columns were read.");
                                    var unusedColumns = inputSchema.Where(c => !inputColumnsRead.Contains(c.Name));
                                    Console.WriteLine("Unused columns: {0}", String.Join(",", unusedColumns.Select(c => c.Name)));
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("Error: exception occurred ({0}, {1}: {2}", sarifFile, xmlFile, e.Message);
                return -1;
            }

            return 0; // success
        }

        public struct Column
        {
            public string Name;
            public int Index;
            public string Type;
        }
        //private static IEnumerable<Column> ParseColumns(string schema)
        //{
        //    // schema looks like: "JobGUID:string,SubmitTime:DateTime?,NewColumn:string"
        //    return schema
        //        .Split(',')
        //        .Select((c, i) => { var a = c.Split(':'); return new Column() { Name = a[0], Index = i, Type = a[1] }; });
        //}

        private static IEnumerable<Column> ParseColumns(string schema)
        {
            // schema looks like: "JobGUID:string,SubmitTime:DateTime?,NewColumn:string"
            var schemaList = schema.Split(',');
            for (int i = 0; i < schemaList.Count(); i++)
            {
                if (schemaList[i].Contains("<") && i < schemaList.Count() && schemaList[i + 1].Contains(">"))
                {
                    schemaList[i] += schemaList[i + 1];
                    schemaList[i + 1] = "";
                    i++;
                }
            }
            return schemaList.Where(elem => !String.IsNullOrEmpty(elem)).Select((c, i) => { var a = c.Split(':'); return new Column { Name = a[0], Index = i, Type = a[1] }; });
        }

    }
}
