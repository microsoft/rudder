using System;
using System.Collections.Generic;
using System.Linq;
using System.Globalization;
using Backend.ThreeAddressCode.Expressions;
using Backend.ThreeAddressCode.Values;
using Backend.ThreeAddressCode.Instructions;
using Backend.Visitors;
using Backend.Analyses;
using Microsoft.Cci;
using Backend.Model;
using ScopeProgramAnalysis.Framework;

namespace ScopeProgramAnalysis
{
    public struct RangeDomain : IAnalysisDomain<RangeDomain>
    {
        public static readonly RangeDomain TOP = new RangeDomain(int.MinValue, int.MinValue);
        public static readonly RangeDomain BOTTOM = new RangeDomain(0, -1);
		public static readonly string STRINGTOP = "_STRING_TOP_";
		public static readonly string STRINGBOTTOM = null;

		public int LowerBound { get; private set; }
        public int UpperBound { get; private set; }

		public string Literal{ get; private set; }

		private bool isString;

		public bool IsTop
        {
            get
            {
                return (!isString && LowerBound==int.MinValue && UpperBound==int.MaxValue) || (isString && Literal==STRINGTOP);
            }
        }

        public bool IsBottom
        {
            get
            {
                return (!isString && LowerBound == 0 && UpperBound == -1) || (isString && Literal == STRINGBOTTOM);
            }
        }

        public RangeDomain(int singleton)
        {
            this.LowerBound = singleton;
            this.UpperBound = singleton;
			this.Literal = null;
			this.isString = false;
        }

        public RangeDomain(int start, int end)
        {
            this.LowerBound = start;
            this.UpperBound = end;
			this.Literal = null;
			this.isString = false;
		}

		public RangeDomain(string literal)
		{
			this.LowerBound = 0;
			this.UpperBound = -1;
			this.Literal = literal;
			this.isString = true;
		}
		public RangeDomain Join(RangeDomain oth)
        {
			if (isString)
			{
				return oth.Literal != this.Literal ? new RangeDomain(STRINGTOP) : this;
			}

            var prevInterval = this;
            if (prevInterval.IsBottom)
                return oth;
            if (oth.IsBottom)
                return prevInterval;
            if (prevInterval.IsTop)
                return prevInterval;
            if (oth.IsTop)
                return oth;
            var newInterval = new RangeDomain(Math.Min(LowerBound, oth.LowerBound), Math.Max(UpperBound, oth.UpperBound));
            return newInterval.Widening(prevInterval);
        }

        public RangeDomain Widening(RangeDomain prev)
        {
			if (isString)
			{
				return prev.Literal != this.Literal ? new RangeDomain(STRINGTOP) : this;
			}
			// Trivial cases
			if (this.IsBottom)
                return prev;
            if (this.IsTop)
                return this;
            if (prev.IsBottom)
                return this;
            if (prev.IsTop)
                return prev;

            var wideningInf = this.LowerBound < prev.LowerBound ? int.MinValue : prev.LowerBound;

            var wideningSup = this.UpperBound > prev.UpperBound ? int.MaxValue : prev.UpperBound;

            return new RangeDomain(wideningInf, wideningSup);
        }

        public RangeDomain Clone()
        {
			if (isString) return new RangeDomain(this.Literal);

            return  new RangeDomain(this.LowerBound, this.UpperBound);
        }

        public bool LessEqual(RangeDomain oth)
        {
			if (isString) return this.Literal==oth.Literal;

			return this.LowerBound>=oth.LowerBound && this.UpperBound<=oth.UpperBound;
        }

        public bool Equals(RangeDomain oth)
        {
			if (isString) return this.Literal == oth.Literal;

			return this.LowerBound==oth.LowerBound && this.UpperBound==oth.UpperBound;
        }

        public RangeDomain Sum(RangeDomain rangeDomain)
        {
			if (isString) return this;

			if (IsTop) return this;
            return new RangeDomain(this.LowerBound+rangeDomain.LowerBound,this.UpperBound+rangeDomain.UpperBound);
        }
        public RangeDomain Sub(RangeDomain rangeDomain)
        {
			if (isString) return this;

			if (IsTop) return this;
            return new RangeDomain(this.LowerBound - rangeDomain.LowerBound, this.UpperBound - rangeDomain.UpperBound);
        }

        public override string ToString()
        {
			if (isString) return this.Literal!=null?$"'{this.Literal}":"_STRING_BOTTOM_";
			if (IsTop) return "_TOP_";
            if(IsBottom)
                return "_BOTTOM_";
            var result = String.Format(CultureInfo.InvariantCulture, "[{0}..{1}]", LowerBound, UpperBound);
            if (LowerBound == UpperBound) result = LowerBound.ToString();
            return result;
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
            result.variableRange = new Dictionary<IVariable, RangeDomain>(this.variableRange);
            return result;
        }

        public bool Equals(VariableRangeDomain oth)
        {
            return this.LessEqual(oth) && oth.LessEqual(this);
        }

