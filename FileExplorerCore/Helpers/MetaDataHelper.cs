using System;
using System.Collections.Generic;
using System.Linq;
using MetadataExtractor;
using Directory = MetadataExtractor.Directory;

namespace FileExplorerCore.Helpers
{
	public static class MetaDataHelper
	{
		public static IEnumerable<Directory> GetData(string path)
		{
			try
			{
				return ImageMetadataReader.ReadMetadata(path)
																	.Where(w => !w.IsEmpty);
			}
			catch (Exception) { }

			return Enumerable.Empty<Directory>();
		}
	}
}
