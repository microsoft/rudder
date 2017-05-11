using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Cci;

namespace ScopeProgramAnalysis.Framework
{
    public class MyLoader : IDisposable
    {
        public static IPlatformType PlatformTypes; 

        private HashSet<IAssemblyReference> failedAssemblies;
        private Tuple<IAssembly, ISourceLocationProvider> mainAssemably;
        private MetadataReaderHost cciHost;
        private IDictionary<IAssembly, ISourceLocationProvider> sourceProviderForAssembly;
        private RuntimeTypeStruct runtimeTypes;
        public RuntimeTypeStruct RuntimeTypes {  get { return runtimeTypes; } }

        public class RuntimeTypeStruct
        {
            public ITypeDefinition rowType = null;
            public ITypeDefinition rowSetType = null;
            public ITypeDefinition reducerType = null;
            public ITypeDefinition processorType = null;
            public ITypeDefinition concurrentProcessor;
            public ITypeDefinition combinerType = null;
            public ITypeDefinition columnType = null;
            public ITypeDefinition schemaType = null;

            public RuntimeTypeStruct(IMetadataHost host, IAssembly scopeRuntime)
            {
                processorType = UnitHelper.FindType(host.NameTable, scopeRuntime, "ScopeRuntime.Processor");
                reducerType = UnitHelper.FindType(host.NameTable, scopeRuntime, "ScopeRuntime.Reducer");
                rowType = UnitHelper.FindType(host.NameTable, scopeRuntime, "ScopeRuntime.Row");
                rowSetType = UnitHelper.FindType(host.NameTable, scopeRuntime, "ScopeRuntime.RowSet");
                columnType = UnitHelper.FindType(host.NameTable, scopeRuntime, "ScopeRuntime.ColumnData");
                schemaType = UnitHelper.FindType(host.NameTable, scopeRuntime, "ScopeRuntime.Schema");
                combinerType = UnitHelper.FindType(host.NameTable, scopeRuntime, "ScopeRuntime.Combiner");
                concurrentProcessor = UnitHelper.FindType(host.NameTable, scopeRuntime, "ScopeRuntime.ConcurrentProcessor",3);

                if (reducerType == null || processorType == null || rowSetType == null ||
                    rowType == null || columnType == null || schemaType == null || combinerType == null || concurrentProcessor == null)
                    throw new InvalidOperationException(
                        String.Format("Could not load all necessary Scope types: Reducer:{0}\tProcessor:{1}\tRowSet:{2}\tRow:{3}\tColumn:{4}\tSchema:{5}\tCombiner:{6}\tConcurrentProcessor:{7}",
                            reducerType != null, processorType != null, rowSetType != null, rowType != null, columnType != null, schemaType != null, combinerType != null, concurrentProcessor != null));

            }
        }

        public IMetadataHost Host { get { return cciHost; }  }

        public MyLoader(MetadataReaderHost host, string directory) 
        {
            this.cciHost = host;
            sourceProviderForAssembly = new Dictionary<IAssembly, ISourceLocationProvider>();
            if (PlatformTypes == null)
                PlatformTypes = host.PlatformType;

            this.failedAssemblies = new HashSet<IAssemblyReference>();
            LoadCoreAssembly();

            LoadRuntimeTypes(directory);
        }

        public ISourceLocationProvider GetSourceLocationProvider(IAssembly assembly)
        {
            if(sourceProviderForAssembly.ContainsKey(assembly))
                return sourceProviderForAssembly[assembly];
            return null;
        }

        public void Dispose()
        {
            this.cciHost.Dispose();
            this.cciHost = null;
            GC.SuppressFinalize(this);
        }

        public IAssembly LoadCoreAssembly()
        {
            var module = cciHost.LoadUnit(cciHost.CoreAssemblySymbolicIdentity) as IModule;

            if (module == null || module == Dummy.Module || module == Dummy.Assembly)
                throw new AnalysisNetException("The input is not a valid CLR module or assembly.");
            sourceProviderForAssembly.Add(module.ContainingAssembly, null);
            return module.ContainingAssembly;
        }

