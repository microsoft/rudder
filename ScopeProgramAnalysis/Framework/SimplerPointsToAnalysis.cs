// Copyright (c) Edgardo Zoppi.  All Rights Reserved.  Licensed under the MIT License.  See License.txt in the project root for license information.

using Backend.Utils;
using System.Collections.Generic;
using System.Linq;
using Backend.Model;
using Microsoft.Cci;
using Backend.ThreeAddressCode.Values;
using Backend.Analyses;
using Backend.Visitors;
using Backend.ThreeAddressCode.Instructions;
using System;
using ScopeProgramAnalysis.Framework;

namespace Backend.Analyses
{
    // May Points-To Analysis
    public class SimplePointsToAnalysis : ForwardDataFlowAnalysis<SimplePointsToGraph>
    {
		public class PTAVisitor : InstructionVisitor
		{
			public SimplePointsToGraph State { get; set; }
			private SimplePointsToAnalysis ptAnalysis;
			private bool analyzeNextDelegateCtor;

			/// <summary>
			/// Hack until I add support from addresses
			/// </summary>
			private Dictionary<IVariable, IValue> addressMap = new Dictionary<IVariable, IValue>();

			internal PTAVisitor(SimplePointsToGraph ptg, SimplePointsToAnalysis ptAnalysis)
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
					if (!(referencedValue is IVariable))
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
				else if (operand is StaticFieldAccess)
				{
					var access = operand as StaticFieldAccess;
					ptAnalysis.ProcessLoad(State, load.Offset, load.Result, SimplePointsToAnalysis.GlobalVariable, access.Field);

				}
				else if (operand is ArrayElementAccess)
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
				var lhs = store.Result;
				if (lhs is InstanceFieldAccess)
				{
					var access = lhs as InstanceFieldAccess;
					ptAnalysis.ProcessStore(State, instruction.Offset, access.Instance, access.Field, store.Operand);
				}
				else if (lhs is StaticFieldAccess)
				{
					var access = lhs as StaticFieldAccess;
					ptAnalysis.ProcessStore(State, instruction.Offset, SimplePointsToAnalysis.GlobalVariable, access.Field, store.Operand);
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
					if (allocation.AllocationType.ResolvedType.IsDelegate)
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
				if (addressMap.ContainsKey(addr))
				{
					var value = addressMap[addr];
					if (value is IVariable)
					{
						ptAnalysis.ProcessCopy(State, value as IVariable, addr);
					}
					if (value is InstanceFieldAccess)
					{
						var fieldAccess = value as InstanceFieldAccess;
						ptAnalysis.ProcessStore(State, instruction.Offset, fieldAccess.Instance, fieldAccess.Field, addr);
					}
				}
			}
			public override void Visit(ConvertInstruction instruction)
			{
				ptAnalysis.ProcessCopy(State, instruction.Result, instruction.Operand);
			}
			public override void Visit(MethodCallInstruction instruction)
			{
				var methodCall = instruction as MethodCallInstruction;
				// Hack for mapping delegates to nodes
				if (methodCall.Method.Name.Value == ".ctor" && this.analyzeNextDelegateCtor)
				{
					ProcessDelegateCtor(methodCall);
					this.analyzeNextDelegateCtor = false;
				}
				if (methodCall.Method.ContainingType.GetName().Contains("JsonConvert"))
				{
					ProcessJsonCall(methodCall);
				}
			}

			private void ProcessJsonCall(MethodCallInstruction methodCall)
			{
				if (methodCall.Method.Name.Value == "DeserializeObject")
				{
					ptAnalysis.ProcessJSonAlloc(State, methodCall.Offset, methodCall.Result);
				}
			}


