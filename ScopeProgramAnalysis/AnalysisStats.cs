using Backend.Utils;
using Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Globalization;
using Model.Types;

namespace ScopeProgramAnalysis
{
    public struct AnalysisReason
    {
        public AnalysisReason(string methodName, IInstruction ins, string reason)
        {
            this.MethodName = methodName;
            this.Instruction = ins;
            this.Reason = reason;
        }
        public AnalysisReason(MethodDefinition methodDef, IInstruction ins, string reason)
        {
            this.MethodName = methodDef.ToSignatureString();
            this.Instruction = ins;
            this.Reason = reason;
        }

        public string MethodName { get; set; }
        public IInstruction Instruction { get; set; }
        public string Reason { get; set; }
        public override string ToString()
        {
            return String.Format(CultureInfo.InvariantCulture,"[{0}:{1}] = {2}", MethodName, Instruction.ToString(), Reason);
        }

    }
    public class AnalysisStats
    {
        public static int TotalNumberFolders { get; set; }
        public static int TotalDllsFound { get; set; }
        public static int TotalDllsFailedToLoad{ get; set; }

        public static int TotalMethods { get; set; }
        public static int TotalProducers { get; set; }

        public static int TotalofFrameworkErrors { get; set; }
        public static int TotalofPTAErrors{ get; set; }
        public static int TotalofDepAnalysisErrors { get; set; }

        public static string CurrentScript = "NoSet";

        //public static Dictionary<string, int> DllThatFailedToLoad = new Dictionary<string, int>();
        public static ISet<string> DllThatFailedToLoad = new HashSet<string> ();

        public static MapSet<string, AnalysisReason> AnalysisReasons = new MapSet<string, AnalysisReason>();
        public static void AddAnalysisReason(AnalysisReason reason)
        {
            AnalysisReasons.Add(CurrentScript, reason);
        }

        public static void  PrintStats(System.IO.TextWriter output)
        {
            output.WriteLine();
            output.WriteLine("------------------Analysis Stats--------------------------------------");
            output.WriteLine("Folders: {0}", TotalNumberFolders);
            output.WriteLine("Dlls Found: {0}", TotalDllsFound);
            output.WriteLine("Dlls Fail to Load: {0}", TotalDllsFailedToLoad);
            output.WriteLine("Showing the first 10...: {0}", String.Join(", ", DllThatFailedToLoad.Take(Math.Min(10,DllThatFailedToLoad.Count()))));
            output.WriteLine("Total Methods: {0}", TotalMethods);
            //output.WriteLine("Total Producers: {0}", TotalProducers);
            output.WriteLine("Total Depencency Analysis errors: {0}", TotalofDepAnalysisErrors);
            output.WriteLine("Total PTA errors: {0}", TotalofPTAErrors);
            output.WriteLine("Total Framework errors: {0}", TotalofFrameworkErrors);
            output.WriteLine("---------------End Analysis Stats-------------------------------------");
            output.WriteLine();

        }

        public static void WriteAnalysisReasons(TextWriter output)
        {
            foreach(var entry in AnalysisReasons)
            {
                output.WriteLine("Analysis reasons for: {0}", entry.Key);
                foreach(var reason in entry.Value)
                {
                    output.WriteLine(reason);

                    //output.WriteLine("{0}:{1} - {2}", reason.Instruction, reason.MethodName, reason.Reason);
                }
            }
        }
    }
}
