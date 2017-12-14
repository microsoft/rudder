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
        public static bool IsCompiledGeneratedClass(this ClassDefinition typeAsClassResolved)
        {
            var result = typeAsClassResolved!=null && typeAsClassResolved.Attributes.Any(attrib => attrib.Type.ToString() == "CompilerGeneratedAttribute");
            return result;
        }

        public static bool IsCompilerGenerated(this IBasicType type)
        {
            var resolvedClass = type.ResolvedType as ClassDefinition;
            if (resolvedClass != null)
            {
                return resolvedClass.IsCompiledGeneratedClass();
            }
            return false;
        }

        public static bool IsCollection(this IBasicType type)
        {
            var result = type.ResolvedType != null
                && TypeHelper.Type1ImplementsType2(type.ResolvedType, PlatformTypes.ICollection);
            return result;
        }
        public static bool IsEnumerable(this IBasicType type)
        {
            var result = /*type.ResolvedType != null &&*/ type.Name.Contains("Enumerable");
            return result;
        }
        public static bool IsIEnumerable(this IBasicType type)
        {
            var result = type.ResolvedType != null 
            && TypeHelper.Type1ImplementsType2(type.ResolvedType, PlatformTypes.IEnumerable);
            // && type.Name.Contains("IEnumerable")
            return result;
        }
        public static bool IsIEnumerator(this IBasicType type)
        {
            var result = /*type.ResolvedType != null &&*/ type.Name.Contains("IEnumerator");
            return result;
        }
        public static bool IsEnumerator(this IBasicType type)
        {
            var result = /*type.ResolvedType != null &&*/ type.Name.Contains("Enumerator");
            return result;
        }


        public static bool IsContainerMethod(this IMethodReference method)
        {
            var result = method.ContainingType!= null && TypeHelper.IsContainer(method.ContainingType);
            return result;
        }

        public static bool IsDictionary(this IBasicType type)
        {
            var result = type != null
                && (type.Name.Contains("SortedDictionary")
                    || type.Name.Contains("Dictionary")
                    || type.Name.Contains("IDictionary"));

            return result;
        }

        public static bool IsSet(this IBasicType type)
        {
            var result = type != null
                && (type.Name.Contains("Set"));
            return result;
        }

        public static bool IsSubClass(this ClassDefinition class1, IType class2)
        {
            var result = false;
            if (class1.Equals(class2))
                return true;
            if(class1.Base!=null && class1.Base is ITypeDefinition)
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
        public static bool IsRowType(this IType type)
        {
            var basicType = type as IBasicType;
            if(basicType!=null)
            {
                var resolvedClass = basicType.ResolvedType as ClassDefinition;
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
        public static bool IsScopeMapUsage(this IType type)
        {
            var basicType = type as IBasicType;
            return basicType != null && basicType.Name == "ScopeMapUsage";
        }


        public static bool IsIEnumerableRow(this IType type)
        {
            var basicType = type as IBasicType;
            if (basicType != null)
            {
                if (basicType.GenericArguments != null && basicType.GenericArguments.Count == 1)
                {
                    return basicType.GenericArguments[0].IsRowType() && basicType.IsIEnumerable();
                }
            }
            return false;
        }

        public static bool IsIEnumeratorRow(this IType type)
        {
            var basicType = type as IBasicType;
            if (basicType != null)
            {
                if (basicType.GenericArguments != null && basicType.GenericArguments.Count == 1)
                {
                    return basicType.GenericArguments[0].IsRowType() && basicType.IsIEnumerator();
                }
            }
            return false;
        }
        public static bool IsIEnumerableScopeMapUsage(this IType type)
        {
            var basicType = type as IBasicType;
            if (basicType != null)
            {
                return basicType.GenericName == "IEnumerable<ScopeMapUsage>";
            }
            return false;
        }

        public static bool IsScopeRuntime(this IType type)
        {
            var basicType = type as IBasicType;
            if (basicType != null)
            {
                return basicType.ContainingNamespace == "ScopeRuntime";
            }
            return false;
        }

        public static bool IsIEnumeratorScopeMapUsage(this IType type)
        {
            return type.ToString().Contains("ScopeMapUsage");

            //var basicType = type as IBasicType;
            //if (basicType != null)
            //{
            //    return basicType.GenericName == "IEnumerator<ScopeMapUsage>";
            //}
            //return false;
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
            var basictype = (type as IBasicType);
            if (basictype != null)
            {
                var resolvedClass = basictype.ResolvedType as ITypeDefinition;
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

        public static bool TypeEquals(this IType type1, IType type2)
        {
            return Model.Types.TypeHelper.TypesAreEquivalent(type1, type2);
        }

        public static bool SameType(this IBasicType containingType, ITypeDefinition iteratorClass)
        {
            return containingType.Equals(iteratorClass);
        }
        public static bool IsString(this IBasicType containingType)
        {
            return containingType.Name == "String";
        }

        public static bool IsTuple(this IBasicType containingType)
        {
            return containingType.Name == "Tuple";
        }

        internal static bool IsValueType(this IBasicType containingType)
        {
            return containingType.TypeKind == TypeKind.ValueType;
        }
    }
}
