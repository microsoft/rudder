using System;
using System.Collections.Generic;
using System.Linq;
using Backend.ThreeAddressCode.Values;
using Backend.ThreeAddressCode.Instructions;
using Backend.Analysis;
using Backend.Visitors;
using Backend.Utils;
using Microsoft.Cci;
using ScopeAnalyzer.Misc;
using ScopeAnalyzer;
using ScopeAnalyzer.Interfaces;

namespace ScopeAnalyzer.Analyses
{


    /// <summary>
    /// Domain that keeps track of which variables may have escaped.
    /// </summary>
    public class VarEscapeSet : BooleanMapDomain<IVariable>
    {

        private VarEscapeSet(Dictionary<IVariable, Boolean> vs)
        {
            mapping = vs;
        }

        public static VarEscapeSet Bottom(IEnumerable<IVariable> vars)
        {
            Dictionary<IVariable, Boolean> vs = new Dictionary<IVariable, Boolean>();
            foreach (var v in vars)
            {
                vs[v] = false;
            }
            return new VarEscapeSet(vs);
        }

        public static VarEscapeSet Top(IEnumerable<IVariable> vars)
        {
            Dictionary<IVariable, Boolean> vs = new Dictionary<IVariable, bool>();
            foreach (var v in vars)
            {
                vs[v] = true;
            }
            return new VarEscapeSet(vs);
        }

        public void Escape(IVariable v)
        {
            SetTrue(v);
        }

        public void Return(IVariable v)
        {
            SetFalse(v);
        }

        public bool Escaped(IVariable v)
        {
            return IsTrue(v);
        }

        public void SetAllEscaped()
        {
            SetAllTrue();
        }

        public VarEscapeSet Clone()
        {
            return new VarEscapeSet(new Dictionary<IVariable, Boolean>(mapping));
        }

        public override string ToString()
        {
            string summary = "May escape information about variables:\n";
            foreach (var v in mapping.Keys)
            {
                summary += String.Format("\t{0} ({1}): {2}\n", v.Name, v.Type, mapping[v]);
            }
            return summary;
        }
    }


    /// <summary>
    /// Domain that keeps track of which fields may have escaped, fields being fixed.
    /// </summary>
    public class FieldEscapeSet : BooleanMapDomain<IFieldAccess>
    {

        private FieldEscapeSet(Dictionary<IFieldAccess, Boolean> fe)
        {
            mapping = fe;
        }

        public static FieldEscapeSet Bottom(IEnumerable<IFieldAccess> fields)
        {
            Dictionary<IFieldAccess, Boolean> fe = new Dictionary<IFieldAccess, bool>();
            foreach (var f in fields)
            {
                fe[f] = false;
            }
            return new FieldEscapeSet(fe);
        }

        public static FieldEscapeSet Top(IEnumerable<IFieldAccess> fields)
        {
            Dictionary<IFieldAccess, Boolean> fe = new Dictionary<IFieldAccess, bool>();
            foreach (var f in fields)
            {
                fe[f] = true;
            }
            return new FieldEscapeSet(fe);
        }

        public void Escape(IFieldAccess f)
        {
            SetTrue(f);
        }

        public void Return(IFieldAccess f)
        {
            SetFalse(f);
        }

        public bool Escaped(IFieldAccess f)
        {
            return IsTrue(f);
        }

        public void SetAllEscaped()
        {
            SetAllTrue();
        }

        public FieldEscapeSet Clone()
        {
            return new FieldEscapeSet(new Dictionary<IFieldAccess, bool>(mapping));
        }

        public override string ToString()
        {
            string summary = "May escape information about fields:\n";
            foreach (var f in mapping.Keys)
            {
                summary += String.Format("\t{0} ({1}): {2}\n", f.ToExpression().ToString(), f.Type, mapping[f]);
            }
            return summary;
        }

    }

    /// <summary>
    /// Lattice product between VarEscapeSet and FieldEscapeSet.
    /// </summary>
    public class ScopeEscapeDomain
    {
        VarEscapeSet vset;
        FieldEscapeSet fset;

