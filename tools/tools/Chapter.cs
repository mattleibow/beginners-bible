using System.Collections.Generic;

namespace tools
{
	public class Chapter
	{
		public int Number { get; set; }

		public string ContentPath { get; set; }

		public ChapterMetadata Metadata { get; set; }

		public string Title => Metadata.Title;

		public string Bible => Metadata.Bible;

		public List<Page> Pages { get; set; } = new List<Page>();
	}
}
