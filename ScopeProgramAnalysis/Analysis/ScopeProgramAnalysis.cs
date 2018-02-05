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

namespace ScopeProgramAnalysis
{
    public struct ScopeProcessorInfo
    {
        public ScopeProcessorInfo(ITypeDefinition pClass, IMethodDefinition entryMethod, IMethodDefinition getIteratorMethod, IMethodDefinition moveNextMethod, IMethodDefinition factoryMethod)
        {
            ProcessorClass = pClass;
            EntryMethod = entryMethod;
            GetIteratorMethod = getIteratorMethod;
            MoveNextMethod = moveNextMethod;
            FactoryMethod = factoryMethod;
            InputSchema = null;
            OutputSchema = null;
        }
        public ITypeDefinition ProcessorClass { get; set; }
        public IMethodDefinition EntryMethod { get; set; }
        public IMethodDefinition GetIteratorMethod { get; set; }
        public IMethodDefinition MoveNextMethod { get; set; }
        public IMethodDefinition FactoryMethod { get; set; }

        public Schema InputSchema { get;  set; }
        public Schema OutputSchema { get;  set; }

    }
    public class DependencyStats
    {
        public int SchemaInputColumnsCount;
        public int SchemaOutputColumnsCount;

        // Diego's analysis
        public long DependencyTime; // in milliseconds
        public List<Tuple<string, string>> PassThroughColumns = new List<Tuple<string, string>>();
        public List<string> UnreadInputs = new List<string>();
        public bool TopHappened;
        public bool OutputHasTop;
        public bool InputHasTop;
        public int ComputedInputColumnsCount;
        public int ComputedOutputColumnsCount;
        public bool Error;
        public string ErrorReason;
        public string DeclaredPassthroughColumns;
        public string DeclaredDependencies;

        // Zvonimir's analysis
        public long UsedColumnTime; // in milliseconds
        public bool UsedColumnTop;
        public string UsedColumnColumns;

        public int NumberUsedColumns { get; internal set; }
        public List<string> UnWrittenOutputs = new List<string>();
        public ISet<string> UnionColumns = new HashSet<string>();
        public bool ZvoTop;
    }



	public class ScopeProgramAnalysis
	{
		struct BothAnalysisResults {
			public DependencyPTGDomain DepAnalysisResult { get;  set; }
			public TimeSpan Time { get; set; }
			public ISet<TraceableColumn> InputsColumns { get; set; }
			public ISet<TraceableColumn> OutputColumns { get; set; }
			public ScopeAnalyzer.Analyses.ColumnsDomain BagOfColumnsUsedColumns { get; set; }
			public TimeSpan BagOfColumnsTime { get; set; }
		};

        private IMetadataHost host;
        public IDictionary<string, ITypeDefinition> FactoryReducerMap { get; private set; }
        private MyLoader loader;
        public InterproceduralManager InterprocAnalysisManager { get; private set; }

		private Dictionary<IMethodDefinition, BothAnalysisResults> previousResults = new Dictionary<IMethodDefinition, BothAnalysisResults> ();


		//public static Schema InputSchema;
		//public static Schema OutputSchema;

		public IEnumerable<string> ReferenceFiles { get; private set; }
        public HashSet<string> ClassFilters { get; private set; }
        public HashSet<string> EntryMethods { get; private set; }
        public HashSet<string> ClousureFilters { get; private set; }

        public readonly string MethodUnderAnalysisName = "MoveNext";

        private Regex[] compilerGeneretedMethodMatchers = new Regex[]
            {
                    new Regex(@"^___Scope_Generated_Classes___.ScopeFilterTransformer_\d+$", RegexOptions.Compiled),
                    new Regex(@"^___Scope_Generated_Classes___.ScopeGrouper_\d+$", RegexOptions.Compiled),
                    new Regex(@"^___Scope_Generated_Classes___.ScopeProcessorCrossApplyExpressionWrapper_\d+$", RegexOptions.Compiled),
                    new Regex(@"^___Scope_Generated_Classes___.ScopeOptimizedClass_\d+$", RegexOptions.Compiled),
                    new Regex(@"^___Scope_Generated_Classes___.ScopeTransformer_\d+$", RegexOptions.Compiled),
                    new Regex(@"^___Scope_Generated_Classes___.ScopeGrouper_\d+$", RegexOptions.Compiled),
                    new Regex(@"^___Scope_Generated_Classes___.ScopeCrossApplyProcessor_\d+$", RegexOptions.Compiled),
                    new Regex(@"^___Scope_Generated_Classes___.ScopeProcessorCrossApplyExpressionWrapper_\d+$", RegexOptions.Compiled),
                    new Regex(@"^___Scope_Generated_Classes___.ScopeReducer__\d+$", RegexOptions.Compiled)
                    // ScopeRuntime.
            };

        public ScopeProgramAnalysis(MyLoader loader)
        {
            this.host = loader.Host;
            this.loader = loader;
            this.FactoryReducerMap = new Dictionary<string, ITypeDefinition>();
            this.InterprocAnalysisManager = new InterproceduralManager(loader);
        }
        public enum ScopeMethodKind { Processor, Reducer, All };

        private void ComputeColumns(string xmlFile, string processNumber)
        {
            XElement x = XElement.Load(xmlFile);
            var processor = x
                .Descendants("operator")
                .Where(op => op.Attribute("id") != null && op.Attribute("id").Value == processNumber)
                .FirstOrDefault()
                ;

            Console.Write("Processor: {0}. ", processor.Attribute("className").Value);

            var inputSchema = ParseColumns(processor.Descendants("input").FirstOrDefault().Attribute("schema").Value);

        }

        private static IEnumerable<Column> ParseColumns(string schema)
        {
            // schema looks like: "JobGUID:string,SubmitTime:DateTime?,NewColumn:string"
            var schemaList = schema.Split(',');
            for (int i = 0; i < schemaList.Count(); i++)
            {
                if (schemaList[i].Contains("<") && i < schemaList.Count()-1 && schemaList[i + 1].Contains(">"))
                {
                    schemaList[i] += schemaList[i + 1];
                    schemaList[i + 1] = "";
                    i++;
                }
            }
            return schemaList.Where( elem => !String.IsNullOrEmpty(elem)).Select((c, i) => { var a = c.Split(':'); return new Column(a[0].Trim(' '), new RangeDomain(i), a[1].Trim(' ')); });

            //return schema
            //    .Split(',')
            //    .Select((c, i) => { var a = c.Split(':'); return new Column(a[0], new RangeDomain(i), a[1]); });
        }

