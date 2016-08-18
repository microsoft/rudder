using System;
using System.Collections.Generic;
using System.Linq;
using Backend.ThreeAddressCode.Values;
using Backend.ThreeAddressCode.Instructions;
using Backend.Analysis;
using Backend.Visitors;
using Backend.Utils;
using Microsoft.Cci;
using ScopeAnalyzer.Interfaces;
using ScopeAnalyzer.Misc;

namespace ScopeAnalyzer.Analyses
{
    public class ConstantSetDomain : SetDomain<Constant>
    {
        private ConstantSetDomain(List<Constant> cons)
        {
            elements = cons;
        }

        public static ConstantSetDomain Top
        {
            get { return new ConstantSetDomain(null); }
        }

        public static ConstantSetDomain Bottom
        {
            get { return new ConstantSetDomain(new List<Constant>()); }
        }

        public void SetNotConstant()
        {
            base.SetTop();
        }

        public ConstantSetDomain Clone()
        {
            var ncons = elements == null ? null : new List<Constant>(elements);
            return new ConstantSetDomain(ncons);
        }

        public override string ToString()
        {
            if (IsTop) return "Not a constant";
            if (IsBottom) return "Not known";
            string summary = "Constants:";
            foreach(var c in elements)
            {
                summary += "\t" + c.ToString();
            }
            return summary;
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }
    }

    public class ConstantPropagationDomain
    {
        Dictionary<IVariable, ConstantSetDomain> varMapping;
        Dictionary<IFieldAccess, ConstantSetDomain> fieldMapping;

        private ConstantPropagationDomain()
        {
            varMapping = new Dictionary<IVariable, ConstantSetDomain>();
            fieldMapping = new Dictionary<IFieldAccess, ConstantSetDomain>();
        }

        public static ConstantPropagationDomain Top(IEnumerable<IVariable> vars, IEnumerable<IFieldAccess> fields)
        {
            var cpd = new ConstantPropagationDomain();
            foreach(var v in vars)
            {
                cpd.Set(v, ConstantSetDomain.Top);
            }

            foreach(var f in fields)
            {
                cpd.Set(f, ConstantSetDomain.Top);
            }

            return cpd;
        }

        public static ConstantPropagationDomain Bottom(IEnumerable<IVariable> vars, IEnumerable<IFieldAccess> fields)
        {
            var cpd = new ConstantPropagationDomain();
            foreach (var v in vars)
            {
                cpd.Set(v, ConstantSetDomain.Bottom);
            }

            foreach (var f in fields)
            {
                cpd.Set(f, ConstantSetDomain.Bottom);
            }

            return cpd;
        }

        public bool IsTop
        {
            get { return varMapping.Values.All(v => v.IsTop) && fieldMapping.Values.All(f => f.IsTop); }
        }

        public bool IsBottom
        {
            get { return varMapping.Values.All(v => v.IsBottom) && fieldMapping.Values.All(f => f.IsBottom); }
        }

        public int VarCount
        {
            get { return varMapping.Count; }
        }

        public int FieldCount
        {
            get { return fieldMapping.Count; }
        }
   
              
        public ConstantPropagationDomain Clone()
        {
            var clone = new ConstantPropagationDomain();
            foreach(var v in varMapping.Keys)
            {
                clone.Set(v, varMapping[v].Clone());
            }
            foreach (var f in fieldMapping.Keys)
            {
                clone.Set(f, fieldMapping[f].Clone());
            }

            return clone;
        }

        public bool Contains(IVariable v)
        {
            return varMapping.ContainsKey(v);
        }

        public bool Contains(IVariable v, Constant c)
        {
            return varMapping[v].Contains(c);
        }

        public bool Contains(IVariable v, ConstantSetDomain cpd)
        {
            return varMapping[v].Contains(cpd);
        }

        public ConstantSetDomain Constants(IVariable v)
        {
            return varMapping[v];
        }

        public bool Contains(IFieldAccess f)
        {
            return fieldMapping.ContainsKey(f);
        }

        public bool Contains(IFieldAccess f, Constant c)
        {
            return fieldMapping[f].Contains(c);
        }

        public bool Contains(IFieldAccess f, ConstantSetDomain cpd)
        {
            return fieldMapping[f].Contains(cpd);
        }

