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

        private string assemblyFolder;
        private string assemblyParentFolder;
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

            //if (pdbReader != null)
            //{
            //    pdbReader.Dispose();
            //}
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
            this.assemblyFolder = Path.GetDirectoryName(fileName);
            this.assemblyParentFolder = Directory.GetParent(Path.GetDirectoryName(fileName)).FullName;
            cciHost.AddLibPath(assemblyFolder);
            cciHost.AddLibPath(assemblyParentFolder);
            this.mainAssemably = this.LoadAssembly(fileName);
            return this.mainAssemably;
        }


        //public IAssembly TryToLoadReferencedAssembly(IAssemblyReference reference)
        //{
        //    var assembly = this.ourHost.Assemblies.SingleOrDefault(a => a.MatchReference(reference));
        //    if (assembly == null && !failedAssemblies.Contains(reference))
        //    {
        //        try
        //        {
        //            AnalysisStats.TotalDllsFound++;
        //            assembly = TryToLoadAssembly(reference.Name.Value);
        //        }
        //        catch (Exception e)
        //        {
        //            System.Console.WriteLine("We could not solve this reference: {0}", reference.Name);
        //            failedAssemblies.Add(reference);
        //            throw e;
        //        }
        //    }
        //    return assembly;
        //}

        private Tuple<IAssembly, ISourceLocationProvider> TryToLoadAssembly(string assemblyReferenceName)
        {
            //if(assemblyReferenceName=="mscorlib")
            //{
            //    return LoadCoreAssembly();
            //}

            var extensions = new string[] { ".dll", ".exe" };
            var referencePath = "";
            foreach (var extension in extensions)
            {
                referencePath = Path.Combine(assemblyFolder, assemblyReferenceName) + extension;
                if (File.Exists(referencePath))
                    break;
                referencePath = Path.Combine(assemblyParentFolder, assemblyReferenceName) + extension;
                if (File.Exists(referencePath))
                    break;
            }
            //var cciAssemblyFromReference = cciHost.LoadUnitFrom(referencePath) as Cci.IModule;
            //// var cciAssemblyFromReference = cciHost.LoadUnit(assemblyReference.AssemblyIdentity) as Cci.IAssembly;
            //return cciAssemblyFromReference;
            return LoadAssembly(referencePath);
        }

        internal Tuple<IAssembly, ISourceLocationProvider> LoadScopeRuntime()
        {
            return TryToLoadAssembly("ScopeRuntime");
        }
        //public Assembly LoadAssemblyAndReferences(string fileName)
        //{
        //    var module = cciHost.LoadUnitFrom(fileName) as Cci.IModule;

        //    if (module == null || module == Cci.Dummy.Module || module == Cci.Dummy.Assembly)
        //        throw new Exception("The input is not a valid CLR module or assembly.");

        //    var pdbFileName = Path.ChangeExtension(fileName, "pdb");
        //    Cci.PdbReader pdbReader = null;

        //    if (File.Exists(pdbFileName))
        //    {
        //        using (var pdbStream = File.OpenRead(pdbFileName))
        //        {
        //            pdbReader = new Cci.PdbReader(pdbStream, cciHost);
        //        }
        //    }
        //    var assembly = this.ExtractAssembly(module, pdbReader);

        //    if (pdbReader != null)
        //    {
        //        pdbReader.Dispose();
        //    }

        //    ourHost.Assemblies.Add(assembly);
        //    this.assemblyFolder = Path.GetDirectoryName(fileName);
        //    this.assemblyParentFolder = Directory.GetParent(Path.GetDirectoryName(fileName)).FullName;
        //    cciHost.AddLibPath(assemblyFolder);
        //    cciHost.AddLibPath(assemblyParentFolder);

        //    foreach (var assemblyReference in module.AssemblyReferences)
        //    {
        //        try
        //        {
        //            Cci.IModule cciAssemblyFromReference = TryToLoadCCIAssembly(assemblyReference);

        //            if (cciAssemblyFromReference == null || cciAssemblyFromReference == Cci.Dummy.Assembly)
        //                throw new Exception("The input is not a valid CLR module or assembly.");

        //            var pdbLocation = cciAssemblyFromReference.DebugInformationLocation;
        //            if (File.Exists(pdbFileName))
        //            {
        //                using (var pdbStream = File.OpenRead(pdbFileName))
        //                {
        //                    pdbReader = new Cci.PdbReader(pdbStream, cciHost);
        //                }
        //            }
        //            var assemblyFromRef = this.ExtractAssembly(cciAssemblyFromReference, pdbReader);
        //            ourHost.Assemblies.Add(assemblyFromRef);
        //            if (pdbReader != null)
        //            {
        //                pdbReader.Dispose();
        //            }
        //        }
        //        catch (Exception e)
        //        {

        //        }
        //    }
        //    return assembly;

        //}


    }

    //public class MyHost : Host
    //{
    //    public MyLoader Loader { get; set; }

    //    public override ITypeDefinition ResolveReference(INamedTypeReference typeToResolve)
    //    {
    //        var resolvedType = base.ResolveReference(typeToResolve);
    //        if (resolvedType == null)
    //        {
    //            try
    //            {
    //                Loader.TryToLoadReferencedAssembly(typeToResolve.ContainingAssembly);
    //                resolvedType = base.ResolveReference(typeToResolve);
    //            }
    //            catch (Exception e)
    //            {
    //                AnalysisStats.DllThatFailedToLoad.Add(typeToResolve.ContainingAssembly.Name);
    //                AnalysisStats.TotalDllsFailedToLoad++;
    //                System.Diagnostics.Debug.WriteLine("Failed to load {0}: {1}", typeToResolve.ContainingAssembly.Name, e.Message);
    //            }

    //        }
    //        return resolvedType;
    //    }
    //    public override ITypeDefinitionMember ResolveReference(ITypeMemberReference member)
    //    {
    //        return base.ResolveReference(member);
    //    }

    //}
}