        /// <summary>
        /// Searches for the Reduce or Process method in a processor.
        /// </summary>
        /// <param name="concurrentProcessorType">This parameter is needed in case <paramref name="c"/> is
        /// a type that implemented ConcurrentProcessor which means the real processor is the type
        /// argument used in the ConcurrentProcessor.</param>
        /// <param name="c">The initial class to begin the search at.</param>
        /// <returns>The processor's method, null if not found</returns>
        private static IMethodDefinition FindEntryMethod(ITypeDefinition concurrentProcessorType, ITypeDefinition c)
        {
            // If c is a subtype of ConcurrentProcessor, then c should really be the first generic argument
            // which is the type of the real processor. Note that this is tricky to find since c might
            // be a non-generic type that is a subtype of another type that in turn is a subtype of
            // ConcurrentProcessor. I.e., c is of type T <: U<A> <: ConcurrentProcessor<A,B,C>
            var found = false;
            var c2 = c;
            IGenericTypeInstanceReference gtir = null;
            while (!found)
            {
                gtir = c2 as IGenericTypeInstanceReference;
                if (gtir != null && TypeHelper.TypesAreEquivalent(gtir.GenericType.ResolvedType, concurrentProcessorType))
                {
                    found = true;
                    break;
                }
                var baseClass = c2.BaseClasses.SingleOrDefault();
                if (baseClass == null) break;
                c2 = baseClass.ResolvedType;
            }
            if (found)
            {
                c = gtir.GenericArguments.ElementAt(0).ResolvedType;
            }

            // First, walk up the inheritance hierarchy until we find out whether this is a processor or a reducer.
            string entryMethodName = null;
            var baseType = c.BaseClasses.SingleOrDefault();
            if (baseType != null)
            {
                var baseClass = baseType.ResolvedType; // need to resolve to get base class
                if (baseClass != null)
                {
                    while (entryMethodName == null)
                    {
                        var fullName = baseClass.FullName();
                        if (fullName == "ScopeRuntime.Processor")
                        {
                            entryMethodName = "Process";
                            break;
                        }
                        else if (fullName == "ScopeRuntime.Reducer")
                        {
                            entryMethodName = "Reduce";
                            break;
                        }
                        else
                        {
                            if (baseClass.BaseClasses.SingleOrDefault() == null) break; // Object has no base class
                            baseClass = baseClass.BaseClasses.SingleOrDefault().ResolvedType;
                            if (baseClass == null) break;
                        }
                    }
                }
                if (entryMethodName == null) return null;

                // Now, find the entry method (potentially walking up the inheritance hierarchy again, stopping
                // point is not necessarily the same as the class found in the walk above).
                var entryMethod = c.Methods.Where(m => m.Name.Value == entryMethodName).SingleOrDefault();
                var baseClass2 = c.ResolvedType;
                while (entryMethod == null)
                {
                    var baseType2 = baseClass2.BaseClasses.SingleOrDefault();
                    if (baseType2 == null) break;
                    baseClass2 = baseType2.ResolvedType;
                    entryMethod = baseClass2.Methods.Where(m => m.Name.Value == entryMethodName).SingleOrDefault();
                }
                return entryMethod;
            }
            return null;
        }

        private static MyLoader CreateHost(string inputPath, out IAssembly loadedAssembly)
        {
            var host = new PeReader.DefaultHost();

            if (inputPath.StartsWith("file:")) // Is there a better way to tell that it is a Uri?
            {
                inputPath = new Uri(inputPath).LocalPath;
            }
            var d = Path.GetDirectoryName(Path.GetFullPath(inputPath));
            if (!Directory.Exists(d))
                throw new InvalidOperationException("Can't find directory from path: " + inputPath);

            var loader = new MyLoader(host, d);

            Types.Initialize(host);

            loadedAssembly = loader.LoadMainAssembly(inputPath);
			return loader;
        }


		/// <summary>
		/// This method analyzes 1 processor, computes de dependencies and convert it to a Sarif output
		/// </summary>
		/// <param name="inputPath"></param>
		/// <param name="processToAnalyze"></param>
		/// <param name="inputSchema"></param>
		/// <param name="outputSchema"></param>
		/// <param name="runResult"></param>
		/// <param name="errorReason"></param>
		/// <returns></returns>
        public bool AnalyzeProcessor(
            string inputPath,
            ScopeProcessorInfo processToAnalyze,
            out Run runResult,
            out AnalysisReason errorReason)
        {
            runResult = null;
            errorReason = default(AnalysisReason);

            this.InterprocAnalysisManager.SetProcessToAnalyze(processToAnalyze);

            try
            {
                DependencyPTGDomain depAnalysisResult;
                ISet<TraceableColumn> inputColumns;
                ISet<TraceableColumn> outputColumns;
                ScopeAnalyzer.Analyses.ColumnsDomain bagOColumnsUsedColumns;
                TimeSpan depAnalysisTime;
                TimeSpan bagOColumnsTime;

				// If the processor 
				BothAnalysisResults bothAnalysesResult;
				if (!previousResults.TryGetValue(processToAnalyze.MoveNextMethod, out bothAnalysesResult))
				{
					var dependencyAnalysis = new SongTaoDependencyAnalysis(loader, this.InterprocAnalysisManager, processToAnalyze);
					var depAnalysisResultandTime = dependencyAnalysis.AnalyzeMoveNextMethod();
					depAnalysisResult = depAnalysisResultandTime.Item1;
					depAnalysisTime = depAnalysisResultandTime.Item2;
					var analysisReasons = AnalysisStats.AnalysisReasons;

					inputColumns = dependencyAnalysis.InputColumns;
					outputColumns = dependencyAnalysis.OutputColumns;

					var a = TypeHelper.GetDefiningUnit(processToAnalyze.ProcessorClass) as IAssembly;
					var z = ScopeAnalyzer.ScopeAnalysis.AnalyzeMethodWithBagOColumnsAnalysis(loader.Host, a, Enumerable<IAssembly>.Empty, processToAnalyze.MoveNextMethod);
					bagOColumnsUsedColumns = z.UsedColumnsSummary ?? ScopeAnalyzer.Analyses.ColumnsDomain.Top;
					bagOColumnsTime = z.ElapsedTime;

					bothAnalysesResult = new BothAnalysisResults()
					{
						DepAnalysisResult = depAnalysisResult,
						Time = depAnalysisTime,
						BagOfColumnsUsedColumns = bagOColumnsUsedColumns,
						BagOfColumnsTime = bagOColumnsTime,
						InputsColumns = inputColumns,
						OutputColumns = outputColumns
					};
                    previousResults.Add(processToAnalyze.MoveNextMethod, bothAnalysesResult);
                }
                var producesAnalyzer = new ProducesMethodAnalyzer(loader, processToAnalyze.ProcessorClass);
                // var overApproximatedPassthrough = producesAnalyzer.InferAnnotations(inputSchema);

                var r = CreateResultsAndThenRun(inputPath, processToAnalyze, bothAnalysesResult, this.FactoryReducerMap);
                runResult = r;

                return true;
            }
            catch (Exception e)
            {
                var id = String.Format("[{0}] {1}", TypeHelper.GetDefiningUnit(processToAnalyze.ProcessorClass).Name.Value, processToAnalyze.ProcessorClass.FullName());
                var r = SarifLogger.CreateRun(inputPath, id, String.Format(CultureInfo.InvariantCulture, "Thrown exception {0}\n{1}", e.Message, e.StackTrace.ToString()), new List<Result>());
                runResult = r;
                return true;

                //var body = MethodBodyProvider.Instance.GetBody(moveNextMethod);
                //errorReason = new AnalysisReason(moveNextMethod, body.Instructions[0],
                //                String.Format(CultureInfo.InvariantCulture, "Thrown exception {0}\n{1}", e.Message, e.StackTrace.ToString()));
                //return false;
            }
            finally
            {
                processToAnalyze.InputSchema = null;
                processToAnalyze.OutputSchema = null;
            }
        }

