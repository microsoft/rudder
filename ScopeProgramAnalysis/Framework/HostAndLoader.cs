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

        public IMetadataHost Host { get { return cciHost; }  }

        public MyLoader(MetadataReaderHost host) 
        {
            this.cciHost = host;
            sourceProviderForAssembly = new Dictionary<IAssembly, ISourceLocationProvider>();
            if (PlatformTypes == null)
                PlatformTypes = host.PlatformType;

            this.failedAssemblies = new HashSet<IAssemblyReference>();
        }
        public MyLoader()
        {
            this.cciHost = new PeReader.DefaultHost();
            this.failedAssemblies = new HashSet<IAssemblyReference>();
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
                throw new Exception("The input is not a valid CLR module or assembly.");

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

            while (!found)
            {
                foreach (var extension in extensions)
                {
                    path = Path.Combine(d, assemblyName + extension);
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
                return LoadAssembly(path);
            else
                return null;
        }

        internal Tuple<IAssembly, ISourceLocationProvider> LoadScopeRuntime(string path)
        {
            Tuple<IAssembly, ISourceLocationProvider> t;
            t = TryToLoadAssembly("ScopeRuntime", path);
            if (t == null)
            {
                var currentDirectory = Directory.GetCurrentDirectory();
                t = TryToLoadAssembly("ScopeRuntime", currentDirectory);
            }
            if (t == null)
                throw new InvalidOperationException("Cannot find ScopeRuntime");
            return t;
        }

    }

}
