import itertools
import csv
from os import listdir
from os.path import isfile, join
mypath = "."
onlyfiles = [f for f in listdir(mypath) if  isfile(join(mypath, f)) and f.endswith('.passthrough')]

totalFiles = 0
totalBench = 0
totalBenchWithinfo = 0
totalColumns = 0
totalPassthrough = 0
maxPassthrough = 0
totalRatio = 0
minRatio = 1
maxRatio = 0
fileWithMax = ''
totalTOP = 0
totalWithPR = 0
totalOutputFound = 0
totalInputFound = 0
totalOutputSchema = 0
totalInputSchema = 0
for f in onlyfiles:
	with open(f) as file:
		totalFiles+=1
		while True:
			line = file.readline()
			if not line: break
			if '=== Summary ===' in line:
				found_summary = True
				if found_summary:
					break		
		lines = file.readlines()[0:]

		for row in csv.reader(lines, delimiter='+'):
			if row:
				if(len(row)>2):
					totalBench+=1
					if(row[0].find('ScopeFilterTransformer_')==-1 and row[0].find('ScopeTransformer_')==-1 \
						and row[0].find('ScopeGrouper_')==-1  and row[0].find('ScopeProcessorCrossApplyExpressionWrapper')==-1 \
						and row[0].find('ScopeReducer_')==-1 and row[0].find('ScopeRuntime.')==-1):
						#print(row[0])
						totalBenchWithinfo+=1
						print(row)
						p = int(row[1])
						t = int(row[2])
						if(row[2]>=0 and row[3]>=0):
							totalOutputFound+= int(row[2])
							totalInputFound+= int(row[3])
							totalOutputSchema+= int(row[4])
							totalInputSchema+= int(row[5])

							if(p>=0): 
								if(t>0): 
									ratio = float (p)/ float(t)
								else:
									 ratio = 0
								if(ratio>1):
									print('Problematic:', f)
									print(row)
								if(ratio>maxRatio):
									maxRatio = ratio
								if(ratio<minRatio):
									minRatio = ratio
								if(p>maxPassthrough):
									maxPassthrough = p	 
									fileWithMax = file
								totalPassthrough += p
								totalColumns += t
								totalRatio +=ratio
								if(p>0):
										totalWithPR+=1
							else:
								totalTOP+=1
						else:
								print(row)
								totalTOP+=1
					#else:
					#	print(row[0])


print('Total Files' , totalFiles)
print('Total classes' , totalBench)
print('Total classes (filtered)' , totalBenchWithinfo)
print('Total classes without _TOP_' , totalBenchWithinfo-totalTOP)
print('Total classes with Pass-Trough' , totalWithPR)
print('Percentage of classes' , (float(totalWithPR)/float(totalBenchWithinfo))*100.0)
#print('Total classes with TOP' , totalTOP)
print('Total Columns' , totalOutputFound)
print('Total Pass-Trough' , totalPassthrough)
print('Percentage of columns' , float(totalPassthrough)/float(totalOutputFound)*100.0)
print('Min Ratio' , minRatio)
print('Max Ratio' , maxRatio)
print('Max Pass-Trough' , maxPassthrough)
print('File name' , fileWithMax)
print('Total Input:', totalInputFound, ' Total Schema:', totalInputSchema, 'Ratio:', float(totalInputFound)/float(totalInputSchema)*100)
print('Total Output:', totalOutputFound, ' Total Schema:', totalOutputSchema, 'Ratio:', float(totalOutputFound)/float(totalOutputSchema)*100)
	