using System.Linq;
using Backend;
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

        public static bool IsCompilerGenerated(this INamedTypeReference type)
        {
            var resolvedClass = type.ResolvedType as INamedTypeDefinition;
            if (resolvedClass != null)
            {
                return resolvedClass.IsCompiledGeneratedClass();
            }
            return false;
        }

        public static bool IsCollection(this INamedTypeReference type)
        {
            var result = type.ResolvedType != null
                && TypeHelper.Type1ImplementsType2(type.ResolvedType, type.PlatformType.SystemCollectionsICollection);
            return result;
        }
        public static bool IsEnumerable(this INamedTypeReference type)
        {
            var result = /*type.ResolvedType != null &&*/ type.Name.Value.Contains("Enumerable");
            return result;
        }
        public static bool IsIEnumerable(this INamedTypeReference type)
        {
            var result = type.ResolvedType != null 
            && TypeHelper.Type1ImplementsType2(type.ResolvedType, type.PlatformType.SystemCollectionsIEnumerable);
            // && type.Name.Contains("IEnumerable")
            return result;
        }
        public static bool IsIEnumerator(this INamedTypeReference type)
        {
            var result = /*type.ResolvedType != null &&*/ type.Name.Value.Contains("IEnumerator");
            return result;
        }
        public static bool IsEnumerator(this INamedTypeReference type)
        {
            var result = /*type.ResolvedType != null &&*/ type.Name.Value.Contains("Enumerator");
            return result;
        }


        public static bool IsContainerMethod(this IMethodReference method)
        {
            var result = method.ContainingType!= null && Types.Instance.IsContainer(method.ContainingType);
            return result;
        }

        public static bool IsDictionary(this INamedTypeReference type)
        {
            var result = type != null
                && (type.Name.Value.Contains("SortedDictionary")
                    || type.Name.Value.Contains("Dictionary")
                    || type.Name.Value.Contains("IDictionary"));

            return result;
        }

        public static bool IsSet(this INamedTypeReference type)
        {
            var result = type != null
                && (type.Name.Value.Contains("Set"));
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

        public  static bool IsRowSetType(this INamedTypeReference type)
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
                if (basicType.GenericArguments != null && basicType.GenericArguments.Count == 1)
                {
                    return basicType.GenericArguments[0].IsRowType() && basicType.IsIEnumerator();
                }
            }
            return false;
        }
        public static bool IsIEnumerableScopeMapUsage(this ITypeReference type)
        {
            var basicType = type as INamedTypeReference;
            if (basicType != null)
            {
                return basicType.GenericName == "IEnumerable<ScopeMapUsage>";
            }
            return false;
        }

        public static bool IsScopeRuntime(this ITypeReference type)
        {
            var basicType = type as INamedTypeReference;
            if (basicType != null)
            {
                return basicType.ContainingNamespace == "ScopeRuntime";
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


        public static bool IsRowListType(this INamedTypeReference type)
        {
            var resolvedClass = type.ResolvedType as INamedTypeDefinition;
            if (resolvedClass != null)
            {
                return resolvedClass.IsSubClass(ScopeTypes.RowList);
            }
            return false;
        }


        public static bool IsColumnDataType(this INamedTypeReference type)
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
                var resolvedClass = basictype.ResolvedType as ITypeReferenceDefinition;
                if (resolvedClass != null)
                {
                    return resolvedClass.TypeEquals(PlatformTypes.Int32)
                        || resolvedClass.TypeEquals(PlatformTypes.Int16)
                        || resolvedClass.TypeEquals(PlatformTypes.Int64);
                }
            }
            else
            { }
            return false;
        }

        public static bool TypeEquals(this ITypeReference type1, ITypeReference type2)
        {
            return Model.Types.TypeHelper.TypesAreEquivalent(type1, type2);
        }

        public static bool SameType(this INamedTypeReference containingType, ITypeDefinition iteratorClass)
        {
            return containingType.Equals(iteratorClass);
        }
        public static bool IsString(this INamedTypeReference containingType)
        {
            return containingType.Name.Value == "String";
        }

        public static bool IsTuple(this INamedTypeReference containingType)
        {
            return containingType.Name.Value == "Tuple";
        }

        internal static bool IsValueType(this INamedTypeReference containingType)
        {
            return containingType.IsValueType;
        }
    }
}
