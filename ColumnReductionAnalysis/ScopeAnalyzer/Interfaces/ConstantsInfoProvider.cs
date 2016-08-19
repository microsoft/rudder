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
    /// Interface for providing information about results of constant-set propagation.
    /// </summary>
    interface ConstantsInfoProvider
    {
        /// <summary>
        /// Returns a collection of constants that var can take at instruction. The
        /// collection is null, if the var is not a constant at the instruction, and
        /// empty if the value of var is undefined.
        /// </summary>
        /// <param name="instruction"></param>
        /// <param name="var"></param>
        /// <returns></returns>
        IEnumerable<Constant> GetConstants(Instruction instruction, IVariable var);

        /// <summary>
        /// Returns a collection of constants that field can take at instruction. The
        /// collection is null, if the field is not a constant at the instruction, and
        /// empty if the value of field is undefined. 
        /// </summary>
        /// <param name="instruction"></param>
        /// <param name="fdef"></param>
        /// <returns></returns>
        IEnumerable<Constant> GetConstants(Instruction instruction, IFieldAccess field);

        /// <summary>
        /// Returns a collection of constants that array element at index can take at instruction.
        /// The collection is null, if the array element is not a constant at the instruction, and
        /// empty if the value of the element is undefined. 
        /// </summary>
        /// <param name="instruction"></param>
        /// <param name="array"></param>
        /// <param name="index"></param>
        /// <returns></returns>
        IEnumerable<Constant> GetConstants(Instruction instruction, IVariable array, int index);
    }
}
