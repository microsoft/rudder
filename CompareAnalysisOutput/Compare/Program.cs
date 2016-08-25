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
            if (args.Length < 2)
            {
                Console.WriteLine("Usage: Compare.exe <sarif file> <xml file>");
                return -1;
            }
            var sarifFile = args[0];
            if (!File.Exists(sarifFile))
            {
                Console.WriteLine("Error: Sarif file not found: {0}", sarifFile);
                return -1;
            }

            var xmlFile = args[1];
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

                    var inputSchema = processor.Descendants("input").FirstOrDefault().Attribute("schema");
                    var outputSchema = processor.Descendants("output").FirstOrDefault().Attribute("schema");

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
                        } else if (result.Id == "Summary")
                        {
                            // then this is the summary of the inputs and outputs for this processor
                            var inputs = result.GetProperty<List<string>>("Inputs");
                            var outputs = result.GetProperty<List<string>>("Outputs");
                            if (inputs.Any(i => i.Equals("Col(Input,_All_)")))
                            {
                                // then there was a CopyTo call in the processor that copied the input to the output
                                Console.WriteLine("All input columns were read.");
                            } else
                            {
                                Console.WriteLine("Only some input columns were read.");
                                var inputColumnsRead = inputs.Select(i => i.Split(',')[1].Trim('"', ')'));
                            }
                        }
                    }
                }
            } catch (Exception e)
            {
                Console.WriteLine("Error: exception occurred ({0}, {1}: {2}", sarifFile, xmlFile, e.Message);
                return -1;
            }

            return 0; // success
        }
    }
}
