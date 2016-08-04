using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ScopeAnalyzer
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

        private Options(string asmbPath, string refAssemblyPath, string outPath)
        {
            mainAssembliesPath = asmbPath;

            if (mainAssembliesPath.EndsWith(".dll") || mainAssembliesPath.EndsWith(".exe"))
            {
                mainAssemblies = new List<string>() { mainAssembliesPath };
            }
            else
            {
                mainAssemblies = CollectAssemblies(mainAssembliesPath);
            }

            referenceAssembliesPath = refAssemblyPath;
            if (referenceAssembliesPath != null) referenceAssemblies = CollectAssemblies(referenceAssembliesPath);

            outputPath = outPath;
        }

        public static Options ParseCommandLineArguments(string[] args)
        {
            if (args.Length < 2) throw new Exception("Some arguments missing!");

            if (args.Length == 2)
            {
                return new Options(args[0], args[1], null);
            }
            else
            {
                return new Options(args[0], args[1], args[2]);
            }
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

        private List<string> CollectAssemblies(string path)
        {
            DirectoryInfo dir;
            try
            {
                dir = new DirectoryInfo(path);
            }
            catch
            {
                throw new Exception(String.Format("Cannot access the directory with assemblies: '{0}'", path));
            }

            FileInfo[] exeFiles = dir.GetFiles("*.exe");
            FileInfo[] dllFiles = dir.GetFiles("*.dll");

            var assemblies = new List<string>();
            foreach (var file in exeFiles) assemblies.Add(file.FullName);
            foreach (var file in dllFiles) assemblies.Add(file.FullName);
            return assemblies;
        }

      
    }
}
