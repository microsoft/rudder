using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Cci;
using Backend.ThreeAddressCode.Values;
using Backend.ThreeAddressCode.Instructions;
using Backend.Analyses;

namespace ScopeAnalyzer.Misc
{
    using Backend.Model;
    using CCI = Microsoft.Cci;

    public static class Extensions
    {
        public static string FullName(this ITypeReference tref)
        {
            return CCI.TypeHelper.GetTypeName(tref, NameFormattingOptions.Signature);
        }

        public static string Name(this ITypeReference tref)
        {
            return CCI.TypeHelper.GetTypeName(tref, CCI.NameFormattingOptions.OmitContainingType | CCI.NameFormattingOptions.OmitContainingNamespace | CCI.NameFormattingOptions.PreserveSpecialNames);
        }

        public static string NestedName(this ITypeReference tref)
        {
            return CCI.TypeHelper.GetTypeName(tref, CCI.NameFormattingOptions.OmitContainingNamespace | CCI.NameFormattingOptions.PreserveSpecialNames);
        } 

        public static string FullName(this IMethodDefinition method)
        {
            return MemberHelper.GetMethodSignature(method, NameFormattingOptions.Signature | NameFormattingOptions.ParameterName);
        }



        public static bool SubtypeOf(this ITypeReference subtype, ITypeDefinition supertype, IMetadataHost host)
        {
            if (!(subtype is INamedTypeReference && supertype is INamedTypeDefinition)) return false;
            var subt = subtype as INamedTypeReference;
            var supt = supertype as INamedTypeDefinition;

            while (subt.IsAlias) subt = subt.AliasForType.AliasedType;

            if (subt.ResolvedType.SubtypeOf(supertype, host)) return true;
            return subt.Resolve(host).SubtypeOf(supt, host);
        }

        public static bool SubtypeOf(this ITypeDefinition subtype, ITypeDefinition supertype, IMetadataHost host)
        {
            if (!(subtype is INamedTypeDefinition && supertype is INamedTypeDefinition)) return false;
            var subt = subtype as INamedTypeDefinition;
            var supt = supertype as INamedTypeDefinition;
            return subt.SubtypeOf(supt, host);
        }

        public static bool SubtypeOf(this INamedTypeDefinition subtype, INamedTypeDefinition supertype, IMetadataHost host)
        {
            if (subtype.IsEnum || supertype.IsEnum || subtype.IsValueType || supertype.IsValueType) return false;

            if (subtype.Equals(supertype) || CCI.TypeHelper.Type1DerivesFromOrIsTheSameAsType2(subtype, supertype, true)) return true;

            foreach (var subcl in subtype.BaseClasses)
            {
                if (subcl.SubtypeOf(supertype, host)) return true;
            }

            return false;
        }


        public static bool IncludesType(this ITypeReference type, ITypeDefinition included, IMetadataHost host)
        {
            while (type.IsAlias) type = type.AliasForType.AliasedType;

            var toCheck = new List<ITypeReference>();
            if (type is IArrayTypeReference)
            {
                var t = type as IArrayTypeReference;
                return t.ElementType.IncludesType(included, host);
            }
            else if (type is IGenericTypeInstanceReference)
            {
                var t = type as IGenericTypeInstanceReference;
                foreach (var tgi in t.GenericArguments)
                {
                    if (tgi.IncludesType(included, host)) return true;
                }
                return false;
            }
            else if (type is IGenericParameterReference)
            { 
                // TODO: check this.
                var t = type as IGenericParameterReference;
                var resolved = t.Resolve(host);
                // for soundness
                if (resolved == null || resolved.IsDummy()) return true;
                return CCI.TypeHelper.Type1DerivesFromOrIsTheSameAsType2(resolved, included);
            }
            else if (type is INamedTypeReference)
            {
                var t = type as INamedTypeReference;
                var resolved = t.Resolve(host);
                // for soundness
                if (resolved == null || resolved.IsDummy()) return true;
                if (resolved.IsReferenceType) return resolved.SubtypeOf(included, host);
                else return resolved.Equals(included);
            }     
            else if (type is IPointerTypeReference)
            {
                var t = type as IPointerTypeReference;
                return t.TargetType.IncludesType(included, host);
            }
            else if (type is IManagedPointerTypeReference)
            {
                var t = type as IManagedPointerTypeReference;
                return t.TargetType.IncludesType(included, host);
            }
            else
            {
                //TODO: is this worth analyzing in more depth?
                return true;
            }
        }



