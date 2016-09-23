using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Backend;
using Backend.Analyses;
using Backend.Model;
using Backend.ThreeAddressCode.Instructions;
using Backend.Transformations;
using Backend.Utils;
using Microsoft.Cci;

namespace ScopeProgramAnalysis.Framework
{
    public static class Extensions
    {
        public static ControlFlowGraph DoAnalysisPhases(this IMethodDefinition method, IMetadataHost host, ISourceLocationProvider locationProvider, 
                                                            IEnumerable<IMethodReference> methodsToTryToInline = null)
        {
            var disassembler = new Disassembler(host, method, locationProvider);

            var methodBody = disassembler.Execute();

            MethodBodyProvider.Instance.AddBody(method, methodBody);

            if (methodsToTryToInline != null)
            {
                DoInlining(method, host, methodBody, locationProvider, methodsToTryToInline);
            }

            var cfAnalysis = new ControlFlowAnalysis(methodBody);
            var cfg = cfAnalysis.GenerateExceptionalControlFlow();
            // var cfg = cfAnalysis.GenerateNormalControlFlow();

            var domAnalysis = new DominanceAnalysis(cfg);
            domAnalysis.Analyze();
            domAnalysis.GenerateDominanceTree();

            var loopAnalysis = new NaturalLoopAnalysis(cfg);
            loopAnalysis.Analyze();

            var domFrontierAnalysis = new DominanceFrontierAnalysis(cfg);
            domFrontierAnalysis.Analyze();

            var splitter = new WebAnalysis(cfg);
            splitter.Analyze();
            splitter.Transform();

            methodBody.UpdateVariables();


            var analysis = new TypeInferenceAnalysis(cfg);
            analysis.Analyze();

            var copyProgapagtion = new ForwardCopyPropagationAnalysis(cfg);
            copyProgapagtion.Analyze();
            copyProgapagtion.Transform(methodBody);

            //var backwardCopyProgapagtion = new BackwardCopyPropagationAnalysis(cfg);
            //backwardCopyProgapagtion.Analyze();
            //backwardCopyProgapagtion.Transform(methodBody);

            var liveVariables = new LiveVariablesAnalysis(cfg);
            var resultLiveVar = liveVariables.Analyze();


            var ssa = new StaticSingleAssignment(methodBody, cfg);
            ssa.Transform();
            ssa.Prune(liveVariables);
            methodBody.UpdateVariables();
            return cfg;
        }

        public static IEnumerable<IMethodReference> GetMethodsInvoked(this IMethodDefinition method)
        {
            return MethodBodyProvider.Instance.GetBody(method).Instructions.OfType<MethodCallInstruction>().Select(ins => ins.Method);
        }

        private static void DoInlining(IMethodDefinition method, IMetadataHost host, MethodBody methodBody, ISourceLocationProvider sourceLocationProvider, IEnumerable<IMethodReference> methodsToTryToInline = null)
        {
            if (methodsToTryToInline == null)
                methodsToTryToInline = new HashSet<IMethodReference>();

            var methodCalls = methodBody.Instructions.OfType<MethodCallInstruction>().Where(ins => methodsToTryToInline.Contains(ins.Method)).ToList();
            foreach (var methodCall in methodCalls)
            {
                var callee = methodCall.Method.ResolvedMethod;
                if (callee != null)
                {
                    // var calleeCFG = DoAnalysisPhases(callee, host);
                    var disassemblerCallee = new Disassembler(host, callee, sourceLocationProvider);
                    var methodBodyCallee = disassemblerCallee.Execute();
                    methodBody.Inline(methodCall, methodBodyCallee);
                }
            }

            methodBody.UpdateVariables();

        }

        public static string ContainingNamespace(this ITypeReference type)
        {
            if(type is INamedTypeDefinition)
                return TypeHelper.GetDefiningNamespace((INamedTypeDefinition)type).ToString();
            return String.Empty;
        }

        public static string ContainingAssembly(this ITypeReference type)
        {
            return TypeHelper.GetDefiningUnitReference(type).Name.Value;
        }

        public static bool IsConstructor(this IMethodReference method)
        {
            return method.Name.Value==".ctor";
        }
        public static bool IsConstructorCall(this Instruction ins)
        {
            if (ins is MethodCallInstruction)
            {
                var call = ins as MethodCallInstruction;
                if (call.Method.IsConstructor())
                {
                    return true;
                }
            }
            return false;
        }

        public static IMethodReference FindMethodImplementation(this ITypeReference receiverType, IMethodReference method)
        {
            var result = method;

            while (receiverType != null && !method.ContainingType.Equals(receiverType))
            {
                var receiverTypeDef = receiverType.ResolvedType;
                if (receiverTypeDef == null) break;

                var matchingMethod = receiverTypeDef.Methods.SingleOrDefault(m => MemberHelper.SignaturesAreEqual(m, method));

                if (matchingMethod != null)
                {
                    result = matchingMethod;
                    break;
                }
                else
                {
                    receiverType = receiverTypeDef.BaseClasses.SingleOrDefault();
                }

            }

            return result;
        }


        public static bool IsDelegateType(this ITypeReference type)
        {
            return type.ResolvedType != null && type.ResolvedType.IsDelegate;
        }

        public static bool IsClassOrStruct(this ITypeReference type)
        {
            if (type.IsValueType)
            {
                if (!type.TypeCode.Equals(TypeCode.Object) || !type.TypeCode.Equals(TypeCode.String))
                {
                    return true;
                }
            }
            return false;
        }

        public static bool MapLessEquals<K, V>(this MapSet<K, V> left, MapSet<K, V> right)
        {
            var result = false;
            if (!left.Keys.Except(right.Keys).Any() && left.Keys.Count() <= right.Keys.Count())
            {
                return left.All(kv => kv.Value.All(n => right[kv.Key].Contains(n)));
                //&& right.All(kv => kv.Value.IsSubsetOf(left[kv.Key]));
            }
            return result;
        }

        public static string FullName(this ITypeReference tref)
        {
            return TypeHelper.GetTypeName(tref, NameFormattingOptions.Signature | NameFormattingOptions.TypeParameters);
        }
        public static string GetName(this ITypeReference tref)
        {
            if (tref is INamedTypeReference)
                return (tref as INamedTypeReference).Name.Value;

            return TypeHelper.GetTypeName(tref, NameFormattingOptions.SmartTypeName);
        }

    }

}
