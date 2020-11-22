using System.Collections.Generic;
using System.Diagnostics;

namespace tools
{
	[DebuggerDisplay("{Title,nq}")]
	public class Chapter
	{
		public int Number { get; set; }

		public string? ContentPath { get; set; }

		public ChapterMetadata? Metadata { get; set; }

		public string? Title => Metadata?.Title;

		public string? Bible => Metadata?.Bible;

		public List<Page>? Pages { get; set; }

		public bool HasTranslation { get; set; }
	}
}
