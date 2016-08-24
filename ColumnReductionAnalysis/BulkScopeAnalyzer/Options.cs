using System;
using System.IO;

namespace BulkScopeAnalyzer
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
        const string OUTPUT_DIR = "result-output";
        const string TRACE_PATH = "bulk-trace.txt";

        public string ScopeAnalyzerPath { get; }
        public string ScopeDirPath { get; }
        public string OutputPath { get; }
        public string TracePath  { get; }
        public string LibPath { get; }
        public bool Verbose { get; }

        public bool AskingForHelp { get; }


        private Options(string scDirPath, string sAnalyzer, string outPath, string trcPath, string lPath, bool verbose, bool askhelp)
        {
            ScopeDirPath = scDirPath;
            ScopeAnalyzerPath = sAnalyzer;

            if (outPath == null)
            {
                // Delete all files in the default output folder.
                OutputPath = Path.GetFullPath(OUTPUT_DIR);
                System.IO.DirectoryInfo di = new DirectoryInfo(OutputPath);
                foreach (FileInfo file in di.GetFiles())
                {
                    file.Delete();
                }
            } else
            {
                OutputPath = outPath;
            }

            if (trcPath == null)
            {
                TracePath = Path.GetFullPath(TRACE_PATH);
            }
            else
            {
                TracePath = trcPath;
            }

            if (lPath == null)
            {
                LibPath = ScopeDirPath;
            } else
            {
                LibPath = lPath;
            }

            Verbose = verbose;
            AskingForHelp = askhelp;
        }

        public static Options ParseCommandLineArguments(string[] args)
        {
            string scDirPath = null, sAnalyzer = null, outPath = null, trcPath = null, libPath = null;
            bool verbose = false;
            bool askhelp = false;
            foreach(var arg in args)
            {
                if (arg.StartsWith("/projects:")) scDirPath = arg.Split(new string[] { "/projects:" }, StringSplitOptions.None)[1];
                else if (arg.StartsWith("/libs:")) libPath = arg.Split(new string[] { "/libs:" }, StringSplitOptions.None)[1];
                else if (arg.StartsWith("/outputDir:")) outPath = arg.Split(new string[] { "/outputDir:" }, StringSplitOptions.None)[1];
                else if (arg.StartsWith("/trace:")) trcPath = arg.Split(new string[] { "/trace:" }, StringSplitOptions.None)[1];
                else if (arg.StartsWith("/scopeAnalyzer:")) sAnalyzer = arg.Split(new string[] { "/scopeAnalyzer:" }, StringSplitOptions.None)[1];
                else if (arg.Trim() == "/verbose") verbose = true;
                else if (arg.Trim() == "/help") askhelp = true;
            }

            if (scDirPath == null)
                throw new ParsingOptionsException("Projects folder path must be given!");
            else
                scDirPath = Path.GetFullPath(scDirPath);

            if (sAnalyzer == null)
                throw new ParsingOptionsException("ScopeAnalyzer path must be given!");
            else
                sAnalyzer = Path.GetFullPath(sAnalyzer);

            if (outPath != null)
                outPath = Path.GetFullPath(outPath);

            if (trcPath != null)
                trcPath = Path.GetFullPath(trcPath);

            if (libPath != null)
                libPath = Path.GetFullPath(libPath);

            return new Options(scDirPath, sAnalyzer, outPath, trcPath, libPath, verbose, askhelp);
        }  
    }
}