        public ConstantSetDomain Constants(IFieldAccess f)
        {
            return fieldMapping[f];
        }


        public void Join(ConstantPropagationDomain other)
        {
            if (VarCount != other.VarCount) throw new IncompatibleConstantPropagationDomains("Not the same variable set!");

            for(int i = 0; i < varMapping.Keys.Count; i++)
            {
                var v = varMapping.Keys.ElementAt(i);
                if (!other.Contains(v)) throw new IncompatibleConstantPropagationDomains("Not the same variable set! " + v.ToString());
                var ncsd = Constants(v).Clone();
                ncsd.Join(other.Constants(v));
                Set(v, ncsd);
            }

            if (FieldCount != other.FieldCount) throw new IncompatibleConstantPropagationDomains("Not the same field set!");

            for (int i = 0; i < fieldMapping.Keys.Count; i++)
            {
                var f = fieldMapping.Keys.ElementAt(i);
                if (!other.Contains(f)) throw new IncompatibleConstantPropagationDomains("Not the same field set! " + f.ToString());
                var ncsd = Constants(f).Clone();
                ncsd.Join(other.Constants(f));
                Set(f, ncsd);
            }
        }


        public void Set(IVariable v, ConstantSetDomain cons)
        {
            varMapping[v] = cons;
        }

        public void Set(IFieldAccess f, ConstantSetDomain cons)
        {
            fieldMapping[f] = cons;
        }
         
        
        public IReadOnlyCollection<IVariable> Variables
        {
            get { return varMapping.Keys.ToList().AsReadOnly(); }
        }

        public IReadOnlyCollection<IFieldAccess> Fields
        {
            get { return fieldMapping.Keys.ToList().AsReadOnly(); }
        }

        public void SetNonConstant(IVariable v)
        {
            Constants(v).SetNotConstant();
        }

        public void SetNonConstant(IFieldAccess f)
        {
            Constants(f).SetNotConstant();
        }

        public void SetNonConstant()
        {
            SetFieldNonConstant();
            SetVarNonConstant();      
        }

        public void SetFieldNonConstant()
        {
            foreach (var f in fieldMapping.Keys)
            {
                SetNonConstant(f);
            }
        }

        public void SetVarNonConstant()
        {
            foreach (var v in varMapping.Keys)
            {
                SetNonConstant(v);
            }
        }


        public override bool Equals(object obj)
        {
            if (obj == this) return true;

            var other = obj as ConstantPropagationDomain;

            if (VarCount != other.VarCount) return false;
            if (FieldCount != other.FieldCount) return false;

            foreach (var v in varMapping.Keys)
            {
                if (!other.Contains(v)) return false;
                if (!other.Constants(v).Equals(Constants(v))) return false;
            }

            foreach (var f in fieldMapping.Keys)
            {
                if (!other.Contains(f)) return false;
                if (!other.Constants(f).Equals(Constants(f))) return false;
            }

            return true;
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }

        public override string ToString()
        {
            string summary = String.Empty;
            foreach(var v in varMapping.Keys)
            {
                summary += String.Format("{0} ({1}): {2}\n", v.Name, (v.Type == null ? "unknown" : v.Type.FullName()), varMapping[v].ToString());
            }
            summary += "\n";
            foreach (var f in fieldMapping.Keys)
            {
                summary += String.Format("{0} ({1}): {2}\n", f.Name, (f.Type == null ? "unknown" : f.Type.FullName()), fieldMapping[f].ToString());
            }
            return summary;
        }

        public string Summary()
        {
            string summary = String.Empty;
            foreach (var v in varMapping.Keys)
            {
                if (varMapping[v].IsBottom || varMapping[v].IsTop) continue;

                summary += String.Format("{0} ({1}): {2}\n", v.Name, (v.Type == null ? "unknown" : v.Type.FullName()), varMapping[v].ToString());
            }
            summary += "\n";
            foreach (var f in fieldMapping.Keys)
            {
                if (fieldMapping[f].IsBottom || fieldMapping[f].IsTop) continue;

                summary += String.Format("{0} ({1}): {2}\n", f.Name, (f.Type == null ? "unknown" : f.Type.FullName()), fieldMapping[f].ToString());
            }
            return summary;
        }

