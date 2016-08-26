import os
import sys

def do_work(path):
	print("Analyzing folder " + path)

	txts = get_traces_paths(path)
	no_assemblies = len(txts)
	print(str(no_assemblies) + " dll traces found.")

	no_failed_assemblies = 0
	no_cpp_assemblies = 0
	no_not_interesting_assemblies = 0
	no_methods = 0
	no_failed_methods = 0
	no_interesting_methods = 0
	no_unsupported_methods = 0
	no_concrete_methods = 0
	no_proper_subset_methods = 0
	no_equal_set_methods = 0
	no_imprecision_methods = 0
	no_column_savings = 0
	no_column_string_accesses = 0
	no_column_index_accesses = 0
	no_concrete_methods_mapped = 0
	no_column_percentages = 0
	#no_column_byte_savings = 0
	#no_column_byte_percentages = 0
	cumulative_time = 0
	no_input_all_used_methods = no_input_unused_methods = no_input_column_percentages = no_input_column_savings = 0
	no_output_all_used_methods = no_output_unused_methods = no_output_column_percentages = no_output_column_savings = 0
	for txt in txts:
		f = open(path + "\\" + txt, "r")
		trace = f.read()
		f.close()

		if not trace.strip().endswith("SUCCESS") or "LOAD FAILURE" in trace:
			no_failed_assemblies += 1
			if is_main_cpp(trace): no_cpp_assemblies += 1
			continue


		stats = trace.split("Done analyzing the assembly")
		if len(stats) != 2:
		#	print("No stats for " + txt)
			no_failed_assemblies += 1
			continue


		lines = stats[1].split("\n")

		interesting_assembly = False
		time = 0
		for line in lines:
			line = line.strip()
			
			if line.startswith("Methods:"): no_methods += extract_stat(line)
			elif line.startswith("Methods failed"): no_failed_methods += extract_stat(line)
			elif line.startswith("Interesting methods"): 
				cnt = extract_stat(line)
				if cnt == 0:
					no_not_interesting_assemblies += 1
				else:
					interesting_assembly = True
				no_interesting_methods += cnt
			elif line.startswith("Unsupported"): no_unsupported_methods += extract_stat(line)
			elif line.startswith("Concrete-columns"): no_concrete_methods += extract_stat(line)
			elif line.startswith("Concrete methods"): no_concrete_methods_mapped += extract_stat(line)

			elif line.startswith("Union unused:"): no_proper_subset_methods += extract_stat(line)
			elif line.startswith("Union all used"): no_equal_set_methods += extract_stat(line)
			elif line.startswith("Union superset"): no_imprecision_methods += extract_stat(line)
			
			elif line.startswith("Input unused:"): no_input_unused_methods += extract_stat(line)
			elif line.startswith("Input all used"): no_input_all_used_methods += extract_stat(line)
			
			elif line.startswith("Output unused:"): no_output_unused_methods += extract_stat(line)
			elif line.startswith("Output all used"): no_output_all_used_methods += extract_stat(line)
			

			elif line.startswith("Used columns string accesses"): no_column_string_accesses += extract_stat(line)
			elif line.startswith("Used columns index accesses"): no_column_index_accesses += extract_stat(line)
			elif line.startswith("Total analysis time"): time = extract_time(line)
				

			elif line.startswith("!Union columns count cumulative"): no_column_savings += extract_stat(line)			
			elif line.startswith("!Union columns percentage count cumulative"): no_column_percentages += extract_stat_float(line)
			#elif line.startswith("!Union columns byte cumulative"): no_column_byte_savings += extract_stat(line)			
			#elif line.startswith("!Union columns percentage byte cumulative"): no_column_byte_percentages += extract_stat_float(line)
			
			elif line.startswith("!Input columns count cumulative"): no_input_column_savings += extract_stat(line)			
			elif line.startswith("!Input columns percentage count cumulative"): no_input_column_percentages += extract_stat_float(line)

			elif line.startswith("!Output columns count cumulative"): no_output_column_savings += extract_stat(line)			
			elif line.startswith("!Output columns percentage count cumulative"): no_output_column_percentages += extract_stat_float(line)

		if interesting_assembly: cumulative_time += time

	print("")
	print(str(no_failed_assemblies) + " dlls failed to be analyzed. (" + str(no_cpp_assemblies) + " of them are cpp asemblies.)")
	print(str(no_assemblies - no_failed_assemblies) + " dlls successfully analyzed.")
	no_interesting_assemblies = no_assemblies - no_failed_assemblies - no_not_interesting_assemblies
	print(str(no_interesting_assemblies) + " dlls with some methods of interest.")
	print("")

	print("")
	print(str(no_methods) + " methods in total.")
	print(str(no_interesting_methods) + " methods of interest.")
	print(str(no_failed_methods) + " failed to be analyzed.")
	print(str(no_unsupported_methods) + " of them with unsupported features.")
	print("")
	
	print(str(no_concrete_methods) + " interesting methods with concrete column indexing.")
	print("\t" + str(no_column_string_accesses) + " string column accesses.")
	print("\t" + str(no_column_index_accesses) + " index column accesses.")
	print(str(no_concrete_methods_mapped) + " concrete methods successfully mapped to xml ids.")
	print("")

	print(str(no_proper_subset_methods) + " mapped methods used less union columns than declared.")
	print("\t" + str(0 if no_proper_subset_methods == 0 else no_column_savings/no_proper_subset_methods) + " average unused columns count.")
	print("\t" + str(0 if no_proper_subset_methods == 0 else no_column_percentages/no_proper_subset_methods) + " average unused columns percentage.")
	#print("\t" + str(0 if no_proper_subset_methods == 0 else no_column_byte_savings/no_proper_subset_methods) + " average unused columns byte size.")
	#print("\t" + str(0 if no_proper_subset_methods == 0 else no_column_byte_percentages/no_proper_subset_methods) + " average unused columns byte size percentage.")
	print(str(no_equal_set_methods) + " mapped methods used all union columns declared.")
	print(str(no_imprecision_methods) + " mapped methods with imprecise column analysis.")
	print("")

	print(str(no_input_unused_methods) + " mapped methods used less input columns than declared.")
	print("\t" + str(0 if no_input_unused_methods == 0 else no_input_column_savings/no_input_unused_methods) + " average unused columns count.")
	print("\t" + str(0 if no_input_unused_methods == 0 else no_input_column_percentages/no_input_unused_methods) + " average unused columns percentage.")
	print(str(no_input_all_used_methods) + " mapped methods used all input columns declared.")
	print("")

	print(str(no_output_unused_methods) + " mapped methods used less output columns than declared.")
	print("\t" + str(0 if no_output_unused_methods == 0 else no_output_column_savings/no_output_unused_methods) + " average unused columns count.")
	print("\t" + str(0 if no_output_unused_methods == 0 else no_output_column_percentages/no_output_unused_methods) + " average unused columns percentage.")
	print(str(no_output_all_used_methods) + " mapped methods used all output columns declared.")
	print("")

	print(str(0 if no_interesting_assemblies == 0 else cumulative_time/float(no_interesting_assemblies)) + " average time (s) per assembly")
	print(str(0 if no_interesting_methods == 0 else cumulative_time/float(no_interesting_methods)) + " average time (s) per method")
	print("")




def is_main_cpp(content):
	lines = content.split("\n")
	for line in lines:
		if not ("LOAD FAILURE" in line): continue
		if "not a valid CLR module" in line: return True
	return False

def get_traces_paths(main_path):
	paths = []
	for f in os.listdir(main_path):
		if f.endswith(".txt"):
			paths.append(f)
	return paths


def extract_stat(line):
	stat = line.split(":")[1].strip()
	return int(stat)

def extract_time(line):
	parts = line.split(":")
	hours = int(parts[1].strip())
	mins = int(parts[2].strip())
	secsnm = parts[3].strip()
	sec_parts = secsnm.split(".")
	secs = int(sec_parts[0].strip())
	msecs = int(sec_parts[1].strip())
	return hours*60*60 + mins*60 + secs + msecs/float(100)


def extract_stat_float(line):
	stat = line.split(":")[1].strip()
	return float(stat)



if __name__ == "__main__":
	path = os.path.abspath(sys.argv[1])
	do_work(path)
