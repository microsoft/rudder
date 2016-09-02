import itertools
import csv
from os import listdir
from os.path import isfile, join
mypath = "."
onlyfiles = [f for f in listdir(mypath) if  isfile(join(mypath, f)) and f.endswith('.sarif')]

totalBench = 0
totalFiles = 0
for f in onlyfiles:
	with open(f) as file:
		totalFiles+=1
		while True:
			line = file.readline()
			if not line: break
			if '\"id\": \"Summary\"' in line:
				lines = file.readlines()[1:]
				for line in lines:
					if '\"id\"' in line:
						if '\"id\": \"Summary\"' in line:
							break
						if(line.find('ScopeFilterTransformer_')==-1 and line.find('ScopeTransformer_')==-1 \
							and line.find('ScopeGrouper_')==-1  and line.find('ScopeProcessorCrossApplyExpressionWrapper')==-1 \
							and line.find('ScopeReducer_')==-1 and line.find('ScopeRuntime.')==-1) \
							and line.find('\"SingleColumn\"')==-1:
							totalBench+=1
							print(line)
							break
						#else:

							
			

print('Total Files' , totalFiles)
print('Total Classes' , totalBench)