        public class IncompatibleConstantPropagationDomains : Exception
        {
            public IncompatibleConstantPropagationDomains(string message) : base(message)
            {

            }
        }
    }



    class ConstantPropagationSetAnalysis : ForwardDataFlowAnalysis<ConstantPropagationDomain>
    {
        IMethodDefinition method;
        IEnumerable<IVariable> variables;
        IEnumerable<Tuple<IFieldAccess, IFieldReference>> fields;
        IEnumerable<IFieldDefinition> fieldDefinitions;
        IEnumerable<ITypeDefinition> schemaTypes;

        IMetadataHost host;

        bool unsupported = false;

        Dictionary<Instruction, ConstantPropagationDomain> preResults = new Dictionary<Instruction, ConstantPropagationDomain>();
        Dictionary<Instruction, ConstantPropagationDomain> postResults = new Dictionary<Instruction, ConstantPropagationDomain>();

        public ConstantPropagationSetAnalysis(ControlFlowGraph cfg, IMethodDefinition m, IMetadataHost h, IEnumerable<ITypeDefinition> schemas) : base(cfg)
        {
            method = m;
            host = h;
            schemaTypes = schemas;

            Initialize();
        }

        private void Initialize()
        {
            var vars = cfg.GetVariables();
            variables = vars.Where(v => IsConstantType(v.Type, host)).ToList();

            var fs = cfg.FieldAccesses();
            fields = fs.Where(f => f.Item2 != null && IsConstantType(f.Item1.Type, host)).ToList();

            var fdefs = new List<IFieldDefinition>();
            var mtype = (method.ContainingType as INamedTypeReference).Resolve(host);

            // Now we find all constant closure fields.
            foreach (var field in mtype.Fields)
            {
                if (IsConstantType(field.Type, host))
                    fdefs.Add(field);
            }
            fieldDefinitions = fdefs;


            var instructions = new List<Instruction>();
            foreach (var block in cfg.Nodes)
                instructions.AddRange(block.Instructions);

            if (instructions.Any(i => i is ThrowInstruction || i is CatchInstruction))
                unsupported = true;
        }


        public IMetadataHost Host
        {
            get { return host; }
        }

        public bool Unsupported
        {
            get { return unsupported; }
        }

        public Dictionary<Instruction, ConstantPropagationDomain> PreResults
        {
            get { return preResults; }
        }

        public Dictionary<Instruction, ConstantPropagationDomain> PostResults
        {
            get { return postResults; }
        }


        public bool IsSchema(ITypeReference tref)
        {
            return schemaTypes.Any(s => tref.SubtypeOf(s, host));
        }

        public IEnumerable<Tuple<IFieldAccess, IFieldReference>> Fields
        {
            get { return fields; }
        }

        public IEnumerable<IFieldDefinition> ClosureFieldDefinitions
        {
            get { return fieldDefinitions; }
        }


        #region Dataflow interface implementation

        protected override bool Compare(ConstantPropagationDomain left, ConstantPropagationDomain right)
        {
            return left.Equals(right);
        }

        protected override ConstantPropagationDomain Flow(CFGNode node, ConstantPropagationDomain input)
        {
            var nState = input.Clone();
            var visitor = new ConstantPropagationTransferVisitor(nState, this);
            visitor.Visit(node);
            UpdateResults(visitor);
            return visitor.State.Clone();
        }


        protected override ConstantPropagationDomain InitialValue(CFGNode node)
        {
            if (unsupported)
            {
                return ConstantPropagationDomain.Top(variables, fields.Select(f => f.Item1));
            }
            else
            {
                var iv = ConstantPropagationDomain.Bottom(variables, fields.Select(f => f.Item1));
                //iv.SetFieldNonConstant();
                return iv;
            }          
        }

        protected override ConstantPropagationDomain Join(ConstantPropagationDomain left, ConstantPropagationDomain right)
        {
            var join = left.Clone();
            join.Join(right);
            return join;
        }

        #endregion


        private void UpdateResults(ConstantPropagationTransferVisitor visitor)
        {
            foreach (var key in visitor.PreStates.Keys)
            {
                preResults[key] = visitor.PreStates[key];
            }

            foreach (var key in visitor.PostStates.Keys)
            {
                postResults[key] = visitor.PostStates[key];
            }
        }

