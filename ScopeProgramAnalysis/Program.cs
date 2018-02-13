using Backend.Utils;
using Microsoft.CodeAnalysis.Sarif;
using Microsoft.CodeAnalysis.Sarif.Readers;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Backend.Analyses;
using System.Globalization;
using ScopeProgramAnalysis.Framework;
using System.Text.RegularExpressions;
using Microsoft.Cci;
using Backend;
using Backend.Model;
using System.Threading.Tasks;
using System.Diagnostics;
using static ScopeProgramAnalysis.ScopeProgramAnalysis;

namespace ScopeProgramAnalysis
{
    public class Program
    {
        static void Main(string[] args)
        {
            var useScopeFactory = true;
            var scopeKind = ScopeMethodKind.All;
			var interProcAnalysis = true;

			string input;

            //const string root = @"c:\users\t-diga\source\repos\scopeexamples\metting\";
            //const string input = root + @"__ScopeCodeGen__.dll";
            //scopeKind = ScopeMethodKind.Reducer;
            //const string input = @"D:\MadanExamples\3213e974-d0b7-4825-9fd4-6068890d3327\__ScopeCodeGen__.dll";

            // Mike example: FileChunker
            //const string input = @"C:\Users\t-diga\Source\Repos\ScopeExamples\ExampleWithXML\69FDA6E7DB709175\ScopeMapAccess_4D88E34D25958F3B\__ScopeCodeGen__.dll";
            //const string input = @"\\research\root\public\mbarnett\Parasail\ExampleWithXML\69FDA6E7DB709175\ScopeMapAccess_4D88E34D25958F3B\__ScopeCodeGen__.dll";
            //const string input = @"C:\Users\t-diga\Source\Repos\ScopeExamples\\ExampleWithXML\ILAnalyzer.exe";
            //useScopeFactory = false;

            // const string input = @"D:\MadanExamples\13c04344-e910-4828-8eae-bc49925b4c9b\__ScopeCodeGen__.dll";
            //const string input = @"D:\MadanExamples\15444206-b209-437e-b23b-2d916f18cd35\__ScopeCodeGen__.dll";
            // const string input = @"D:\MadanExamples\208afef3-4cae-428c-a7a2-75ea7350b1ea\__ScopeCodeGen__.dll";
            //const string input = @"D:\MadanExamples\9e5dad20-19f4-4a4d-8b95-319fd2e047f8\__ScopeCodeGen__.dll";
            //const string input = @"D:\MadanExamples\0f0d828a-5a11-4750-83e6-4e2294c51e5a\__ScopeCodeGen__.dll";

            // This one gave me out of memory...
            //const string input = @"D:\MadanExamples\901f84e4-be76-49fe-8fc1-5508a8b561a6\__ScopeCodeGen__.dll";

            //const string input = @"\\madanm2\parasail2\TFS\parasail\ScopeSurvey\AutoDownloader\bin\Debug\03374553-725d-4d47-9400-90a9d168a658\__ScopeCodeGen__.dll";

            //const string input = @"D:\MadanExamples\3d2b4d2c-42b4-45c3-be19-71c1266ae835\__ScopeCodeGen__.dll";
            // const string input  = @" D:\MadanExamples\0061a95f-fbe7-4b0d-9878-c7fea686bec6\__ScopeCodeGen__.dll";
            // const string input = @"D:\MadanExamples\01c085ee-9e42-418d-b0e8-a94ee1a0d76b\__ScopeCodeGen__.dll";
            // const string input = @"\\madanm2\parasail2\TFS\parasail\ScopeSurvey\AutoDownloader\bin\Debug\49208328-24d1-42fb-8fa4-f74ba84760d3\__ScopeCodeGen__.dll";
            //const string input = @"\\madanm2\parasail2\TFS\parasail\ScopeSurvey\AutoDownloader\bin\Debug\8aecff28-5719-4b34-9f9f-cb3135df67d4\__ScopeCodeGen__.dll";
            //const string input = @"\\madanm2\parasail2\TFS\parasail\ScopeSurvey\AutoDownloader\bin\Debug\018c2f92-f63d-4790-a843-40a1b0e0e58a\__ScopeCodeGen__.dll";

            // From Zvonimir's PDF summary:
            const string zvonimirDirectory = @"\\research\root\public\mbarnett\Parasail\InterestingScopeProjects";
            //const string zvonimirDirectory = @"const string input = @"\\madanm2\parasail2\TFS\parasail\ScopeSurvey\AutoDownloader\bin\Debug";
            // Example 1 
            //string input = Path.Combine(zvonimirDirectory, @"0003cc74-a571-4638-af03-77775c5542c6\__ScopeCodeGen__.dll");
            // Example 2
            //string input = Path.Combine(zvonimirDirectory, @"0ce5ea59-dec8-4f6f-be08-0e0746e12515\CdpLogCache.Scopelib.dll");
            // Example 3
            //input = Path.Combine(zvonimirDirectory, @"10c15390-ea74-4b20-b87e-3f3992a130c0\__ScopeCodeGen__.dll");
            // Example 4
            //input = Path.Combine(zvonimirDirectory, @"2407f5f1-0930-4ce5-88d3-e288a86e54ca\__ScopeCodeGen__.dll");
            // Example 5
            //input = Path.Combine(zvonimirDirectory, @"3b9f1ec4-0ad8-4bde-879b-65c92d109159\__ScopeCodeGen__.dll");

            //const string input = @"\\madanm2\parasail2\TFS\parasail\ScopeSurvey\AutoDownloader\bin\Debug\0003cc74-a571-4638-af03-77775c5542c6\__ScopeCodeGen__.dll";
            //const string input = @"\\madanm2\parasail2\TFS\parasail\ScopeSurvey\AutoDownloader\bin\Debug\10c15390-ea74-4b20-b87e-3f3992a130c0\__ScopeCodeGen__.dll";
            //const string input = @"\\madanm2\parasail2\TFS\parasail\ScopeSurvey\AutoDownloader\bin\Debug\018c2f92-f63d-4790-a843-40a1b0e0e58a\__ScopeCodeGen__.dll";
            //const string input = @"\\madanm2\parasail2\TFS\parasail\ScopeSurvey\AutoDownloader\bin\Debug\0ab0de7e-6110-4cd4-8c30-6e72c013c2f0\__ScopeCodeGen__.dll";

            // Mike's example: 
            //input = @"\\research\root\public\mbarnett\Parasail\Diego\SimpleProcessors_9E4B4B56B06EFFD2\__ScopeCodeGen__.dll";

            //const string input = @"\\madanm2\parasail2\TFS\parasail\ScopeSurvey\AutoDownloader\bin\Debug\02e7c1bd-42ab-4f5b-8506-d6c49e562790\__ScopeCodeGen__.dll";

            // Loop
            //const string 
            //input = @"\\madanm2\parasail2\TFS\parasail\ScopeSurvey\AutoDownloader\bin\Debug\02e7c1bd-42ab-4f5b-8506-d6c49e562790\__ScopeCodeGen__.dll";

            // const string input = @"\\madanm2\parasail2\TFS\parasail\ScopeSurvey\AutoDownloader\bin\Debug\__ScopeCodeGen__.dll";

            //const string input = @"\\madanm2\parasail2\TFS\parasail\ScopeSurvey\AutoDownloader\bin\Debug\018c2f92-f63d-4790-a843-40a1b0e0e58a\__ScopeCodeGen__.dll";
            //input = @"\\madanm2\parasail2\TFS\parasail\ScopeSurvey\AutoDownloader\bin\Debug\98a9c4a1-6996-4c34-8d33-f7dd140ffbf9\__ScopeCodeGen__.dll";

            //input = @"\\madanm2\parasail2\TFS\parasail\ScopeSurvey\AutoDownloader\bin\Debug\d9b320ac-a1ff-415a-93e3-0d47d3d949ad\__ScopeCodeGen__.dll";

            // input = @"\\madanm2\parasail2\TFS\parasail\ScopeSurvey\AutoDownloader\bin\Debug\69dc12be-aacd-48a5-a776-e2766178a343\Microsoft.Bing.Platform.Inferences.Offline.SignalsCooking.dll";

            //input = @"\\madanm2\parasail2\TFS\parasail\ScopeSurvey\AutoDownloader\bin\Debug\6a02b587-21b6-4b4d-84c5-4caaebc9d5ad\__ScopeCodeGen__.dll";

            //input = @"\\madanm2\parasail2\TFS\parasail\ScopeSurvey\AutoDownloader\bin\Debug\0c3e04fe-75ec-59a8-a3e6-a85aecfe5476\__ScopeCodeGen__.dll";

            //input = @"\\madanm2\parasail2\TFS\parasail\ScopeSurvey\AutoDownloader\bin\Debug\00f41d12-10e8-4aa4-b54d-1c275bd99550\__ScopeCodeGen__.dll";

            // This one show me the problem in the topological order
            //input = @"\\madanm2\parasail2\TFS\parasail\ScopeSurvey\AutoDownloader\bin\Debug\4554b01e-829b-4e37-b818-688b074b00bf\__ScopeCodeGen__.dll";

            // This one is complaining about a missing schema
            //input = @"\\research\root\public\mbarnett\Parasail\First100JobsFromMadan\00e0c351-4bae-4970-989b-92806b1e657c\__ScopeCodeGen__.dll";
            // This one fails with changes of type edgardo did:
            //input = @"\\research\root\public\mbarnett\Parasail\First100JobsFromMadan\0b5243a6-cf68-4c35-8b45-8ce0e0162e14\__ScopeCodeGen__.dll";

            // Could not find column
            //input = @" \\research\root\public\mbarnett\Parasail\First100JobsFromMadan\0c92351b-f81e-4da1-91c3-930c7778fac6\__ScopeCodeGen__.dll";

            // Failed to find Schema
            //input = @"\\research\root\public\mbarnett\Parasail\First100JobsFromMadan\0c92351b-f81e-4da1-91c3-930c7778fac6\__ScopeCodeGen__.dll";

            // Has 27 passthrough out of 31 out
            //input = @"\\madanm2\parasail2\TFS\parasail\ScopeSurvey\AutoDownloader\bin\Debug\02c4581e-781a-4798-8875-162b4d740b5f\__ScopeCodeGen__.dll";

            // Can not find methods in the factory
            //input = @"\\madanm2\parasail2\TFS\parasail\ScopeSurvey\AutoDownloader\bin\Debug\0e4ca5d2-3478-431f-a4ad-f0b256780daf\__ScopeCodeGen__.dll";

            const string madansDirectory = @"\\madanm2\parasail2\TFS\parasail\ScopeSurvey\AutoDownloader\bin\Debug";
            //input = Path.Combine(madansDirectory, @"01dc7d9c-f0bf-44b3-9228-9d22dda03e5d\__ScopeCodeGen__.dll"); //PassThroughReducer
            //input = Path.Combine(madansDirectory, @"2968e7c3-33a0-4a93-8ac8-81cd105bdbc4\__ScopeCodeGen__.dll"); // WindowsLoginSessionPathComputedColumnProcessor
            //input = Path.Combine(madansDirectory, @"11f04fe1-fa82-4de6-9557-e54a82f88e5a\__ScopeCodeGen__.dll"); // LiveIDStructuredStreamDecompileProcessor
            //input = Path.Combine(madansDirectory, @"2c0e5058-12a9-4fee-a36f-1b036f85aaee\__ScopeCodeGen__.dll"); // TopNReducer
            //input = Path.Combine(madansDirectory, @"30b000af-f6ad-413e-9b27-00f5b63aff1f\__ScopeCodeGen__.dll"); // ConfigurablePassThroughReducer

            const string oneHundredJobsDirectory = @"\\research\root\public\mbarnett\Parasail\First100JobsFromMadan";
            //input = Path.Combine(oneHundredJobsDirectory, @"00e0c351-4bae-4970-989b-92806b1e657c\__ScopeCodeGen__.dll");
            //input = Path.Combine(oneHundredJobsDirectory, @"0b610085-e88d-455c-81ea-90c727bbdf58\__ScopeCodeGen__.dll");
            //input = Path.Combine(oneHundredJobsDirectory, @"0ba011a3-fd85-4f85-92ce-e8a230d33dc3\__ScopeCodeGen__.dll");
            // Times out for unknown reason
            //input = Path.Combine(oneHundredJobsDirectory, @"0cb45fd4-ee48-4091-a95b-6ed802173335\__ScopeCodeGen__.dll");
            // Times out for unknown reason
            //input = Path.Combine(oneHundredJobsDirectory, @"0e86b352-b968-40fd-8377-8b3a5812aa61\__ScopeCodeGen__.dll");
            // No __ScopeCodeGen__ assembly
            //input = Path.Combine(oneHundredJobsDirectory, @"000ef3c1-abb3-4a54-8ea1-60c74139d936\__ScopeCodeGen__.dll");
            // __ScopeCodeGen__ assembly, but no processors found
            //input = Path.Combine(oneHundredJobsDirectory, @"00a41169-9711-4a14-bf02-7d068ad6dded\__ScopeCodeGen__.dll");
            //input = @"C:\dev\Bugs\Parasail\099f4b11-eeeb-4357-87aa-2de336b6eb46\__ScopeCodeGen__.dll";
            //input = @"C:\dev\Parasail\ScopeSurvey\ScopeMapAccess\bin\LocalDebug\7d7e61ab-6687-4e2d-99fc-636bf4eb3e0d\__ScopeCodeGen__.dll";
            //input = @"C:\dev\Parasail\ScopeSurvey\ScopeMapAccess\bin\LocalDebug\79dde6c1-efa3-44e6-a842-8397dea70df4\__ScopeCodeGen__.dll";

            //input = @"C:\dev\Parasail\ScopeSurvey\ScopeMapAccess\bin\LocalDebug\17764c26-9312-4d0a-9ac1-9ae08e0303ee\__ScopeCodeGen__.dll";
            input = @"C:\dev\Parasail\ScopeSurvey\ScopeMapAccess\bin\LocalDebug\75f9adff-7f3e-47f5-b282-99518ee7f8b3\__ScopeCodeGen__.dll";

			input = @"C:\Temp\Scope\InterestingScopeProjects\0ce5ea59-dec8-4f6f-be08-0e0746e12515\__ScopeCodeGen__.dll";
			input = @"C:\Temp\Scope\\JsonSurvey\NewtonSoftMethodSurvey_E18FC06FBAF9E44\__ScopeCodeGen__.dll";

			//input = @"C:\Temp\Scope\JsonSurvey\0195e2b3-3fb3-4f36-bc10-cadbfd76c8cd\__ScopeCodeGen__.dll";
			// Example that uses JsonConvert
				//var jsonObj = JsonConvert.DeserializeObject<MyType>(s);
			    //      string a = jsonObj.A;
			//input = @"C:\Temp\Scope\JsonSurvey\SimpleJsonProcessors\bin\Debug\6BEA71AFD72D97FF\UDO1_C79877185261167E\__ScopeCodeGen__.dll";
			
			// Example that uses o = JObject.Parse and o[field] 
			// This one is too complex. I uses complex getters and setters to access the row columns
			// input = @"C:\Temp\Scope\JsonSurvey\0195e2b3-3fb3-4f36-bc10-cadbfd76c8cd\__ScopeCodeGen__.dll";

			// Simpler example made my Mike
			input = @"C:\Temp\Scope\JsonSurvey\ProcessorWithJObject\__ScopeCodeGen__.dll";

			// UDO with JSon extracted by Mike
			input = @"C:\Temp\Scope\JsonSurvey\JobsWithUDOs\0ab0c6d5-731f-5029-8943-32ba867897c1\__ScopeCodeGen__.dll";
			
			// 1 UDO with Json
			input = @"C:\Temp\Scope\JsonSurvey\JobsWithUDOs\0a671f56-c4c1-47da-a0c0-09c5701854c8\__ScopeCodeGen__.dll";
			// 1 UDO with Json + compiler generated process that generates a dictionary from JSon... 
			input = @"C:\Temp\Scope\JsonSurvey\JobsWithUDOs\0a671f56-c4c1-47da-a0c0-09c5701854c8\__ScopeCodeGen__.dll";

			string[] directories = Path.GetDirectoryName(input).Split(Path.DirectorySeparatorChar);
            var outputPath = Path.Combine(@"c:\Temp\", directories.Last()) + "_" + Path.ChangeExtension(Path.GetFileName(input), ".sarif");

			var io = ScopeDemo(5);
			input = io.Item1;
			outputPath = io.Item2;

            var logPath = Path.Combine(@"c:\Temp\", "analysis.log");
            var outputStream = File.CreateText(logPath);

            var log = AnalyzeDll(input, scopeKind, useScopeFactory, interProcAnalysis);
            SarifLogger.WriteSarifOutput(log, outputPath);


            AnalysisStats.PrintStats(outputStream);
            AnalysisStats.WriteAnalysisReasons(outputStream);
            outputStream.WriteLine("End.");
            outputStream.Flush();

            Console.WriteLine("Finished. Press any key to exit.");
            System.Console.ReadKey();

        }

		public static Tuple<string, string> ScopeDemo(int item)
		{
			var input = @"C:\Temp\Scope\JsonSurvey\";
			var output = @"C:\Temp\Scope\Demo\";
			var benchmark = "";
			switch (item)
			{
				case 1:
					// Example that uses JsonConvert
					/*
					var jsonObj = JsonConvert.DeserializeObject<MyType>(s);
					string a = jsonObj.A;
					*/
					benchmark = @"SimpleJsonProcessors\bin\Debug\6BEA71AFD72D97FF\UDO1_C79877185261167E\__ScopeCodeGen__.dll";
					break;
				case 2:
					// Simpler example made my Mike
					/*
					var jsonObj = JObject.Parse(s);
					string a = (string)jsonObj["A"];
					 */
					benchmark = @"\ProcessorWithJObject\__ScopeCodeGen__.dll";
					break;
				case 3:
					// All output columns came from one input columns with Json (although) there are many other inputs
					/*
						string jsonString = row["message"].String;
						JObject jo = (JObject)JsonConvert.DeserializeObject(jsonString);
						outputRow[0].Set(jo["ApplicationName"].ToString());
						outputRow[1].Set(jo["ApplicationSubSystem"].ToString());
						outputRow[2].Set(jo["ApplicationVersion"].ToString());
						... many more
					 */
					benchmark = @"\JobsWithUDOs\0ab0c6d5-731f-5029-8943-32ba867897c1\__ScopeCodeGen__.dll";
					break;
				case 4:
					// 1) Use of JSon than contains a dictionary, and access to that content in a conditional
					/* 
					 *   System.Collections.Generic.Dictionary<System.String, System.String> col_EP = (System.Collections.Generic.Dictionary<System.String, System.String>)row.EP.Value;
						bool succeed = false;
						try
						{
							string staticconstant_0 = "ScanTarget";
							string staticconstant_1 = "cosmos11.AzureAnalytics.Dev";
							string staticconstant_2 = "cosmos11.AzureAnalytics.Dev_local.PublishedData";
							string staticconstant_3 = "cosmos11.AzureAnalytics.Dev_local.PublishedData.Microsoft.Cloud";
							succeed = col_EP.ContainsKey(staticconstant_0) 
											&& (col_EP[staticconstant_0].ToLower() == staticconstant_1.ToLower() 
													|| col_EP[staticconstant_0].ToLower() == staticconstant_2.ToLower() || col_EP[staticconstant_0].ToLower() == staticconstant_3.ToLower());
						}
					*/
					//  Control dependency on "Col(Input,Json(Col(Input,ExtendedProps[8])).[ScanTarget])"
					
					// 2) Use Json Tags that is an enumeration 
					/*
					 * List<Tag> tags = new List<Tag>();
						try
						{
							tags = JsonConvert.DeserializeObject<List<Tag>>(row["Tags"].String);
						}
						...
						foreach (Tag tag in tags)
						{
							...
								if(!String.IsNullOrEmpty(tag.Column)){
									output[TagColumnString].UnsafeSet(tag.Column);
								} else {
									output[TagColumnString].UnsafeSet("_empty");
								}
							...
						}
					*/
					// "column": "Col(Output,TagColumn[6])",
					// "data depends": ["String","Col(Input,Json(Col(Input,Tags[6])).[*].Column)"],

					benchmark = @"\JobsWithUDOs\0a671f56-c4c1-47da-a0c0-09c5701854c8\__ScopeCodeGen__.dll";
					break;
				case 5:
					// Use Select Token
					// This example puts all Json Tokens in a dictionary and they produces the column output out of the dict
					// this is why the analysis conservately add many dependencies for each colunm
					/*
					"column": "Col(Output,Name[6])",
					"data depends": ["Name","Col(Input,Json(Col(Input,jsonValue[13])).name)","Col(Input,Json(Col(Input,jsonValue[13])).subscriptionId)","Col(Input,Json(Col(Input,jsonValue[13])).groupName)","Col(Input,Json(Col(Input,jsonValue[13])).etag)","Col(Input,Json(Col(Input,jsonValue[13])).lastOperationId)","Col(Input,Json(Col(Input,jsonValue[13])).lastOperationType)","Col(Input,Json(Col(Input,jsonValue[13])).lastModifiedTime)","Col(Input,Json(Col(Input,jsonValue[13])).createdTime)","Col(Input,Json(Col(Input,jsonValue[13])).resourceGuid)","Col(Input,Json(Col(Input,jsonValue[13])).properties.provisioningState)","Col(Input,Json(Col(Input,jsonValue[13])).properties.allocatedTenant.id)","Col(Input,Json(Col(Input,jsonValue[13])).properties.virtualMachine.id)","Col(Input,Json(Col(Input,jsonValue[13])).properties.primary)","Col(Input,Json(Col(Input,jsonValue[13])).properties.macAddress)","Col(Input,Json(Col(Input,jsonValue[13])).properties.networkSecurityGroup.id)","Col(Input,Json(Col(Input,jsonValue[13])).properties.ipConfigurations)","Col(Input,Json(Col(Input,jsonValue[13])).properties.dnsSettings)","False","String","True","0","ToString(Int32&)","Col(Input,Json(Col(Input,jsonValue[13])).properties.enableAcceleratedNetworking)"],
					"control depends": ["Int32","3","RC(Input)"]
					 */
					benchmark = @"\JobsWithUDOs\06c226da-559e-46f6-a83c-35608aa544dc\__ScopeCodeGen__.dll";
					break;
				case 6:
					//benchmark = @"\JobsWithUDOs\0a671f56-c4c1-47da-a0c0-09c5701854c8\__ScopeCodeGen__.dll";
					break;

				case 10:
					// Example that uses o = JObject.Parse and o[field] 
					// This one is too complex because it is a compiler generated UDO. 
					// I uses complex getters and setters to access the row columns direcly in the query
					// And uses Helper methods with parameters 
					// required: 1) interprocedural string and columns analysis. 2) support for compiler generated properties

					/*
					 * rsMDSAccountMetaData = SELECT CloudName.Trim().ToLower() AS nCloud,
                              DivisionName,
                              OrganizationName,
                              ServiceGroupName,
                              ServiceName,
                              ServiceOid,
                              Helper.GetAttributeValue(Value, "Namespaces") AS STMDSNamespace, 
                              Helper.GetAttributeValue(Value, "Endpoint") AS Endpoint
							  ...
					*/
					benchmark = @"\0195e2b3-3fb3-4f36-bc10-cadbfd76c8cd\__ScopeCodeGen__.dll";

					break;

				default:
					benchmark = @"NewtonSoftMethodSurvey_E18FC06FBAF9E44\__ScopeCodeGen__.dll";
					item = 0;
					break;
			}

			input += benchmark;

			string[] directories = Path.GetDirectoryName(input).Split(Path.DirectorySeparatorChar);
			Directory.CreateDirectory(String.Format("{0}\\Demo_{1}", output, item));

			output += String.Format("Demo_{0}\\{1}_{2}", item, directories.Last(), ".sarif");
			return Tuple.Create<string, string>(input,output);
		}

        public static SarifLog AnalyzeDll(string inputPath, ScopeMethodKind kind, bool useScopeFactory = true, bool interProc = false, StreamWriter outputStream = null, TimeSpan timeout = default(TimeSpan))
        {
            if (timeout == default(TimeSpan))
            {
                timeout = TimeSpan.FromMinutes(1);
            }
            if (System.Diagnostics.Debugger.IsAttached)
            {
                timeout = TimeSpan.FromMilliseconds(-1);
            }
            var task = Task.Run(() => ScopeProgramAnalysis.AnalyzeDll(inputPath, kind, useScopeFactory, interProc, outputStream));
            if (task.Wait(timeout))
                return task.Result;
            else
            {
                var log = SarifLogger.CreateSarifOutput();
                var r = SarifLogger.CreateRun(inputPath, "No results", "Timeout", new List<Result>());
                log.Runs.Add(r);
                return log;
            }
        }

        public static void AnalyzeDllAndWriteLog(string inputPath, string outputPath, ScopeMethodKind kind,
                    bool useScopeFactory = true, bool interProc = false, StreamWriter outputStream = null)
        {
            var log = AnalyzeDll(inputPath, ScopeMethodKind.All, useScopeFactory, interProc, outputStream);
            SarifLogger.WriteSarifOutput(log, outputPath);
        }
    }

}
