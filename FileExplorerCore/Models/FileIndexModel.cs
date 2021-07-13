using System.Collections.Generic;
using System.Reflection.Metadata.Ecma335;
using System.Runtime.Serialization.Formatters.Binary;

namespace FileExplorerCore.Models
{
	public class FileIndexModel
	{
		public List<FileIndexModel>? SubFiles { get; set; }

		public string Name { get; set; }

		public FileIndexModel(string name)
		{
			Name = name;
		}

		public override string ToString()
		{
			return Name;
		}
	}
}