        public static bool IsConstantType(ITypeReference tref, IMetadataHost host)
        {
            // when type is unknown.
            if (tref == null)
                return true;

            var t = tref;
            while (t.IsAlias) t = t.AliasForType.AliasedType;

            var type = t.Resolve(host);
            return IsConstantType(type);
        }

        public static bool IsConstantType(ITypeDefinition type)
        {
            if (type.IsEnum || type.IsGeneric || type.IsAbstract || (type.IsStruct && !type.IsValueType) || type.IsComObject ||
                type.IsDummy() || type.IsDelegate || type.IsInterface || type.IsRuntimeSpecial)
                return false;

            if (!(type is INamedTypeReference))
                return false;
            
            var nmtype = type as INamedTypeReference;
            while (nmtype.IsAlias)
                nmtype = nmtype.AliasForType.AliasedType;

            if (!type.IsValueType && !(type.IsReferenceType && nmtype.FullName() == "System.String"))
                return false;

            return true;
        }


        class ConstantPropagationTransferVisitor : InstructionVisitor
        {
            ConstantPropagationSetAnalysis parent;
            ConstantPropagationDomain current;

            Dictionary<Instruction, ConstantPropagationDomain> preState = new Dictionary<Instruction, ConstantPropagationDomain>();
            Dictionary<Instruction, ConstantPropagationDomain> postState = new Dictionary<Instruction, ConstantPropagationDomain>();

            public ConstantPropagationTransferVisitor (ConstantPropagationDomain start, ConstantPropagationSetAnalysis dad)
            {
                parent = dad;
                SetCurrent(start);
            }

            public Dictionary<Instruction, ConstantPropagationDomain> PostStates
            {
                get { return postState; }
            }

            public Dictionary<Instruction, ConstantPropagationDomain> PreStates
            {
                get { return preState; }
            }

            public ConstantPropagationDomain State
            {
                get { return current; }
            }


            public override void Visit(IInstructionContainer container)
            {
                preState.Clear();
                postState.Clear();
                base.Visit(container);
            }

            public override void Visit(Instruction instruction)
            {
                Default(instruction);
            }

            public override void Default(Instruction instruction)
            {
                SavePreState(instruction, FreshCurrent());
                SetCurrent(FreshCurrent());
                SavePostState(instruction, FreshCurrent());
            }

            private void DefaultTop(Instruction instruction)
            {
                SavePreState(instruction, FreshCurrent());
                var nstate = FreshCurrent();
                UpdateStateNotConstant(nstate);
                SetCurrent(nstate);
                SavePostState(instruction, FreshCurrent());
            }

            private void DefaultVarTop(Instruction instruction, IVariable v)
            {
                SavePreState(instruction, FreshCurrent());
                var nstate = FreshCurrent();

                if (IsConstantType(v.Type))
                {
                    UpdateStateNotConstant(nstate, v);
                }
                SetCurrent(nstate);
                SavePostState(instruction, FreshCurrent());
            }



            public override void Visit(CopyMemoryInstruction instruction)
            {
                DefaultTop(instruction);
            }

            public override void Visit(CopyObjectInstruction instruction)
            {
                DefaultTop(instruction);
            }

            public override void Visit(LocalAllocationInstruction instruction)
            {
                DefaultTop(instruction);
            }

            public override void Visit(InitializeMemoryInstruction instruction)
            {
                DefaultTop(instruction);
            }

            public override void Visit(BinaryInstruction instruction)
            {
                //TODO: very imprecise, can we do it better?
                DefaultVarTop(instruction, instruction.Result);
            }

            public override void Visit(ConvertInstruction instruction)
            {
                //TODO: very imprecise, can we do it better?
                DefaultVarTop(instruction, instruction.Result);
            }

            public override void Visit(UnaryInstruction instruction)
            {
                //TODO: very imprecise, can we do it better?
                DefaultVarTop(instruction, instruction.Result);
            }

