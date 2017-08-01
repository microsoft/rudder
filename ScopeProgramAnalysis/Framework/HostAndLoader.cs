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
        private IAssembly mainAssemably;
        private MetadataReaderHost cciHost;
        private RuntimeLoader.RuntimeLoader myRuntimeLoader;
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
            this.myRuntimeLoader = new RuntimeLoader.RuntimeLoader(host);
            sourceProviderForAssembly = new Dictionary<IAssembly, ISourceLocationProvider>();
            if (PlatformTypes == null)
                PlatformTypes = host.PlatformType;

            this.failedAssemblies = new HashSet<IAssemblyReference>();
            //LoadCoreAssembly();

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

        public IAssembly LoadMainAssembly(string fileName)
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
            this.mainAssemably = myRuntimeLoader.LoadAssembly(fileName);
            return this.mainAssemably;
        }

        private IAssembly TryToLoadAssembly(string assemblyName, string directory)
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
                    return myRuntimeLoader.LoadAssembly(path);
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

        private void LoadRuntimeTypes(string directory)
        {
            var scopeRuntime = myRuntimeLoader.LoadScopeRuntime();
            this.runtimeTypes = new RuntimeTypeStruct(this.Host, scopeRuntime);
        }
    }

}
