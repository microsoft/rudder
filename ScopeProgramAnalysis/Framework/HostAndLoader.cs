using CCIProvider;
using Model;
using Model.Types;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ScopeProgramAnalysis.Framework
{
    public class MyLoader : Loader
    {
        private string assemblyFolder;
        private string assemblyParentFolder;
        private HashSet<IAssemblyReference> failedAssemblies;
        private Assembly mainAssemably;

        public MyLoader(Host host) : base(host)
        {
            this.failedAssemblies = new HashSet<IAssemblyReference>();
        }
        public Assembly LoadMainAssembly(string fileName)
        {
            this.assemblyFolder = Path.GetDirectoryName(fileName);
            this.assemblyParentFolder = Directory.GetParent(Path.GetDirectoryName(fileName)).FullName;
            cciHost.AddLibPath(assemblyFolder);
            cciHost.AddLibPath(assemblyParentFolder);
            this.mainAssemably = base.LoadAssembly(fileName);
            return this.mainAssemably;
        }


        public Assembly TryToLoadReferencedAssembly(IAssemblyReference reference)
        {
            var assembly = this.ourHost.Assemblies.SingleOrDefault(a => a.MatchReference(reference));
            if (assembly == null && !failedAssemblies.Contains(reference))
            {
                try
                {
                    AnalysisStats.TotalDllsFound++;
                    assembly = TryToLoadAssembly(reference.Name);
                }
                catch (Exception e)
                {
                    System.Console.WriteLine("We could not solve this reference: {0}", reference.Name);
                    failedAssemblies.Add(reference);
                    throw e;
                }
            }
            return assembly;
        }

        private Assembly TryToLoadAssembly(string assemblyReferenceName)
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

    public class MyHost : Host
    {
        public MyLoader Loader { get; set; }

        public override ITypeDefinition ResolveReference(IBasicType typeToResolve)
        {
            var resolvedType = base.ResolveReference(typeToResolve);
            if (resolvedType == null)
            {
                try
                {
                    Loader.TryToLoadReferencedAssembly(typeToResolve.ContainingAssembly);
                    resolvedType = base.ResolveReference(typeToResolve);
                }
                catch (Exception e)
                {
                    AnalysisStats.DllThatFailedToLoad.Add(typeToResolve.ContainingAssembly.Name);
                    AnalysisStats.TotalDllsFailedToLoad++;
                    System.Diagnostics.Debug.WriteLine("Failed to load {0}: {1}", typeToResolve.ContainingAssembly.Name, e.Message);
                }

            }
            return resolvedType;
        }
        public override ITypeMemberDefinition ResolveReference(ITypeMemberReference member)
        {
            return base.ResolveReference(member);
        }

    }
}