            public override void Visit(MethodCallInstruction instruction)
            {
                if (instruction.HasResult)
                {
                    if (IsSchemaGetItem(instruction))
                    {
                        SavePreState(instruction, FreshCurrent());
                        var nstate = FreshCurrent();
                        UpdateStateCopy(nstate, instruction.Result, instruction.Arguments.ElementAt(1));
                        SetCurrent(nstate);
                        SavePostState(instruction, FreshCurrent());                     
                    }
                    else
                    {
                        DefaultVarTop(instruction, instruction.Result);
                    }
                }
                else
                {
                    Default(instruction);
                }
            }


            private bool IsSchemaGetItem(MethodCallInstruction instruction)
            {
                if (!instruction.HasResult || instruction.Arguments.Count != 2)
                    return false;

                if (!IsConstantType(instruction.Arguments.ElementAt(1).Type))
                    return false;

                if (instruction.Method.Name.Value != "get_Item")
                    return false;

                if (!parent.IsSchema(instruction.Method.ContainingType))
                    return false;

                return true;
            }


            public override void Visit(IndirectMethodCallInstruction instruction)
            {
                if (instruction.HasResult)
                {
                    DefaultVarTop(instruction, instruction.Result);
                }
                else
                {
                    Default(instruction);
                }
            }


            public override void Visit(LoadInstruction instruction)
            {
                SavePreState(instruction, FreshCurrent());
                var nstate = FreshCurrent();

                var operand = instruction.Operand;
                var result = instruction.Result;
             
                if(IsConstantType(result.Type))
                {
                    if (operand is Constant)
                    {
                        var c = operand as Constant;
                        UpdateState(nstate, result, c);
                    }
                    else if (operand is IVariable)
                    {
                        var v = operand as IVariable;
                        UpdateStateCopy(nstate, result, v);
                    }
                    else if (operand is InstanceFieldAccess)
                    {
                        var ifa = operand as InstanceFieldAccess;
                        UpdateStateCopy(nstate, result, ifa);
                    }
                    else if (operand is StaticFieldAccess)
                    {
                        var sfa = operand as StaticFieldAccess;
                        UpdateStateCopy(nstate, result, sfa);
                    }
                    else if (operand is Dereference || operand is Reference)
                    {
                        UpdateStateNotConstant(nstate, result);
                    }
                    else
                    {
                        UpdateStateNotConstant(nstate, result);
                    }                  
                }

                SetCurrent(nstate);
                SavePostState(instruction, FreshCurrent());
            }

            public override void Visit(StoreInstruction instruction)
            {
                SavePreState(instruction, FreshCurrent());
                var nstate = FreshCurrent();

                var operand = instruction.Operand;
                var result = instruction.Result;
                if (IsConstantType(result.Type))
                {                  
                    if (result is InstanceFieldAccess)
                    {
                        var ifa = result as InstanceFieldAccess;
                        UpdateStateCopy(nstate, ifa, operand);
                    }
                    else if (result is StaticFieldAccess)
                    {
                        var sfa = result as InstanceFieldAccess;
                        UpdateStateCopy(nstate, sfa, operand);
                    }
                    else if (result is Dereference || result is Reference)
                    {
                        UpdateStateNotConstant(nstate);
                    }
                    //TODO: see other assignable values
                }

                SetCurrent(nstate);
                SavePostState(instruction, FreshCurrent());
            }          


            public override void Visit(PhiInstruction instruction)
            {
                SavePreState(instruction, FreshCurrent());
                var nstate = FreshCurrent();

                var result = instruction.Result;
                if (IsConstantType(result.Type))
                {
                    var consargs = instruction.Arguments.Where(v => IsConstantType(v.Type)).ToList();
                    if (consargs.Count == 0)
                    {
                        UpdateStateNotConstant(nstate, result);
                    }
                    else if (consargs.All(v => !nstate.Constants(v).IsTop))
                    {
                        var cpd = ConstantSetDomain.Bottom;
                        foreach(var v in consargs)
                        {
                            if (!IsConstantType(v.Type)) continue;

                            cpd.Join(nstate.Constants(v));
                        }
                        nstate.Set(result, cpd);
                    }
                    else
                    {
                        UpdateStateNotConstant(nstate, result);
                    }
                }

                SetCurrent(nstate);
                SavePostState(instruction, FreshCurrent());
            }

