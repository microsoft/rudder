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
        public static bool IsSubClass(this ClassDefinition class1, ITypeDefinition class2)
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

    }
}
