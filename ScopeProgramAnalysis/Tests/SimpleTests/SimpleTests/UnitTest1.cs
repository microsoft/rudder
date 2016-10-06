using Microsoft.VisualStudio.TestTools.UnitTesting;
using static ScopeProgramAnalysis.ScopeProgramAnalysis;
using CodeUnderTest;
using System.Collections.Generic;
using System.Linq;

namespace SimpleTests
{
    [TestClass]
    public class UnitTest1
    {
        [TestMethod]
        public void AnalyzeEntireCodeUnderTestDll()
        {
            var t = typeof(CopyProcessor);
            var log = AnalyzeDll(t.Assembly.Location, ScopeMethodKind.All, true,  
                                    false, false, null);
        }
        [TestMethod]
        public void CopyProcessor()
        {
            var t = typeof(CopyProcessor);
            var run = AnalyzeProcessor(t, "JobGUID: string, JobName: string", "JobGUID: string, JobName: string");

            Assert.IsTrue(run.ColumnDependsOn("JobGUID", "JobGUID"));
            Assert.IsTrue(run.ColumnDependsOn("JobName", "JobName"));
            Assert.IsTrue(run.Inputs("JobGUID", "JobName"));
            Assert.IsTrue(run.Outputs("JobGUID", "JobName"));
        }
        [TestMethod]
        public void AddOneColumnProcessor()
        {
            var t = typeof(AddOneColumnProcessor);
            var run = AnalyzeProcessor(t, "JobGUID: string, JobName: string", "JobGUID: string, JobName: string, NewColumn: string");
            Assert.IsTrue(run.ColumnDependsOn("JobGUID", "JobGUID"));

            Assert.IsTrue(run.ColumnDependsOn("JobName", "JobName"));
            Assert.IsTrue(run.ColumnDependsOn("NewColumn", "Concat(String,String)", "JobGUID"));
            Assert.IsTrue(run.Inputs("JobGUID", "JobName"));
            Assert.IsTrue(run.Outputs("JobGUID", "JobName", "NewColumn"));
        }

        [TestMethod]
        public void SubtypeOfCopyProcessor()
        {
            var t = typeof(SubtypeOfCopyProcessor);
            var run = AnalyzeProcessor(t, "JobGUID: string, JobName: string", "JobGUID: string, JobName: string");

            Assert.IsTrue(run.ColumnDependsOn("JobGUID", "JobGUID"));
            Assert.IsTrue(run.ColumnDependsOn("JobName", "JobName"));
            Assert.IsTrue(run.Inputs("JobGUID", "JobName"));
            Assert.IsTrue(run.Outputs("JobGUID", "JobName"));
        }

        [TestMethod]
        public void TestDictValues()
        {
            var t = typeof(TestDictProcessor);
            var run = AnalyzeProcessor(t, "JobGUID: string, JobName: string", "JobGUID: string, JobName: string");
        }

        [TestMethod]
        public void ReturnMethodCall()
        {
            var t = typeof(ProcessReturningMethodCall);
            var run = AnalyzeProcessor(t, "JobGUID: string, JobName: string", "JobGUID: string, JobName: string");

            Assert.IsNotNull(run);
            Assert.IsTrue(run.Id == "ProcessReturningMethodCall");
            Assert.IsTrue(run.ToolNotifications.Count == 1);
            Assert.IsTrue(run.ToolNotifications[0].Message == "Closure class not found");
        }
        [TestMethod]
        public void TopN()
        {
            var t = typeof(TopN);
            var run = AnalyzeProcessor(t, "JobGUID: string, JobName: string", "JobGUID: string, JobName: string");
        }
        [TestMethod]
        public void AccumulateList()
        {
            var t = typeof(AccumulateList);
            var run = AnalyzeProcessor(t, "X: ulong, Y: int", "X: ulong, Y: int");
        }
        [TestMethod]
        public void Dictionary()
        {
            var t = typeof(UseDictionary);
            var run = AnalyzeProcessor(t, "X: long, Y: int", "X: long");
        }
        [TestMethod]
        public void LastX()
        {
            var t = typeof(LastX);
            var run = AnalyzeProcessor(t, "X: double", "X: double");
        }
        [TestMethod]
        public void ConditionalSchemaWriteColumn()
        {
            var t = typeof(ConditionalSchemaWriteColumn);
            var run = AnalyzeProcessor(t, "X: int", "X: int");

            Assert.IsTrue(run.ColumnDependsOn("X", "X", "Double"));
            Assert.IsTrue(run.Inputs("X"));
            Assert.IsTrue(run.Outputs("X"));
        }
        [TestMethod]
        public void SubtypeOfGenericProcessor()
        {
            var t = typeof(SubtypeOfGenericProcessor);
            var run = AnalyzeProcessor(t, "X: int", "X: int");

            Assert.IsTrue(run.ColumnDependsOn("X", "X"));
            Assert.IsTrue(run.Inputs("X"));
            Assert.IsTrue(run.Outputs("X"));
        }
        [TestMethod]
        public void IterateOverColumns()
        {
            var t = typeof(IterateOverColumns);
            var run = AnalyzeProcessor(t, "X: int, Y: string", "X: int, Y: string");

            Assert.IsTrue(run.ColumnDependsOn("X", "X"));
            Assert.IsTrue(run.ColumnDependsOn("Y", "Y"));
            Assert.IsTrue(run.Inputs("X", "Y"));
            Assert.IsTrue(run.Outputs("X", "Y"));
        }

    }
}
