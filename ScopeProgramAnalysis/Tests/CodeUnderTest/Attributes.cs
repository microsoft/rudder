using ScopeRuntime;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CodeUnderTest
{
    public class ReadsAttribute : Attribute
    {
        public ReadsAttribute(params string[] columns) { }
    }
    public class WritesAttribute : Attribute
    {
        public WritesAttribute(params string[] columns) { }
    }
}