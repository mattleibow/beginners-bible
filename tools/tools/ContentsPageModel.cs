using System.Collections.Generic;
using System.Linq;

namespace tools
{
	public class ContentsPageModel
	{
		public Book Book { get; set; }

		public List<Chapter> Chapters { get; set; }

		public List<Language> AllLanguages { get; set; }

		public IEnumerable<IGrouping<string, Chapter>> Testaments =>
			Chapters
				.Where(c => !string.IsNullOrEmpty(c.Metadata?.Tags?.Testament))
				.GroupBy(c => c.Metadata!.Tags!.Testament!);

		public IEnumerable<IGrouping<string, Chapter>> People =>
			Chapters
				.Where(c => c.Metadata?.Tags?.People != null)
				.SelectMany(c => c.Metadata!.Tags!.People!.Select(p => (person: p, chapter: c)))
				.OrderBy(x => x.person)
				.GroupBy(x => x.person, x => x.chapter);
	}
}
