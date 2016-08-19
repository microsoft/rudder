using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Backend.ThreeAddressCode.Values;
using Backend.ThreeAddressCode.Instructions;
using Microsoft.Cci;

namespace ScopeAnalyzer.Interfaces
{
    /// <summary>
    /// Interface for providin information about results of escape analysis.
    /// </summary>
    interface EscapeInfoProvider
    {
        bool Escaped(Instruction instruction, IVariable var);

        bool Escaped(Instruction instruction, IFieldAccess field);

        bool Escaped(Instruction instruction, IVariable array, int index);
    }
}