            public override void Visit(SizeofInstruction instruction)
            {
                SavePreState(instruction, FreshCurrent());
                var nstate = FreshCurrent();

                if (instruction.HasResult && IsConstantType(instruction.Result.Type))
                {
                    UpdateStateNotConstant(nstate, instruction.Result);
                }

                SetCurrent(nstate);
                SavePostState(instruction, FreshCurrent());
            }

            

            private void SetCurrent(ConstantPropagationDomain curr)
            {
                current = curr;
            }

            private void SavePreState(Instruction instruction, ConstantPropagationDomain cpd)
            {
                preState[instruction] = cpd;
            }

            private void SavePostState(Instruction instruction, ConstantPropagationDomain cpd)
            {
                postState[instruction] = cpd;
            }

            private ConstantPropagationDomain FreshCurrent()
            {
                return current.Clone();
            }



            private void UpdateState(ConstantPropagationDomain state, IVariable v, Constant c)
            {
                var cpd = ConstantSetDomain.Bottom;
                cpd.Add(c);
                state.Set(v, cpd);
            }


            private void UpdateStateNotConstant(ConstantPropagationDomain state, IVariable v)
            {
                state.SetNonConstant(v);
            }

            private void UpdateStateNotConstant(ConstantPropagationDomain state, IFieldAccess f)
            {
                state.SetNonConstant(GetAccess(f));
            }

            private void UpdateStateNotConstant(ConstantPropagationDomain state)
            {
                state.SetNonConstant();
            }

            private void UpdateStateCopy(ConstantPropagationDomain state, IVariable dest, IVariable src)
            {
                var cl = state.Constants(src).Clone();
                state.Set(dest, cl);
            }

            private void UpdateStateCopy(ConstantPropagationDomain state, IFieldAccess dest, IVariable src)
            {
                //var cl = state.Constants(src).Clone();
                //state.Set(dest, cl);

                /*
                 * When we set a field of an object O to a constant, we also need to set the same field
                 * to objects that are must aliased to O. We know that all field accesses corresponding
                 * to closure fields must alias each other since they are compiler generated. That is,
                 * a user cannot instantiate such class nor does the compiler instantiate it inside the class itself.
                 */
               
                var faccess = GetAccessRef(dest);
                IFieldDefinition fdef = faccess.Item2.Resolve(parent.Host);

                // We know that closure field accesses must alias each other.
                if (IsClosureField(fdef))
                {
                    foreach (var pair in parent.Fields)
                    {
                        var fresolved = pair.Item2.Resolve(parent.Host);

                        if (fresolved == fdef || fresolved.Equals(fdef))
                        {
                            state.Set(GetAccess(pair.Item1), state.Constants(src).Clone());
                        }
                    }
                }
                else
                {
                    // TODO: refine this by type of the field.
                    state.SetFieldNonConstant();
                }
            }

            private void UpdateStateCopy(ConstantPropagationDomain state, IVariable dest, IFieldAccess src)
            {
                var cl = state.Constants(GetAccess(src)).Clone();
                state.Set(dest, cl);
            }

            private bool IsClosureField(IFieldDefinition fdef)
            {
                foreach(var fd in parent.ClosureFieldDefinitions)
                {
                    if (fd == fdef || fd.Equals(fdef))
                        return true;
                }

                return false;
            }

            /// <summary>
            /// Same field accesses can have different objects associated.
            /// This function gets that object that is used in "state."
            /// </summary>
            /// <param name="f"></param>
            /// <returns></returns>
            private IFieldAccess GetAccess(IFieldAccess f)
            {
                foreach(var pair in parent.Fields)
                {
                    if (f.ToExpression().ToString() == pair.Item1.ToExpression().ToString())
                    {
                        return pair.Item1;
                    }
                }
                return null;
            }

            private Tuple<IFieldAccess, IFieldReference> GetAccessRef(IFieldAccess f)
            {
                foreach (var pair in parent.Fields)
                {
                    if (f.ToExpression().ToString() == pair.Item1.ToExpression().ToString())
                    {
                        return pair;
                    }
                }
                return null;
            }

            private bool IsConstantType(ITypeReference tref)
            {
                return ConstantPropagationSetAnalysis.IsConstantType(tref, parent.Host);
            }
        }
    }
}
