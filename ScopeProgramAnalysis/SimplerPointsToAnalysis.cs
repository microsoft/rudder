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
    public class SimplerPointsToAnalysis : ForwardDataFlowAnalysis<PointsToGraph>
    {
        class PTAVisitor : InstructionVisitor
        {
            private PointsToGraph ptg;
            private SimplerPointsToAnalysis ptAnalysis;
            internal PTAVisitor(PointsToGraph ptgNode, SimplerPointsToAnalysis ptAnalysis)
            {
                this.ptg = ptgNode;
                this.ptAnalysis = ptAnalysis;
            }
            public override void Visit(LoadInstruction instruction)
            {
                var offset = instruction.Offset;
                var load = instruction as LoadInstruction;

                if (load.Operand is Constant)
                {
                    var constant = load.Operand as Constant;

                    if (constant.Value == null)
                    {
                        ptAnalysis.ProcessNull(ptg, load.Result);
                    }
                }
                if (load.Operand is IVariable)
                {
                    var variable = load.Operand as IVariable;
                    ptAnalysis.ProcessCopy(ptg, load.Result, variable);
                }
                else if (load.Operand is InstanceFieldAccess)
                {
                    var access = load.Operand as InstanceFieldAccess;
                    ptAnalysis.ProcessLoad(ptg, offset, load.Result, access);
                }
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
            }
            public override void Visit(PhiInstruction instruction)
            {
                foreach(var v in instruction.Arguments)
                {
                    ptAnalysis.ProcessCopy(ptg, instruction.Result, v);
                }
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

        public SimplerPointsToAnalysis(ControlFlowGraph cfg, MethodDefinition method)
			: base(cfg)
		{
            this.method = method;
			this.CreateInitialGraph();
		}

        protected override PointsToGraph InitialValue(CFGNode node)
        {
			return this.initialGraph;
        }

        protected override bool Compare(PointsToGraph left, PointsToGraph right)
        {
            return left.GraphEquals(right);
        }

        protected override PointsToGraph Join(PointsToGraph left, PointsToGraph right)
        {
            //var result = left.Clone();
            var result = left;
            result.Union(right);
			return result;
        }

        protected override PointsToGraph Flow(CFGNode node, PointsToGraph input)
        {
            var ptg = input;

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

		private void CreateInitialGraph()
		{
            this.ReturnVariable = new LocalVariable("$RV");
            this.ReturnVariable.Type = PlatformTypes.Object;

            var ptg = new PointsToGraph();
			var variables = cfg.GetVariables();

            int counter = -1;
			foreach (var variable in variables)
			{
				if (variable.Type.TypeKind == TypeKind.ValueType) continue;

				if (variable.IsParameter)
				{
					var isThisParameter = variable.Name == "this";
					var kind = isThisParameter ? PTGNodeKind.Object : PTGNodeKind.Unknown;

                    var ptgId = new PTGID(new MethodContex(this.method), counter--);
                    var node = new ParameterNode(ptgId, variable.Name, variable.Type);
                    ptg.Add(node);
					ptg.PointsTo(variable, node);
				}
				else
				{
					ptg.Add(variable);
				}
			}

			this.initialGraph = ptg;
		}

		private void ProcessNull(PointsToGraph ptg, IVariable dst)
		{
			if (dst.Type.TypeKind == TypeKind.ValueType) return;

			ptg.RemoveEdges(dst);
			ptg.PointsTo(dst, ptg.Null);
		}

        private void ProcessObjectAllocation(PointsToGraph ptg, uint offset, IVariable dst)
		{
			if (dst.Type.TypeKind == TypeKind.ValueType) return;
            var ptgId = new PTGID(new MethodContex(this.method), (int)offset);

            var node = this.NewNode(ptg, ptgId, dst.Type);

            ptg.RemoveEdges(dst);
            ptg.PointsTo(dst, node);
        }

		private void ProcessArrayAllocation(PointsToGraph ptg, uint offset, IVariable dst)
        {
			if (dst.Type.TypeKind == TypeKind.ValueType) return;
            var ptgId = new PTGID(new MethodContex(this.method), (int)offset);

            var node = this.NewNode(ptg, ptgId, dst.Type);

            ptg.RemoveEdges(dst);
            ptg.PointsTo(dst, node);
        }

        private void ProcessCopy(PointsToGraph ptg, IVariable dst, IVariable src)
        {
			if (dst.Type.TypeKind == TypeKind.ValueType || src.Type.TypeKind == TypeKind.ValueType) return;

            ptg.RemoveEdges(dst);
            var targets = ptg.GetTargets(src);

            foreach (var target in targets)
            {
                ptg.PointsTo(dst, target);
            }
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
                    if (MayReacheableFromParameter(ptg, node))
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

		private PTGNode NewNode(PointsToGraph ptg, PTGID ptgID, IType type, PTGNodeKind kind = PTGNodeKind.Object)
		{
			PTGNode node;
            node = ptg.GetNode(ptgID, type, kind);
            return node;
		}
    }
}
