using Backend.Analyses;
using Model.ThreeAddressCode.Values;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Backend.Model;
using Model.ThreeAddressCode.Visitor;
using Model.ThreeAddressCode.Instructions;
using Model.Types;

namespace ScopeProgramAnalysis
{
    public struct RangeDomain : IAnalysisDomain<RangeDomain>
    {
        int Start { get; set; }
        int End { get; set; }

        public bool IsTop
        {
            get
            {
                return Start==int.MinValue && End==int.MaxValue;
            }
        }

        public RangeDomain(int start, int end)
        {
            this.Start = start;
            this.End = end;
        }
        public RangeDomain Join(RangeDomain oth)
        {
            return new RangeDomain(Math.Min(Start, oth.Start), Math.Max(End, oth.End));
        }
        public RangeDomain Clone()
        {
            return  new RangeDomain(this.Start, this.End);
        }

        public bool LessEqual(RangeDomain oth)
        {
            return this.Start>=oth.Start && oth.End<=this.End;
        }

        public bool Equals(RangeDomain oth)
        {
            return this.Start==oth.Start && this.End==oth.End;
        }
    }
    public class VariableRangeDomain : IAnalysisDomain<VariableRangeDomain>
    {
        IDictionary<IVariable, RangeDomain> variableRange;
        public VariableRangeDomain()
        {
            this.variableRange = new Dictionary<IVariable, RangeDomain>();
        }
        public bool IsTop
        {
            get
            {
                 return false;
            }
        }

        public VariableRangeDomain Clone()
        {
            var result = new VariableRangeDomain();
            result.variableRange.Union(this.variableRange);
            return result;
        }

        public bool Equals(VariableRangeDomain oth)
        {
            return this.LessEqual(oth) && oth.LessEqual(this);
        }

        public VariableRangeDomain Join(VariableRangeDomain right)
        {
            var result = this;
            foreach(var kv in result.variableRange)
            {
                if (right.variableRange.ContainsKey(kv.Key))
                {
                    result.variableRange[kv.Key] = result.variableRange[kv.Key].Join(right.variableRange[kv.Key]);
                }
            }
            foreach (var k in right.variableRange.Keys.Except(result.variableRange.Keys))
            {
                result.variableRange[k] = right.variableRange[k];
            }
            return result;
        }

        public bool LessEqual(VariableRangeDomain oth)
        {
            var result = this.variableRange.All(kv => oth.variableRange.ContainsKey(kv.Key) && kv.Value.LessEqual(oth.variableRange[kv.Key]));
            return result;
        }

        public void SetValue(IVariable var, RangeDomain value)
        {
            this.variableRange[var] = value;
        }
    }


    public class RangeAnalysis: ForwardDataFlowAnalysis<VariableRangeDomain> 
    {
        public readonly RangeDomain TOP = new RangeDomain(int.MinValue, int.MinValue);

        public RangeAnalysis(ControlFlowGraph cfg): base(cfg)
        {

        }
        protected override bool Compare(VariableRangeDomain newState, VariableRangeDomain oldState)
        {
            return newState.LessEqual(oldState);
        }

        protected override VariableRangeDomain Flow(CFGNode node, VariableRangeDomain input)
        {
            var visitor = new RangeAnalysisVisitor(this);
            visitor.Visit(node);
            return visitor.State;
        }

        protected override VariableRangeDomain InitialValue(CFGNode node)
        {
            throw new NotImplementedException();
        }

        protected override VariableRangeDomain Join(VariableRangeDomain left, VariableRangeDomain right)
        {
            throw new NotImplementedException();
        }

        internal class RangeAnalysisVisitor: InstructionVisitor
        {
            private RangeAnalysis rangeAnalysis;

            public RangeAnalysisVisitor(RangeAnalysis rangeAnalysis)
            {
                this.rangeAnalysis = rangeAnalysis;
            }

            public VariableRangeDomain State { get; internal set; }

            public override void Visit(LoadInstruction instruction)
            {
                if(instruction.Operand is Constant)
                {
                    var K = instruction.Operand as Constant;
                    if (K.Type.Equals(PlatformTypes.Int32))
                    {
                        int value = (int)K.Value;
                        this.State.SetValue(instruction.Result, new RangeDomain(value, value));
                    }
                }
            }
            public override void Default(Instruction instruction)
            {
                base.Default(instruction);
            }
        }
    }
}
