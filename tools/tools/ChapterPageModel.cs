using System.Collections.Generic;

namespace tools
{
	public class ChapterPageModel
	{
		public Book Book { get; set; }

		public Chapter Chapter { get; set; }

		public List<Language> AllLanguages { get; set; }
	}
}
