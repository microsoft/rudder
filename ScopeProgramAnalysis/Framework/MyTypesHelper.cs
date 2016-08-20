using Model.Types;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ScopeProgramAnalysis.Framework
{
    public static class MyTypesHelper
    {
        public static bool IsCollectionMethod(this IMethodReference method)
        {
            var result = method.ContainingType.ResolvedType != null
                && TypeHelper.Type1ImplementsType2(method.ContainingType.ResolvedType, PlatformTypes.ICollection);
            return result;
        }
        public static bool IsEnumerableMethod(this IMethodReference method)
        {
            var result = method.ContainingType.ResolvedType != null
                && method.ContainingType.Name.Contains("Enumerable");
            return result;
        }
        public static bool IsContainerMethod(this IMethodReference method)
        {
            var result = method.ContainingType!= null && TypeHelper.IsContainer(method.ContainingType);
            return result;
        }
        public static bool IsSubClass(this ClassDefinition class1, IType class2)
        {
            var result = false;
            if (class1.Equals(class2))
                return true;
            if(class1.Base!=null && class1 is ITypeDefinition)
            {
                var baseClass = class1.Base as ITypeDefinition;
                if (TypeHelper.TypesAreEquivalent(baseClass, class2))
                    return true;
                else
                {
                    if(baseClass.ResolvedType!=null)
                        (baseClass.ResolvedType as ClassDefinition).IsSubClass(class2);
                }
            }
            return result;
        }

        public  static bool IsRowSetType(this IBasicType type)
        {
            var resolvedClass = type.ResolvedType as ClassDefinition;
            if(resolvedClass!=null)
            {
                return resolvedClass.IsSubClass(ScopeTypes.RowSet);
            }
            return false;
        }
        public static bool IsRowType(this IBasicType type)
        {
            var resolvedClass = type.ResolvedType as ClassDefinition;
            if (resolvedClass != null)
            {
                return resolvedClass.IsSubClass(ScopeTypes.Row);
            }
            return false;
        }

        public static bool IsRowListType(this IBasicType type)
        {
            var resolvedClass = type.ResolvedType as ClassDefinition;
            if (resolvedClass != null)
            {
                return resolvedClass.IsSubClass(ScopeTypes.RowList);
            }
            return false;
        }


        public static bool IsColumnDataType(this IBasicType type)
        {
            var resolvedClass = type.ResolvedType as ClassDefinition;
            if (resolvedClass != null)
            {
                return resolvedClass.IsSubClass(ScopeTypes.ColumnData);
            }
            return false;
        }
        public static bool IsInteger(this IType type)
        {
            var resolvedClass = (type as IBasicType).ResolvedType as ITypeDefinition;
            if (resolvedClass != null)
            {
                return resolvedClass.TypeEquals(PlatformTypes.Int32)
                    || resolvedClass.TypeEquals(PlatformTypes.Int16)
                    || resolvedClass.TypeEquals(PlatformTypes.Int64);
            }
            return false;
        }

        public static bool TypeEquals(this IType type1, IType type2)
        {
            return Model.Types.TypeHelper.TypesAreEquivalent(type1, type2);
        }

    }
}
