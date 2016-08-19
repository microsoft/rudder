using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.Cci;
using Frontend;
using Backend;

namespace BulkScopeAnalyzer
{
    class ProcessorMappingAnalyzer : MetadataTraverser
    {
        Assembly assembly;
        private Dictionary<string, string> mapping = new Dictionary<string, string>();

        private Regex[] compilerGeneretedMethodMatchers = new Regex[] { new Regex(@"^___Scope_Generated_Classes___.ScopeFilterTransformer_\d+$", RegexOptions.Compiled),
                                                                        new Regex(@"^___Scope_Generated_Classes___.ScopeGrouper_\d+$", RegexOptions.Compiled) };

        public ProcessorMappingAnalyzer(Assembly asm)
        {
            assembly = asm;
        }

        public Dictionary<string, string> ProcessorIdMapping
        {
            get { return mapping; }
        }

        public override void TraverseChildren(IMethodDefinition methodDefinition)
        {
            var name = Microsoft.Cci.TypeHelper.GetTypeName(methodDefinition.ContainingType,
                Microsoft.Cci.NameFormattingOptions.OmitContainingNamespace);

            if (name == "___Scope_Generated_Classes___.__OperatorFactory__")
            {
                var mname = methodDefinition.Name.Value;
                if (mname.StartsWith("Create_"))
                {
                    try
                    {
                        var id = "Process_" + mname.Split('_').Last();
                        var processorName = GetProcessorName(methodDefinition);
                        if (processorName != null)
                            mapping[processorName] = id;
                    }
                    catch
                    {
                        Console.WriteLine("ERROR: failed to extract specific processor id: " + mname);
                    }
                }
            }
        }


        private string GetProcessorName(IMethodDefinition method)
        {
            var disassembler = new Disassembler(assembly.Host, method, null);
            var methodBody = disassembler.Execute();
            var ins = methodBody.Instructions.OfType<Backend.ThreeAddressCode.Instructions.CreateObjectInstruction>().Single();
            var ct = ins.Constructor.ContainingType;
            var name =  Microsoft.Cci.TypeHelper.GetTypeName(ct, NameFormattingOptions.Signature);

            if (IsCompilerGenerated(name))
                return null;

            return name;
        }

        private bool IsCompilerGenerated(string name)
        {
            foreach(var reg in compilerGeneretedMethodMatchers)
            {
                if (reg.IsMatch(name.Trim()))
                    return true;
            }
            return false;
        }


        public void Analyze()
        {
            base.Traverse(assembly.Module);
        }

    }
}
