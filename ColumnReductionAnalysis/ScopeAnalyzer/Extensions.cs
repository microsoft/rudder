using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Cci;
using Backend.ThreeAddressCode.Values;
using Backend.ThreeAddressCode.Instructions;
using Backend.Analysis;

namespace ScopeAnalyzer
{
    using CCI = Microsoft.Cci;

    public static class Extensions
    {
        public static string FullName(this ITypeReference tref)
        {
            return CCI.TypeHelper.GetTypeName(tref, NameFormattingOptions.Signature);
        }

        public static string Name(this ITypeReference tref)
        {
            return CCI.TypeHelper.GetTypeName(tref, CCI.NameFormattingOptions.OmitContainingType | CCI.NameFormattingOptions.PreserveSpecialNames);
        }

        public static string FullName(this IMethodDefinition method)
        {
            return MemberHelper.GetMethodSignature(method, NameFormattingOptions.Signature | NameFormattingOptions.ParameterName);
        }

        public static bool SubtypeOf(this ITypeReference subtype, ITypeDefinition supertype)
        {
            if (!(subtype is INamedTypeReference && supertype is INamedTypeDefinition)) return false;
            var subt = subtype as INamedTypeReference;
            var supt = supertype as INamedTypeDefinition;

            while (subt.IsAlias) subt = subt.AliasForType.AliasedType;
            var subtdef = subt.ResolvedType;
            return subtdef.SubtypeOf(supt);
        }

        public static bool SubtypeOf(this ITypeDefinition subtype, ITypeDefinition supertype)
        {
            if (!(subtype is INamedTypeDefinition && supertype is INamedTypeDefinition)) return false;
            var subt = subtype as INamedTypeDefinition;
            var supt = supertype as INamedTypeDefinition;
            return subt.SubtypeOf(supt);
        }

        public static bool SubtypeOf(this INamedTypeDefinition subtype, ITypeDefinition supertype)
        {
            if (!(supertype is INamedTypeDefinition)) return false;
            var supt = supertype as INamedTypeDefinition;
            return subtype.SubtypeOf(supt);
        }

        public static bool SubtypeOf(this INamedTypeDefinition subtype, INamedTypeDefinition supertype)
        {
            if (subtype.IsEnum || supertype.IsEnum || subtype.IsValueType || supertype.IsValueType) return false;

            if (subtype.Equals(supertype)) return true;

            foreach (var subcl in subtype.BaseClasses)
            {
                if (subcl.SubtypeOf(supertype)) return true;
            }

            return false;
        }

        public static ITypeDefinition Resolve(this ITypeReference type, IMetadataHost host)
        {
            return CCI.TypeHelper.Resolve(type, host);
        }

        public static bool IsDummy(this ITypeDefinition tdef)
        {
            return tdef.FullName().Equals("Microsoft.Cci.DummyNamespaceTypeDefinition");
        }

        public static IFieldDefinition Resolve(this IFieldReference fref, IMetadataHost host)
        {
            return CCI.MemberHelper.ResolveField(fref, host);
        }

        public static Instruction NormalExitInstruction(this ControlFlowGraph cfg)
        {
            if (cfg.Exit.Instructions.Count > 0)
            {
               return cfg.Exit.Instructions.Last();
            }
            else
            {
                if (cfg.Exit.Predecessors.Count != 1 && cfg.Exit.Predecessors.ElementAt(0).Instructions.Count == 0)
                    throw new Exception("Could not find last instruction");
                return cfg.Exit.Predecessors.ElementAt(0).Instructions.Last();
            }
        }

    }
}
