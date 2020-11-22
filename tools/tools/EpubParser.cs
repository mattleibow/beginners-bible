using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace tools
{
	public class EpubParser
	{
		private readonly bool verbose;
		private string output;
		private ZipArchive epubArchive;

		public EpubParser(bool verbose, string epub, string output)
		{
			this.verbose = verbose;
			this.output = output;
			epubArchive = ZipFile.OpenRead(epub);
		}

		public async Task ProcessAsync()
		{
			// load toc.ncx and parse all chapters
			var toc = await ParseTableOfContentsAsync();

			var index = 1;
			foreach (var entry in toc)
			{
				var folderName = entry.Text
					.ToLowerInvariant()
					.Replace(' ', '-')
					.Replace("!", "", StringComparison.OrdinalIgnoreCase)
					.Replace("\x2019", "", StringComparison.OrdinalIgnoreCase)
					.Replace("'", "", StringComparison.OrdinalIgnoreCase);

				folderName = $"{index:000}-{folderName}";

				var folder = Path.Combine(output, folderName);
				if (!Directory.Exists(folder))
					Directory.CreateDirectory(folder);

				await ParseChapterAsync(index, entry, folder);

				index++;
			}
		}

		private async Task ParseChapterAsync(int index, TableOfContentsEntry entry, string output)
		{
			var toc = epubArchive.GetEntry($"OEBPS/{entry.Href}");
			using var stream = toc.Open();

			var xdoc = XDocument.Load(stream);
			var ns = xdoc.Root.Name.Namespace;

			var xbody = xdoc.Descendants(ns + "body").FirstOrDefault();
			if (xbody == null)
				return;

			var xchapter = xbody.Elements(ns + "div").FirstOrDefault(x => x.Attribute("class").Value == "chapter" || x.Attribute("class").Value == "section");
			if (xchapter == null)
				return;

			var xelements = xchapter.Elements().ToList();

			var chapter = new Chapter
			{
				ContentPath = entry.Href,
				Number = index,
				Metadata = new ChapterMetadata
				{
					Tags = new ChapterMetadataTags
					{
						People = new List<string>(),
					},
				},
				Pages = new List<Page>(),
			};

			var pageIndex = 1;
			foreach (var xelement in xelements)
			{
				var xline = xelement;
				var tag = xline.Name.LocalName;
				var klass = xline.Attribute("class")?.Value;

				// some lines are bad
				if (tag == "div" && klass == "chapter")
				{
					xline = xelement.Elements().First();
					tag = xline.Name.LocalName;
					klass = xline.Attribute("class")?.Value;
				}

				if (tag == "h1" && klass == "chapter")
				{
					chapter.Metadata.Title = xline.Value;
				}
				else if (tag == "p" && klass == "center")
				{
					chapter.Metadata.Bible = xline.Value;
				}
				else if (tag == "div" && klass == "image")
				{
					var img = xline.Element(ns + "img")?.Attribute("src")?.Value;
					if (img != null)
					{
						var imgFile = epubArchive.GetEntry($"OEBPS/{img}");
						var imgOut = Path.Combine(output, $"{pageIndex:000}.jpg");
						using (var imgStream = imgFile.Open())
						using (var imgOutStream = File.Create(imgOut))
							await imgStream.CopyToAsync(imgOutStream);

						chapter.Pages.Add(new Page
						{
							Number = pageIndex++
						});
					}
				}
				else if (tag == "p")
				{
					var page = chapter.Pages.Last();

					if (!string.IsNullOrEmpty(page.Content))
						page.Content += Environment.NewLine;

					foreach (var x in xline.Nodes())
					{
						if (x is XText xt)
							page.Content += xt.Value.Trim();
						else if (x is XElement xe)
							page.Content += xe.Value.Trim() + Environment.NewLine;
					}
				}
			}

			var contentOut = Path.Combine(output, "content.md");
			using var contentStream = File.Create(contentOut);
			using var writer = new StreamWriter(contentStream);

			await writer.WriteLineAsync("---");
			await writer.WriteLineAsync($"title: {chapter.Title}");
			await writer.WriteLineAsync($"bible: {chapter.Bible}");

			foreach (var page in chapter.Pages)
			{
				await writer.WriteLineAsync("---");
				await writer.WriteLineAsync();
				if (string.IsNullOrEmpty(page.Content))
					await writer.WriteLineAsync("<!-- blank page -->");
				else
					await writer.WriteLineAsync(page.Content);
				await writer.WriteLineAsync();
			}
		}

		private async Task<List<TableOfContentsEntry>> ParseTableOfContentsAsync()
		{
			var toc = epubArchive.GetEntry("OEBPS/toc.ncx");
			using var stream = toc.Open();

			var xdoc = await XDocument.LoadAsync(stream, LoadOptions.None, default);
			var ns = xdoc.Root.Name.Namespace;

			var entries = xdoc
				.Descendants(ns + "navPoint")
				.Select(x => new TableOfContentsEntry(
					x.Attribute("id").Value,
					x.Element(ns + "navLabel").Element(ns + "text").Value,
					int.Parse(x.Attribute("playOrder").Value),
					x.Element(ns + "content").Attribute("src").Value))
				.OrderBy(x => x.Order)
				.ToList();

			return entries;
		}
	}

	public class TableOfContentsEntry
	{
		public TableOfContentsEntry(string id, string text, int order, string href)
		{
			Id = id ?? throw new ArgumentNullException(nameof(id));
			Text = text ?? throw new ArgumentNullException(nameof(text));
			Order = order;
			Href = href ?? throw new ArgumentNullException(nameof(href));

			var idx = Href.IndexOf('#');
			if (idx != -1)
				Href = Href.Substring(0, idx);
		}

		public string Id { get; set; }

		public string Text { get; set; }

		public int Order { get; set; }

		public string Href { get; set; }
	}
}