        public static IEnumerable<Tuple<string, DependencyStats>> ExtractDependencyStats(SarifLog log)
        {
            var dependencyStats = new List<Tuple<string, DependencyStats>>();
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


                if (processorName == "No results")
                {
                    var ret2 = new DependencyStats();
                    ret2.Error = true;
                    ret2.ErrorReason = String.Join(",", run.ToolNotifications.Select(e => e.Message));
                    dependencyStats.Add(Tuple.Create(processorName, ret2));
                    continue;
                }

                var ret = new DependencyStats();

                var visitedColumns = new HashSet<string>();
                var inputColumnsRead = new HashSet<string>();
                if (run.Results.Any())
                {
                    foreach (var result in run.Results)
                    {
                        if (result.Id == "SingleColumn")
                        {
                            var columnProperty = result.GetProperty("column");
                            if (!columnProperty.StartsWith("Col(")) continue;
                            var columnName = columnProperty.Contains(",") ? columnProperty.Split(',')[1].Trim('"', ')') : columnProperty;
                            if (columnName == "_All_")
                            {
                                // ignore this for now because it is more complicated
                                continue;
                            }
                            if (columnName == "_TOP_")
                            {
                                ret.TopHappened = true;
                            }
                            if (visitedColumns.Contains(columnName))
                                continue;

                            visitedColumns.Add(columnName);

                            var dataDependencies = result.GetProperty<List<string>>("data depends");
                            if (dataDependencies.Count == 1)
                            {
                                var inputColumn = dataDependencies[0];
                                if (!inputColumn.StartsWith("Col(Input"))
                                {
                                    // then it is dependent on only one thing, but that thing is not a column.
                                    continue;
                                }
                                if (inputColumn.Contains("TOP"))
                                {
                                    // a pass through column cannot depend on TOP
                                    continue;
                                }
                                // then it is a pass-through column
                                var inputColumnName = inputColumn.Contains(",") ? inputColumn.Split(',')[1].Trim('"', ')') : inputColumn;
                                ret.PassThroughColumns.Add(Tuple.Create(columnName, inputColumnName));
                            }
                        }
                        else if (result.Id == "Summary")
                        {
                            // Do nothing
                            var columnProperty = result.GetProperty<List<string>>("Inputs");
                            var totalInputColumns = columnProperty.Count;
                            ret.InputHasTop = columnProperty.Contains("Col(Input,_TOP_)") || columnProperty.Contains("_TOP_");
                            var inputColumns = columnProperty.Select(x => x.Contains(",") ? x.Split(',')[1].Trim('"', ')') : x);
                            ret.ComputedInputColumnsCount = inputColumns.Count();


                            columnProperty = result.GetProperty<List<string>>("Outputs");
                            var totalOutputColumns = columnProperty.Count;
                            var outputColumns = columnProperty.Select(x => x.Contains(",") ? x.Split(',')[1].Trim('"', ')') : x);

                            ret.OutputHasTop = columnProperty.Contains("Col(Output,_TOP_)") || columnProperty.Contains("_TOP_");
                            ret.ComputedOutputColumnsCount = totalOutputColumns;

                            columnProperty = result.GetProperty<List<string>>("SchemaInputs");
                            ret.SchemaInputColumnsCount = columnProperty.Count;
                            if (!ret.InputHasTop)
                            {
                                ret.UnreadInputs = columnProperty.Where(schemaInput => !inputColumns.Contains(schemaInput)).ToList();
                            }


                            columnProperty = result.GetProperty<List<string>>("SchemaOutputs");
                            ret.SchemaOutputColumnsCount = columnProperty.Count;
                            if (!ret.InputHasTop)
                            {
                                ret.UnWrittenOutputs = columnProperty.Where(schemaOuput => !outputColumns.Contains(schemaOuput)).ToList();
                            }


                            ret.UnionColumns = new HashSet<string>();
                            var schemaOutputs = result.GetProperty<List<string>>("SchemaOutputs").Select(c => c.Contains("[") ? c.Substring(0, c.IndexOf('[')) : c);
                            var schemaInputs = result.GetProperty<List<string>>("SchemaInputs").Select(c => c.Contains("[") ? c.Substring(0, c.IndexOf('[')) : c);
                            ret.UnionColumns.AddRange(schemaInputs);
                            ret.UnionColumns.UnionWith(schemaOutputs);


                            ret.UsedColumnTop = result.GetProperty<bool>("UsedColumnTop");
                            ret.TopHappened |= result.GetProperty<bool>("DependencyAnalysisTop");
                            ret.DeclaredPassthroughColumns = result.GetProperty("DeclaredPassthrough");
                            ret.DeclaredDependencies = result.GetProperty("DeclaredDependency");

                            ret.DependencyTime = result.GetProperty<long>("DependencyAnalysisTime");

                            ret.UsedColumnColumns = result.GetProperty("BagOColumns");
                            int nuo = 0;
                            if (!result.TryGetProperty<int>("BagNOColumns", out nuo))
                            {
                                ret.NumberUsedColumns = ret.UnionColumns.Count;
                                ret.ZvoTop = true;
                            }
                            else
                            {
                                ret.ZvoTop = ret.UsedColumnColumns == "All columns used.";
                                ret.NumberUsedColumns = ret.ZvoTop ? ret.UnionColumns.Count : nuo;

                            }
                            ret.UsedColumnTime = result.GetProperty<long>("BagOColumnsTime");
                        }
                    }
                }
                else
                {
                    if (run.ToolNotifications.Any())
                    {
                        ret.Error = true;
                        ret.ErrorReason = String.Join(",", run.ToolNotifications.Select(e => e.Message));
                    }
                }
                dependencyStats.Add(Tuple.Create(processorName, ret));
            }
            return dependencyStats;
        }


