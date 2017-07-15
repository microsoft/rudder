using Backend.Analyses;
using Microsoft.Cci;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ScopeProgramAnalysis.Framework;
namespace ScopeProgramAnalysis
{
    class ProducesMethodAnalyzer
    {
        MyLoader loader;
        ITypeDefinition processorClass;

        public ProducesMethodAnalyzer(MyLoader loader, ITypeDefinition processorClass)
        {
            this.loader = loader;
            this.processorClass = processorClass;
        }
        public  object InferAnnotations(Schema inputSchema)
        {
            var producesMethod = processorClass.Methods.Where(m => m.Name.Value == "Produces").SingleOrDefault();
            var method = producesMethod.DoAnalysisPhases(loader.Host, null);

            return null;
        }
    }
}
