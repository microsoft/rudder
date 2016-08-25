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
    /// Domain that keeps track of which variables may have escaped, variables being fixed..
    /// </summary>
    public class VarEscapeDomain : BooleanMapDomain<IVariable>
    {

        private VarEscapeDomain(Dictionary<IVariable, Boolean> vs)
        {
            mapping = vs;
        }

        public static VarEscapeDomain Bottom(IEnumerable<IVariable> vars)
        {
            Dictionary<IVariable, Boolean> vs = new Dictionary<IVariable, Boolean>();
            foreach (var v in vars)
            {
                vs[v] = false;
            }
            return new VarEscapeDomain(vs);
        }

        public static VarEscapeDomain Top(IEnumerable<IVariable> vars)
        {
            Dictionary<IVariable, Boolean> vs = new Dictionary<IVariable, bool>();
            foreach (var v in vars)
            {
                vs[v] = true;
            }
            return new VarEscapeDomain(vs);
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

        public VarEscapeDomain Clone()
        {
            return new VarEscapeDomain(new Dictionary<IVariable, Boolean>(mapping));
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
    public class FieldEscapeDomain : BooleanMapDomain<IFieldAccess>
    {

        private FieldEscapeDomain(Dictionary<IFieldAccess, Boolean> fe)
        {
            mapping = fe;
        }

        public static FieldEscapeDomain Bottom(IEnumerable<IFieldAccess> fields)
        {
            Dictionary<IFieldAccess, Boolean> fe = new Dictionary<IFieldAccess, bool>();
            foreach (var f in fields)
            {
                fe[f] = false;
            }
            return new FieldEscapeDomain(fe);
        }

        public static FieldEscapeDomain Top(IEnumerable<IFieldAccess> fields)
        {
            Dictionary<IFieldAccess, Boolean> fe = new Dictionary<IFieldAccess, bool>();
            foreach (var f in fields)
            {
                fe[f] = true;
            }
            return new FieldEscapeDomain(fe);
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

        public FieldEscapeDomain Clone()
        {
            return new FieldEscapeDomain(new Dictionary<IFieldAccess, bool>(mapping));
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
    /// Lattice product between VarEscapeDomain and FieldEscapeDomain. Array accesses
    /// are not tracked.
    /// </summary>
    public class ScopeEscapeDomain
    {
        VarEscapeDomain vset;
        FieldEscapeDomain fset;

        private ScopeEscapeDomain(VarEscapeDomain vs, FieldEscapeDomain fs)
        {
            vset = vs;
            fset = fs;
        }


        public static ScopeEscapeDomain Top(IEnumerable<IVariable> vars, IEnumerable<IFieldAccess> fields)
        {
            return new ScopeEscapeDomain(VarEscapeDomain.Top(vars), FieldEscapeDomain.Top(fields));
        }

        public static ScopeEscapeDomain Bottom(IEnumerable<IVariable> vars, IEnumerable<IFieldAccess> fields)
        {
            return new ScopeEscapeDomain(VarEscapeDomain.Bottom(vars), FieldEscapeDomain.Bottom(fields));
        }

        public bool IsTop
        {
            get { return vset.IsTop && fset.IsTop; }
        }

        public bool IsBottom
        {
            get { return vset.IsBottom && fset.IsBottom; }
        }

        public VarEscapeDomain Variables
        {
            get { return vset; }
        }

        public FieldEscapeDomain Fields
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

     
      
    /// <summary>
    /// This is a very naive implementation of an escape analysis particularly targeted for Scope Reducer/Processor/Combiner
    /// closure methods.We implicitly assume that all reference values may escape so we don't need to keep track of them. The only 
    /// exception are the variables related to types Row and Rowset. We know that special closure Row(set) related fields do 
    /// not escape upon entering the method.That being said, the analysis assumes that all other fields (and array elements)
    /// may escape and overapproximates the escapage of Row(set) related variables and the mentioned closure fields.
    /// Currently, we assume all Row(set) variables may alias. Implementation does not rely on object sharing, yet it makes 
    /// object operations as pure as possible.
    /// </summary>
    public class NaiveScopeMayEscapeAnalysis : ForwardDataFlowAnalysis<ScopeEscapeDomain>
    {
        IMethodDefinition method;
        IMetadataHost host;
        List<ITypeDefinition> rowTypes;
        List<ITypeDefinition> rowsetTypes;

        // Analysis results for each instruction, before and after the instruction is executed.
        Dictionary<Instruction, ScopeEscapeDomain> preResults = new Dictionary<Instruction, ScopeEscapeDomain>();
        Dictionary<Instruction, ScopeEscapeDomain> postResults = new Dictionary<Instruction, ScopeEscapeDomain>();

        // Remember which variables and fields we track during the analysis.
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
        /// Tells if some Row(ish) type object escaped at any point.
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
                        // This should never happen, given how closure is generated. TODO: remove this check.
                        unsupported = true;
                        Utils.WriteLine("WARNING: too many closure environments found!");
                    }
                }
                // Save row related closure field.
                if (!field.IsStatic && PossiblyRow(field.Type)) fieldDefinitions.Add(field);
            }

            if (env == null)
            {
                //Sometimes environment is not used so it will not appear in the closure class.
                Utils.WriteLine("WARNING: no closure environment found!");
            }

            // We keep track of the syntactic field accesses that correspond to row field definitions
            // of the enclosing closure class. These acceses are assumed to not escape at the beginning.
            // Note that this is fine since generated closure class is singleton: every processor is 
            // assigned a different closure class. Hence there won't be any other accesses that correspond
            // to closure field definitions and that can be potentialy unsafe.
            var frefs = cfg.FieldAccesses();
            fieldsToTrack = frefs.Where(f => f.Item2 != null && fieldDefinitions.Contains(f.Item2.Resolve(host))).AsEnumerable();

            var vars = cfg.GetVariables();
            varsToTrack = vars.Where(v => PossiblyRow(v.Type)).ToList();

            // We currently do not support exception handling.
            var instructions = new List<Instruction>();
            foreach (var block in cfg.Nodes)
                instructions.AddRange(block.Instructions);

            if (instructions.Any(i => i is ThrowInstruction || i is CatchInstruction))
                unsupported = true;
        }


        /// <summary>
        /// Updates the results of the analysis.
        /// </summary>
        /// <param name="visitor"></param>
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


        #region Checking for a Row

        /// <summary>
        /// Conservatively is given type related to Row, i.e., is it a subtype of Row,
        /// a collection of Rows, and so forth.
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        public bool PossiblyRow(ITypeReference type)
        {
            // when type is unknown.
            if (type == null)
                return true;

            foreach (var rt in rowTypes)
            {
                if (PossiblyRow(type, rt, host)) return true;
            }

            // Rowset is a custom collection of rows, so we check it as well.
            foreach (var rst in rowsetTypes)
            {
                if (PossiblyRow(type, rst, host)) return true;
            }
            return false;
        }


        /// <summary>
        /// Check if a type is related to Row or Rowset. Check if type is subtype of
        /// rowishType, is it a collection of rowishType, a pointer to a rowishType,
        /// a generic instantiated by a rowishType, etc.
        /// </summary>
        /// <param name="type"></param>
        /// <param name="rowishType"></param>
        /// <param name="host"></param>
        /// <returns></returns>
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

        /// <summary>
        /// Performs transfer function on a block represent by node.
        /// </summary>
        /// <param name="node"></param>
        /// <param name="input"></param>
        /// <returns></returns>
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


        /// <summary>
        /// All reference variables except Row/Rowset are assumed to be escaped. A Row(set) variable is set as escaped if (1) 
        /// it is assigned to a static variable, (2) it is assigned to a field, (3) it is passed as a parameter to a foreign
        /// method or it is a result of such a method, or(4) it is being assigned by an escaped Row(set) variable / field.
        /// Hence, the computed escape variables should all be of type Row.However, sometimes the actual types are not
        /// available so we safely add such variables as escaped, just to be sure.
        /// </summary>
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
                    // Make row escaped if it is tracked and is set to non-tracked field,
                    // static field, and any sort of reference/dereferences.
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



            public override void Visit(MethodCallInstruction instruction)
            {
                // We don't classify rows as escaped if they are passed
                // to whitelisted trusted methods.
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
                // Make every argument row (except receiver) escaped.
                for (int i = beginIndex; i < arguments.Count; i++)
                {
                    var v = arguments[i];
                    if (PossiblyRow(v.Type))
                    {
                        escaped = true;
                        if (!someRowEscaped && !parent.InterestingRowEscaped) Utils.WriteLine("ESCAPE by method call: " + instruction.ToString());
                        SetEscaped(nstate, v, instruction);
                    }
                }

                // If some argument has escaped, we make say receiver had escaped as well.
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

                var name = mtype.NestedName();
                if (name.StartsWith("IEnumerator<") || name.StartsWith("IEnumerable<") ||
                    name.StartsWith("List<") || name.StartsWith("HashSet<") ||
                    name.StartsWith("Dictionary<") || name.StartsWith("Queue<") ||
                    name.StartsWith("Stack<") || name.StartsWith("LinkedList<") ||
                    name.StartsWith("SortedList<") || name.StartsWith("SortedSet<"))
                    return true;

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



            public override void Visit(InitializeMemoryInstruction instruction)
            {
                SavePreState(instruction, FreshCurrent());
                var nstate = FreshCurrent();
                
                if (PossiblyRow(instruction.Value.Type))
                    SetEscaped(nstate, instruction.Value, instruction);

                SetCurrent(nstate);
                SavePostState(instruction, FreshCurrent());
            }


            #endregion


            /// <summary>
            /// Check whether a field access corresponds to a field we track.
            /// </summary>
            /// <param name="field"></param>
            /// <returns></returns>
            private bool IsFieldTracked(IFieldAccess field)
            {
                if (parent.TrackedFields.Contains(field)) return true;

                // for sanity
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