        private ScopeEscapeDomain(VarEscapeSet vs, FieldEscapeSet fs)
        {
            vset = vs;
            fset = fs;
        }


        public static ScopeEscapeDomain Top(IEnumerable<IVariable> vars, IEnumerable<IFieldAccess> fields)
        {
            return new ScopeEscapeDomain(VarEscapeSet.Top(vars), FieldEscapeSet.Top(fields));
        }

        public static ScopeEscapeDomain Bottom(IEnumerable<IVariable> vars, IEnumerable<IFieldAccess> fields)
        {
            return new ScopeEscapeDomain(VarEscapeSet.Bottom(vars), FieldEscapeSet.Bottom(fields));
        }

        public bool IsTop
        {
            get { return vset.IsTop && fset.IsTop; }
        }

        public bool IsBottom
        {
            get { return vset.IsBottom && fset.IsBottom; }
        }

        public VarEscapeSet Variables
        {
            get { return vset; }
        }

        public FieldEscapeSet Fields
        {
            get { return fset; }
        }

        public void Escape(IVariable v)
        {
            vset.Escape(v);
        }

        public void Escape(IFieldAccess f)
        {
            fset.Escape(f);
        }

        public void Return(IVariable v)
        {
            vset.Return(v);
        }

        public void Return(IFieldAccess f)
        {
            fset.Return(f);
        }

        public bool Escaped(IVariable v)
        {
            return vset.Escaped(v);
        }

        public bool Escaped(IFieldAccess f)
        {
            return fset.Escaped(f);
        }

        public void SetAllEscaped()
        {
            vset.SetAllEscaped();
            fset.SetAllEscaped();
        }

        public void Join(ScopeEscapeDomain sed)
        {
            var nvset = vset.Clone();        
            nvset.Join(sed.Variables);
            vset = nvset;

            var nfset = fset.Clone();
            nfset.Join(sed.Fields);
            fset = nfset;
        }


        public override bool Equals(object obj)
        {
            var other = obj as ScopeEscapeDomain;
            return other.Variables.Equals(vset) && other.Fields.Equals(fset);
        }

        public ScopeEscapeDomain Clone()
        {
            var v = vset.Clone();
            var f = fset.Clone();
            return new ScopeEscapeDomain(v, f);
        }

        public override string ToString()
        {
            return fset.ToString() + "\n" + vset.ToString();
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }
    }


    /*
     * This is a very naive implementation of an escape analysis particularly targeted for Scope Reducer/Processor
     * methods. We implicitly assume that all reference values may be escaped so we don't need to keep track of them. 
     * The only exception are the variables of type Row/Rowset. We know that special "this" Row(set) fields do not 
     * escape upon entering the method. That being said, the analysis assumes that all other fields and array elements
     * may escape and overapproximates the escapage of Row(set) variables and the mentioned fields. 
     * Currently, we assume all Row(set) variables may alias. TODO: include aliasing information.
     */
    public class NaiveScopeMayEscapeAnalysis : ForwardDataFlowAnalysis<ScopeEscapeDomain>
    {
        IMethodDefinition method;
        IMetadataHost host;
        List<ITypeDefinition> rowTypes;
        List<ITypeDefinition> rowsetTypes;

        Dictionary<Instruction, ScopeEscapeDomain> preResults = new Dictionary<Instruction, ScopeEscapeDomain>();
        Dictionary<Instruction, ScopeEscapeDomain> postResults = new Dictionary<Instruction, ScopeEscapeDomain>();
        IEnumerable<Tuple<IFieldAccess, IFieldReference>> fieldsToTrack;
        IEnumerable<IVariable> varsToTrack; 
      
        bool unsupported = false;
        bool interestingRowEscaped = false;


        public NaiveScopeMayEscapeAnalysis(ControlFlowGraph cfg, IMethodDefinition m, IMetadataHost h, List<ITypeDefinition> rowtype, List<ITypeDefinition> rowsettype) : base(cfg)
        {
            method = m;
            host = h;

            rowTypes = rowtype;
            rowsetTypes = rowsettype;

            Initialize();
        }


        public IMetadataHost Host
        {
            get { return host; }
        }

        public bool Unsupported
        {
            get { return unsupported; }
        }