        private static Run CreateResultsAndThenRun(string inputPath,
            ScopeProcessorInfo processToAnalyze,
			BothAnalysisResults analysisResult,
			IDictionary<string, ITypeDefinition> processorMap)
        {
			Schema inputSchema = processToAnalyze.InputSchema;

			var results = new List<Result>();

			DependencyPTGDomain depAnalysisResult = analysisResult.DepAnalysisResult;
			var depAnalysisTime = analysisResult.Time;
			var inputColumns = analysisResult.InputsColumns;
			var outputColumns = analysisResult.OutputColumns;
			var bagOColumnsUsedColumns = analysisResult.BagOfColumnsUsedColumns;
			var bagOColumnsTime = analysisResult.BagOfColumnsTime;


            var inputUses = new HashSet<Traceable>();
            var outputModifies = new HashSet<Traceable>();

            string declaredPassthroughString = "";
            string declaredDependencyString = "";
            if (processToAnalyze.FactoryMethod != null)
            {
                try
                {
                    var resultOfProducesMethod = ExecuteProducesMethod(processToAnalyze.ProcessorClass, processToAnalyze.FactoryMethod, inputSchema);
                    var declaredPassThroughDictionary = resultOfProducesMethod.Item1;
                    declaredPassthroughString = String.Join("|", declaredPassThroughDictionary.Select(e => e.Key + " <: " + e.Value));
                    var dependenceDictionary = resultOfProducesMethod.Item2;
                    declaredDependencyString = String.Join("|", dependenceDictionary.Select(e => e.Key + " <: " + e.Value));
                } catch (Exception e)
                {
                    declaredPassthroughString = "Exception while trying to execute produces method: " + e.Message;
                }
            } else
            {
                declaredPassthroughString = "Null Factory Method";
            }

            var inputSchemaString = processToAnalyze.InputSchema.Columns.Select(t => t.ToString());
            var outputSchemaString = processToAnalyze.OutputSchema.Columns.Select(t => t.ToString());

            if (!depAnalysisResult.IsTop)
            {
				#region Compute Sarif Results for the Dependency Analysis
				// Compute dependencies for each output column
				if (depAnalysisResult.Dependencies.A4_Ouput.Any())
				{
					ObtainOutputDependenciesInSarifFormat(results, depAnalysisResult, inputUses, outputModifies);
				}
				else
                {
					var result = new Result();
                    result.Id = "SingleColumn";
                    result.SetProperty("column", "_EMPTY_");
					// TODO: Check: before escape info was added for every column
					//var escapes = depAnalysisResult.Dependencies.A1_Escaping.Select(traceable => traceable.ToString());
					//result.SetProperty("escapes", escapes);
					results.Add(result);

                }

				// Add computed inputs, outputs and schemas
                var resultSummary = new Result();
                resultSummary.Id = "Summary";

                var inputsString = inputColumns.Select(t => t.ToString());
                var outputsString = outputColumns.Select(t => t.ToString());
                resultSummary.SetProperty("Inputs", inputsString);
                resultSummary.SetProperty("Outputs", outputsString);

                resultSummary.SetProperty("SchemaInputs", inputSchemaString);
                resultSummary.SetProperty("SchemaOutputs", outputSchemaString);

				var escapes = depAnalysisResult.Dependencies.A1_Escaping.Select(traceable => traceable.ToString());
				// TODO: Check: before escape info was added for every column
				resultSummary.SetProperty("escapes", escapes);
				resultSummary.SetProperty("DependencyAnalysisTop", depAnalysisResult.IsTop);

				resultSummary.SetProperty("DeclaredPassthrough", declaredPassthroughString);
				resultSummary.SetProperty("DeclaredDependency", declaredDependencyString);
				resultSummary.SetProperty("DependencyAnalysisTime", 0); // (int)depAnalysisTime.TotalMilliseconds);
				#endregion

				#region Results for Svonimir analysis
				// Cannot compare the dependency analysis and the used-column analysis.
				// It can be that D <= UC or that UC <= D. (Where <= means the partial order where
				// any result is less-than-or-equal to "top".)
				// So just set a property for each as to whether they returned "top".
				resultSummary.SetProperty("UsedColumnTop", bagOColumnsUsedColumns.IsTop);
                //// Comparison means that the results are consistent, *not* that they are equal.
                //// In particular, the dependency analysis may be able to return a (non-top) result
                //// when the used-column analysis cannot.
                //if (!bagOColumnsUsedColumns.IsTop && !bagOColumnsUsedColumns.IsBottom)
                //{
                //    var a = bagOColumnsUsedColumns.Elements.Select(e => e.Value.ToString());
                //    var b = inputColumns.Union(outputColumns).Select(tc => tc.Column).Distinct();
                //    var compareResults = Util.SetEqual(a, b, (x, y) => Util.ColumnNameMatches(x, y));
                //    resultSummary.SetProperty("Comparison", compareResults);
                //} else if (bagOColumnsUsedColumns.IsTop)
                //{
                //    // The used column analysis is more conservative than the dependency analysis.
                //    // Top is less-equal-to any other result
                //    resultSummary.SetProperty("Comparison", true);
                //}
                //else
                //{
                //    // Should look into why this might be the case.
                //    resultSummary.SetProperty("Comparison", false);
                //}
                resultSummary.SetProperty("BagOColumns", bagOColumnsUsedColumns.ToString());
                resultSummary.SetProperty("BagNOColumns", bagOColumnsUsedColumns.Count);

                resultSummary.SetProperty("BagOColumnsTime", 0); // (int) bagOColumnsTime.TotalMilliseconds);
				#endregion

				results.Add(resultSummary);
            }
            else
            {
                var result = new Result();
                result.Id = "SingleColumn";
                result.SetProperty("column", "_TOP_");
                result.SetProperty("depends", "_TOP_");
                results.Add(result);
                var resultEmpty = new Result();
                resultEmpty.Id = "Summary";
                resultEmpty.SetProperty("Inputs", new List<string>() { "_TOP_" });
                resultEmpty.SetProperty("Outputs", new List<string>() { "_TOP_" });
                resultEmpty.SetProperty("SchemaInputs", inputSchemaString);
                resultEmpty.SetProperty("SchemaOutputs", outputSchemaString);
                resultEmpty.SetProperty("UsedColumnTop", bagOColumnsUsedColumns.IsTop);
                resultEmpty.SetProperty("DependencyAnalysisTop", false);
                resultEmpty.SetProperty("BagOColumns", bagOColumnsUsedColumns.ToString());
                resultEmpty.SetProperty("BagNOColumns", bagOColumnsUsedColumns.Count);
                resultEmpty.SetProperty("DeclaredPassthrough", declaredPassthroughString);
                resultEmpty.SetProperty("DeclaredDependency", declaredDependencyString);
                resultEmpty.SetProperty("BagOColumnsTime", 0); //(int)bagOColumnsTime.TotalMilliseconds);
				resultEmpty.SetProperty("DependencyAnalysisTime", 0); // (int)depAnalysisTime.TotalMilliseconds);
				results.Add(resultEmpty);
            }

            var actualProcessorClass = processToAnalyze.EntryMethod.ContainingType;

            var id = String.Format("[{0}] {1}", TypeHelper.GetDefiningUnit(processToAnalyze.ProcessorClass).Name.Value, processToAnalyze.ProcessorClass.FullName());

            // Very clumsy way to find the process number and the processor name from the MoveNext method.
            // But it is the process number and processor name that allow us to link these results to the information
            // in the XML file that describes the job.
            // Climb the containing type chain from the MoveNext method until we find the entry in the dictionary whose
            // value matches one of the classes.
            var done = false;
            foreach (var kv in processorMap)
            {
                if (done) break;
                var c = processToAnalyze.MoveNextMethod.ContainingType.ResolvedType;
                while (c != null)
                {
                    if (kv.Value == c)
                    {
                        id = kv.Value.FullName() + "|" + kv.Key;
                        done = true;
                        break;
                    }
                    if (c is INestedTypeDefinition)
                    {
                        c = (c as INestedTypeDefinition).ContainingTypeDefinition;
                    }
                    else
                    {
                        c = null;
                    }
                }
            }

            string actualClassContainingIterator = null;
            if (processToAnalyze.ProcessorClass != actualProcessorClass)
                actualClassContainingIterator = "Analyzed processsor: " + actualProcessorClass.FullName();

            var r = SarifLogger.CreateRun(inputPath, id, actualClassContainingIterator, results);
            return r;
        }

		private static void ObtainOutputDependenciesInSarifFormat(List<Result> results, DependencyPTGDomain depAnalysisResult, 
																  HashSet<Traceable> inputUses, HashSet<Traceable> outputModifies)
		{
			MapSet<TraceableColumn, Traceable> outColumnMap, outColumnControlMap;
			outColumnMap = depAnalysisResult.ComputeOutputDependencies(out outColumnControlMap);

			// Use the output/input depencency map to produce the SarifOutput
			foreach (var entryOutput in outColumnMap)
			{
				var result = new Result();
				result.Id = "SingleColumn";
				var column = entryOutput.Key;
				var columnString = column.ToString();
				var dependsOn = entryOutput.Value;
				var controlDepends = new HashSet<Traceable>();

				result.SetProperty("column", columnString);
				result.SetProperty("data depends", dependsOn.Select(traceable => traceable.ToString()));

				if (outColumnControlMap.ContainsKey(column))
				{
					var controlDependsOn = outColumnControlMap[column];
					result.SetProperty("control depends", controlDependsOn.Select(traceable => traceable.ToString()));
				}
				else
				{
					result.SetProperty("control depends", new string[] { });
				}
				results.Add(result);

				inputUses.AddRange(dependsOn.Where(t => t.TableKind == ProtectedRowKind.Input));
				outputModifies.Add(column);
			}
		}


