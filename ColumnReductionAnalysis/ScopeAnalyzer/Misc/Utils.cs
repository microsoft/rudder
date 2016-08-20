using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ScopeAnalyzer
{
    /// <summary>
    /// Class that provides varisous helper functionalities, such as printing to a file,
    /// getting all dll files from a folder, and so on.
    /// </summary>
    public class Utils
    {
        public const string MAIN_DLL_NAME = "__ScopeCodeGen__.dll";
        public const string PROCESSOR_ID_MAPPING_NAME = "__processoridmapping__";
        public const string VERTEX_DEF_NAME = "ScopeVertexDef.xml";


        public static StreamWriter Output;

        public static void WriteLine(string message)
        {
            Console.WriteLine(message);
            if (Output != null && Output != Console.Out)
            {
                Output.WriteLine(message);
                Output.Flush();
            }
        }

        public static void SetOutput(string path)
        {
            var fs = new FileStream(path, FileMode.Create);
            Output = new StreamWriter(fs);
        }

        public static void OutputClose()
        {
            if (Output == null) return;
            Output.Close();
        }

        public static List<string> CollectAssemblies(string path)
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


        public static string[] GetSubDirectoriesPaths(string dir)
        {
            return Directory.GetDirectories(dir);
        }
    }
}
