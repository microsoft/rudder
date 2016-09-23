using System.Linq;
using Backend;
using Backend.Utils;
using Microsoft.Cci;

namespace ScopeProgramAnalysis.Framework
{
    public static class MyTypesHelper
    {
        public static bool IsCompiledGeneratedClass(this INamedTypeDefinition typeAsClassResolved)
        {
            var result = typeAsClassResolved!=null && typeAsClassResolved.Attributes.Any(attrib => attrib.Type.ToString() == "CompilerGeneratedAttribute");
            return result;
        }

        public static bool IsCompilerGenerated(this ITypeReference type)
        {
            var resolvedClass = type.ResolvedType as INamedTypeDefinition;
            if (resolvedClass != null)
            {
                return resolvedClass.IsCompiledGeneratedClass();
            }
            return false;
        }

        public static bool IsCollection(this ITypeReference type)
        {
            var result = type.ResolvedType != null
                && TypeHelper.Type1ImplementsType2(type.ResolvedType, type.PlatformType.SystemCollectionsICollection);
            return result;
        }
        public static bool IsEnumerable(this ITypeReference type)
        {
            var namedType = type as INamedTypeReference;
            var result = namedType != null && namedType.Name.Value.Contains("Enumerable");
            return result;
        }
        public static bool IsIEnumerable(this ITypeReference type)
        {
            var result = type.ResolvedType != null 
            && TypeHelper.Type1ImplementsType2(type.ResolvedType, type.PlatformType.SystemCollectionsIEnumerable);
            // && type.Name.Contains("IEnumerable")
            return result;
        }
        public static bool IsIEnumerator(this ITypeReference type)
        {
            var namedType = type as INamedTypeReference;
            var result = namedType!=null && namedType.Name.Value.Contains("IEnumerator");
            return result;
        }
        public static bool IsEnumerator(this ITypeReference type)
        {
            var namedType = type as INamedTypeReference;
            var result = namedType != null && namedType.Name.Value.Contains("Enumerator");
            return result;
        }


        public static bool IsContainerMethod(this IMethodReference method)
        {
            var result = method.ContainingType!= null && Types.Instance.IsContainer(method.ContainingType);
            return result;
        }

        public static bool IsDictionary(this ITypeReference type)
        {
            var namedType = type as INamedTypeReference;
            var result = namedType != null && 
                (namedType.Name.Value.Contains("SortedDictionary")
                    || namedType.Name.Value.Contains("Dictionary")
                    || namedType.Name.Value.Contains("IDictionary"));

            return result;
        }

        public static bool IsSet(this ITypeReference type)
        {
            var namedType = type as INamedTypeReference;
            var result = namedType != null && namedType.Name.Value.Contains("Set");
            return result;
        }

        public static bool IsSubClass(this INamedTypeDefinition class1, ITypeReference class2)
        {
            
            var result = false;
            if (class1.Equals(class2))
                return true;
            if(class1.BaseClasses!=null && class1.BaseClasses.SingleOrDefault() is ITypeDefinition)
            {
                var baseClass = class1.BaseClasses.SingleOrDefault() as ITypeDefinition;
                if (TypeHelper.TypesAreEquivalent(baseClass, class2))
                    return true;
                else
                {
                    if(baseClass.ResolvedType!=null)
                        (baseClass.ResolvedType as INamedTypeDefinition).IsSubClass(class2);
                }
            }
            return result;
        }

        public  static bool IsRowSetType(this ITypeReference type)
        {
            var resolvedClass = type.ResolvedType as INamedTypeDefinition;
            if(resolvedClass!=null)
            {
                return resolvedClass.IsSubClass(ScopeTypes.RowSet);
            }
            return false;
        }
        public static bool IsRowType(this ITypeReference type)
        {
            var basicType = type as INamedTypeReference;
            if(basicType!=null)
            {
                var resolvedClass = basicType.ResolvedType as INamedTypeDefinition;
                if (resolvedClass != null)
                {
                    return resolvedClass.IsSubClass(ScopeTypes.Row);
                }
            }
            return false;
        }

        /// <summary>
        /// TODO: Hack: This is a type that Mike used
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        public static bool IsScopeMapUsage(this ITypeReference type)
        {
            var basicType = type as INamedTypeReference;
            return basicType != null && basicType.Name.Value == "ScopeMapUsage";
        }


        public static bool IsIEnumerableRow(this ITypeReference type)
        {
            var basicType = type as INamedTypeReference;
            if (basicType != null)
            {
                return basicType.Equals(ScopeTypes.IEnumerable_Row);
            }
            return false;
        }

        public static bool IsIEnumeratorRow(this ITypeReference type)
        {
            var basicType = type as INamedTypeReference;
            if (basicType != null)
            {
                if (basicType != null)
                {
                    return basicType.Equals(ScopeTypes.IEnumerator_Row);
                }
            }
            return false;
        }
        public static bool IsIEnumerableScopeMapUsage(this ITypeReference type)
        {
            var basicType = type as INamedTypeReference;
            if (basicType != null && basicType.Equals(basicType.PlatformType.SystemCollectionsGenericIEnumerable))
            {
                return basicType.FullName() == "System.Collections.IEnumerable<ScopeMapUsage>";
            }
            return false;
        }

        public static bool IsScopeRuntime(this ITypeReference type)
        {
            var basicType = type as INamedTypeReference;
            if (basicType != null)
            {
                return basicType.ContainingNamespace() == "ScopeRuntime";
            }
            return false;
        }

        public static bool IsIEnumeratorScopeMapUsage(this ITypeReference type)
        {
            return type.ToString().Contains("ScopeMapUsage");

            //var basicType = type as INamedTypeReference;
            //if (basicType != null)
            //{
            //    return basicType.GenericName == "IEnumerator<ScopeMapUsage>";
            //}
            //return false;
        }


        public static bool IsRowListType(this ITypeReference type)
        {
            var resolvedClass = type.ResolvedType as INamedTypeDefinition;
            if (resolvedClass != null)
            {
                return resolvedClass.IsSubClass(ScopeTypes.RowList);
            }
            return false;
        }


        public static bool IsColumnDataType(this ITypeReference type)
        {
            var resolvedClass = type.ResolvedType as INamedTypeDefinition;
            if (resolvedClass != null)
            {
                return resolvedClass.IsSubClass(ScopeTypes.ColumnData);
            }
            return false;
        }
        public static bool IsInteger(this ITypeReference type)
        {
            var basictype = (type as INamedTypeReference);
            if (basictype != null)
            {
                var resolvedClass = basictype.ResolvedType;
                if (resolvedClass != null)
                {
                    return TypeHelper.IsPrimitiveInteger(resolvedClass);
                } 
            }
            else
            { }
            return false;
        }

        public static bool TypeEquals(this ITypeReference type1, ITypeReference type2)
        {
            return TypeHelper.TypesAreEquivalent(type1, type2);
        }

        public static bool SameType(this INamedTypeReference containingType, ITypeDefinition iteratorClass)
        {
            return containingType.Equals(iteratorClass);
        }
        public static bool IsString(this ITypeReference containingType)
        {
            var namedType = containingType as INamedTypeReference;
            return namedType!=null &&  namedType.Name.Value == "String";
        }

        public static bool IsTuple(this ITypeReference containingType)
        {
            var namedType = containingType as INamedTypeReference;
            return namedType != null && namedType.Name.Value == "Tuple";
        }

        internal static bool IsValueType(this ITypeReference containingType)
        {
            return containingType.IsValueType;
        }
    }
}