        public VariableRangeDomain Join(VariableRangeDomain right)
        {
            var result = new VariableRangeDomain();
            
            foreach(var entry in this.variableRange)
            {
                if (right.variableRange.ContainsKey(entry.Key))
                {
                    result.variableRange[entry.Key] = entry.Value.Join(right.variableRange[entry.Key]);
                }
                else
                {
                    result.variableRange[entry.Key] = entry.Value;
                }
            }
            foreach (var k in right.variableRange.Keys.Except(result.variableRange.Keys))
            {
                result.variableRange[k] = right.variableRange[k];
            }
            return result;
        }

        public void AssignValue(IVariable v, RangeDomain value)
        {
            variableRange[v] = value;
        }

        public void AddValue(IVariable v, RangeDomain value)
        {
            if (variableRange.ContainsKey(v))
            {
                variableRange[v] = variableRange[v].Join(value);
            }
            else
            {
                variableRange[v] = value;
            }

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

        public RangeDomain GetValue(IVariable var)
        {
            if(this.variableRange.ContainsKey(var))
                return this.variableRange[var];
            return RangeDomain.BOTTOM;
        }
    }


    public class RangeAnalysis: ForwardDataFlowAnalysis<VariableRangeDomain> 
    {

        private object originalValue = null;
		private VariableRangeDomain initRange = null;
		public IVariable ReturnVariable { get; private set; }

		public DataFlowAnalysisResult<VariableRangeDomain>[] Result { get; private set; }

        public RangeAnalysis(ControlFlowGraph cfg, IMethodDefinition method): base(cfg)
        {
			this.ReturnVariable = new LocalVariable(method.Name + "_" + "$RV") { Type = method.Type.PlatformType.SystemObject };
		}
		public RangeAnalysis(ControlFlowGraph cfg, IMethodDefinition method, VariableRangeDomain initRange) : this(cfg, method)
		{
			this.initRange = initRange;
		}


		public override DataFlowAnalysisResult<VariableRangeDomain>[] Analyze()
        {
            Result = base.Analyze();
            return Result;
        }

        protected override bool Compare(VariableRangeDomain newState, VariableRangeDomain oldState)
        {
            return newState.LessEqual(oldState);
        }

        protected override VariableRangeDomain Flow(CFGNode node, VariableRangeDomain input)
        {
            var visitor = new RangeAnalysisVisitor(this, input);
            visitor.Visit(node);
            return visitor.State;
        }

        protected override VariableRangeDomain InitialValue(CFGNode node)
        {
			if (this.cfg.Entry.Id == node.Id && this.initRange != null)
			{
				return this.initRange;
			}
			return new VariableRangeDomain();
        }

		protected override VariableRangeDomain Join(VariableRangeDomain left, VariableRangeDomain right)
        {
            return left.Join(right);
        }

        internal class RangeAnalysisVisitor: InstructionVisitor
        {
            private RangeAnalysis rangeAnalysis;

            public RangeAnalysisVisitor(RangeAnalysis rangeAnalysis, VariableRangeDomain oldState)
            {
                this.rangeAnalysis = rangeAnalysis;
                this.State = oldState;
            }

            public VariableRangeDomain State { get; internal set; }

            public override void Visit(LoadInstruction instruction)
			{
				var source = instruction.Operand;
				var dest = instruction.Result;
				Copy(source, dest);
			}

			private void Copy(IValue source, IVariable dest)
			{
				if (source is Constant)
				{
					var value = ExtractConstant(source as Constant);
					this.State.SetValue(dest, value);
				}
				if (source is IVariable)
				{
					this.State.SetValue(dest, this.State.GetValue(source as IVariable));
				}
			}

			public override void Visit(ReturnInstruction instruction)
			{
				Copy(instruction.Operand, this.rangeAnalysis.ReturnVariable); 
			}

			private RangeDomain ExtractConstant(Constant K)
			{
				int value = -1;

				if (TypeHelper.IsPrimitiveInteger(K.Type) && !(K.Value is Int64 || K.Value is UInt64))
				{
					value = (int)K.Value;
					return new RangeDomain(value, value);
				}
				if (K.Type.IsString())
				{
					var literal = (string)K.Value;
					return new RangeDomain(literal);
				}

                return RangeDomain.BOTTOM;
            }

            public override void Visit(BinaryInstruction instruction)
            {
                var op1 = this.State.GetValue(instruction.LeftOperand);
                var op2 = this.State.GetValue(instruction.RightOperand);
                switch(instruction.Operation)
                {
                    case BinaryOperation.Add:
                        this.State.AssignValue(instruction.Result, op1.Sum(op2));
                        break;
                    case BinaryOperation.Sub:
                        this.State.AssignValue(instruction.Result, op1.Sub(op2));
                        break;
                    default:
                        this.State.AssignValue(instruction.Result, RangeDomain.TOP);
                        break;
                }
                     
            }

            private IExpression GetExpression(IVariable leftOperand)
            {
                throw new NotImplementedException();
            }

            public override void Default(Instruction instruction)
            {
                foreach (var result in instruction.ModifiedVariables)
                {
                    var range = RangeDomain.BOTTOM;
                    foreach (var arg in instruction.UsedVariables)
                    {
                        range = range.Join(this.State.GetValue(arg));
                    }
                    this.State.AssignValue(result, range);
                }
            }
        }
    }
}
