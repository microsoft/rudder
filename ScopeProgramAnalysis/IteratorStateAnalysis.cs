using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Backend.Model;
using Model.ThreeAddressCode.Instructions;
using Model.ThreeAddressCode.Visitor;
using Model.ThreeAddressCode.Values;
using Model.ThreeAddressCode.Expressions;
using Backend.Utils;
using Model.Types;
using Model;
using System.Globalization;
using ScopeProgramAnalysis;
using Backend.Analyses;

namespace ScopeProgramAnalysis
{

    #region Iterator State Analysis (to be completed)
    public class IteratorState
    {
        public enum IteratorInternalState { TOP = -100, BOTTOM = -2, INITIALIZED = -3, CONTINUING = 1, END = -1 };

        public IteratorInternalState IntState = IteratorInternalState.BOTTOM;

        internal IteratorState()
        {
            this.IntState = IteratorInternalState.BOTTOM;
        }
        internal IteratorState(IteratorInternalState intState)
        {
            this.IntState = intState;
        }
        public IteratorState Clone()
        {
            return new IteratorState(this.IntState);
        }
        internal IteratorState Union(IteratorState right)
        {
            var intState = Join(this.IntState, right.IntState);
            return new IteratorState(intState);
        }
        private static IteratorInternalState Join(IteratorInternalState left, IteratorInternalState right)
        {
            IteratorInternalState res = IteratorInternalState.BOTTOM;
            switch (right)
            {
                case IteratorInternalState.BOTTOM:
                    res = left;
                    break;
                case IteratorInternalState.TOP:
                    res = IteratorInternalState.TOP;
                    break;
                default:
                    res = left == right ? left : IteratorInternalState.TOP;
                    break;
            }
            return res;
        }
        public bool LessEqual(IteratorState right)
        {
            var left = this;
            var res = true;
            switch (right.IntState)
            {
                case IteratorInternalState.BOTTOM:
                    res = false;
                    break;
                case IteratorInternalState.TOP:
                    res = true;
                    break;
                default:
                    res = left.IntState == right.IntState ? true : false;
                    break;
            }
            return res;
        }
        public override string ToString()
        {
            return IntState.ToString();
        }
        public override bool Equals(object obj)
        {
            var oth = obj as IteratorState;
            return oth.IntState.Equals(this.IntState);
        }
        public override int GetHashCode()
        {
            return IntState.GetHashCode();
        }
    }

    public class IteratorStateAnalysis : ForwardDataFlowAnalysis<IteratorState>
    {

        internal class MoveNextVisitorForItStateAnalysis : InstructionVisitor
        {
            internal IteratorState State { get; }
            private IDictionary<IVariable, IExpression> equalities;

            internal MoveNextVisitorForItStateAnalysis(IteratorStateAnalysis itAnalysis, IDictionary<IVariable, IExpression> equalitiesMap, IteratorState state)
            {
                this.State = state;
                this.equalities = equalitiesMap;
            }
            public override void Visit(StoreInstruction instruction)
            {
                var storeStmt = instruction;
                if (storeStmt.Result is InstanceFieldAccess)
                {
                    var access = storeStmt.Result as InstanceFieldAccess;
                    if (access.Field.Name == "<>1__state")
                    {
                        State.IntState = (IteratorState.IteratorInternalState)int.Parse(this.equalities.GetValue(storeStmt.Operand).ToString(), CultureInfo.InvariantCulture);
                    }
                }
            }
        }

        private IDictionary<IVariable, IExpression> equalities;
        DataFlowAnalysisResult<PointsToGraph>[] ptgs;

        public IteratorStateAnalysis(ControlFlowGraph cfg, DataFlowAnalysisResult<PointsToGraph>[] ptgs, IDictionary<IVariable, IExpression> equalitiesMap) : base(cfg)
        {
            this.ptgs = ptgs;
            this.equalities = equalitiesMap;
        }

        protected override bool Compare(IteratorState newState, IteratorState oldSTate)
        {
            return newState.LessEqual(oldSTate);
        }

        protected override IteratorState Flow(CFGNode node, IteratorState input)
        {
            var oldInput = input.Clone();
            var visitor = new MoveNextVisitorForItStateAnalysis(this, this.equalities, oldInput);
            visitor.Visit(node);
            return visitor.State;
        }

        protected override IteratorState InitialValue(CFGNode node)
        {
            return new IteratorState();
        }

        protected override IteratorState Join(IteratorState left, IteratorState right)
        {
            return left.Union(right);
        }
    }

    #endregion
}
