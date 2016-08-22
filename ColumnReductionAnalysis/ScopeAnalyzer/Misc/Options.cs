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

    /// <summary>
    /// Class for parsing and making sense of command line options.
    /// </summary>
    public class Options
    {

        private List<string> mainAssemblies = new List<string>();
        private string mainAssembliesPath;
        private List<string> referenceAssemblies = new List<string>();
        private string referenceAssembliesPath;

        // Path where to print the analysis trace and results.
        private string outputPath;
        // Path to file that keeps mapping from processor/reducer/combiner names to ids.
        private string processorIdPath;
        // Path to file that contains ScopeVertexDef xml content.
        private string vertexDefPath;

        public bool Verbose { get; }


        private Options(string asmbPath, string refAssemblyPath, string outPath, string prcIdPath, string vrxPath, bool verbose)
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
            processorIdPath = prcIdPath;
         
            if (vrxPath != null)
            {
                vertexDefPath = vrxPath;
            }
            else
            {
                vertexDefPath = Path.GetFullPath(Path.GetDirectoryName(mainAssembliesPath)) + "\\" + Utils.VERTEX_DEF_NAME;
            }

            Verbose = verbose;
        }

        public static Options ParseCommandLineArguments(string[] args)
        {
            string assembly = null, libs = null, output = null, processor = null, vertex = null;
            bool verbose = false;
            foreach(var arg in args)
            {
                if (arg.StartsWith("/assembly:")) assembly = arg.Split(new string[] { "/assembly:" }, StringSplitOptions.None)[1];
                else if (arg.StartsWith("/libs:")) libs = arg.Split(new string[] { "/libs:" }, StringSplitOptions.None)[1];
                else if (arg.StartsWith("/output:")) output = arg.Split(new string[] { "/output:" }, StringSplitOptions.None)[1];
                else if (arg.StartsWith("/processorIds:")) processor = arg.Split(new string[] { "/processorIds:" }, StringSplitOptions.None)[1];
                else if (arg.StartsWith("/vertexDef:")) vertex = arg.Split(new string[] { "/vertexDef:" }, StringSplitOptions.None)[1];
                else if (arg.Trim() == "/verbose") verbose = true;
            }

            if (assembly == null)
                throw new ParsingOptionsException("No given assembly to analyze!");

            return new Options(assembly, libs, output, processor, vertex, verbose);
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
