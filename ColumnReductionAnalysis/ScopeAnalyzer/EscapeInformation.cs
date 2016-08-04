using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Backend.ThreeAddressCode.Values;
using Backend.ThreeAddressCode.Instructions;
using Backend.Analysis;
using Backend.Visitors;
using Microsoft.Cci;

namespace ScopeAnalyzer
{
    interface EscapeInformation
    {
        bool Escaped(Instruction instruction, IVariable var);

        bool Escaped(Instruction instruction, IFieldDefinition fdef);

        bool Escaped(Instruction instruction, IVariable array, int index);

    }
}