        public Dictionary<Instruction, ScopeEscapeDomain> PreResults
        {
            get { return preResults; }
        }

        public Dictionary<Instruction, ScopeEscapeDomain> PostResults
        {
            get { return preResults; }
        }

        public List<ITypeDefinition> RowTypes
        {
            get { return rowTypes; }
        }

        public IEnumerable<IFieldAccess> TrackedFields
        {
            get { return fieldsToTrack.Select(t => t.Item1).AsEnumerable(); }
        }

        public IEnumerable<IVariable> TrackedVariables
        {
            get { return varsToTrack; }
        }

        /// <summary>
        /// Tells if some Row(ish) type escaped at any point.
        /// </summary>
        public bool InterestingRowEscaped
        {
            get { return interestingRowEscaped; }
        }


        private void Initialize()
        {
            var fieldDefinitions = new List<IFieldDefinition>();
            var mtype = (method.ContainingType as INamedTypeReference).Resolve(host);

            IFieldDefinition env = null;
            // Now we find fields to track.
            foreach (var field in mtype.Fields)
            {
                // Skip this, as it references an escaped "environment".
                if (field.Type.Resolve(host).Equals((mtype as INestedTypeDefinition).ContainingType.Resolve(host)))
                {
                    if (env == null)
                    {
                        env = field;
                    }
                    else
                    {
                        unsupported = true;
                        Utils.WriteLine("WARNING: too many closure environments found!");
                    }
                }

                if (!field.IsStatic && PossiblyRow(field.Type)) fieldDefinitions.Add(field);
            }

            if (env == null)
            {
                unsupported = true;
                Utils.WriteLine("WARNING: no closure environment found!");
            }

            /*
             * We keep track of the syntactic field accesses that correspond to field definitions of the enclosing
             * closure class. These acceses are assumed to be safe. Note that this is fine since generated closure
             * class is singleton: every processor is assigned a different closure class. Hence there won't be any
             * other accesses that correspond to closure field definitions and that can be potentialy unsafe.
             */
            var frefs = cfg.FieldAccesses();
            fieldsToTrack = frefs.Where(f => f.Item2 != null && fieldDefinitions.Contains(f.Item2.Resolve(host))).AsEnumerable();

            var vars = cfg.GetVariables();
            varsToTrack = vars.Where(v => PossiblyRow(v.Type)).ToList();

            var instructions = new List<Instruction>();
            foreach (var block in cfg.Nodes)
                instructions.AddRange(block.Instructions);

            if (instructions.Any(i => i is ThrowInstruction || i is CatchInstruction))
                unsupported = true;
        }

        private void UpdateResults(EscapeTransferVisitor visitor)
        {
            interestingRowEscaped |= visitor.SomeRowEscaped;

            foreach (var key in visitor.PreStates.Keys)
            {
                preResults[key] = visitor.PreStates[key];
            }

            foreach (var key in visitor.PostStates.Keys)
            {
                postResults[key] = visitor.PostStates[key];
            }
        }


        #region Checking for a row

        public bool PossiblyRow(ITypeReference type)
        {
            // when type is unknown.
            if (type == null)
                return true;

            foreach (var rt in rowTypes)
            {
                if (PossiblyRow(type, rt, host)) return true;
            }

            foreach (var rst in rowsetTypes)
            {
                if (PossiblyRow(type, rst, host)) return true;
            }
            return false;
        }

        public bool PossiblyRow(ITypeReference type, ITypeDefinition rowishType, IMetadataHost host)
        {
            if (type.IsEnum || type.IsValueType) return false;
            if (type.IncludesType(rowishType, host)) return true;

            return false;
        }

        #endregion checking for a row


        #region Dataflow interface implementation

        protected override ScopeEscapeDomain InitialValue(CFGNode node)
        {
            if (unsupported)
            {
                return ScopeEscapeDomain.Top(varsToTrack, fieldsToTrack.Select(f=> f.Item1).ToList());
            }
            else
            {
                return ScopeEscapeDomain.Bottom(varsToTrack, fieldsToTrack.Select(f => f.Item1).ToList());
            }
        }

