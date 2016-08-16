using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Backend.ThreeAddressCode.Values;
using Backend.ThreeAddressCode.Instructions;
using Backend.Analysis;
using ScopeAnalyzer.Interfaces;
using Microsoft.Cci;
using ScopeAnalyzer.Misc;

namespace ScopeAnalyzer.Analyses
{
    public class ColumnsDomain : SetDomain<Constant>
    {
        private ColumnsDomain(List<Constant> columns)
        {
            elements = columns;
        }

        public static ColumnsDomain Top
        {
            get { return new ColumnsDomain(null); }
        }

        public static ColumnsDomain Bottom
        {
            get { return new ColumnsDomain(new List<Constant>()); }
        }

        public void SetAllColumns()
        {
            base.SetTop();
        }

        public ColumnsDomain Clone()
        {
            var ncols = elements == null ? null : new List<Constant>(elements);
            return new ColumnsDomain(ncols);
        }

        public override string ToString()
        {
            if (IsTop) return "All columns used.";
            if (IsBottom) return "Column information unknown.";
            string summary = String.Empty;
            foreach(var el in elements)
            {
                summary += el.ToString() + "\n";
            }
            return summary;
        }
    }

    /// <summary>
    /// Analysis assumes no rows can escape. If some rows can indeed escape,
    /// then this analysis should not be used.
    /// </summary>
    class UsedColumnsAnalysis
    {
        ControlFlowGraph cfg;
        ConstantsInfoProvider constInfo;
        List<ITypeDefinition> rowTypes;
        List<ITypeDefinition> columnTypes;
        IMetadataHost host;
        bool unsupported = false;

        private HashSet<string> trustedRowMethods = new HashSet<string>() { "get_Item", "get_Schema" };

        public UsedColumnsAnalysis(IMetadataHost h, ControlFlowGraph c, ConstantsInfoProvider ci, List<ITypeDefinition> r, List<ITypeDefinition> cd)
        {
            host = h;
            cfg = c;
            constInfo = ci;
            rowTypes = r;
            columnTypes = cd;

            Initialize();
        }

        private void Initialize()
        {
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



        public ColumnsDomain Analyze()
        {
            if (unsupported)
                return ColumnsDomain.Top;


            var cd = ColumnsDomain.Bottom;

            foreach(var node in cfg.Nodes)
            {
                foreach(Instruction instruction in node.Instructions)
                {
                    if (!(instruction is MethodCallInstruction || instruction is IndirectMethodCallInstruction)) continue;

                    if (instruction is MethodCallInstruction)
                    {                    
                        cd.Join(GetCols(instruction as MethodCallInstruction));
                    }
                    else
                    {
                        cd = ColumnsDomain.Top;
                    }

                    // This is a doomed point, no point in continuing the analysis.
                    if (cd.IsTop)
                        return cd;
                }
            }

            return cd;
        }


        /// <summary>
        /// If the caller is a row, then we only accept get_Item method.
        /// </summary>
        /// <param name="arguments"></param>
        /// <returns></returns>
        private ColumnsDomain GetCols(MethodCallInstruction instruction)
        {          
            var ct = instruction.Method.ContainingType;

            // The methods must belong to Row.
            if (rowTypes.All(rt => ct != null && !ct.SubtypeOf(rt, host)))
                return ColumnsDomain.Bottom;

            if (!trustedRowMethods.Contains(instruction.Method.Name.Value))
                return ColumnsDomain.Top;

            // Leting get_Schema through
            if (instruction.Arguments.Count == 1)
            {
                return ColumnsDomain.Bottom;
            }

            var arg = instruction.Arguments.ElementAt(1);
            var cons = constInfo.GetConstants(instruction, arg);
            // If we don't know the initial value of the variable or the 
            // variable cannot take value froma finite a set of constants,
            // then we cannot say what column was used here.
            if (cons == null || cons.Count() == 0)
            {
                return ColumnsDomain.Top;
            }
            else
            {
                var cols = ColumnsDomain.Bottom;
                foreach (var c in cons) cols.Add(c);
                return cols;
            }                       
        }
    }
}
