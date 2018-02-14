using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ScopeProgramAnalysis.Analysis
{
	[Serializable]
	public class ColumDependenciesResult
	{
		public bool Error { get; set; }
		public string ErrorMsg { get; set; }
		public bool IsTop { get; set; }
		public IDictionary<string, IEnumerable<string>> Dependencies { get; set; }
		public ISet<string> PassthroughColumns { get; set; }
		public ColumDependenciesResult()
		{
			Error = false;
			ErrorMsg = "";
			IsTop = false;
			Dependencies = new Dictionary<string, IEnumerable<string>>();
			PassthroughColumns = new HashSet<string>();
		}

		public static ColumDependenciesResult ReadFromTextFile(TextReader s)
		{
			var result = new ColumDependenciesResult();
			var errorAndMsg =  s.ReadLine().Split(',');
			result.Error = Boolean.Parse(errorAndMsg[0]);
			result.ErrorMsg = errorAndMsg[1];

			result.IsTop = Boolean.Parse(s.ReadLine());

			// Read dependencies
			var line = s.ReadLine();
			line = s.ReadLine();
			while (line!= null && line != "Passthrough")
			{
				var dep = line.Split(':');
				var inputs = dep[1].Split(',');
				result.Dependencies.Add(dep[0], inputs.AsEnumerable());
				line = s.ReadLine();
			}
			// Passthrough 
			line = s.ReadLine();
			if(line != null) 
			{
				var pts = line.Split(',').AsEnumerable();
				result.PassthroughColumns.UnionWith(pts);
				line = s.ReadLine();
			}
			return result;
		}

		public void WriteTextFile(TextWriter s)
		{
			s.WriteLine("{0}, {1}", this.Error, this.ErrorMsg);
			s.WriteLine("{0}", this.IsTop);
			s.WriteLine("Dependencies");
			foreach (var dep in this.Dependencies)
			{
				s.WriteLine("{0}: {1}", dep.Key, string.Join(",", dep.Value));
			}
			s.WriteLine("Passthrough");
			s.WriteLine("{0}", string.Join(",", this.PassthroughColumns));
		}
	}
}
