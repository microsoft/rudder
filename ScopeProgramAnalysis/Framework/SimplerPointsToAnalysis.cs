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
        public class PTAVisitor : InstructionVisitor
        {
            public  PointsToGraph State { get; set;  }
            private IteratorPointsToAnalysis ptAnalysis;
            private bool analyzeNextDelegateCtor;

            /// <summary>
            /// Hack until I add support from addresses
            /// </summary>
            private Dictionary<IVariable, IValue> addressMap = new Dictionary<IVariable, IValue>();

            internal PTAVisitor(PointsToGraph ptg, IteratorPointsToAnalysis ptAnalysis)
            {
                this.State = ptg;
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
                    if(!(referencedValue is IVariable))
                    { }
                    addressMap[instruction.Result] = referencedValue;
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
                        ptAnalysis.ProcessNull(State, load.Result);
                    }
                }
                if (operand is IVariable)
                {
                    var variable = operand as IVariable;
                    ptAnalysis.ProcessCopy(State, load.Result, variable);
                }
                else if (operand is InstanceFieldAccess)
                {
                    var access = operand as InstanceFieldAccess;
                    ptAnalysis.ProcessLoad(State, load.Offset, load.Result, access.Instance, access.Field);
                }
                else if(operand is StaticFieldAccess)
                {
                    var access = operand as StaticFieldAccess;
                    ptAnalysis.ProcessLoad(State, load.Offset, load.Result,  IteratorPointsToAnalysis.GlobalVariable, access.Field);

                }
                else if(operand is ArrayElementAccess)
                {
                    var arrayAccess = operand as ArrayElementAccess;
                    var baseArray = arrayAccess.Array;
                    ptAnalysis.ProcessLoad(State, load.Offset, load.Result, baseArray, new FieldReference("[]", operand.Type, this.ptAnalysis.method.ContainingType));
                }
                else if (operand is VirtualMethodReference)
                {
                    var loadDelegateStmt = operand as VirtualMethodReference;
                    var methodRef = loadDelegateStmt.Method;
                    var instance = loadDelegateStmt.Instance;
                    ptAnalysis.ProcessDelegateAddr(State, load.Offset, load.Result, methodRef, instance);

                }
                else if (operand is StaticMethodReference)
                {
                    var loadDelegateStmt = operand as StaticMethodReference;
                    var methodRef = loadDelegateStmt.Method;
                    ptAnalysis.ProcessDelegateAddr(State, load.Offset, load.Result, methodRef, null);
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
                var lhs= store.Result;
                if (lhs is InstanceFieldAccess)
                {
                    var access = lhs as InstanceFieldAccess;
                    ptAnalysis.ProcessStore(State, instruction.Offset, access.Instance, access.Field, store.Operand);
                }
                else if(lhs is StaticFieldAccess)
                {
                    var access = lhs as StaticFieldAccess;
                    ptAnalysis.ProcessStore(State, instruction.Offset, IteratorPointsToAnalysis.GlobalVariable, access.Field, store.Operand);
                }
                else if (lhs is ArrayElementAccess)
                {
                    var arrayAccess = lhs as ArrayElementAccess;
                    var baseArray = arrayAccess.Array;
                    ptAnalysis.ProcessStore(State, instruction.Offset, baseArray, new FieldReference("[]", lhs.Type, this.ptAnalysis.method.ContainingType), store.Operand);
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
                    ptAnalysis.ProcessObjectAllocation(State, allocation.Offset, allocation.Result);
                }
            }
            public override void Visit(CreateArrayInstruction instruction)
            {
                var allocation = instruction;
                ptAnalysis.ProcessArrayAllocation(State, allocation.Offset, allocation.Result);
            }
            public override void Visit(InitializeMemoryInstruction instruction)
            {
                var addr = instruction.TargetAddress;
                //ptAnalysis.ProcessArrayAllocation(State, allocation.Offset, allocation.Result);
            }
            public override void Visit(InitializeObjectInstruction instruction)
            {
                var addr = instruction.TargetAddress;
                ptAnalysis.ProcessObjectAllocation(State, instruction.Offset, addr);
                if(addressMap.ContainsKey(addr))
                {
                    var value = addressMap[addr];
                    if (value is IVariable)
                    {
                        ptAnalysis.ProcessCopy(State, value as IVariable, addr);
                    }
                    if(value is InstanceFieldAccess)
                    {
                        var fieldAccess = value as InstanceFieldAccess;
                        ptAnalysis.ProcessStore(State, instruction.Offset, fieldAccess.Instance, fieldAccess.Field , addr);
                    }
                }
            }
            public override void Visit(ConvertInstruction instruction)
            {
                var convertion = instruction as ConvertInstruction;
                ptAnalysis.ProcessCopy(State, convertion.Result, convertion.Operand);
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
                        State.RemoveRootEdges(methodCall.Arguments[0]);
                        if (methodCall.Arguments.Count == 3)
                        {
                            // instance delegate
                            foreach (var dn in State.GetTargets(methodCall.Arguments[2]).OfType<DelegateNode>())
                            {
                                dn.Instance = methodCall.Arguments[1];
                                State.PointsTo(methodCall.Arguments[0], dn);
                            }
                        }
                        else
                        {
                            foreach (var dn in State.GetTargets(methodCall.Arguments[1]).OfType<DelegateNode>())
                            {
                                State.PointsTo(methodCall.Arguments[0], dn);
                            }
                        }
                    }
                }
            }

            public override void Visit(PhiInstruction instruction)
            {
                ptAnalysis.ProcessCopy(State, instruction.Result, instruction.Arguments);
            }
            public override void Visit(ReturnInstruction instruction)
            {
                if (instruction.HasOperand)
                {
                    var rv = ptAnalysis.ReturnVariable;
                    ptAnalysis.ProcessCopy(State, rv, instruction.Operand);
                }
            }

        }

        private PointsToGraph initialGraph;
        private MethodDefinition method;
        public IVariable ReturnVariable { get; private set; }
        public static IVariable GlobalVariable = new LocalVariable("$Global") { Type = PlatformTypes.Object };  //   { get; private set; }
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
       
        public PointsToGraph GetInitialValue()
        {
            if (this.initPTG != null)
            {
                return this.initPTG;
            }
            return this.initialGraph; // .Clone();
        }

        protected override PointsToGraph InitialValue(CFGNode node)
        {
            if (this.cfg.Entry.Id == node.Id && this.initPTG != null)
            {
                return this.initPTG;
            }
            return this.initialGraph; // .Clone();
        }

        public override DataFlowAnalysisResult<PointsToGraph>[] Analyze()
        {
            Result = base.Analyze();
            return Result;
        }
        
        protected override bool Compare(PointsToGraph left, PointsToGraph right)
        {
            return left.GraphLessEquals(right);
        }

        protected override PointsToGraph Join(PointsToGraph left, PointsToGraph right)
        {
            //var result = new PointsToGraph();
            //result.Union(left);

            //var result = left.ShalowClone();
            //result.Union(right);
            
            var result = left.Join(right);
            return result;
        }

        protected override PointsToGraph Flow(CFGNode node, PointsToGraph input)
        {
            var ptg = input.Clone();

            var ptaVisitor = new PTAVisitor(ptg, this);
            ptaVisitor.Visit(node);

            //foreach (var instruction in node.Instructions)
            //{
            //    this.Flow(ptg, instruction as Instruction);
            //}

            return ptaVisitor.State;
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

            //IteratorPointsToAnalysis.GlobalVariable= new LocalVariable("$Global");
            //IteratorPointsToAnalysis.GlobalVariable.Type = PlatformTypes.Object;

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
                if (variable.Type==null || !variable.Type.IsClassOrStruct()) continue;
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
            ptg.Add(IteratorPointsToAnalysis.GlobalVariable);
            ptg.PointsTo(IteratorPointsToAnalysis.GlobalVariable, PointsToGraph.GlobalNode);
			this.initialGraph = ptg;
		}

		private void ProcessNull(PointsToGraph ptg, IVariable dst)
		{
            ptg.RemoveRootEdges(dst);

            if (!dst.Type.IsClassOrStruct()) return;
            ptg.PointsTo(dst, PointsToGraph.NullNode);
		}

        private void ProcessObjectAllocation(PointsToGraph ptg, uint offset, IVariable dst)
		{
            ptg.RemoveRootEdges(dst);
            if (!dst.Type.IsClassOrStruct()) return;
            var ptgId = new PTGID(new MethodContex(this.method), (int)offset);

            var node = this.NewNode(ptg, ptgId, dst.Type);

            ptg.PointsTo(dst, node);
        }

		internal void ProcessArrayAllocation(PointsToGraph ptg, uint offset, IVariable dst)
        {
            ptg.RemoveRootEdges(dst);

            if (!dst.Type.IsClassOrStruct()) return;
            var ptgId = new PTGID(new MethodContex(this.method), (int)offset);

            var node = this.NewNode(ptg, ptgId, dst.Type);

            ptg.PointsTo(dst, node);
        }

        internal void ProcessCopy(PointsToGraph ptg, IVariable dst, IEnumerable<IVariable> srcs)
        {
            ptg.RemoveRootEdges(dst);

            if (!dst.Type.IsClassOrStruct()) return;

            var targets = srcs.Where(src => src.Type.IsClassOrStruct()).SelectMany(src =>  ptg.GetTargets(src, false));

            foreach (var target in targets)
            {
                ptg.PointsTo(dst, target);
            }
        }
        internal void ProcessCopy(PointsToGraph ptg, IVariable dst, IVariable src)
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

		public void ProcessLoad(PointsToGraph ptg, uint offset, IVariable dst, IVariable instance, IFieldReference field)
        {
			if (!dst.Type.IsClassOrStruct()|| !field.Type.IsClassOrStruct()) return;
            // TODO: I need to support value types when they are Structs..
            if (!instance.Type.IsClassOrStruct()) return;

            ptg.RemoveRootEdges(dst);
			var nodes = ptg.GetTargets(instance, false);
            foreach (var node in nodes)
            {
                var hasField = node.Targets.ContainsKey(field);

                if (!hasField)
				{
                    // ptg.PointsTo(node, access.Field, ptg.Null);
                    if (!ptg.Equals(PointsToGraph.GlobalNode) && !MayReacheableFromParameter(ptg, node))
                    {
                        System.Console.WriteLine("In {0}:{1:X4} there is variable that is not a parameter and has no object to load.", this.method.ToSignatureString(), offset);
                    }

                    {
                        var ptgId = new PTGID(new MethodContex(this.method), (int)offset);
                        // TODO: Should be a LOAD NODE
                        // Preventive assignement of a new Node unknown (should be only for parameters)
                        var target = this.NewNode(ptg, ptgId, dst.Type, PTGNodeKind.Unknown);
                        ptg.PointsTo(node, field, target);
                    }
                }

                var targets = node.Targets[field];

                foreach (var target in targets)
                {
                    ptg.PointsTo(dst, target);
                }
            }
        }

        public PTGNode CreateSummaryForCollection(PointsToGraph ptg, uint offset, IVariable collectionVariable)
        {
            var ptgId = new PTGID(new MethodContex(this.method), (int)offset);
            var collectionNode = this.NewNode(ptg, ptgId, collectionVariable.Type);
            var itemNode = this.NewNode(ptg, ptgId, collectionVariable.Type);
            var itemsField = new FieldReference("$item", PlatformTypes.Object, this.method.ContainingType);
            ptg.PointsTo(collectionNode, itemsField, itemNode);

            return collectionNode;
        }

        public IFieldReference AddItemforCollection(PointsToGraph ptg, uint offset, IVariable collectionVariable, IVariable item)
        {
            var itemsField = new FieldReference("$item", PlatformTypes.Object, this.method.ContainingType);
            this.ProcessStore(ptg, offset, collectionVariable, itemsField, item);
            return itemsField;
        }

        public IFieldReference GetItemforCollection(PointsToGraph ptg, uint offset , IVariable collectionVariable, IVariable result)
        {
            var itemsField = new FieldReference("$item", PlatformTypes.Object, this.method.ContainingType);
            this.ProcessLoad(ptg, offset, result, collectionVariable, itemsField);
            return itemsField;
        }

        public PTGNode ProcessGetEnum(PointsToGraph ptg, uint offset, IVariable collectionVariable, IVariable result)
        {
            var ptgId = new PTGID(new MethodContex(this.method), (int)offset);

            var enumNode = this.NewNode(ptg, ptgId, PlatformTypes.IEnumerator);
            ptg.RemoveRootEdges(result);
            ptg.PointsTo(result, enumNode);

            var nodes = ptg.GetTargets(collectionVariable);
            if(nodes.Count==1 && nodes.Single()==PointsToGraph.NullNode)
            {
                var collectionNode = new PTGNode(ptgId, collectionVariable.Type);
                ptg.PointsTo(collectionVariable, collectionNode);
            }

            var collecitonField = new FieldReference("$collection", PlatformTypes.Object, this.method.ContainingType);
            this.ProcessStore(ptg, offset, result,  collecitonField, collectionVariable);
            //foreach (var colNode in ptg.GetTargets(collectionVariable))
            //{
            //    ptg.PointsTo(enumNode, collecitonField, colNode);
            //}
            return enumNode;
        }

        public IEnumerable<PTGNode> ProcessGetCurrent(PointsToGraph ptg, uint offset, IVariable enumVariable, IVariable result)
        {
            var targets = new HashSet<PTGNode>();
            // get Collection
            var collectionField = new FieldReference("$collection", PlatformTypes.Object, this.method.ContainingType);
            var collectionNodes = ptg.GetTargets(enumVariable, collectionField);

            if (collectionNodes.Any())
            {
                var itemsField = new FieldReference("$item", PlatformTypes.Object, this.method.ContainingType);
                targets.AddRange(collectionNodes.SelectMany(n => ptg.GetTargets(n, itemsField)));
            }
            ptg.RemoveRootEdges(result);
            ptg.PointsTo(result, targets);
            return targets;
        }


        private bool MayReacheableFromParameter(PointsToGraph ptg, PTGNode n)
        {
            var result = method.Body.Parameters.Where(p => ptg.Reachable(p,n)).Any();
            // This version does not need the inverted mapping of nodes-> variables (which may be expensive to maintain)
            // var result = method.Body.Parameters.Any(p =>ptg.GetTargets(p).Contains(n));
            return result;
        }

        public void ProcessStore(PointsToGraph ptg, uint offset, IVariable instance, IFieldReference field, IVariable src)
        {
			if (!field.Type.IsClassOrStruct() || !src.Type.IsClassOrStruct()) return;

			var nodes = ptg.GetTargets(instance, false);
            var targets = ptg.GetTargets(src).Except(new HashSet<PTGNode>() { PointsToGraph.NullNode });

            if (targets.Any())
            {
                foreach (var node in nodes)
                {
                    foreach (var target in targets)
                    {
                        ptg.PointsTo(node, field, target);
                    }
                }
            }
            else
            {
                // Create a fake node for the target 
                var ptgID = new PTGID(new MethodContex(this.method), (int)offset);
                var fakeNode = new PTGNode(ptgID, src.Type);
                foreach (var node in nodes)
                {
                    ptg.PointsTo(node, field, fakeNode);
                }
            }
        }

        protected void ProcessDelegateAddr(PointsToGraph ptg, uint offset, IVariable dst, IMethodReference methodRef, IVariable instance)
        {
            var ptgID = new PTGID(new MethodContex(this.method), (int)offset);
            var delegateNode = new DelegateNode(ptgID, methodRef, instance);
            ptg.Add(delegateNode);
            ptg.RemoveRootEdges(dst);
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
