import os
import sys

def do_work(path):
	print("Analyzing folder " + path)

	mappings = get_mappings_paths(path)
	no_mappings = len(mappings)
	print(str(no_mappings) + " mapping files found.")

	count = 0
	empty_mappings = 0
	for mapping in mappings:
		f = open(path + "\\" + mapping, "r")
		lines = f.readlines()
		f.close()
		lines = list(filter(lambda x: not (not x.strip()), lines))
		if (len(lines) == 0): 
			#print(mapping)
			empty_mappings += 1

		uniques = set()
		for line in lines:
			parts = line.split("\t")
			uniques.add(parts[0].strip())
		count += len(uniques)

	print(str(count) + " is total number of (unique) processor mappings.")
	print(str(empty_mappings) + " is total number of empty processor mapping files.")





def get_mappings_paths(main_path):
	paths = []
	for f in os.listdir(main_path):
		if f.endswith(".txt"):
			paths.append(f)
	return paths




if __name__ == "__main__":
	path = os.path.abspath(sys.argv[1])
	do_work(path)
