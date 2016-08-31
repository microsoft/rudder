using Model;
using Model.Types;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ScopeProgramAnalysis
{
    public static class ScopeTypes
    {
        private const string scopeNameSpace = "ScopeRuntime";
        private const string scopeAssembly = "CodeUnderTest"; //"ScopeRuntime";

        private static readonly ICollection<BasicType> scopeTypes = new List<BasicType>();
        public static readonly BasicType Producer = New(scopeAssembly, scopeNameSpace, "Producer", TypeKind.ReferenceType);
        public static readonly BasicType Reducer = New(scopeAssembly, scopeNameSpace, "Reducer", TypeKind.ReferenceType);

        public static readonly BasicType Row = New(scopeAssembly, scopeNameSpace, "Row", TypeKind.ReferenceType);
        public static readonly BasicType RowSet = New(scopeAssembly, scopeNameSpace, "RowSet", TypeKind.ReferenceType);
        public static readonly BasicType RowList = New(scopeAssembly, scopeNameSpace, "RowList", TypeKind.ReferenceType);
        public static readonly BasicType IEnumerable_Row = New(scopeAssembly, scopeNameSpace, "IEnumerable", TypeKind.ReferenceType, "Row");
        public static readonly BasicType IEnumerator_Row = New(scopeAssembly, scopeNameSpace, "IEnumerator", TypeKind.ReferenceType, "Row");
     
        public static readonly BasicType ColumnData = New(scopeAssembly, scopeNameSpace, "ColumnData", TypeKind.ReferenceType);
        public static readonly BasicType ColumnData_Generic = New(scopeAssembly, scopeNameSpace, "ColumnData", TypeKind.ReferenceType, "T");

        public static void Resolve(Host host)
        {
            foreach (var type in scopeTypes)
            {
                type.Resolve(host);
            }
        }

        private static BasicType New(string containingAssembly, string containingNamespace, string name, TypeKind kind, params string[] genericArguments)
        {
            var result = new BasicType(name, kind)
            {
                ContainingAssembly = new AssemblyReference(containingAssembly),
                ContainingNamespace = containingNamespace
            };

            foreach (var arg in genericArguments)
            {
                var typevar = new TypeVariable(arg);
                result.GenericArguments.Add(typevar);
            }

            scopeTypes.Add(result);
            return result;
        }

    }
}