        protected override ScopeEscapeDomain Join(ScopeEscapeDomain left, ScopeEscapeDomain right)
        {
            var join = left.Clone();
            join.Join(right);
            return join;
        }

        protected override ScopeEscapeDomain Flow(CFGNode node, ScopeEscapeDomain input)
        {
            var nState = input.Clone();
            var visitor = new EscapeTransferVisitor(nState, this);
            visitor.Visit(node);
            UpdateResults(visitor);
            return visitor.State.Clone();
        }

        protected override bool Compare(ScopeEscapeDomain left, ScopeEscapeDomain right)
        {
            return left.Equals(right);
        }

        #endregion


     

        /* 
        * All reference variables except Row/Rowset are assumed to be escaped. A Row(set) variable is set as escaped if (1) 
        * it is assigned to a static variable, (2) it is assigned to a field, (3) it is passed as a parameter to a foreign 
        * method or it is a result of such a method, or (4) it is being assigned by an escaped Row(set) variable/field. 
        * Hence, the computed escape variables should all be of type Row. However, sometimes the actual types are not 
        * available so we safely add such variables as escaped, just to be sure.
        */
        class EscapeTransferVisitor : InstructionVisitor
        {
            ScopeEscapeDomain currentState;
            NaiveScopeMayEscapeAnalysis parent;

            Dictionary<Instruction, ScopeEscapeDomain> preState = new Dictionary<Instruction, ScopeEscapeDomain>();
            Dictionary<Instruction, ScopeEscapeDomain> postState = new Dictionary<Instruction, ScopeEscapeDomain>();

            bool someRowEscaped = false;

            public EscapeTransferVisitor(ScopeEscapeDomain start, NaiveScopeMayEscapeAnalysis dad)
            {
                SetCurrent(start);
                parent = dad;
            }



            #region Transfer functions

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

            public override void Visit(DefinitionInstruction instruction)
            {
                Default(instruction);
            }

            public override void Default(Instruction instruction)
            {
                SavePreState(instruction, FreshCurrent());
                SetCurrent(FreshCurrent());
                SavePostState(instruction, FreshCurrent());
            }

            public override void Visit(StoreInstruction instruction)
            {
                SavePreState(instruction, FreshCurrent());

                var nstate = FreshCurrent();
                var result = instruction.Result;
                var operand = instruction.Operand;

                if (PossiblyRow(operand.Type))
                {
                    if (result is InstanceFieldAccess)
                    {             
                        var r = result as InstanceFieldAccess;
                        if (!IsFieldTracked(r))
                        {
                            SetEscaped(nstate, operand, instruction);
                        }
                    }
                    else
                    {
                        SetEscaped(nstate, operand, instruction);
                    }
                }

                SetCurrent(nstate);
                SavePostState(instruction, FreshCurrent());
            }

            public override void Visit(LoadInstruction instruction)
            {
                #region deprecated
                //SavePreState(instruction, FreshCurrent());
                //var nstate = FreshCurrent();
                //var result = instruction.Result;
                //var operand = instruction.Operand;

                //if (PossiblyRow(result.Type))
                //{
                //    // If the operand has anything to do with (de)referencing a pointer, then we set
                //    // everything as escaped.
                //    if (operand is Dereference || operand is Reference)
                //    {
                //        SetAllEscaped(nstate);
                //    }
                //    else if (operand is InstanceFieldAccess)
                //    {
                //        var op = operand as InstanceFieldAccess;
                //        if (!IsFieldTracked(op.Field))
                //        {
                //            SetEscaped(nstate, result, instruction, false);
                //        }
                //        else
                //        {
                //            ProjectState(nstate, result, op.Field, false);

                //            //if (nstate.Escaped(op.Field))
                //            //    SetEscaped(nstate, result, instruction);
                //            //else if (nstate.Escaped(result))
                //            //    SetEscaped(nstate, op.Field, instruction);
                //        }
                //    }
                //    // Set results as escaped in this case
                //    else if (operand is UnknownValue || operand is StaticFieldAccess || operand is ArrayElementAccess)
                //    {
                //        SetEscaped(nstate, result, instruction, false);
                //    }
                //    // TODO: should this case even occur? Typing should not allow this.
                //    else if (operand is VirtualMethodReference || operand is StaticMethodReference)
                //    {
                //        SetEscaped(nstate, result, instruction, false);
                //    }
                //    // We add result as escaped only if the operand variable is escaped too.
                //    else if (operand is IVariable)
                //    {
                //        var op = operand as IVariable;
                //        ProjectState(nstate, result, op, false);
                //    }
                //    // Other cases either don't occur (expressions) at this code layer or don't do much.                
                //}

                //SetCurrent(nstate);
                //SavePostState(instruction, FreshCurrent());
                #endregion
                Default(instruction);
            }


