import itertools
import csv
from os import listdir
from os.path import isfile, join
mypath = "."
onlyfiles = [f for f in listdir(mypath) if  isfile(join(mypath, f)) and f.endswith('.log')]


for f in onlyfiles:
	with open(f) as file:
		while True:
			line = file.readline()
			if not line: break
			if 'Throw exception' in line:
				lines = file.readlines()[0:5]
				print(f)
				print(lines)
				print('------')
				break
