using Backend.Analyses;
using Backend.Model;
using Backend.Serialization;
using Backend.Transformations;
using Backend.Utils;
using CCIProvider;
using Model;
using Model.ThreeAddressCode.Instructions;
using Model.Types;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Program
{
    public class MethodCFGCache
    {
        private IDictionary<MethodDefinition, ControlFlowGraph> methodCFGMap;
        private Host host;

        public MethodCFGCache(Host host)
        {
            this.host = host;
            this.methodCFGMap = new Dictionary<MethodDefinition, ControlFlowGraph>();
        }

        public ControlFlowGraph  GetCFG(MethodDefinition method)
        {
            ControlFlowGraph methodCFG = null;
            if (!this.methodCFGMap.ContainsKey(method))
            {
                methodCFG = method.DoAnalysisPhases(this.host);
                this.methodCFGMap[method] = methodCFG;
            }
            else
            {
                methodCFG = this.methodCFGMap[method];
            }
            return methodCFG;
        }
    }
    class Program
    {
        private Host host;
        private MethodCFGCache cache;
        public Program(Host host)
        {
            this.host = host;
            this.cache =  new MethodCFGCache(host);
        }
        public static void Main(string[] args)
        {
            string root = AppDomain.CurrentDomain.BaseDirectory + @"..\..\..";
            string input = root + @"\Test\bin\Debug\Test.dll";
            Analyze(input);

        }
        public static void Analyze(string inputPath)
        {
            var host = new Host();
            PlatformTypes.Resolve(host);

            var loader = new Loader(host);
            loader.LoadAssembly(inputPath);
            // loader.LoadCoreAssembly();

            var program = new Program(host);
           
            var classUnderAnalysis = host.Assemblies.SelectMany(a => a.RootNamespace.GetAllTypes().OfType<ClassDefinition>())
                            .SingleOrDefault(c => c.Name == "ExamplesCallGraph");

            var methodUnderAnalysis = classUnderAnalysis.Methods.SingleOrDefault(m => m.Name == "Example1");

            var roots = new HashSet<MethodDefinition>() { methodUnderAnalysis };
            foreach (var rootMethodRef in roots)
            {
                var rootMethod = host.ResolveReference(rootMethodRef) as MethodDefinition;
                // var cfg = chcga.GetControlFlowGraph(rootMethod);
                var cfg = program.cache.GetCFG(rootMethod);

                var pta = new PointsToAnalysisWithResults(cfg, rootMethod);
                pta.Analyze();
                var dfa = new LocalTaintingDFAnalysis(rootMethod, cfg, pta, host, /*cg, */ program.cache);
                var result = dfa.Analyze();
                System.Console.WriteLine("Method: {0}", rootMethod.Name);
                System.Console.Out.WriteLine(result[1].Output);
            }

            System.Console.WriteLine("Done!");
            System.Console.ReadKey();

        }
    }
}