			private void ProcessDelegateCtor(MethodCallInstruction methodCall)
            {
                if (methodCall.Arguments.Any())
                {
                    var arg0Type = methodCall.Arguments[0].Type;
                    if (arg0Type.ResolvedType.IsDelegate)
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

        private SimplePointsToGraph initialGraph;
        private IMethodDefinition method;
        public IVariable ReturnVariable { get; private set; }
        public static IVariable GlobalVariable; 
        
        public DataFlowAnalysisResult<SimplePointsToGraph>[] Result { get; private set; }
        public IVariable ThisVariable { get; private set; }

        // private IDictionary<string, IVariable> specialFields;
        protected SimplePointsToGraph initPTG;

        public SimplePointsToAnalysis(ControlFlowGraph cfg, IMethodDefinition method) //  IDictionary<string, IVariable> specialFields)
			: base(cfg)
		{
            this.method = method;

            if (GlobalVariable==null)
                GlobalVariable = new LocalVariable("$Global") { Type = method.Type.PlatformType.SystemObject };

            // this.specialFields = specialFields;
            this.CreateInitialGraph();
            
		}

        public SimplePointsToAnalysis(ControlFlowGraph cfg, IMethodDefinition method, SimplePointsToGraph initPTG) : base(cfg)
        {
            this.method = method;
            this.CreateInitialGraph(false, initPTG);
            //this.initialGraph.Union(initPTG);
            this.initPTG = this.initialGraph; // initPTG;

        }
       
        public SimplePointsToGraph GetInitialValue()
        {
            if (this.initPTG != null)
            {
                return this.initPTG;
            }
            return this.initialGraph; // .Clone();
        }

        protected override SimplePointsToGraph InitialValue(CFGNode node)
        {
            if (this.cfg.Entry.Id == node.Id && this.initPTG != null)
            {
                return this.initPTG;
            }
            return this.initialGraph; // .Clone();
        }

        public override DataFlowAnalysisResult<SimplePointsToGraph>[] Analyze()
        {
            Result = base.Analyze();
            return Result;
        }
        
        protected override bool Compare(SimplePointsToGraph left, SimplePointsToGraph right)
        {
            return left.GraphLessEquals(right);
        }

        protected override SimplePointsToGraph Join(SimplePointsToGraph left, SimplePointsToGraph right)
        {
            //var result = new SimplePointsToGraph();
            //result.Union(left);

            //var result = left.ShalowClone();
            //result.Union(right);
            
            var result = left.Join(right);
            return result;
        }

        protected override SimplePointsToGraph Flow(CFGNode node, SimplePointsToGraph input)
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

        //private void Flow(SimplePointsToGraph ptg, Instruction instruction)
        //{
        //    var ptaVisitor = new PTAVisitor(ptg, this);
        //    ptaVisitor.Visit(instruction);
        //}

		private void CreateInitialGraph(bool createNodeForParams = true, SimplePointsToGraph initialGraph = null)
		{
            this.ReturnVariable = new LocalVariable(this.method.Name + "_" + "$RV") { Type = this.method.Type.PlatformType.SystemObject };

            //IteratorPointsToAnalysis.GlobalVariable= new LocalVariable("$Global");
            //IteratorPointsToAnalysis.GlobalVariable.Type = MyLoader.PlatformTypes.SystemObject;

            var ptg = initialGraph==null ? new SimplePointsToGraph() : initialGraph.Clone();

			var variables = cfg.GetVariables();

            var body = MethodBodyProvider.Instance.GetBody(this.method);

            var parameters = body.Parameters;

            if (parameters!=null)
                variables.AddRange(parameters);

            int counter = -1;
            IVariable thisVariable = null;
            SimplePTGNode thisNode = null;
			foreach (var variable in variables)
			{
                // TODO: replace when Egdardo fixes type inferece
                if (variable.Type==null || !variable.Type.IsClassOrStruct()) continue;
                // if (variable.Type.TypeKind == TypeKind.ValueType) continue;

				if (variable.IsParameter)
				{
					var isThisParameter = variable.Name == "this";
					var kind = isThisParameter ? SimplePTGNodeKind.Object : SimplePTGNodeKind.Unknown;
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
                    ptg.PointsTo(variable, SimplePointsToGraph.NullNode);
				}
			}

            //foreach(var specialField in specialFields)
            //{
            //    counter = -1000;
            //    var variable = specialField.Value;
            //    var fieldName =  specialField.Key;
            //    var ptgId = new PTGID(new MethodContex(this.method), counter--);
            //    var node = new SimplePTGNode(ptgId, variable.Type);
            //    ptg.Add(node);
            //    ptg.PointsTo(thisNode, new FieldReference(fieldName, variable.Type, method.ContainingType), node);
            //}
            ptg.Add(this.ReturnVariable);
            ptg.Add(SimplePointsToAnalysis.GlobalVariable);
            ptg.PointsTo(SimplePointsToAnalysis.GlobalVariable, SimplePointsToGraph.GlobalNode);
			this.initialGraph = ptg;
		}

		private void ProcessNull(SimplePointsToGraph ptg, IVariable dst)
		{
            ptg.RemoveRootEdges(dst);

            if (!dst.Type.IsClassOrStruct()) return;
            ptg.PointsTo(dst, SimplePointsToGraph.NullNode);
		}

        private void ProcessObjectAllocation(SimplePointsToGraph ptg, uint offset, IVariable dst)
		{
            ptg.RemoveRootEdges(dst);
            if (!dst.Type.IsClassOrStruct()) return;
            var ptgId = new PTGID(new MethodContex(this.method), (int)offset);

            var node = this.NewNode(ptg, ptgId, dst.Type);

            ptg.PointsTo(dst, node);
        }

		internal void ProcessArrayAllocation(SimplePointsToGraph ptg, uint offset, IVariable dst)
        {
            ptg.RemoveRootEdges(dst);

            if (!dst.Type.IsClassOrStruct()) return;
            var ptgId = new PTGID(new MethodContex(this.method), (int)offset);

            var node = this.NewNode(ptg, ptgId, dst.Type);

            ptg.PointsTo(dst, node);
        }

		private void ProcessJSonAlloc(SimplePointsToGraph ptg, uint offset, IVariable dst)
		{
			ptg.RemoveRootEdges(dst);
			if (!dst.Type.IsClassOrStruct()) return;
			var ptgId = new PTGID(new MethodContex(this.method), (int)offset);

			var node = new JSonNode(ptgId, dst.Type);

			ptg.PointsTo(dst, node);
		}


		internal void ProcessCopy(SimplePointsToGraph ptg, IVariable dst, IEnumerable<IVariable> srcs)
        {
            ptg.RemoveRootEdges(dst);

            if (!dst.Type.IsClassOrStruct()) return;

            var targets = srcs.Where(src => src.Type.IsClassOrStruct()).SelectMany(src =>  ptg.GetTargets(src, false));

            foreach (var target in targets)
            {
                ptg.PointsTo(dst, target);
            }
        }
        internal void ProcessCopy(SimplePointsToGraph ptg, IVariable dst, IVariable src)
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

		public void ProcessLoad(SimplePointsToGraph ptg, uint offset, IVariable dst, IVariable instance, IFieldReference field)
        {
			if (!dst.Type.IsClassOrStruct()|| !field.Type.IsClassOrStruct()) return;
            // TODO: I need to support value types when they are Structs..
            if (!instance.Type.IsClassOrStruct()) return;

            ptg.RemoveRootEdges(dst);
			var nodes = ptg.GetTargets(instance, false);
            foreach (var node in nodes)
            {
                var targets = ptg.GetTargets(node, field);

                var hasField = targets.Any();

                if (!hasField)
				{
                    var reachable = MayReacheableFromParameter(ptg, node);
                    // ptg.PointsTo(node, access.Field, ptg.Null);
                    if (!reachable)
                    {
                        //Console.WriteLine("In {0}:{1:X4}.  Variable {2} field {3} has no object to load and {2} is not a parameter.", 
                        //    this.method.ToString(), offset, instance, field);
                        if(field.Name.Value=="[]")
                        {
                            targets.AddRange(nodes);
                        }
                    }

                    if (reachable)
                    {
                        var ptgId = new PTGID(new MethodContex(this.method), (int)offset);
                        // TODO: Should be a LOAD NODE
                        // Preventive assignement of a new Node unknown (should be only for parameters)
                        var target = this.NewNode(ptg, ptgId, dst.Type, SimplePTGNodeKind.Unknown);
                        ptg.PointsTo(node, field, target);
                    }
                }


                ptg.PointsTo(dst, targets);
            }
        }

		#region Methods that provides summaries for handling collections
		public SimplePTGNode CreateSummaryForCollection(SimplePointsToGraph ptg, uint offset, IVariable collectionVariable)
        {
            var ptgId = new PTGID(new MethodContex(this.method), (int)offset);
            var collectionNode = this.NewNode(ptg, ptgId, collectionVariable.Type);
            //var itemNode = this.NewNode(ptg, ptgId, collectionVariable.Type);
            //var itemsField = new FieldReference("$item", MyLoader.PlatformTypes.SystemObject, this.method.ContainingType);
            //ptg.PointsTo(collectionNode, itemsField, itemNode);

            return collectionNode;
        }

        public IFieldReference AddItemforCollection(SimplePointsToGraph ptg, uint offset, IVariable collectionVariable, IVariable item)
        {
            var itemsField = new FieldReference("$item", MyLoader.PlatformTypes.SystemObject, this.method.ContainingType);
            this.ProcessStore(ptg, offset, collectionVariable, itemsField, item);
            return itemsField;
        }

        public IFieldReference GetItemforCollection(SimplePointsToGraph ptg, uint offset , IVariable collectionVariable, IVariable result)
        {
            var itemsField = new FieldReference("$item", MyLoader.PlatformTypes.SystemObject, this.method.ContainingType);
            this.ProcessLoad(ptg, offset, result, collectionVariable, itemsField);
            return itemsField;
        }

        public SimplePTGNode ProcessGetEnum(SimplePointsToGraph ptg, uint offset, IVariable collectionVariable, IVariable result)
        {
            var ptgId = new PTGID(new MethodContex(this.method), (int)offset);

            var enumNode = this.NewNode(ptg, ptgId, MyLoader.PlatformTypes.SystemCollectionsIEnumerator);
            ptg.RemoveRootEdges(result);
            ptg.PointsTo(result, enumNode);

            var nodes = ptg.GetTargets(collectionVariable);
            if(nodes.Count==1 && nodes.Single()==SimplePointsToGraph.NullNode)
            {
                var collectionNode = new SimplePTGNode(ptgId, collectionVariable.Type);
                ptg.PointsTo(collectionVariable, collectionNode);
            }

            var collecitonField = new FieldReference("$collection", MyLoader.PlatformTypes.SystemObject, this.method.ContainingType);
            this.ProcessStore(ptg, offset, result,  collecitonField, collectionVariable);
            //foreach (var colNode in ptg.GetTargets(collectionVariable))
            //{
            //    ptg.PointsTo(enumNode, collecitonField, colNode);
            //}
            return enumNode;
        }

        public IEnumerable<SimplePTGNode> ProcessGetCurrent(SimplePointsToGraph ptg, uint offset, IVariable enumVariable, IVariable result, out bool createdNodes)
        {
            createdNodes = false;
            var targets = new HashSet<SimplePTGNode>();
            // get Collection
            var collectionField = new FieldReference("$collection", MyLoader.PlatformTypes.SystemObject, this.method.ContainingType);
            var collectionNodes = ptg.GetTargets(enumVariable, collectionField);

            if (collectionNodes.Any())
            {
                var itemsField = new FieldReference("$item", MyLoader.PlatformTypes.SystemObject, this.method.ContainingType);
                targets.AddRange(collectionNodes.SelectMany(n => ptg.GetTargets(n, itemsField)));
				if (!targets.Any())
				{
					foreach (var collectionNode in collectionNodes)
					{
						var ptgId = new PTGID(new MethodContex(this.method), (int)offset);
						// DIEGODIEGO: I now prefer to use the result type
						// var itemNode = this.NewNode(ptg, ptgId, collectionNode.Type);
						var itemNode = this.NewNode(ptg, ptgId, result.Type);
						ptg.PointsTo(collectionNode, itemsField, itemNode);
						targets.Add(itemNode);
						createdNodes = true;
					}
				}
            }
            ptg.RemoveRootEdges(result);
            ptg.PointsTo(result, targets);
            return targets;
        }
		#endregion


		private bool MayReacheableFromParameter(SimplePointsToGraph ptg, SimplePTGNode n)
        {
            var body = MethodBodyProvider.Instance.GetBody(method);
            var rootNodes = body.Parameters.SelectMany(p => ptg.GetTargets(p)).Union(new HashSet<SimplePTGNode>() { SimplePointsToGraph.GlobalNode });
            var reachable = ptg.ReachableNodes(rootNodes).Contains(n);
            // This version does not need the inverted mapping of nodes-> variables (which may be expensive to maintain)
            // var result = method.Body.Parameters.Any(p =>ptg.GetTargets(p).Contains(n));
            return reachable;
            ;
        }

        public void ProcessStore(SimplePointsToGraph ptg, uint offset, IVariable instance, IFieldReference field, IVariable src)
        {
			if (!field.Type.IsClassOrStruct() || !src.Type.IsClassOrStruct()) return;

			var nodes = ptg.GetTargets(instance, false);
            var targets = ptg.GetTargets(src).Except(new HashSet<SimplePTGNode>() { SimplePointsToGraph.NullNode });

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
                var fakeNode = new SimplePTGNode(ptgID, src.Type);
                foreach (var node in nodes)
                {
                    ptg.PointsTo(node, field, fakeNode);
                }
            }
        }

        protected void ProcessDelegateAddr(SimplePointsToGraph ptg, uint offset, IVariable dst, IMethodReference methodRef, IVariable instance)
        {
            var ptgID = new PTGID(new MethodContex(this.method), (int)offset);
            var delegateNode = new DelegateNode(ptgID, methodRef, instance);
            ptg.Add(delegateNode);
            ptg.RemoveRootEdges(dst);
            ptg.PointsTo(dst, delegateNode);
        }


        private SimplePTGNode NewNode(SimplePointsToGraph ptg, PTGID ptgID, ITypeReference type, SimplePTGNodeKind kind = SimplePTGNodeKind.Object)
		{
			SimplePTGNode node;
            node = new SimplePTGNode(ptgID, type, kind);
            //node = ptg.GetNode(ptgID, type, kind);
            return node;
		}
    }

