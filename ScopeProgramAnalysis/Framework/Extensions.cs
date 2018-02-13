using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Backend;
using Backend.Analyses;
using Backend.Model;
using Backend.ThreeAddressCode.Expressions;
using Backend.ThreeAddressCode.Instructions;
using Backend.ThreeAddressCode.Values;
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
			AnalysisStats.extraAnalysisOverHead.Start();
			var disassembler = new Disassembler(host, method, locationProvider);

			var methodBody = disassembler.Execute();

			MethodBodyProvider.Instance.AddBody(method, methodBody);

			if (methodsToTryToInline != null)
			{
				DoInlining(method, host, methodBody, locationProvider, methodsToTryToInline);
			}

			var cfAnalysis = new ControlFlowAnalysis(methodBody);
			//var cfg = cfAnalysis.GenerateExceptionalControlFlow();
			var cfg = cfAnalysis.GenerateNormalControlFlow();

			var domAnalysis = new DominanceAnalysis(cfg);
			domAnalysis.Analyze();
			domAnalysis.GenerateDominanceTree();

			var loopAnalysis = new NaturalLoopAnalysis(cfg);
			loopAnalysis.Analyze();

			var domFrontierAnalysis = new DominanceFrontierAnalysis(cfg);
			domFrontierAnalysis.Analyze();

			var splitter = new WebAnalysis(cfg, method);
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
			AnalysisStats.extraAnalysisOverHead.Stop();
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
			if (type is INamedTypeDefinition)
				return TypeHelper.GetDefiningNamespace((INamedTypeDefinition)type).ToString();
			return String.Empty;
		}

		public static string ContainingAssembly(this ITypeReference type)
		{
			return TypeHelper.GetDefiningUnitReference(type).Name.Value;
		}

		public static bool IsConstructor(this IMethodReference method)
		{
			return method.Name.Value == ".ctor";
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

			while (receiverType != null && !method.ContainingType.TypeEquals(receiverType))
			{
				var receiverTypeDef = receiverType.ResolvedType;
				if (receiverTypeDef == null) break;

				//var foo = receiverTypeDef.Methods.Where(m => MemberHelper.SignaturesAreEqual(m, method));
				//var bar = receiverTypeDef.Methods.Where(m => MemberHelper.MethodsAreEquivalent(m, method.ResolvedMethod));
				var matchingMethod = receiverTypeDef.Methods.SingleOrDefault(m => m.Name.UniqueKey == method.Name.UniqueKey && MemberHelper.SignaturesAreEqual(m, method));

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
			// BUG: type cannot be NULL!
			if (type == null)
				return true;
			if (type.IsValueType)
			{
				if (type.ResolvedType.IsStruct)
				{
					switch (type.TypeCode)
					{
						case PrimitiveTypeCode.Boolean:
						case PrimitiveTypeCode.Char:
						case PrimitiveTypeCode.Int8:
						case PrimitiveTypeCode.Float32:
						case PrimitiveTypeCode.Float64:
						case PrimitiveTypeCode.Int16:
						case PrimitiveTypeCode.Int32:
						case PrimitiveTypeCode.Int64:
						case PrimitiveTypeCode.UInt8:
						case PrimitiveTypeCode.UInt16:
						case PrimitiveTypeCode.UInt32:
						case PrimitiveTypeCode.UInt64:
						case PrimitiveTypeCode.Void:
							return false;
						default:
							return true;
					}
				}
			}
			return true;
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

			return TypeHelper.GetTypeName(tref, NameFormattingOptions.OmitContainingType | NameFormattingOptions.OmitContainingNamespace | NameFormattingOptions.SmartTypeName);
		}
		public static string GetNameThatMatchesReflectionName(this ITypeDefinition typeDefinition)
		{
			return Microsoft.Cci.TypeHelper.GetTypeName(typeDefinition, NameFormattingOptions.OmitContainingType | NameFormattingOptions.OmitContainingNamespace | NameFormattingOptions.UseGenericTypeNameSuffix);
		}



		#region Methods to Compute a sort of propagation of Equalities 
		public static void PropagateExpressions(this ControlFlowGraph cfg, IDictionary<IVariable, IExpression> equalities)
		{
			foreach (var node in cfg.ForwardOrder)
			{
				PropagateExpressions(node, equalities);
			}
		}

		private static void PropagateExpressions(CFGNode node, IDictionary<IVariable, IExpression> equalities)
		{
			foreach (var instruction in node.Instructions)
			{
				PropagateExpressions(instruction, equalities);
			}
		}

		private static void PropagateExpressions(Instruction instruction, IDictionary<IVariable, IExpression> equalities)
		{
			var definition = instruction as DefinitionInstruction;

			if (definition != null && definition.HasResult)
			{
				var expr = definition.ToExpression().ReplaceVariables(equalities);
				equalities.Add(definition.Result, expr);
			}
		}

		#endregion
	}
}
