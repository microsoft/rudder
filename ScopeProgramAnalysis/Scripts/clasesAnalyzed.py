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
		#print(file)
		line = file.readline()
		while line:
			if '\"id\"' in line:
				if '\"id\": \"Summary\"' not in line:
					if '\"id\": \"SingleColumn\"' not in line:
						if(line.find('ScopeFilterTransformer_')==-1 and line.find('ScopeTransformer_')==-1 \
						and line.find('ScopeGrouper_')==-1  and line.find('ScopeProcessorCrossApplyExpressionWrapper')==-1 \
						and line.find('ScopeReducer_')==-1 and line.find('ScopeRuntime.')==-1) \
						and line.find('\"SingleColumn\"')==-1 \
						and line.find("|Process")!=-1:
							totalBench+=1
							#print(line)
			line = file.readline()
	
							
			

print('Total Files' , totalFiles)
print('Total Classes' , totalBench)
