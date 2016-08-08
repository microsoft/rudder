using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Backend.ThreeAddressCode.Values;
using Backend.ThreeAddressCode.Instructions;
using Microsoft.Cci;

namespace ScopeAnalyzer
{
    interface EscapeInfoProvider
    {
        bool Escaped(Instruction instruction, IVariable var);

        bool Escaped(Instruction instruction, IFieldReference field);

        bool Escaped(Instruction instruction, IVariable array, int index);
    }
}
