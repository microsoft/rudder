using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Backend.ThreeAddressCode.Values;
using Backend.ThreeAddressCode.Instructions;
using Backend.Analyses;
using ScopeAnalyzer.Interfaces;
using Microsoft.Cci;
using ScopeAnalyzer.Misc;
using Backend.Model;

namespace ScopeAnalyzer.Analyses
{
    /// <summary>
    /// Domain that simply keeps track of what constants are used for Row column accesses.
    /// </summary>
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
                summary += el.ToString() + "\r\n";
            }
            return summary;
        }

    }


    /// <summary>
    /// Analysis that overraproximates constants used in accessing columns, i.e., constants
    /// passed to Row get_Item method. The analysis assumes no rows can escape and it uses the
    /// results of constant-set propagation analysis.  
    /// </summary>
    class UsedColumnsAnalysis
    {
        ControlFlowGraph cfg;
        IMetadataHost host;

        ConstantsInfoProvider constInfo;
        List<ITypeDefinition> rowTypes;
        List<ITypeDefinition> columnTypes;
        
        public bool Unsupported { get; set; }

        public int ColumnStringAccesses { get; set; }

        public int ColumnIndexAccesses { get; set; }


        private HashSet<string> trustedRowMethods = new HashSet<string>() { "get_Item", "get_Schema", "Reset" };

        public UsedColumnsAnalysis(IMetadataHost h, ControlFlowGraph c, ConstantsInfoProvider ci, List<ITypeDefinition> r, List<ITypeDefinition> cd)
        {
            host = h;
            cfg = c;
            constInfo = ci;
            rowTypes = r;
            columnTypes = cd;

            ColumnIndexAccesses = 0;
            ColumnStringAccesses = 0;

            Initialize();
        }

        private void Initialize()
        {
            var instructions = new List<Instruction>();
            foreach (var block in cfg.Nodes)
                instructions.AddRange(block.Instructions);

            if (instructions.Any(i => i is ThrowInstruction || i is CatchInstruction))
                Unsupported = true;
        }



        public IMetadataHost Host
        {
            get { return host; }
        }


        public ColumnsDomain Analyze()
        {
            if (Unsupported)
                return ColumnsDomain.Top;

            var cd = ColumnsDomain.Bottom;

            foreach(var node in cfg.Nodes)
            {
                foreach(Instruction instruction in node.Instructions)
                {
                    // We are only interested in method calls, since that is how columns are accessed.
                    if (!(instruction is MethodCallInstruction || instruction is IndirectMethodCallInstruction)) continue;

                    if (instruction is MethodCallInstruction)
                    {                    
                        cd.Join(GetCols(instruction as MethodCallInstruction));
                    }
                    else
                    {
                        cd = ColumnsDomain.Top;
                    }

                    // This is a doomed point, jump out, no point in continuing forward..
                    if (cd.IsTop)
                        return cd;
                }
            }

            return cd;
        }


        /// <summary>
        /// Check if a method stands for accessing a column and soundly compute
        /// what columns are actually accessed.
        /// </summary>
        /// <param name="arguments"></param>
        /// <returns></returns>
        private ColumnsDomain GetCols(MethodCallInstruction instruction)
        {          
            var ct = instruction.Method.ContainingType;

            // The methods must belong to Row.
            if (rowTypes.All(rt => ct != null && !ct.SubtypeOf(rt, host)))
                return ColumnsDomain.Bottom;

            // If we don't trust the method, then only safe thing to do is to
            // set all columns have been used.
            if (!trustedRowMethods.Contains(instruction.Method.Name.Value))
            {
                Utils.WriteLine("USED COLUMNS untrusted method: " + instruction.ToString());
                return ColumnsDomain.Top;
            }

            // get_Schema function is safe.
            if (instruction.Arguments.Count == 1)
            {
                return ColumnsDomain.Bottom;
            }

            var arg = instruction.Arguments.ElementAt(1);
            var cons = constInfo.GetConstants(instruction, arg);
            // If we don't know the initial value of the variable or the 
            // variable cannot take value from a finite set of constants,
            // then we cannot say what column was used here, and in general.
            if (cons == null || cons.Count() == 0)
            {
                Utils.WriteLine("USED COLUMNS top|bottom: " + instruction.ToString());
                return ColumnsDomain.Top;
            }
            else
            {
                UpdateColumnAccessesStats(arg.Type);

                var cols = ColumnsDomain.Bottom;
                foreach (var c in cons) cols.Add(c);
                return cols;
            }                       
        }

        /// <summary>
        /// Method used to update statistics on how columns are accesed, by
        /// a string or integer indices.
        /// </summary>
        /// <param name="type"></param>
        private void UpdateColumnAccessesStats(ITypeReference type)
        {
            if (type.FullName() == "System.String")
            {
                ColumnStringAccesses++;
            }
            else if (type.FullName() == "System.Int32" || type.FullName() == "System.Int64")
            {
                ColumnIndexAccesses++;
            }
            else
            {
                Utils.WriteLine("WARNING: column access of type " + type.FullName());
            }
        }
    }
}
