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
        public static void InitializeScopeTypes(IMetadataHost host, IAssembly scopeRuntime)
        {
            foreach (var type in scopeRuntime.GetAllTypes())
            {
                if (type.GetFullName() == "ScopeRuntime.Reducer") Reducer = type;
                else if (type.GetFullName() == "ScopeRuntime.Processor") Processor = type;
                else if (type.GetFullName() == "ScopeRuntime.Producer") Producer = type;
                else if (type.GetFullName() == "ScopeRuntime.Row")
                {
                    Row = type;
                }
                else if (type.GetFullName() == "ScopeRuntime.RowSet") RowSet = type;
                else if (type.GetFullName() == "ScopeRuntime.RowList") RowList = type;
                else if (type.GetFullName() == "ScopeRuntime.ColumnData") ColumnData = type;
                else if (type.GetFullName() == "ScopeRuntime.ColumnData<T>") ColumnData_Generic = type;
                else if (type.GetFullName() == "ScopeRuntime.Schema") Schema = type;
                else if (type.GetFullName() == "ScopeRuntime.Combiner") Combiner = type;
                else if (type.GetFullName() == "ScopeRuntime.ScopeMap<K, V>") ScopeMap = type;
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

        public static string GetFullName(this ITypeReference tref)
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
        private IMetadataHost host;
        private IAssembly cachedScopeRuntime = null;

        public RuntimeLoader(IMetadataHost host)
        {
            this.host = host;
        }

        public IAssembly LoadScopeRuntime()
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
                var t = LoadAssembly(path2);
                var scopeRuntime = t;

                ScopeTypes.InitializeScopeTypes(host, scopeRuntime);
                cachedScopeRuntime = scopeRuntime;
            }

            return cachedScopeRuntime;
        }
        public IAssembly LoadAssembly(string fileName)
        {
            var unit = host.LoadedUnits.SingleOrDefault(u => Path.GetFileName(u.Location) == fileName);
            if (unit != null)
            {
                return (IAssembly) unit;
            }

            var module = host.LoadUnitFrom(fileName) as IModule;

            if (module == null || module == Dummy.Module || module == Dummy.Assembly)
                throw new Exception(String.Format("The input '{0}' is not a valid CLR module or assembly.", fileName));

            return module.ContainingAssembly;
        }


    }
}
