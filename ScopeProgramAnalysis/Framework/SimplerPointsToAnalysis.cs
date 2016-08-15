// Copyright (c) Edgardo Zoppi.  All Rights Reserved.  Licensed under the MIT License.  See License.txt in the project root for license information.

using Model.ThreeAddressCode.Instructions;
using Model.ThreeAddressCode.Values;
using Backend.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Model.Types;
using Model;
using Backend.Model;
using Model.ThreeAddressCode.Visitor;

namespace Backend.Analyses
{
    // May Points-To Analysis
    public class IteratorPointsToAnalysis : ForwardDataFlowAnalysis<PointsToGraph>
    {
        class PTAVisitor : InstructionVisitor
        {
            private PointsToGraph ptg;
            private IteratorPointsToAnalysis ptAnalysis;
            private bool analyzeNextDelegateCtor;

            internal PTAVisitor(PointsToGraph ptgNode, IteratorPointsToAnalysis ptAnalysis)
            {
                this.ptg = ptgNode;
                this.ptAnalysis = ptAnalysis;
                this.analyzeNextDelegateCtor = false;
            }
            public override void Visit(LoadInstruction instruction)
            {
                var load = instruction as LoadInstruction;
                var operand = load.Operand;

                HandleLoadWithOperand(load, operand);

                if (operand is Reference)
                {
                    var referencedValue = (operand as Reference).Value;
                    var isHandled = HandleLoadWithOperand(load, referencedValue);
                }
                else if (operand is Dereference)
                {
                    var reference = (operand as Dereference).Reference;
                    var isHandled = HandleLoadWithOperand(load, reference);
                }

            }

            private bool HandleLoadWithOperand(LoadInstruction load, IValue operand)
            {
                var result = true;
                if (operand is Constant)
                {
                    var constant = operand as Constant;

                    if (constant.Value == null)
                    {
                        ptAnalysis.ProcessNull(ptg, load.Result);
                    }
                }
                if (operand is IVariable)
                {
                    var variable = operand as IVariable;
                    ptAnalysis.ProcessCopy(ptg, load.Result, variable);
                }
                else if (operand is InstanceFieldAccess)
                {
                    var access = operand as InstanceFieldAccess;
                    ptAnalysis.ProcessLoad(ptg, load.Offset, load.Result, access);
                }
                else if (operand is VirtualMethodReference)
                {
                    var loadDelegateStmt = operand as VirtualMethodReference;
                    var methodRef = loadDelegateStmt.Method;
                    var instance = loadDelegateStmt.Instance;
                    ptAnalysis.ProcessDelegateAddr(ptg, load.Offset, load.Result, methodRef, instance);

                }
                else if (operand is StaticMethodReference)
                {
                    var loadDelegateStmt = operand as StaticMethodReference;
                    var methodRef = loadDelegateStmt.Method;
                    ptAnalysis.ProcessDelegateAddr(ptg, load.Offset, load.Result, methodRef, null);
                }
                else
                {
                    result = false;
                }
                return result;
            }

            public override void Visit(StoreInstruction instruction)
            {
                var store = instruction;
                if (store.Result is InstanceFieldAccess)
                {
                    var access = store.Result as InstanceFieldAccess;
                    ptAnalysis.ProcessStore(ptg, access, store.Operand);
                }
            }
            public override void Visit(CreateObjectInstruction instruction)
            {
                if (instruction is CreateObjectInstruction)
                {
                    var allocation = instruction as CreateObjectInstruction;
                    // hack for handling delegates
                    if (allocation.AllocationType.IsDelegateType())
                    {
                        this.analyzeNextDelegateCtor = true;
                    }
                    // TODO: Check if we can avoid adding the node in case of delegate (it was already added in load address for method)
                    ptAnalysis.ProcessObjectAllocation(ptg, allocation.Offset, allocation.Result);
                }
            }
            public override void Visit(CreateArrayInstruction instruction)
            {
                var allocation = instruction;
                ptAnalysis.ProcessArrayAllocation(ptg, allocation.Offset, allocation.Result);
            }
            public override void Visit(ConvertInstruction instruction)
            {
                var convertion = instruction as ConvertInstruction;
                ptAnalysis.ProcessCopy(ptg, convertion.Result, convertion.Operand);
            }
            public override void Visit(MethodCallInstruction instruction)
            {
                var methodCall = instruction as MethodCallInstruction;
                // Hack for mapping delegates to nodes
                if (methodCall.Method.Name == ".ctor" && this.analyzeNextDelegateCtor)
                {
                    ProcessDelegateCtor(methodCall);
                    this.analyzeNextDelegateCtor = false;
                }
            }

