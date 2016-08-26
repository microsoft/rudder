The ScopeAnalyzer.exe tool performs Scope column-reduction analysis on a given assembly, i.e., designated processor/reducer/combiner methods.

ARGUMENTS:

/assembly:<path> -     <path> to the assembly to be analyzed. A path to a directory can be given as well in which case all assemblies in the 
                       given directory will be analyzed. (MANDATORY)

/libs:<path> -         <path> to the directory with reference assemblies. The argument is optional. If it is not given, the directory of the 
                       main assembly is assumed.

/verbose -             Optional argument for more detailed tracing.

/help -                Points the user to read this document.

/output:<path> -       Optional argument for redirecting the trace output to file at <path>, in addition to printing everything to console.

/processorIds:<path> - Optional argument for specifying <path> to the file with mapping between processor full names to its corresponding ids.
                       The file format is as follows. Each line consists of two strings, one denoting the processor and the other denoting
					   its id. The strings are separated with a tab. If this argument is not given, results of column reduction will not be
					   available; only the inferred columns access will be shown. If the file can be found, only processors appearing in the
					   mapping be analyzed. Otherwise, all processor will be analyzed.

/vertexDef:<path> -    Optional argument for specifying <path> to the file with processor schema XML information. It is used in conjunction with
					   /processorIds argument to compute actual column reduction. If the argument is not given, the tool then looks for a 
					   "ScopeVertefDef.xml" file in the directory where the main assembly is. If the file cannot be found, results of column 
					   reduction will not be available; only the inferred columns access will be reported.