        public static ITypeDefinition Resolve(this ITypeReference type, IMetadataHost host)
        {
            return CCI.TypeHelper.Resolve(type, host);
        }

        public static bool IsDummy(this ITypeDefinition tdef)
        {
            return tdef.FullName().Equals("Microsoft.Cci.DummyNamespaceTypeDefinition") || tdef is Dummy;
        }

        public static IFieldDefinition Resolve(this IFieldReference fref, IMetadataHost host)
        {
            var resolved = fref.ResolvedField;
            if (resolved is Dummy)
            {
                return CCI.MemberHelper.ResolveField(fref, host);
            }
            else
            {
                return resolved;
            }       
        }


       
        public static IEnumerable<IFieldReference> FieldsReferences(this ControlFlowGraph cfg)
        {
            var fields = new HashSet<IFieldReference>();
            foreach (var node in cfg.Nodes)
            {
                foreach (var ins in node.Instructions)
                {
                    if (ins is LoadInstruction || ins is StoreInstruction)
                    {
                        IValue operand;
                        if (ins is LoadInstruction)
                        {
                            operand = (ins as LoadInstruction).Operand;
                        }
                        else
                        {
                            operand = (ins as StoreInstruction).Result;
                        }

                        IFieldReference field = null;
                        if (operand is InstanceFieldAccess)
                        {
                            field = (operand as InstanceFieldAccess).Field;
                        }
                        else if (operand is StaticFieldAccess)
                        {
                            field = (operand as StaticFieldAccess).Field;
                        }

                        if (field == null) continue;

                        fields.Add(field);
                    }
                }
            }
            return fields;
        }


        /// <summary>
        /// Returns a unique set of pairs (field access, field reference). In the case that
        /// the access is array length, then field reference is null.
        /// </summary>
        /// <param name="cfg"></param>
        /// <returns></returns>
        public static IEnumerable<Tuple<IFieldAccess, IFieldReference>> FieldAccesses(this ControlFlowGraph cfg)
        {
            var fields = new Dictionary<string, Tuple<IFieldAccess, IFieldReference>>();
            foreach (var node in cfg.Nodes)
            {
                foreach (var ins in node.Instructions)
                {
                    // Field access appear only in load and store instructions.
                    if (ins is LoadInstruction || ins is StoreInstruction)
                    {
                        IValue operand;
                        if (ins is LoadInstruction)
                        {
                            // here field access can be only operand.
                            operand = (ins as LoadInstruction).Operand;
                        }
                        else
                        {
                            // here field access can be only result.
                            operand = (ins as StoreInstruction).Result;
                        }

                        if (operand is IFieldAccess)
                        {
                            var op = operand as IFieldAccess;
                            IFieldReference fref;
                            // Array length access does not have field reference
                            if (op is InstanceFieldAccess)
                                fref = (op as InstanceFieldAccess).Field;
                            else if (operand is StaticFieldAccess)
                                fref = (op as StaticFieldAccess).Field;
                            else
                                fref = null;

                            // IFieldAccess objects might be semantically same but have
                            // several object representations. We match syntactically.
                            var ops = op.ToExpression().ToString();
                            if (!fields.ContainsKey(ops))
                                fields[ops] = new Tuple<IFieldAccess, IFieldReference>(op, fref);
                        }                      
                    }
                }
            }
            return fields.Values.AsEnumerable();
        }
    }
}
