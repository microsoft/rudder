using System.Collections.Generic;
using System.Linq;
using Microsoft.Cci;
using Console;

namespace BulkScopeAnalyzer
{
    class ScopeAnnotationAnalyzer : MetadataTraverser
    {
        Assembly assembly;

        public HashSet<string> UDFS { get; }


        public ScopeAnnotationAnalyzer(Assembly asm)
        {
            assembly = asm;
            UDFS = new HashSet<string>();
        }


        public void Analyze()
        {
            base.Traverse(assembly.Module);
        }

        public override void TraverseChildren(INamedTypeDefinition type)
        {
            if (type.Attributes.Count() > 1)
            {
                foreach (var atr in type.Attributes)
                {
                    // We look only for Scope annotation attributes.
                    if (Microsoft.Cci.TypeHelper.GetTypeName(atr.Type, NameFormattingOptions.Signature) != "ScopeRuntime.ScopeAnnotationAttribute")
                    {
                        continue;
                    }

                    if (atr.Arguments.Count() < 2) continue;
                   
                    if (atr.Arguments.ElementAt(0) is Microsoft.Cci.IMetadataConstant)
                    {
                        var argName = (atr.Arguments.ElementAt(0) as Microsoft.Cci.IMetadataConstant).Value.ToString();
                        if (argName == "OriginalClassName")
                        {
                            UDFS.Add((atr.Arguments.ElementAt(1) as Microsoft.Cci.IMetadataConstant).Value.ToString());
                        }
                    }                     
                }
            }

            base.TraverseChildren(type);
        }      
    }
}
