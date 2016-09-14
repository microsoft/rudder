using Microsoft.VisualStudio.TestTools.UnitTesting;
using static ScopeProgramAnalysis.ScopeProgramAnalysis;
using CodeUnderTest;

namespace SimpleTests
{
    [TestClass]
    public class UnitTest1
    {
        [TestMethod]
        public void TestMethod1()
        {
            var t = typeof(CopyProcessor);
            var log = AnalyzeDll(t.Assembly.Location, ScopeMethodKind.All, false, false, null);
        }
        [TestMethod]
        public void TestMethod2()
        {
            var t = typeof(CopyProcessor);
            var run = AnalyzeProcessor(t, "JobGUID: string, JobName: string", "JobGUID: string, JobName: string");

        }
    }
}
