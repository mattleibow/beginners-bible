using System.Collections.Generic;

namespace tools
{
	public class LandingPageModel
	{
		public Book Book { get; set; }

		public List<Book> AllBooks { get; set; }

		public List<Language> AllLanguages { get; set; }
	}
}