            private void ProcessDelegateCtor(MethodCallInstruction methodCall)
            {
                if (methodCall.Arguments.Any())
                {
                    var arg0Type = methodCall.Arguments[0].Type;
                    if (arg0Type.IsDelegateType())
                    {
                        ptg.RemoveEdges(methodCall.Arguments[0]);
                        if (methodCall.Arguments.Count == 3)
                        {
                            // instance delegate
                            foreach (var dn in ptg.GetTargets(methodCall.Arguments[2]).OfType<DelegateNode>())
                            {
                                dn.Instance = methodCall.Arguments[1];
                                ptg.PointsTo(methodCall.Arguments[0], dn);
                            }
                        }
                        else
                        {
                            foreach (var dn in ptg.GetTargets(methodCall.Arguments[1]).OfType<DelegateNode>())
                            {
                                ptg.PointsTo(methodCall.Arguments[0], dn);
                            }
                        }
                    }
                }
            }

            public override void Visit(PhiInstruction instruction)
            {
                ptAnalysis.ProcessCopy(ptg, instruction.Result, instruction.Arguments);
            }
            public override void Visit(ReturnInstruction instruction)
            {
                if (instruction.HasOperand)
                {
                    var rv = ptAnalysis.ReturnVariable;
                    ptAnalysis.ProcessCopy(ptg, rv, instruction.Operand);
                }
            }

        }

        private PointsToGraph initialGraph;
        private MethodDefinition method;
        public IVariable ReturnVariable { get; private set; }
        public DataFlowAnalysisResult<PointsToGraph>[] Result { get; private set; }
        public IVariable ThisVariable { get; private set; }

        // private IDictionary<string, IVariable> specialFields;
        protected PointsToGraph initPTG;

        public IteratorPointsToAnalysis(ControlFlowGraph cfg, MethodDefinition method) //  IDictionary<string, IVariable> specialFields)
			: base(cfg)
		{
            this.method = method;
            // this.specialFields = specialFields;
            this.CreateInitialGraph();
            
		}

        public IteratorPointsToAnalysis(ControlFlowGraph cfg, MethodDefinition method, PointsToGraph initPTG) : base(cfg)
        {
            this.method = method;
            this.CreateInitialGraph(false);
            this.initialGraph.Union(initPTG);
            this.initPTG = this.initialGraph; // initPTG;
        }
       
        protected override PointsToGraph InitialValue(CFGNode node)
        {
            if (this.cfg.Entry.Id == node.Id && this.initPTG != null)
            {
                return this.initPTG;
            }
            return this.initialGraph.Clone();
        }

        public override DataFlowAnalysisResult<PointsToGraph>[] Analyze()
        {
            Result = base.Analyze();
            return Result;
        }
        
        protected override bool Compare(PointsToGraph left, PointsToGraph right)
        {
            return left.GraphEquals(right);
        }

        protected override PointsToGraph Join(PointsToGraph left, PointsToGraph right)
        {
            //var result = new PointsToGraph();
            //result.Union(left);
            var result = left;
            result.Union(right);
			return result;
        }

