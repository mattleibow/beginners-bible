using System.Collections.Generic;

namespace tools
{
	public class Book
	{
		public string Title { get; set; }

		public Language Language { get; set; }

		public string ContentPath { get; set; }

		public bool IsTranslation { get; set; }

		public Dictionary<string, string> Books { get; set; }

		public Dictionary<string, string> People { get; set; }
	}
}