    internal class Name : IName
    {
        private string name;
        public Name(string name)
        {
            this.name = name;
        }
        public int UniqueKey
        {
            get
            {
                return name.GetHashCode();
            }
        }

        public int UniqueKeyIgnoringCase
        {
            get
            {
                return name.ToLower().GetHashCode();
            }
        }

        public string Value
        {
            get
            {
                return name;
            }
        }
    }


    internal class FieldReference: IFieldReference
    {
        private ITypeReference containingType;
        private ITypeReference type;
        private Name name;
        private bool isStatic;

        public FieldReference(string name, ITypeReference type, ITypeReference containingType, bool isStatic = false)
        {
            this.name = new Name(name);
            this.type = type;
            this.containingType = containingType;
            this.isStatic = isStatic;
        }

        public IEnumerable<ICustomAttribute> Attributes
        {
            get
            {
                throw new NotImplementedException();
            }
        }

        public ITypeReference ContainingType
        {
            get
            {
                return containingType;
            }
        }

        public IEnumerable<ICustomModifier> CustomModifiers
        {
            get
            {
                throw new NotImplementedException();
            }
        }

        public uint InternedKey
        {
            get
            {
                throw new NotImplementedException();
            }
        }

        public bool IsModified
        {
            get
            {
                throw new NotImplementedException();
            }
        }

        public bool IsStatic
        {
            get
            {
                return isStatic;
            }
        }

        public IEnumerable<ILocation> Locations
        {
            get
            {
                throw new NotImplementedException();
            }
        }

        public IName Name
        {
            get
            {
                return name;
            }
        }

        public IFieldDefinition ResolvedField
        {
            get
            {
                throw new NotImplementedException();
            }
        }

        public ITypeDefinitionMember ResolvedTypeDefinitionMember
        {
            get
            {
                throw new NotImplementedException();
            }
        }

        public ITypeReference Type
        {
            get
            {
                return type;
            }
        }

        public void Dispatch(IMetadataVisitor visitor)
        {
            throw new NotImplementedException();
        }

        public void DispatchAsReference(IMetadataVisitor visitor)
        {
            throw new NotImplementedException();
        }
        public override bool Equals(object obj)
        {
            var oth = obj as FieldReference;
            return oth != null && oth.name.Value.Equals(this.name.Value)
                && oth.Type.TypeEquals(this.type)
                && oth.containingType.TypeEquals(this.containingType);
        }
        public override int GetHashCode()
        {
            return this.name.Value.GetHashCode()+this.type.GetHashCode()
                +this.containingType.GetHashCode();
        }
    }
}