        public Tuple<IAssembly,ISourceLocationProvider> LoadAssembly(string fileName)
        {
            var unit = cciHost.LoadedUnits.SingleOrDefault(u => u.Location == fileName);
            if(unit!=null)
            {
                return Tuple.Create(unit as IAssembly, sourceProviderForAssembly[unit as IAssembly]);
            }

            var module = cciHost.LoadUnitFrom(fileName) as IModule;

            if (module == null || module == Dummy.Module || module == Dummy.Assembly)
                throw new Exception(String.Format("The input '{0}' is not a valid CLR module or assembly.", fileName));

            var pdbFileName = Path.ChangeExtension(fileName, "pdb");
            PdbReader pdbReader = null;

            if (File.Exists(pdbFileName))
            {
                using (var pdbStream = File.OpenRead(pdbFileName))
                {
                    pdbReader = new PdbReader(pdbStream, cciHost);
                }
            }

            sourceProviderForAssembly.Add(module.ContainingAssembly, pdbReader);

            if (module.ContainingAssembly.NamespaceRoot.Members.Any(m => m.Name.Value == "ScopeRuntime"))
                ScopeTypes.InitializeScopeTypes(cciHost);


            return Tuple.Create(module.ContainingAssembly, pdbReader as ISourceLocationProvider);
        }

        public Tuple<IAssembly, ISourceLocationProvider> LoadMainAssembly(string fileName)
        {
            if (fileName.StartsWith("file:")) // Is there a better way to tell that it is a Uri?
            {
                fileName = new Uri(fileName).LocalPath;
            }
            var d = Path.GetFullPath(fileName);
            var assemblyFolder = Path.GetDirectoryName(d);
            var assemblyParentFolder = Directory.GetParent(assemblyFolder).FullName;
            cciHost.AddLibPath(assemblyFolder);
            cciHost.AddLibPath(assemblyParentFolder);
            this.mainAssemably = this.LoadAssembly(fileName);
            return this.mainAssemably;
        }

        private Tuple<IAssembly, ISourceLocationProvider> TryToLoadAssembly(string assemblyName, string directory)
        {
            var extensions = new string[] { ".dll", ".exe" };
            var path = "";
            bool found = false;
            var d = directory;
            var pathsTried = new List<string>();

            while (!found)
            {
                foreach (var extension in extensions)
                {
                    path = Path.Combine(d, assemblyName + extension);
                    pathsTried.Add(path);
                    if (File.Exists(path))
                    {
                        found = true;
                        break;
                    }
                }
                if (!found)
                {
                    d = Directory.GetParent(d).FullName;
                    if (!Directory.Exists(d))
                        break;
                }

            }
            if (found)
            {
                try
                {
                    return LoadAssembly(path);
                }
                catch (Exception e)
                {
                    var length = new System.IO.FileInfo(path).Length;
                    var msg = "Length: " + length + "|" +
                        "Paths: " + String.Join(",", pathsTried) + e.Message;
                    throw new Exception(msg);
                }
            }
            else
                return null;
        }

        internal Tuple<IAssembly, ISourceLocationProvider> LoadScopeRuntime(string path)
        {
            var thisAssembly = System.Reflection.Assembly.GetExecutingAssembly();
            var embeddedAssemblyStream = thisAssembly.GetManifestResourceStream("ScopeProgramAnalysis.scoperuntime.exe");
            if (!Directory.Exists("MyScopeRuntime"))
            {
                var d = Directory.CreateDirectory("MyScopeRuntime");
            }
            var path2 = "MyScopeRuntime\\ScopeRuntime.exe";
            if (!File.Exists(path2))
            {
                using (var fileStream = File.Create(path2))
                {
                    embeddedAssemblyStream.Seek(0, SeekOrigin.Begin);
                    embeddedAssemblyStream.CopyTo(fileStream);
                }
            }
            var t = LoadAssembly(path2);
            return t;

            //Tuple<IAssembly, ISourceLocationProvider> t;
            //t = TryToLoadAssembly("ScopeRuntime", path);
            //if (t == null)
            //{
            //    var currentDirectory = Directory.GetCurrentDirectory();
            //    t = TryToLoadAssembly("ScopeRuntime", currentDirectory);
            //}
            //if (t == null)
            //    throw new InvalidOperationException("Cannot find ScopeRuntime");
            //return t;
        }

        private void LoadRuntimeTypes(string directory)
        {
            var scopeRuntime = LoadScopeRuntime(directory).Item1;
            this.runtimeTypes = new RuntimeTypeStruct(this.Host, scopeRuntime);
        }
    }

}
