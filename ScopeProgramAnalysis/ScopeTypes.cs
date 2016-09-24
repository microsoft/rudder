using System.Collections.Generic;
using Microsoft.Cci;
using System.Linq;
using Backend.Utils;
using ScopeProgramAnalysis.Framework;

namespace ScopeProgramAnalysis
{
    public static class ScopeTypes
    {
        private const string scopeNameSpace = "ScopeRuntime";
        private const string scopeAssemblyName = "ScopeRuntime"; // "CodeUnderTest"; 

        private static ICollection<INamedTypeReference> scopeTypes = new List<INamedTypeReference>();
        public static  INamedTypeReference Producer;
        public static INamedTypeReference Processor;
        public static  INamedTypeReference Reducer;
        public static INamedTypeReference Combiner;

        public static  INamedTypeReference Row;
        public static  INamedTypeReference RowSet;
        public static  INamedTypeReference RowList;
        public static  INamedTypeReference IEnumerable_Row;
        public static  INamedTypeReference IEnumerator_Row;

        public static  INamedTypeReference ColumnData;
        public static INamedTypeReference ColumnData_Generic;

        public static INamedTypeReference Schema;

        /// <summary>
        /// This is "ugly" but I don't know how to query a type by name
        /// </summary>
        /// <param name="host"></param>
        public static void InitializeScopeTypes(IMetadataHost host)
        {
            var scopeAssembly = host.LoadedUnits.Single(u => u.Name.Value == scopeAssemblyName) as IAssembly;
            foreach (var type in scopeAssembly.GetAllTypes())
            {
                if (type.FullName() == "ScopeRuntime.Reducer") Reducer = type;
                else if (type.FullName() == "ScopeRuntime.Processor") Processor = type;
                else if (type.FullName() == "ScopeRuntime.Producer") Producer = type;
                else if (type.FullName() == "ScopeRuntime.Row")
                {
                    Row = type;
                    IEnumerable_Row = TypeHelper.SpecializeTypeReference(type, host.PlatformType.SystemCollectionsGenericIEnumerable, host.InternFactory) as INamedTypeReference;
                    IEnumerator_Row = TypeHelper.SpecializeTypeReference(type, host.PlatformType.SystemCollectionsGenericIEnumerator, host.InternFactory) as INamedTypeReference;

                }
                else if (type.FullName() == "ScopeRuntime.RowSet") RowSet = type;
                else if (type.FullName() == "ScopeRuntime.RowList") RowList = type;
                else if (type.FullName() == "ScopeRuntime.ColumnData") ColumnData = type;
                else if (type.FullName() == "ScopeRuntime.ColumnData<T>") ColumnData_Generic = type;
                else if (type.FullName() == "ScopeRuntime.Schema") Schema = type;
                else if (type.FullName() == "ScopeRuntime.Combiner") Combiner = type;
                if(type.ContainingNamespace() == "ScopeRuntime")
                    scopeTypes.Add(type);
            }
        }
        public static bool Contains(ITypeReference type)
        {
            if(type is INamedTypeReference)
                return scopeTypes.Contains(((INamedTypeReference)type).ResolvedType);
            return false;
        }

    }
}