		private static Tuple<Dictionary<string, string>, Dictionary<string, string>> ExecuteProducesMethod(ITypeDefinition processorClass, IMethodDefinition factoryMethod, Schema inputSchema)
        {
            var sourceDictionary = new Dictionary<string, string>();
            var dependenceDictionary = new Dictionary<string, string>();

            var processorAssembly = System.Reflection.Assembly.LoadFrom(TypeHelper.GetDefiningUnitReference(processorClass).ResolvedUnit.Location);
            if (processorAssembly == null) { sourceDictionary.Add("666", "no processorAssembly"); goto L; }
            var processorClass2 = processorAssembly.GetType(TypeHelper.GetTypeName(processorClass, NameFormattingOptions.UseReflectionStyleForNestedTypeNames));
            if (processorClass2 == null) { sourceDictionary.Add("666", "no processorClass2"); goto L; }
            var finalizeMethod = processorClass2.GetMethod("Finalize", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            if (finalizeMethod != null) { sourceDictionary.Add("666", "Finalize() method found for processor: " + TypeHelper.GetTypeName(processorClass)); goto L; }


            if (factoryMethod == null) { sourceDictionary.Add("666", "no factoryMethod"); goto L; }
            // Call the factory method to get an instance of the processor.
            var factoryClass = factoryMethod.ContainingType;
            var assembly = System.Reflection.Assembly.LoadFrom(TypeHelper.GetDefiningUnitReference(factoryClass).ResolvedUnit.Location);
            if (assembly == null) { sourceDictionary.Add("666", "no assembly"); goto L; }
            var factoryClass2 = assembly.GetType(TypeHelper.GetTypeName(factoryClass, NameFormattingOptions.UseReflectionStyleForNestedTypeNames));
            if (factoryClass2 == null) { sourceDictionary.Add("666", "no factoryClass2"); goto L; }
            var factoryMethod2 = factoryClass2.GetMethod(factoryMethod.Name.Value);
            if (factoryMethod2 == null) { sourceDictionary.Add("666", "no factoryMethod2" + " (" + factoryMethod.Name.Value + ")"); goto L; }
            object instance = null;
            try
            {
                instance = factoryMethod2.Invoke(null, null);
            }
            catch (System.Reflection.TargetInvocationException e)
            {
                sourceDictionary.Add("666", "At least one missing assembly: " + e.Message); goto L;
            }
            if (instance == null) { sourceDictionary.Add("666", "no instance"); goto L; }
            var producesMethod = factoryMethod2.ReturnType.GetMethod("Produces");
            if (producesMethod == null) { sourceDictionary.Add("666", "no producesMethod"); goto L; }
            // Schema Produces(string[] columns, string[] args, Schema input)
            try
            {
                string[] arg1 = null;
                string[] arg2 = null;
                var inputSchemaAsString = String.Join(",", inputSchema.Columns.Select(e => e.Name + ": " + e.Type));
                var inputSchema2 = new ScopeRuntime.Schema(inputSchemaAsString);
                var specifiedOutputSchema = producesMethod.Invoke(instance, new object[] { arg1, arg2, inputSchema2, });
                if (specifiedOutputSchema == null) { sourceDictionary.Add("666", "no specifiedOutputSchema"); goto L; }
                foreach (var column in ((ScopeRuntime.Schema)specifiedOutputSchema).Columns)
                {
                    if (column.Source != null)
                        sourceDictionary.Add(column.Name, column.Source.Name);
                }
                var allowColumnPruningMethod = factoryMethod2.ReturnType.GetMethod("get_AllowColumnPruning");
                if (allowColumnPruningMethod != null)
                {
                    var columnPruningAllowed = (bool)allowColumnPruningMethod.Invoke(instance, null);
                    if (columnPruningAllowed)
                    {
                        foreach (var column in ((ScopeRuntime.Schema)specifiedOutputSchema).Columns)
                        {
                            if (column.Dependency != null)
                            {
                                dependenceDictionary.Add(column.Name, String.Join("+", column.Dependency.Keys.Select(e => e.Name)));
                            }
                        }
                    }
                }
            }
            catch
            {
                sourceDictionary.Add("666", "exception during Produces");
            }
            L:
            return Tuple.Create(sourceDictionary, dependenceDictionary);
        }

        /// <summary>
        /// Analyze the ScopeFactory class to get all the Processor/Reducer classes to analyze
        /// For each one obtain:
        /// 1) The class that the factory creates an instance of.
        /// 2) entry point method that creates the class with the iterator clousure and populated with some data)
        /// 3) the GetEnumerator method that creates and enumerator and polulated with data
        /// 4) the MoveNextMethod that contains the actual reducer/producer code
        /// </summary>
        /// <returns></returns>
        private IEnumerable<ScopeProcessorInfo> ObtainScopeMethodsToAnalyze(IAssembly assembly, out List<Tuple<ITypeDefinition, string>> errorMessages)
        {
            errorMessages = new List<Tuple<ITypeDefinition, string>>();

            var processorsToAnalyze = new HashSet<ITypeDefinition>();

            var scopeMethodTuplesToAnalyze = new HashSet<ScopeProcessorInfo>();

            var operationFactoryClass = assembly.GetAllTypes().OfType<INamedTypeDefinition>()
                                        .Where(c => c.Name.Value == "__OperatorFactory__" && c is INestedTypeDefinition &&  (c as INestedTypeDefinition).ContainingType.GetName() == "___Scope_Generated_Classes___").SingleOrDefault();

            if (operationFactoryClass == null)
                return new HashSet<ScopeProcessorInfo>();

            // Hack: use actual ScopeRuntime Types
            var factoryMethods = operationFactoryClass.Methods.Where(m => m.Name.Value.StartsWith("Create_Process_", StringComparison.Ordinal)
                            /*&& m.ReturnType.ToString() == this.ClassFilter*/);

            // var referencesLoaded = false;
            if (!factoryMethods.Any())
            {
                errorMessages.Add(Tuple.Create((ITypeDefinition)null, "No factory methods found"));
            }

            foreach (var factoryMethod in factoryMethods)
            {
                try
                {
                    var nonNullEntryMethods = factoryMethod.Body.Operations
                        .Where(op => op.OperationCode == OperationCode.Newobj)
                        .Select(e => e.Value)
                        .OfType<IMethodReference>()
                        .Select(e => FindEntryMethod(loader.RuntimeTypes.concurrentProcessor, e.ContainingType.ResolvedType))
                        .Where(e => e != null && !(e is Dummy))
                        ;
                    if (nonNullEntryMethods.Count() != 1)
                    {
                        continue;
                    }
                    var entryMethod = nonNullEntryMethods.First();
                    var reducerClassDefinition = entryMethod.ContainingTypeDefinition;

                    var isCompilerGenerated = compilerGeneretedMethodMatchers.Any(regex => regex.IsMatch(reducerClassDefinition.FullName()));
					if (reducerClassDefinition.FullName().Contains(@"ScoperTransformer_4") || reducerClassDefinition.FullName().Contains(@"ScopeFilterTransformer_17"))
					{
					}
					else
						if (isCompilerGenerated)
					    continue;

					if (processorsToAnalyze.Contains(reducerClassDefinition))
                        continue;

                    processorsToAnalyze.Add(reducerClassDefinition);

                    // Closure classes are always named types. Using the type case means the Name property is defined.
                    var candidateClosures = reducerClassDefinition.NestedTypes.OfType<INamedTypeDefinition>()
                                   .Where(c => this.ClousureFilters.Any(filter => c.Name.Value.StartsWith(filter)));
                    if (!candidateClosures.Any())
                    {
                        errorMessages.Add(Tuple.Create(reducerClassDefinition, "Iterator not found"));
                        continue;
                    }
                    foreach (var candidateClosure in candidateClosures)
                    {
                        var getEnumMethods = candidateClosure.Methods
                                                    .Where(m => m.Name.Value == ScopeAnalysisConstants.SCOPE_ROW_ENUMERATOR_METHOD);
                        var getEnumeratorMethod = getEnumMethods.Single();

                        var moveNextMethods = candidateClosure.Methods
                                                    .Where(md => md.Body != null && md.Name.Value.Equals(this.MethodUnderAnalysisName));
                        foreach (var moveNextMethod in moveNextMethods)
                        {
                            var processorToAnalyze = new ScopeProcessorInfo(reducerClassDefinition, entryMethod, getEnumeratorMethod, moveNextMethod, factoryMethod);
                            scopeMethodTuplesToAnalyze.Add(processorToAnalyze);

                            // TODO: Hack for reuse. Needs refactor
                            if (factoryMethod != null)
                            {
                                var processID = factoryMethod.Name.Value.Substring(factoryMethod.Name.Value.IndexOf("Process_"));
                                this.FactoryReducerMap.Add(processID, entryMethod.ContainingType as ITypeDefinition);
                            }
                        }
                    }
                }
                catch (Exception e)
                {
                    AnalysisStats.TotalofDepAnalysisErrors++;
                    Console.WriteLine("Error in Dependency Analysis", e.Message);
                    errorMessages.Add(Tuple.Create((ITypeDefinition)operationFactoryClass, "Exception occurred while looking for processors"));
                }

            }
            return scopeMethodTuplesToAnalyze;
        }

        public IEnumerable<ScopeProcessorInfo> ObtainScopeMethodsToAnalyzeFromAssemblies()
        {
            var scopeMethodTuplesToAnalyze = new HashSet<ScopeProcessorInfo>();

            var alreadyLoadedAssemblies = new List<IAssembly>(host.LoadedUnits.OfType<IAssembly>());
            var candidateClasses = alreadyLoadedAssemblies
                .Where(a => a.Name.Value != "mscorlib")
                .SelectMany(a => a.GetAllTypes().OfType<ITypeDefinition>())
                ;
            if (candidateClasses.Any())
            {
                var results = new List<Result>();
                foreach (var candidateClass in candidateClasses)
                {
                    var isCompilerGenerated = compilerGeneretedMethodMatchers.Any(regex => regex.IsMatch(candidateClass.FullName()));

                    if (isCompilerGenerated)
                        continue;

                    var entryMethod = FindEntryMethod(loader.RuntimeTypes.concurrentProcessor, candidateClass);
                    if (entryMethod == null) continue;

                    var containingType = entryMethod.ContainingType.ResolvedType;
                    if (containingType == null) continue;
                    var candidateClousures = containingType.NestedTypes.OfType<INamedTypeDefinition>()
                                    .Where(c => this.ClousureFilters.Any(filter => c.Name.Value.StartsWith(filter)));
                    foreach (var candidateClousure in candidateClousures)
                    {
                        var methods = candidateClousure.Members.OfType<IMethodDefinition>()
                                                .Where(md => md.Body != null
                                                && md.Name.Value.Equals(this.MethodUnderAnalysisName));

                        if (methods.Any())
                        {
                            var moveNextMethod = methods.First();
                            // BUG: Really should do this by getting the name of the Row type used by the processor, but just a quick hack for now to allow unit testing (which uses a different Row type).
                            // System.Collections.Generic.IEnumerable<FakeRuntime.Row>.GetEnumerator
                            // And really, this should be a type test anyway. The point is to find the explicit interface implementation of IEnumerable<T>.GetEnumerator.
                            var getEnumMethods = candidateClousure.Methods
                                                        .Where(m => m.Name.Value.StartsWith("System.Collections.Generic.IEnumerable<") && m.Name.Value.EndsWith(">.GetEnumerator"));
                            var getEnumeratorMethod = getEnumMethods.First();

                            var processorToAnalyze = new ScopeProcessorInfo(candidateClass, entryMethod, getEnumeratorMethod, moveNextMethod, null);
                            scopeMethodTuplesToAnalyze.Add(processorToAnalyze);

                        }
                    }
                }
            }
            return scopeMethodTuplesToAnalyze;
        }

        private void ComputeMethodsToAnalyzeForReducerClass(HashSet<Tuple<IMethodDefinition, IMethodDefinition, IMethodDefinition>> scopeMethodPairsToAnalyze,
            IMethodDefinition factoryMethdod, INamedTypeReference reducerClass)
        {
        }

        //private INamedTypeDefinition ResolveClass(INamedTypeReference classToResolve)
        //{
        //    var resolvedClass = host.ResolveReference(classToResolve) as INamedTypeDefinition;
        //    if(resolvedClass == null)
        //    {
        //        try
        //        {
        //            AnalysisStats.TotalDllsFound++;
        //            loader.TryToLoadReferencedAssembly(classToResolve.ContainingAssembly);
        //            resolvedClass = host.ResolveReference(classToResolve) as INamedTypeDefinition;
        //        }
        //        catch (Exception e)
        //        {
        //            AnalysisStats.DllThatFailedToLoad.Add(classToResolve.ContainingAssembly.Name);
        //            AnalysisStats.TotalDllsFailedToLoad++;
        //        }

        //    }
        //    return resolvedClass;
        //}


		/// <summary>
		/// This methods take a DLL file and generate columns dependencies for every Processor class it found.
		/// </summary>
		/// <param name="inputPath"></param>
		/// <param name="kind"></param>
		/// <param name="useScopeFactory"></param>
		/// <param name="interProc"></param>
		/// <param name="outputStream"></param>
		/// <returns></returns>
        public static SarifLog AnalyzeDll(string inputPath, ScopeMethodKind kind, bool useScopeFactory = true, 
										  bool interProc = false, StreamWriter outputStream = null)
		{
			// Determine whether to use Interproc analysis
			AnalysisOptions.DoInterProcAnalysis = interProc;

			var log = SarifLogger.CreateSarifOutput();
			log.SchemaUri = new Uri("http://step0");

			if (!File.Exists(inputPath))
			{
				var fileName = Path.GetFileName(inputPath);
				var r = SarifLogger.CreateRun(inputPath, "No results", "(AnalyzeDLL) File not found: " + fileName, new List<Result>());
				log.Runs.Add(r);
				return log;
			}

			IAssembly assembly;
			var loader = CreateHost(inputPath, out assembly);

			var scopeProgramAnalysis = new ScopeProgramAnalysis(loader);
			var host = loader.Host;

			AnalysisStats.TotalNumberFolders++;
			AnalysisStats.TotalDllsFound++;

			scopeProgramAnalysis.ComputeProcessorFilters(kind);

			IEnumerable<ScopeProcessorInfo> scopeProcessorsToAnalyze;
			List<Tuple<ITypeDefinition, string>> errorMessages;
			if (useScopeFactory)
			{
				scopeProcessorsToAnalyze = scopeProgramAnalysis.ObtainScopeMethodsToAnalyze(assembly, out errorMessages);
				if (!scopeProcessorsToAnalyze.Any())
				{
					if (outputStream != null)
						outputStream.WriteLine("Failed to obtain methods from the ScopeFactory. ");
				}
			}
			else
			{
				scopeProcessorsToAnalyze = scopeProgramAnalysis.ObtainScopeMethodsToAnalyzeFromAssemblies();
				errorMessages = new List<Tuple<ITypeDefinition, string>>();
			}

			if (!scopeProcessorsToAnalyze.Any() && errorMessages.Count == 0)
			{
				var r = SarifLogger.CreateRun(inputPath, "No results", "No processors found", new List<Result>());
				log.Runs.Add(r);
				return log;
			}

			foreach (var errorMessage in errorMessages)
			{
				var r = SarifLogger.CreateRun(inputPath, errorMessage.Item1 == null ? "No results" : errorMessage.Item1.FullName(), errorMessage.Item2, new List<Result>());
				log.Runs.Add(r);
			}

			var allSchemas = ExtractSchemasFromXML(inputPath, useScopeFactory, scopeProgramAnalysis);

			var processorNumber = 0;

			foreach (var scopeProcessorInfo in scopeProcessorsToAnalyze)
			{
				processorNumber++;
				AnalysisStats.TotalMethods++;

				log.SchemaUri = new Uri(log.SchemaUri.ToString() + String.Format("/processor{0}", processorNumber));

				var processorClass = scopeProcessorInfo.ProcessorClass;
				var entryMethodDef = scopeProcessorInfo.EntryMethod;
				var moveNextMethod = scopeProcessorInfo.MoveNextMethod;
				var getEnumMethod = scopeProcessorInfo.GetIteratorMethod;
				var factoryMethod = scopeProcessorInfo.FactoryMethod;
				Console.WriteLine("Method {0} on class {1}", moveNextMethod.Name, moveNextMethod.ContainingType.FullName());

				Schema inputSchema = null;
				Schema outputSchema = null;
				Tuple<Schema, Schema> schemas;
				if (!TryToGetSchema(allSchemas, moveNextMethod, out schemas))
				{
					continue; // BUG! Silent failure
				}


				var updatedProcessorInfo = scopeProcessorInfo;

				updatedProcessorInfo.InputSchema = schemas.Item1;
				updatedProcessorInfo.OutputSchema = schemas.Item2;

				log.SchemaUri = new Uri(log.SchemaUri.ToString() + "/aboutToAnalyze");
				Run run;
				AnalysisReason errorReason;

				var ok = scopeProgramAnalysis.AnalyzeProcessor(inputPath, updatedProcessorInfo, out run, out errorReason);
				if (ok)
				{
					log.SchemaUri = new Uri(log.SchemaUri.ToString() + "/analyzeOK");

					log.Runs.Add(run);

					if (outputStream != null)
					{
						outputStream.WriteLine("Class: [{0}] {1}", moveNextMethod.ContainingType.FullName(), moveNextMethod.ToString());

						var resultSummary = run.Results.Where(r => r.Id == "Summary").FirstOrDefault();
						if (resultSummary != null) // BUG? What to do if it is null?
						{
							outputStream.WriteLine("Inputs: {0}", String.Join(", ", resultSummary.GetProperty("Inputs")));
							outputStream.WriteLine("Outputs: {0}", String.Join(", ", resultSummary.GetProperty("Outputs")));
						}
					}

				}
				else
				{
					log.SchemaUri = new Uri(log.SchemaUri.ToString() + "/analyzeNotOK/" + errorReason.Reason);

					Console.WriteLine("Could not analyze {0}", inputPath);
					Console.WriteLine("Reason: {0}\n", errorReason.Reason);

					AnalysisStats.TotalofDepAnalysisErrors++;
					AnalysisStats.AddAnalysisReason(errorReason);
				}

			}
			var foo = ExtractDependencyStats(log);
			return log;
		}

		private static bool TryToGetSchema(IReadOnlyDictionary<string, Tuple<Schema, Schema>> allSchemas, IMethodDefinition moveNextMethod, out Tuple<Schema, Schema> schemas)
		{
			return allSchemas.TryGetValue((moveNextMethod.ContainingType as INestedTypeReference).ContainingType.FullName(), out schemas);
		}

		private static IReadOnlyDictionary<string, Tuple<Schema, Schema>> ExtractSchemasFromXML(string inputPath, bool useScopeFactory, ScopeProgramAnalysis scopeProgramAnalysis)
		{
			IReadOnlyDictionary<string, Tuple<Schema, Schema>> allSchemas;
			if (useScopeFactory)
			{
				allSchemas = scopeProgramAnalysis.ReadSchemasFromXML(inputPath);
			}
			else
			{
				allSchemas = scopeProgramAnalysis.ReadSchemasFromXML2(inputPath);
			}

			return allSchemas;
		}

		private void ComputeProcessorFilters(ScopeMethodKind kind)
		{
			ClassFilters = new HashSet<string>();
			ClousureFilters = new HashSet<string>();

			var entryMethods = new HashSet<string>();

			if (kind == ScopeMethodKind.Reducer || kind == ScopeMethodKind.All)
			{
				ClassFilters.Add("Reducer");
				ClousureFilters.Add("<Reduce>d__");
				entryMethods.Add("Reduce");
			}
			if (kind == ScopeMethodKind.Processor || kind == ScopeMethodKind.All)
			{
				ClassFilters.Add("Processor");
				ClousureFilters.Add("<Process>d__");
				entryMethods.Add("Process");
			}
		}

		/// <summary>
		/// This method analyze the Producer associated with the type passed as parameter
		/// </summary>
		/// <param name="processorType"></param>
		/// <param name="inputSchema"></param>
		/// <param name="outputSchema"></param>
		/// <returns></returns>
		public static Run AnalyzeProcessorFromType(Type processorType, string inputSchema, string outputSchema)
		{
			// Determine whether to use Interproc analysis
			AnalysisOptions.DoInterProcAnalysis = false;

			var inputPath = processorType.Assembly.Location;

			IAssembly assembly;
			var loader = CreateHost(inputPath, out assembly);
			var scopeProgramAnalyzer = new ScopeProgramAnalysis(loader);

			var host = loader.Host;

			Run run;
			AnalysisReason errorReason;

			var processorName = processorType.Name;

			var processorClass = assembly
				//.RootNamespace
				.GetAllTypes()
				.OfType<ITypeDefinition>()
				.Where(c => c.GetNameThatMatchesReflectionName() == processorName)
				.SingleOrDefault();
			if (processorClass == null)
			{
				return SarifLogger.CreateRun(inputPath, processorName, "Processor class not found", new List<Result>());
			}
			var entryMethod = FindEntryMethod(loader.RuntimeTypes.concurrentProcessor, processorClass);
			if (entryMethod == null)
			{
				return SarifLogger.CreateRun(inputPath, processorName, "Entry method not found", new List<Result>());
			}

			var closureName = "<" + entryMethod.Name + ">";
			var containingType = entryMethod.ContainingType.ResolvedType as ITypeDefinition;

			//if(containingType is IGenericTypeInstance)
			//{
			//    containingType = (containingType as IGenericTypeInstance).GenericType.ResolvedType;
			//}

			if (containingType == null)
			{
				return SarifLogger.CreateRun(inputPath, processorName, "Containing type of closure type not found", new List<Result>());
			}
			var closureClass = containingType.Members.OfType<ITypeDefinition>().Where(c => c.GetName().StartsWith(closureName)).SingleOrDefault();
			if (closureClass == null)
			{
				return SarifLogger.CreateRun(inputPath, processorName, "Closure class not found", new List<Result>());
			}

			var moveNextMethod = closureClass.Methods.Where(m => m.Name.Value == "MoveNext").SingleOrDefault();
			if (moveNextMethod == null) return null;
			if (moveNextMethod == null)
			{
				return SarifLogger.CreateRun(inputPath, processorName, "MoveNext method not found", new List<Result>());
			}

			var getEnumMethod = closureClass
				.Methods
				.Where(m => m.Name.Value.StartsWith("System.Collections.Generic.IEnumerable<") && m.Name.Value.EndsWith(">.GetEnumerator"))
				.SingleOrDefault();
			if (getEnumMethod == null) return null;
			if (getEnumMethod == null)
			{
				return SarifLogger.CreateRun(inputPath, processorName, "GetEnumerator method not found", new List<Result>());
			}

			var inputColumns = ParseColumns(inputSchema);
			var outputColumns = ParseColumns(outputSchema);

			var processToAnalyze = new ScopeProcessorInfo(processorClass, entryMethod, getEnumMethod, moveNextMethod, null);
			processToAnalyze.InputSchema = new Schema(inputColumns);
			processToAnalyze.OutputSchema = new Schema(outputColumns);

			var ok = scopeProgramAnalyzer.AnalyzeProcessor(inputPath, processToAnalyze, out run, out errorReason);
			if (ok)
			{
				return run;
			}
			else
			{
				return SarifLogger.CreateRun(inputPath, processorName, errorReason.Reason, new List<Result>());
			}
		}



		private IReadOnlyDictionary<string, Tuple<Schema, Schema>>
            ReadSchemasFromXML(string inputPath)
        {
            var d = new Dictionary<string, Tuple<Schema, Schema>>();
            var inputDirectory = Path.GetDirectoryName(inputPath);
            var xmlFile = Path.Combine(inputDirectory, "ScopeVertexDef.xml");
            if (File.Exists(xmlFile))
            {
                XElement x = XElement.Load(xmlFile);
                var operators = x.Descendants("operator");
                foreach (var kv in this.FactoryReducerMap)
                {
                    var processId = kv.Key;
                    var className = kv.Value.FullName();
                    var processors = operators.Where(op => op.Attribute("id") != null && op.Attribute("id").Value == processId);
                    var inputSchemas = processors.SelectMany(processor => processor.Descendants("input").Select(i => i.Attribute("schema")), (processor, schema) => Tuple.Create(processor.Attribute("id"), schema));
                    var outputSchemas = processors.SelectMany(processor => processor.Descendants("output").Select(i => i.Attribute("schema")), (processor, schema) => Tuple.Create(processor.Attribute("id"), schema));
                    var inputSchema = inputSchemas.FirstOrDefault();
                    var outputSchema = outputSchemas.FirstOrDefault();
                    if (inputSchema == null || outputSchema == null) continue; // BUG? Silent failure okay?
                    if (inputSchema.Item1 != outputSchema.Item1) continue; // silent failure okay?
                    var inputColumns = ParseColumns(inputSchema.Item2.Value);
                    var outputColumns = ParseColumns(outputSchema.Item2.Value);
                    d.Add(className, Tuple.Create(new Schema(inputColumns), new Schema(outputColumns)));
                }
            } else
            {
                throw new FileNotFoundException("Cannot find ScopeVertexDef.xml");
            }
            return d;
        }

        private IReadOnlyDictionary<string, Tuple<Schema, Schema>>
            ReadSchemasFromXML2(string inputPath)
        {
            var d = new Dictionary<string, Tuple<Schema, Schema>>();
            var inputDirectory = Path.GetDirectoryName(inputPath);
            var xmlFile = Path.Combine(inputDirectory, "Schema.xml");
            if (File.Exists(xmlFile))
            {
                XElement x = XElement.Load(xmlFile);
                var operators = x.Descendants("operator");
                foreach (var op in operators)
                {
                    var inputSchema = op.Descendants("input").First().Attribute("schema").Value;
                    var outputSchema = op.Descendants("output").First().Attribute("schema").Value;
                    var inputColumns = ParseColumns(inputSchema);
                    var outputColumns = ParseColumns(outputSchema);
                    d.Add(op.Attribute("className").Value, Tuple.Create(new Schema(inputColumns), new Schema(outputColumns)));
                }
            }
            return d;
        }


        public class SarifLogger
        {
            SarifLog log;
            public SarifLogger(SarifLog log)
            {
                this.log = log;
            }

            public static SarifLog CreateSarifOutput()
            {
                JsonSerializerSettings settings = new JsonSerializerSettings()
                {
                    ContractResolver = SarifContractResolver.Instance,
                    Formatting = Formatting.Indented
                };

                SarifLog log = new SarifLog()
                {
                    Runs = new List<Run>()
                };
                return log;
            }

            public static Run CreateRun(string inputPath, string id, string notification, IList<Result> results)
            {
                var run = new Run();
                // run.StableId = method.ContainingType.FullPathName();
                //run.Id = String.Format("[{0}] {1}", method.ContainingType.FullPathName(), method.ToSignatureString());
                run.Id = id;
                run.Tool = Tool.CreateFromAssemblyData();
                run.Tool.Name = "ScopeProgramAnalysis";
                run.Files = new Dictionary<string, FileData>();
                var fileDataKey = UriHelper.MakeValidUri(inputPath);
                var fileData = FileData.Create(new Uri(fileDataKey, UriKind.RelativeOrAbsolute), false);
                run.Files.Add(fileDataKey, fileData);
                run.ToolNotifications = new List<Notification>();
                if (!String.IsNullOrWhiteSpace(notification))
                    run.ToolNotifications.Add(new Notification { Message = notification, });

                run.Results = results;

                return run;
            }
            public static void WriteSarifOutput(SarifLog log, string outputFilePath)
            {
                string sarifText = SarifLogToString(log);
                try
                {
                    //if (!File.Exists(outputFilePath))
                    //{
                    //    File.CreateText(outputFilePath);
                    //}
                    File.WriteAllText(outputFilePath, sarifText);
                }
                catch (Exception e)
                {
                    System.Console.Out.Write("Could not write the file: {0}:{1}", outputFilePath, e.Message);
                }
            }

            public static string SarifLogToString(SarifLog log)
            {
                JsonSerializerSettings settings = new JsonSerializerSettings()
                {
                    ContractResolver = SarifContractResolver.Instance,
                    Formatting = Formatting.Indented
                };

                var sarifText = JsonConvert.SerializeObject(log, settings);
                return sarifText;
            }
            public static string SarifRunToString(Run run)
            {
                JsonSerializerSettings settings = new JsonSerializerSettings()
                {
                    ContractResolver = SarifContractResolver.Instance,
                    Formatting = Formatting.Indented
                };

                var sarifText = JsonConvert.SerializeObject(run, settings);
                return sarifText;
            }
        }
    }

}