            public override void Visit(MethodCallInstruction instruction)
            {
                if (IsClearlySafe(instruction))
                {
                    SavePreState(instruction, FreshCurrent());
                    SetCurrent(FreshCurrent());
                    SavePostState(instruction, FreshCurrent());
                }
                else
                {
                    VisitMethodInvocation(instruction, instruction.Result, instruction.Arguments, instruction.HasResult, instruction.Method.IsStatic);
                }
            }

            public override void Visit(IndirectMethodCallInstruction instruction)
            {
                VisitMethodInvocation(instruction, instruction.Result, instruction.Arguments, instruction.HasResult, instruction.Function.IsStatic);
            }

            private void VisitMethodInvocation(Instruction instruction, IVariable result, IList<IVariable> arguments, bool hasResult, bool isStatic)
            {
                SavePreState(instruction, FreshCurrent());
                var nstate = FreshCurrent();

                var beginIndex = isStatic ? 0 : 1; // to avoid "this".
                bool escaped = false;
                for (int i = beginIndex; i < arguments.Count; i++)
                {
                    var v = arguments[i];
                    if (PossiblyRow(v.Type))
                    {
                        escaped = true;
                        SetEscaped(nstate, v, instruction);
                    }
                }

                //if (hasResult && PossiblyRow(result.Type))
                //{
                //    escaped = true;
                //    SetEscaped(nstate, result, instruction);
                //}

                if (escaped && !isStatic && PossiblyRow(arguments[0].Type))
                {
                    SetEscaped(nstate, arguments[0], instruction);
                }

                SetCurrent(nstate);
                SavePostState(instruction, FreshCurrent());
            }

            private bool IsClearlySafe(MethodCallInstruction instruction)
            {
                //TODO: can this be done better?
                var method = instruction.Method;
                var mtype = method.ContainingType;

                if (mtype.FullName() == "ScopeRuntime.Row" || mtype.FullName() == "ScopeRuntime.RowSet"
                    || mtype.FullName() == "ScopeRuntime.ColumnData")
                    return true;

                if (instruction.Arguments.Count != 1)
                    return false;

                // TODO: can we do this better?
                var name = mtype.NestedName();
                if (name.StartsWith("IEnumerator<") || name.StartsWith("IEnumerable<") ||
                    name.StartsWith("List<") || name.StartsWith("HashSet<") ||
                    name.StartsWith("Dictionary<") || name.StartsWith("Queue<") ||
                    name.StartsWith("Stack<") || name.StartsWith("LinkedList<") ||
                    name.StartsWith("SortedList<") || name.StartsWith("SortedSet<"))
                    return true;

                //if (method.Name.Value == "get_Current" && instruction.Arguments.Count == 1
                //    && method.ContainingType.FullName() == "System.Collections.Generic.IEnumerator<ScopeRuntime.Row>")
                //    return true;

                //if (method.Name.Value == "get_Rows" && instruction.Arguments.Count == 1
                //    && method.ContainingType.FullName() == "ScopeRuntime.RowSet")
                //    return true;

                //if (method.Name.Value == "GetEnumerator" && instruction.Arguments.Count == 1
                //    && method.ContainingType.FullName() == "System.Collections.Generic.IEnumerable<ScopeRuntime.Row>")
                //    return true;

                //if (method.ContainingType.FullName() == "ScopeRuntime.Row" &&
                //    (method.Name.Value == "CopyTo" || method.Name.Value == "CopyScopeCEPStatusTo"))
                //    return true;

                return false;
            }