        protected override PointsToGraph Flow(CFGNode node, PointsToGraph input)
        {
            var ptg = input; // .Clone();

            var ptaVisitor = new PTAVisitor(ptg, this);
            ptaVisitor.Visit(node);

            //foreach (var instruction in node.Instructions)
            //{
            //    this.Flow(ptg, instruction as Instruction);
            //}

            return ptg;
        }

        //private void Flow(PointsToGraph ptg, Instruction instruction)
        //{
        //    var ptaVisitor = new PTAVisitor(ptg, this);
        //    ptaVisitor.Visit(instruction);
        //}

		private void CreateInitialGraph(bool createNodeForParams = true)
		{
            this.ReturnVariable = new LocalVariable(this.method.Name+"_"+"$RV");
            this.ReturnVariable.Type = PlatformTypes.Object;

            var ptg = new PointsToGraph();
			var variables = cfg.GetVariables();
            if(this.method.Body.Parameters!=null)
                variables.AddRange(this.method.Body.Parameters);

            int counter = -1;
            IVariable thisVariable = null;
            PTGNode thisNode = null;
			foreach (var variable in variables)
			{
                // TODO: replace when Egdardo fixes type inferece
                if (variable.Type==null || variable.Type.TypeKind == TypeKind.ValueType) continue;
                // if (variable.Type.TypeKind == TypeKind.ValueType) continue;

				if (variable.IsParameter)
				{
					var isThisParameter = variable.Name == "this";
					var kind = isThisParameter ? PTGNodeKind.Object : PTGNodeKind.Unknown;
                    if (createNodeForParams)
                    {
                        var ptgId = new PTGID(new MethodContex(this.method), counter--);
                        var node = new ParameterNode(ptgId, variable.Name, variable.Type);
                        ptg.Add(node);
                        ptg.PointsTo(variable, node);
                        if (isThisParameter)
                        {
                            thisVariable = variable;
                            thisNode = node;
                        }
                    }
                    if (isThisParameter)
                    {
                        this.ThisVariable = variable;
                    }
                    ptg.Add(variable);
                }
				else
				{
					//ptg.Add(variable);
                    ptg.PointsTo(variable, PointsToGraph.NullNode);
				}
			}

            //foreach(var specialField in specialFields)
            //{
            //    counter = -1000;
            //    var variable = specialField.Value;
            //    var fieldName =  specialField.Key;
            //    var ptgId = new PTGID(new MethodContex(this.method), counter--);
            //    var node = new PTGNode(ptgId, variable.Type);
            //    ptg.Add(node);
            //    ptg.PointsTo(thisNode, new FieldReference(fieldName, variable.Type, method.ContainingType), node);
            //}
            ptg.Add(this.ReturnVariable);
			this.initialGraph = ptg;
		}

		private void ProcessNull(PointsToGraph ptg, IVariable dst)
		{
            ptg.RemoveEdges(dst);

            if (dst.Type.TypeKind == TypeKind.ValueType) return;
            ptg.PointsTo(dst, PointsToGraph.NullNode);
		}

        private void ProcessObjectAllocation(PointsToGraph ptg, uint offset, IVariable dst)
		{
            ptg.RemoveEdges(dst);
            if (dst.Type.TypeKind == TypeKind.ValueType) return;
            var ptgId = new PTGID(new MethodContex(this.method), (int)offset);

            var node = this.NewNode(ptg, ptgId, dst.Type);

            ptg.PointsTo(dst, node);
        }

		private void ProcessArrayAllocation(PointsToGraph ptg, uint offset, IVariable dst)
        {
            ptg.RemoveEdges(dst);

            if (dst.Type.TypeKind == TypeKind.ValueType) return;
            var ptgId = new PTGID(new MethodContex(this.method), (int)offset);

            var node = this.NewNode(ptg, ptgId, dst.Type);

            ptg.PointsTo(dst, node);
        }

