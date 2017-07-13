using Microsoft.Cci;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RuntimeLoader
{

    public static class ScopeTypes
    {
        private const string scopeNameSpace = "ScopeRuntime";
        private const string scopeAssemblyName = "ScopeRuntime"; // "CodeUnderTest"; 

        private static ICollection<INamedTypeReference> scopeTypes = new List<INamedTypeReference>();
        public static INamedTypeReference Producer;
        public static INamedTypeReference Processor;
        public static INamedTypeReference Reducer;
        public static INamedTypeReference Combiner;

        public static INamedTypeReference Row;
        public static INamedTypeReference RowSet;
        public static INamedTypeReference RowList;
        public static INamedTypeReference ScopeMap;

        public static INamedTypeReference ColumnData;
        public static INamedTypeReference ColumnData_Generic;

        public static INamedTypeReference Schema;

        /// <summary>
        /// This is "ugly" but I don't know how to query a type by name
        /// Inspired in Zvonimir code
        /// </summary>
        /// <param name="host"></param>
        public static void InitializeScopeTypes(IMetadataHost host)
        {
            var scopeAssembly = host.LoadedUnits.OfType<IModule>()
                .Single(module => module.ContainingAssembly.NamespaceRoot.Members.Any(m => m.Name.Value == "ScopeRuntime"));
            foreach (var type in scopeAssembly.GetAllTypes())
            {
                if (type.FullName() == "ScopeRuntime.Reducer") Reducer = type;
                else if (type.FullName() == "ScopeRuntime.Processor") Processor = type;
                else if (type.FullName() == "ScopeRuntime.Producer") Producer = type;
                else if (type.FullName() == "ScopeRuntime.Row")
                {
                    Row = type;
                }
                else if (type.FullName() == "ScopeRuntime.RowSet") RowSet = type;
                else if (type.FullName() == "ScopeRuntime.RowList") RowList = type;
                else if (type.FullName() == "ScopeRuntime.ColumnData") ColumnData = type;
                else if (type.FullName() == "ScopeRuntime.ColumnData<T>") ColumnData_Generic = type;
                else if (type.FullName() == "ScopeRuntime.Schema") Schema = type;
                else if (type.FullName() == "ScopeRuntime.Combiner") Combiner = type;
                else if (type.FullName() == "ScopeRuntime.ScopeMap<K, V>") ScopeMap = type;
                if (type.ContainingNamespace() == "ScopeRuntime")
                    scopeTypes.Add(type);
            }
        }
        public static bool Contains(ITypeReference type)
        {
            if (type is INamedTypeReference)
                return scopeTypes.Contains(((INamedTypeReference)type).ResolvedType);
            return false;
        }

        public static string FullName(this ITypeReference tref)
        {
            return TypeHelper.GetTypeName(tref, NameFormattingOptions.Signature | NameFormattingOptions.TypeParameters);
        }

        public static string ContainingNamespace(this ITypeReference type)
        {
            if (type is INamedTypeDefinition)
                return TypeHelper.GetDefiningNamespace((INamedTypeDefinition)type).ToString();
            return String.Empty;
        }


    }

    public class RuntimeLoader
    {
        private static IAssembly cachedScopeRuntime = null;
        private static IDictionary<IAssembly, ISourceLocationProvider> sourceProviderForAssembly = new Dictionary<IAssembly, ISourceLocationProvider>();

        public static IAssembly LoadScopeRuntime(IMetadataHost host)
        {
            if (cachedScopeRuntime == null)
            {
                var thisAssembly = System.Reflection.Assembly.GetExecutingAssembly();
                var embeddedAssemblyStream = thisAssembly.GetManifestResourceStream("RuntimeLoader.scoperuntime.exe");
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
                var t = LoadAssembly(host, path2);
                var scopeRuntime = t.Item1;

                ScopeTypes.InitializeScopeTypes(host);
                cachedScopeRuntime = scopeRuntime;
            }

            return cachedScopeRuntime;
        }
        public static Tuple<IAssembly, ISourceLocationProvider> LoadAssembly(IMetadataHost host, string fileName)
        {
            var unit = host.LoadedUnits.SingleOrDefault(u => u.Location == fileName);
            if (unit != null)
            {
                return Tuple.Create(unit as IAssembly, sourceProviderForAssembly[unit as IAssembly]);
            }

            var module = host.LoadUnitFrom(fileName) as IModule;

            if (module == null || module == Dummy.Module || module == Dummy.Assembly)
                throw new Exception(String.Format("The input '{0}' is not a valid CLR module or assembly.", fileName));

            var pdbFileName = Path.ChangeExtension(fileName, "pdb");
            PdbReader pdbReader = null;

            if (File.Exists(pdbFileName))
            {
                using (var pdbStream = File.OpenRead(pdbFileName))
                {
                    pdbReader = new PdbReader(pdbStream, host);
                }
            }

            sourceProviderForAssembly.Add(module.ContainingAssembly, pdbReader);

            return Tuple.Create(module.ContainingAssembly, pdbReader as ISourceLocationProvider);
        }


    }
}
