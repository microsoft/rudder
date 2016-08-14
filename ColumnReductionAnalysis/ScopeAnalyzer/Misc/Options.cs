using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ScopeAnalyzer.Misc
{
    public class ParsingOptionsException : Exception
    {
        public ParsingOptionsException (string message) : base (message)
        {

        }
    }

    public class Options
    {

        private List<string> mainAssemblies = new List<string>();
        private string mainAssembliesPath;
        private List<string> referenceAssemblies = new List<string>();
        private string referenceAssembliesPath;

        private string outputPath;
        private string processorIdPath;
        private string vertexDefPath;


        private Options(string asmbPath, string refAssemblyPath, string outPath, string prcIdPath, string vrxPath)
        {
            mainAssembliesPath = asmbPath;

            if (mainAssembliesPath.EndsWith(".dll") || mainAssembliesPath.EndsWith(".exe"))
            {
                mainAssemblies = new List<string>() { Path.GetFullPath(mainAssembliesPath) };
            }
            else
            {
                mainAssemblies = Utils.CollectAssemblies(mainAssembliesPath);
            }

            referenceAssembliesPath = refAssemblyPath;
            if (referenceAssembliesPath != null)
                referenceAssemblies = Utils.CollectAssemblies(referenceAssembliesPath);

            outputPath = outPath;

            if (prcIdPath != null)
            {
                processorIdPath = prcIdPath;
            }
            else
            {
                processorIdPath = Path.GetFullPath(Path.GetDirectoryName(mainAssembliesPath)) + "\\" + Utils.PROCESSOR_ID_MAPPING_NAME;
            }

            if (vrxPath != null)
            {
                vertexDefPath = vrxPath;
            }
            else
            {
                vertexDefPath = Path.GetFullPath(Path.GetDirectoryName(mainAssembliesPath)) + "\\" + Utils.VERTEX_DEF_NAME;
            }
        }

        public static Options ParseCommandLineArguments(string[] args)
        {
            string assembly = null, libs = null, output = null, processor = null, vertex = null;

            foreach(var arg in args)
            {
                if (arg.StartsWith("/assembly:")) assembly = arg.Split(new string[] { "/assembly:" }, StringSplitOptions.None)[1];
                else if (arg.StartsWith("/libs:")) libs = arg.Split(new string[] { "/libs:" }, StringSplitOptions.None)[1];
                else if (arg.StartsWith("/output:")) output = arg.Split(new string[] { "/output:" }, StringSplitOptions.None)[1];
                else if (arg.StartsWith("/processorIds:")) processor = arg.Split(new string[] { "/processorIds:" }, StringSplitOptions.None)[1];
                else if (arg.StartsWith("/vertexDef:")) vertex = arg.Split(new string[] { "/vertexDef:" }, StringSplitOptions.None)[1];
            }

            if (assembly == null)
                throw new ParsingOptionsException("No given assembly to analyze!");

            return new Options(assembly, libs, output, processor, vertex);
        }


        public List<string> Assemblies
        {
            get { return mainAssemblies; }
        }

        public string AssembliesPath
        {
            get { return mainAssembliesPath; }
        }

        public string ReferencesPath
        {
            get { return referenceAssembliesPath; }
        }

        public List<string> ReferenceAssemblies
        {
            get { return referenceAssemblies; }
        }

        public string OutputPath
        {
            get { return outputPath; }
        }

        public string VertexDefPath
        {
            get { return vertexDefPath; }
        }

        public string ProcessorIdPath
        {
            get { return processorIdPath; }
        }     
    }
}
