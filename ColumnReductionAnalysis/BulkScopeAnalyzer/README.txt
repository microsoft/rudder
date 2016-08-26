The BulkScopeAnalyzer.exe runs ScopeAnalyzer.exe for every given Scope Project in parallel. Projects without main ScopeCodeGen dll are 
skipped. Compiler generated processors are not analyzed.

ARGUMENTS:

/projects:<path> -     <path> to the directory containing Scope projects, where each project is essentially a directory. (MANDATORY)

/scopeAnalyzer:<path> - <path> to ScopeAnalyzer.exe. (MANDATORY)

/libs:<path> -         <path> to the directory with reference assemblies. The argument is optional. If it is not given, the (root)
					   project directory is assumed.

/outputDir:<path> -    <path> where to save traces for every analyzed Scope directory. If nothing is given, "result-output" is assumed.

/verbose -             Optional argument for more detailed tracing.

/help -                Points the user to read this document.

/trace:<path> -        Optional argument for redirecting the trace output to file at <path>, in addition to printing everything to console.
                       If nothing is given, "bulk-trace.txt" is assumed.