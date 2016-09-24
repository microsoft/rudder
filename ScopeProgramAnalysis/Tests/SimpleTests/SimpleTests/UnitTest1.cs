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
        public void TestMethod1()
        {
            var t = typeof(CopyProcessor);
            var log = AnalyzeDll(t.Assembly.Location, ScopeMethodKind.All, true,  
                                    false, false, null);
        }
        [TestMethod]
        public void TestMethod2()
        {
            var t = typeof(CopyProcessor);
            var run = AnalyzeProcessor(t, "JobGUID: string, JobName: string", "JobGUID: string, JobName: string");

        }
        [TestMethod]
        public void TestMethod3()
        {
            var t = typeof(AddOneColumnProcessor);
            var run = AnalyzeProcessor(t, "JobGUID: string, JobName: string", "JobGUID: string, JobName: string, NewColumn: string");

        }

        [TestMethod]
        public void TestMethod4()
        {
            var t = typeof(SubtypeOfCopyProcessor);
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

    }
}
