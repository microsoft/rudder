using System;
using System.Runtime.Serialization;

namespace ScopeProgramAnalysis.Framework
{
    [Serializable]
    internal class AnalysisNetException : Exception
    {
        public AnalysisNetException()
        {
        }

        public AnalysisNetException(string message) : base(message)
        {
        }

        public AnalysisNetException(string message, Exception innerException) : base(message, innerException)
        {
        }

        protected AnalysisNetException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }
}