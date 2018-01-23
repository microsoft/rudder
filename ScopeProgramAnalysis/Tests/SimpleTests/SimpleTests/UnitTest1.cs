using Microsoft.VisualStudio.TestTools.UnitTesting;
using static ScopeProgramAnalysis.ScopeProgramAnalysis;
using CodeUnderTest;
using System.Collections.Generic;
using System.Linq;
using System;
using Microsoft.CodeAnalysis.Sarif;

namespace SimpleTests
{
    [TestClass]
    public class UnitTest1
    {
        [TestMethod]
        public void AnalyzeEntireCodeUnderTestDll()
        {
            var t = typeof(CopyProcessor);
            var log = ScopeProgramAnalysis.Program.AnalyzeDll(t.Assembly.Location, ScopeMethodKind.All, false, false, null);

            Assert.IsNotNull(log);
        }
        [TestMethod]
        public void CopyProcessor()
        {
            var t = typeof(CopyProcessor);
            var run = AnalyzeProcessor(t, "JobGUID: string, JobName: string", "JobGUID: string, JobName: string");

            Assert.IsNotNull(run);
            Assert.IsTrue(run.BothAnalysesAgree());
            Assert.IsTrue(run.BagOColumnsIsTop());
            Assert.IsTrue(run.ColumnDependsOn("JobGUID", "JobGUID"));
            Assert.IsTrue(run.ColumnDependsOn("JobName", "JobName"));
            Assert.IsTrue(run.Inputs("JobGUID", "JobName"));
            Assert.IsTrue(run.Outputs("JobGUID", "JobName"));
        }

        private Run AnalyzeProcessor(Type t, string v1, string v2)
        {
            return  ScopeProgramAnalysis.ScopeProgramAnalysis.AnalyzeProcessor(t, v1, v2);
        }

        [TestMethod]
        public void ReturnRow()
        {
            var t = typeof(ReturnRowReducer);
            var run = AnalyzeProcessor(t, "JobGUID: string, JobName: string", "JobGUID: string, JobName: string");

            Assert.IsNotNull(run);
            // Assert.IsTrue(run.BothAnalysesAgree());
            //Assert.IsTrue(run.BagOColumnsIsTop());
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

            Assert.IsNotNull(run);
            Assert.IsTrue(run.BothAnalysesAgree());
            Assert.IsTrue(run.BagOColumnsIsTop());
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

            Assert.IsNotNull(run);
            Assert.IsTrue(run.BothAnalysesAgree());
            Assert.IsTrue(run.BagOColumnsIsTop());
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

            Assert.IsNotNull(run);
            Assert.IsTrue(run.BothAnalysesAgree());
        }