        private void ProcessCopy(PointsToGraph ptg, IVariable dst, IEnumerable<IVariable> srcs)
        {
            ptg.RemoveEdges(dst);

            if (dst.Type.TypeKind == TypeKind.ValueType) return;

            var targets = srcs.Where(src => src.Type.TypeKind != TypeKind.ValueType).SelectMany(src =>  ptg.GetTargets(src));

            foreach (var target in targets)
            {
                ptg.PointsTo(dst, target);
            }
        }
        private void ProcessCopy(PointsToGraph ptg, IVariable dst, IVariable src)
        {
            ProcessCopy(ptg, dst, new HashSet<IVariable>() { src });
			//if (dst.Type.TypeKind == TypeKind.ValueType || src.Type.TypeKind == TypeKind.ValueType) return;

   //         ptg.RemoveEdges(dst);
   //         var targets = ptg.GetTargets(src);

   //         foreach (var target in targets)
   //         {
   //             ptg.PointsTo(dst, target);
   //         }
        }

		private void ProcessLoad(PointsToGraph ptg, uint offset, IVariable dst, InstanceFieldAccess access)
        {
			if (dst.Type.TypeKind == TypeKind.ValueType || access.Type.TypeKind == TypeKind.ValueType) return;

            ptg.RemoveEdges(dst);
			var nodes = ptg.GetTargets(access.Instance);
            foreach (var node in nodes)
            {
                var hasField = node.Targets.ContainsKey(access.Field);

                if (!hasField)
				{
                    // ptg.PointsTo(node, access.Field, ptg.Null);
                    if (!MayReacheableFromParameter(ptg, node))
                    {
                        System.Console.WriteLine("In {0}:{1:X4} there is variable that is not a parameter and has no object to load.", this.method.ToSignatureString(), offset);
                    }

                    {
                        var ptgId = new PTGID(new MethodContex(this.method), (int)offset);
                        // TODO: Should be a LOAD NODE
                        // Preventive assignement of a new Node unknown (should be only for parameters)
                        var target = this.NewNode(ptg, ptgId, dst.Type, PTGNodeKind.Unknown);
                        ptg.PointsTo(node, access.Field, target);
                    }
                }

                var targets = node.Targets[access.Field];

                foreach (var target in targets)
                {
                    ptg.PointsTo(dst, target);
                }
            }
        }
        private bool MayReacheableFromParameter(PointsToGraph ptg, PTGNode n)
        {
            var result = method.Body.Parameters.Where(p => ptg.Reachable(p,n)).Any();
            // This version does not need the inverted mapping of nodes-> variables (which may be expensive to maintain)
            // var result = method.Body.Parameters.Any(p =>ptg.GetTargets(p).Contains(n));
            return result;
        }

        private void ProcessStore(PointsToGraph ptg, InstanceFieldAccess access, IVariable src)
        {
			if (access.Type.TypeKind == TypeKind.ValueType || src.Type.TypeKind == TypeKind.ValueType) return;

			var nodes = ptg.GetTargets(access.Instance);
			var targets = ptg.GetTargets(src);

			foreach (var node in nodes)
				foreach (var target in targets)
				{
					ptg.PointsTo(node, access.Field, target);
				}
        }

        protected void ProcessDelegateAddr(PointsToGraph ptg, uint offset, IVariable dst, IMethodReference methodRef, IVariable instance)
        {
            var ptgID = new PTGID(new MethodContex(this.method), (int)offset);
            var delegateNode = new DelegateNode(ptgID, methodRef, instance);
            ptg.Add(delegateNode);
            ptg.RemoveEdges(dst);
            ptg.PointsTo(dst, delegateNode);
        }


        private PTGNode NewNode(PointsToGraph ptg, PTGID ptgID, IType type, PTGNodeKind kind = PTGNodeKind.Object)
		{
			PTGNode node;
            node = ptg.GetNode(ptgID, type, kind);
            return node;
		}
    }
}
