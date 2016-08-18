using System;
using System.Linq;
using System.IO;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ScopeAnalyzer;
using ScopeAnalyzer.Misc;
using Microsoft.Cci;
using ScopeAnalyzer.Analyses;
using Frontend;

namespace ScopeAnalysisTester
{
    [TestClass]
    public class ScopeAnalysisUnitTests
    {
        static string LIB_PATH = "libs";
        static string DLL_PATH = "dlls\\PeriScope.dll"; 

        string[] args = new string[] { String.Format("/assembly:{0}", DLL_PATH), String.Format("/libs:{0}", LIB_PATH) };



        private Tuple<IMetadataHost, Assembly, IEnumerable<Assembly>> Initialize()
        {
            var options = Options.ParseCommandLineArguments(args);
            var host = new PeReader.DefaultHost();
            var data = ScopeAnalyzer.Program.LoadAssemblies(host, options);
            var mainAssemblies = data.Item1;
            Assert.AreEqual(mainAssemblies.Count, 1);
            return new Tuple<IMetadataHost, Assembly, IEnumerable<Assembly>>(host, mainAssemblies[0], data.Item2);
        }

        private HashSet<string> ColumnIndicesAsStrings(ColumnsDomain domain)
        {
            if (domain.IsBottom || domain.IsTop)
                return null;

            var indices = new HashSet<string>();
            foreach(var index in domain.Elements)
            {
                indices.Add(index.Value.ToString());              
            }
            return indices;
        }




        [TestMethod]
        public void TestMethod1()
        {
            var data = Initialize();
            var host = data.Item1;
            var assembly = data.Item2;
            var refAssemblies = data.Item3;
            var interestingReducer = new HashSet<string>() { "PeriScope.FineNoEscapeByField" };

            var scopeAnalysis = new ScopeAnalysis(host, assembly, refAssemblies, interestingReducer);
            scopeAnalysis.Analyze();
            var results = scopeAnalysis.Results;

            var interestingResults = results.Where(r => r.Interesting).ToList();
            Assert.AreEqual(interestingResults.Count, 1);

            var result = interestingResults.ElementAt(0);
            Assert.IsFalse(result.EscapeSummary.IsTop);
            Assert.IsTrue(result.UsedColumnsSummary.IsTop);
        }



        [TestMethod]
        public void TestMethod2()
        {
            var data = Initialize();
            var host = data.Item1;
            var assembly = data.Item2;
            var refAssemblies = data.Item3;
            var interestingReducer = new HashSet<string>() { "PeriScope.FineNoEscapeByField2" };

            var scopeAnalysis = new ScopeAnalysis(host, assembly, refAssemblies, interestingReducer);
            scopeAnalysis.Analyze();
            var results = scopeAnalysis.Results;

            var interestingResults = results.Where(r => r.Interesting).ToList();
            Assert.AreEqual(interestingResults.Count, 1);

            var result = interestingResults.ElementAt(0);
            Assert.IsFalse(result.EscapeSummary.IsTop);
            Assert.IsFalse(result.UsedColumnsSummary.IsBottom);
            Assert.IsFalse(result.UsedColumnsSummary.IsTop);

            var columns = ColumnIndicesAsStrings(result.UsedColumnsSummary);
            Assert.IsTrue(columns.Count == 1);

            var expected = new HashSet<string>() { "count" };
            Assert.IsTrue(columns.SetEquals(expected));
        }



        [TestMethod]
        public void TestMethod3()
        {
            var data = Initialize();
            var host = data.Item1;
            var assembly = data.Item2;
            var refAssemblies = data.Item3;
            var interestingReducer = new HashSet<string>() { "PeriScope.FineMain" };

            var scopeAnalysis = new ScopeAnalysis(host, assembly, refAssemblies, interestingReducer);
            scopeAnalysis.Analyze();
            var results = scopeAnalysis.Results;

            var interestingResults = results.Where(r => r.Interesting).ToList();
            Assert.AreEqual(interestingResults.Count, 1);

            var result = interestingResults.ElementAt(0);
            Assert.IsFalse(result.EscapeSummary.IsTop);
            Assert.IsFalse(result.UsedColumnsSummary.IsBottom);
            Assert.IsFalse(result.UsedColumnsSummary.IsTop);

            var columns = ColumnIndicesAsStrings(result.UsedColumnsSummary);
            Assert.IsTrue(columns.Count == 7);

            var expected = new HashSet<string>() { "ctrls", "query", "alteredQuery", "market", "mvalue", "cvalue", "1" };
            Assert.IsTrue(columns.SetEquals(expected));
        }



        [TestMethod]
        public void TestMethod4()
        {
            var data = Initialize();
            var host = data.Item1;
            var assembly = data.Item2;
            var refAssemblies = data.Item3;
            var interestingReducer = new HashSet<string>() { "PeriScope.FineCallRowMethod" };

            var scopeAnalysis = new ScopeAnalysis(host, assembly, refAssemblies, interestingReducer);
            scopeAnalysis.Analyze();
            var results = scopeAnalysis.Results;

            var interestingResults = results.Where(r => r.Interesting).ToList();
            Assert.AreEqual(interestingResults.Count, 1);

            var result = interestingResults.ElementAt(0);
            Assert.IsFalse(result.EscapeSummary.IsTop);
            Assert.IsTrue(result.UsedColumnsSummary.IsTop);
        }


        [TestMethod]
        public void TestMethod5()
        {
            var data = Initialize();
            var host = data.Item1;
            var assembly = data.Item2;
            var refAssemblies = data.Item3;
            var interestingReducer = new HashSet<string>() { };

            var scopeAnalysis = new ScopeAnalysis(host, assembly, refAssemblies, interestingReducer);
            scopeAnalysis.Analyze();
            var results = scopeAnalysis.Results;
            var interestingResults = results.Where(r => r.Interesting).ToList();
            Assert.AreEqual(interestingResults.Count(), 0);
        }



        [TestMethod]
        public void TestMethod6()
        {
            var data = Initialize();
            var host = data.Item1;
            var assembly = data.Item2;
            var refAssemblies = data.Item3;

            var scopeAnalysis = new ScopeAnalysis(host, assembly, refAssemblies, null);
            scopeAnalysis.Analyze();
            var results = scopeAnalysis.Results;
            var interestingResults = results.Where(r => r.Interesting).ToList();
            Assert.AreEqual(interestingResults.Count(), 8);
        }



        [TestMethod]
        public void TestMethod7()
        {
            var data = Initialize();
            var host = data.Item1;
            var assembly = data.Item2;
            var refAssemblies = data.Item3;
            var interestingReducer = new HashSet<string>() { "PeriScope.BadCallIEnumerableRowMethod", "PeriScope.BadSetOutterField",
                                                             "PeriScope.BadSetStaticField", "PeriScope.BadEscapeByCall" };

            var scopeAnalysis = new ScopeAnalysis(host, assembly, refAssemblies, interestingReducer);
            scopeAnalysis.Analyze();
            var results = scopeAnalysis.Results;
            var interestingResults = results.Where(r => r.Interesting).ToList();
            Assert.AreEqual(interestingResults.Count(), 4);
            foreach(var r in interestingResults)
            {
                Assert.IsTrue(r.EscapeSummary.IsTop);
            }
        }


    }
}