            public override void Visit(CopyMemoryInstruction instruction)
            {
                SavePreState(instruction, FreshCurrent());
                var nstate = FreshCurrent();
                SetAllEscaped(nstate);
                SetCurrent(nstate);
                SavePostState(instruction, FreshCurrent());
            }

            public override void Visit(CopyObjectInstruction instruction)
            {
                SavePreState(instruction, FreshCurrent());
                var nstate = FreshCurrent();
                SetAllEscaped(nstate);
                SetCurrent(nstate);
                SavePostState(instruction, FreshCurrent());
            }

            public override void Visit(LocalAllocationInstruction instruction)
            {
                SavePreState(instruction, FreshCurrent());
                var nstate = FreshCurrent();
                SetAllEscaped(nstate);
                SetCurrent(nstate);
                SavePostState(instruction, FreshCurrent());
            }

            public override void Visit(ConvertInstruction instruction)
            {
                #region deprecated
                //SavePreState(instruction, FreshCurrent());
                //var nstate = FreshCurrent();
                
                //if (instruction.HasResult && PossiblyRow(instruction.Result.Type))
                //{
                //    ProjectState(nstate, instruction.Result, instruction.Operand);
                //}

                //SetCurrent(nstate);
                //SavePostState(instruction, FreshCurrent());
                #endregion

                Default(instruction);
            }

            public override void Visit(InitializeMemoryInstruction instruction)
            {
                SavePreState(instruction, FreshCurrent());
                var nstate = FreshCurrent();
                
                if (PossiblyRow(instruction.Value.Type))
                    SetEscaped(nstate, instruction.Value, instruction);

                SetCurrent(nstate);
                SavePostState(instruction, FreshCurrent());
            }

            public override void Visit(PhiInstruction instruction)
            {
                #region deprecated
                //SavePreState(instruction, FreshCurrent());
                //var nstate = FreshCurrent();

                //if (instruction.HasResult && PossiblyRow(instruction.Result.Type))
                //{
                //    foreach (var v in instruction.Arguments)
                //    {
                //        if (nstate.Escaped(v))
                //        {
                //            SetEscaped(nstate, instruction.Result, instruction);
                //            break;
                //        }
                //    }
                //}

                //SetCurrent(nstate);
                //SavePostState(instruction, FreshCurrent());
                #endregion
                Default(instruction);
            }

            #endregion


            private bool IsFieldTracked(IFieldAccess field)
            {
                if (parent.TrackedFields.Contains(field)) return true;

                // sanity
                foreach (var f in parent.TrackedFields)
                {
                    if (f.ToExpression().ToString() == field.ToExpression().ToString()) return true;
                }

                return false;
            }



            private void SetEscaped(ScopeEscapeDomain state, IVariable v, Instruction instruction)
            {
                someRowEscaped = true;
                state.SetAllEscaped();
                // TODO: refine the above with aliasing information.
            }


            private void SetAllEscaped(ScopeEscapeDomain state)
            {
                someRowEscaped = true;
                state.SetAllEscaped();
                // TODO: refine the above with aliasing information.
            }




            private bool PossiblyRow(ITypeReference type)
            {
                return parent.PossiblyRow(type);
            }


            public bool SomeRowEscaped
            {
                get { return someRowEscaped; }
            }

            public ScopeEscapeDomain State
            {
                get { return currentState; }
                set { currentState = value; }
            }

            private void SetCurrent(ScopeEscapeDomain state)
            {
                State = state;
            }

            private ScopeEscapeDomain FreshCurrent()
            {
                return currentState.Clone();
            }

            private void SavePreState(Instruction instruction, ScopeEscapeDomain state)
            {
                preState[instruction] = state;

            }

            private void SavePostState(Instruction instruction, ScopeEscapeDomain state)
            {
                postState[instruction] = state;
            }

            public Dictionary<Instruction, ScopeEscapeDomain> PostStates
            {
                get { return postState; }
            }

            public Dictionary<Instruction, ScopeEscapeDomain> PreStates
            {
                get { return preState; }
            }
        }

    }
}