        [TestMethod]
        public void ReturnMethodCall()
        {
            var t = typeof(ProcessReturningMethodCall);
            var run = AnalyzeProcessor(t, "JobGUID: string, JobName: string", "JobGUID: string, JobName: string");

            Assert.IsNotNull(run);
            Assert.IsTrue(run.BothAnalysesAgree());
            Assert.IsTrue(run.Id == "ProcessReturningMethodCall");
            Assert.IsTrue(run.ToolNotifications.Count == 1);
            Assert.IsTrue(run.ToolNotifications[0].Message == "Closure class not found");
        }
        [TestMethod]
        public void TopN()
        {
            var t = typeof(TopN);
            var run = AnalyzeProcessor(t, "JobGUID: string, JobName: string", "JobGUID: string, JobName: string");

            Assert.IsNotNull(run);
            Assert.IsTrue(run.BothAnalysesAgree());
            Assert.IsTrue(run.BagOColumnsIsTop());
            Assert.IsTrue(run.ColumnDependsOn("JobGUID", "JobGUID"));
            Assert.IsTrue(run.ColumnDependsOn("JobName", "JobName"));
            Assert.IsTrue(run.Inputs("JobGUID", "JobName"));
            Assert.IsTrue(run.Outputs("JobGUID", "JobName"));
        }
        [TestMethod]
        public void AccumulateList()
        {
            var t = typeof(AccumulateList);
            var run = AnalyzeProcessor(t, "X: ulong, Y: int", "X: ulong, Y: int");

            Assert.IsNotNull(run);
            Assert.IsTrue(run.BothAnalysesAgree());
        }
        [TestMethod]
        public void Dictionary()
        {
            var t = typeof(UseDictionary);
            var run = AnalyzeProcessor(t, "X: long, Y: int", "X: long");

            Assert.IsNotNull(run);
            Assert.IsTrue(run.BothAnalysesAgree());
        }
        [TestMethod]
        public void LastX()
        {
            var t = typeof(LastX);
            var run = AnalyzeProcessor(t, "X: double", "X: double");

            Assert.IsNotNull(run);
            Assert.IsTrue(run.BothAnalysesAgree());
        }
        [TestMethod]
        public void ConditionalSchemaWriteColumn()
        {
            var t = typeof(ConditionalSchemaWriteColumn);
            var run = AnalyzeProcessor(t, "X: int", "X: int");

            Assert.IsNotNull(run);
            Assert.IsTrue(run.BothAnalysesAgree());
            Assert.IsTrue(run.ColumnDependsOn("X", "X", "Double"));
            Assert.IsTrue(run.Inputs("X"));
            Assert.IsTrue(run.Outputs("X"));
        }
        [TestMethod]
        public void SubtypeOfGenericProcessor()
        {
            var t = typeof(SubtypeOfGenericProcessor);
            var run = AnalyzeProcessor(t, "X: int", "X: int");

            Assert.IsNotNull(run);
            Assert.IsTrue(run.BothAnalysesAgree());
            Assert.IsTrue(run.ColumnDependsOn("X", "X"));
            Assert.IsTrue(run.Inputs("X"));
            Assert.IsTrue(run.Outputs("X"));
        }
        [TestMethod]
        public void IterateOverColumns()
        {
            var t = typeof(IterateOverColumns);
            var run = AnalyzeProcessor(t, "X: int, Y: string", "X: int, Y: string");

            Assert.IsNotNull(run);
            Assert.IsTrue(run.BothAnalysesAgree());
            Assert.IsTrue(run.RunIsTop());
        }
        [TestMethod]
        public void ReadOnlyX()
        {
            var t = typeof(ReadOnlyX);
            var run = AnalyzeProcessor(t, "X: string, Y: int", "X: string, Y: int");

            Assert.IsNotNull(run);
            Assert.IsTrue(run.BothAnalysesAgree());
            Assert.IsTrue(run.ColumnDependsOn("X", "X"));
            Assert.IsTrue(run.ColumnDependsOn("Y", "Int32"));
            Assert.IsTrue(run.Inputs("X"));
            Assert.IsTrue(run.Outputs("X", "Y"));
        }
        [TestMethod]
        public void CallMethodOnInputRow()
        {
            var t = typeof(CallMethodOnInputRow);
            var run = AnalyzeProcessor(t, "X: string, Y: int", "X: string, Y: int");

            Assert.IsNotNull(run);
            Assert.IsTrue(run.BothAnalysesAgree()); // Both return TOP
        }
        [TestMethod]
        public void CallScopeMap01()
        {
            var t = typeof(ScopeMap01);
            var run = AnalyzeProcessor(t, "X: string", "X: string, a: int");
            var s = SarifLogger.SarifRunToString(run);

            Assert.IsNotNull(run);
            Assert.IsTrue(run.BothAnalysesAgree());
        }
        [TestMethod]
        public void CallFirstRowReducer()
        {
            var t = typeof(FirstRowReducer);
            var run = AnalyzeProcessor(t, "X: string, Y: int", "X: string, Y: int");

            var s = SarifLogger.SarifRunToString(run);
            Assert.IsNotNull(run);
            // Diego: This test fail because the processor does not read any input columns and do not write an output column explicitly 
            // It seems the doing yield return is not enough
            Assert.IsTrue(run.BothAnalysesAgree());
        }
        [TestMethod]
        public void CallRowCountReducer()
        {
            var t = typeof(RowCountReducer);
            var run = AnalyzeProcessor(t, "X: string, Y: int", "X: string, Y: int");

            var s = SarifLogger.SarifRunToString(run);
            Assert.IsNotNull(run);
            // Diego: This test fail because the processor does not read any input columns and do not write an output column explicitly 
            // It seems the doing yield return is not enough
            Assert.IsTrue(run.BothAnalysesAgree());
        }
        [TestMethod]
        public void CallConditionalColumnReducer()
        {
            var t = typeof(ConditionalColumnReducer);
            var run = AnalyzeProcessor(t, "A: string, B: string", "OutputColumn: string");

            var s = SarifLogger.SarifRunToString(run);
            Assert.IsNotNull(run);
            Assert.IsTrue(run.BothAnalysesAgree());
        }
        [TestMethod]
        public void CallPassColumnValuesToMethodReducer()
        {
            var t = typeof(PassColumnValuesToMethodReducer);
            var run = AnalyzeProcessor(t, "A: string", "A: string");

            var s = SarifLogger.SarifRunToString(run);
            Assert.IsNotNull(run);
            Assert.IsTrue(run.BothAnalysesAgree());
        }

    }
}
