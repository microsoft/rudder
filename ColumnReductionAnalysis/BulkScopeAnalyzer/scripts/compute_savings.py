import os
import sys

def do_work(path):
	print("Analyzing folder " + path)

	txts = get_traces_paths(path)
	no_assemblies = len(txts)
	print(str(no_assemblies) + " dll traces found.")

	savings = {}
	for txt in txts:
		f = open(path + "\\" + txt, "r")
		lines = f.readlines()
		f.close()

		assembly = None
		processors = []
		for line in lines:
			if "Analyzing assembly:" in line:
				assembly = line.split("Analyzing assembly:")[1].strip().split("=")[0].strip()
			elif line.startswith("SAVINGS"):
				processors.append(line.split("SAVINGS")[1].strip().split(":")[0].strip())

		for proc in processors:
			if not (proc in savings): savings[proc] = []
			savings[proc].append(assembly)

	reds = 0
	for proc in savings:
		print(proc)
		redundants = list(filter(lambda x: not x.strip().endswith("__ScopeCodeGen__.dll"), savings[proc]))
		reds += 0 if len(redundants) == 0 else (len(redundants) - 1)
		for asm in savings[proc]:
			print("\t" + asm)
		

	print("Total number of potential redundant processors is " + str(reds))



def get_traces_paths(main_path):
	paths = []
	for f in os.listdir(main_path):
		if f.endswith(".txt"):
			paths.append(f)
	return paths





if __name__ == "__main__":
	path = os.path.abspath(sys.argv[1])
	do_work(path)
