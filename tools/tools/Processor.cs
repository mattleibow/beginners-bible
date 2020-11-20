using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Markdig;
using Markdig.Extensions.Yaml;
using Markdig.Renderers;
using Markdig.Syntax;
using RazorLight;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace tools
{
	public class Processor
	{
		private readonly string rootPath;
		private readonly string outputPath;
		private readonly string templatesPath;

		private MarkdownPipeline markdownPipeline;
		private IDeserializer yamlDeserializer;
		private RazorLightEngine razorEngine;

		public Processor(string rootPath, string outputPath, string templatesPath)
		{
			this.rootPath = rootPath;
			this.outputPath = outputPath;
			this.templatesPath = templatesPath;

			markdownPipeline = new MarkdownPipelineBuilder()
				.UseYamlFrontMatter()
				.UseSoftlineBreakAsHardlineBreak()
				.Build();

			yamlDeserializer = new DeserializerBuilder()
				.WithNamingConvention(CamelCaseNamingConvention.Instance)
				.Build();

			razorEngine = new RazorLightEngineBuilder()
				.UseFileSystemProject(templatesPath)
				.UseMemoryCachingProvider()
				.Build();
		}

		public async Task ProcessAsync()
		{
			var defaultLanguagePath = Path.Combine(rootPath, "content.yaml");
			if (!File.Exists(defaultLanguagePath))
				throw new FileNotFoundException("Unable to find default content.yaml.");

			var books = new Dictionary<string, Book>();

			// read default language first
			{
				var defaultContentPath = Path.Combine(rootPath, "content.yaml");
				using var file = File.OpenRead(defaultContentPath);
				using var streamReader = new StreamReader(file);

				var book = yamlDeserializer.Deserialize<Book>(streamReader);
				book.ContentPath = defaultContentPath;
				books[string.Empty] = book;
			}

			// read all the other languages
			var languagePaths = Directory.GetFiles(rootPath, "content.*.yaml").ToList();
			foreach (var langPath in languagePaths)
			{
				using var file = File.OpenRead(langPath);
				using var streamReader = new StreamReader(file);

				var book = yamlDeserializer.Deserialize<Book>(streamReader);
				book.ContentPath = langPath;
				book.IsTranslation = true;
				books[book.Language.Code] = book;
			}

			// extract all the languages
			var languages = books.Values
				.Select(b => b.Language)
				.OrderBy(b => b.Name)
				.ToList();

			var outImagesPath = Path.Combine(outputPath, "images");
			Directory.CreateDirectory(outImagesPath);

			// process the landing page
			{
				var cover = Path.Combine(rootPath, "cover.jpg");
				CopyFile(cover, Path.Combine(outImagesPath, "cover.jpg"));

				var landing = new LandingPageModel
				{
					Book = books[string.Empty],
					AllBooks = books.Values.ToList(),
					AllLanguages = languages,
				};

				var dest = await ProcessTemplate("landing.cshtml", outputPath, landing);
				Console.WriteLine($"Saved landing page to '{dest}'.");
			}

			// process each book
			foreach (var book in books.Values)
			{
				var outContentPath = Path.Combine(outputPath, book.Language.Code);

				var chapterPaths = Directory.EnumerateDirectories(rootPath).ToList();
				chapterPaths.Sort(StringComparer.OrdinalIgnoreCase);

				var chapters = new List<Chapter>();
				foreach (var chapterPath in chapterPaths)
				{

					var chapter = await ProcessChapterAsync(book, chapterPath, chapters.Count + 1, languages, outImagesPath, outContentPath);
					chapters.Add(chapter);
				}

				await ProcessContentsIndexAsync(book, chapters, languages, outImagesPath, outContentPath);
			}

			// copy the rest of the files
			CopyNonTemplate("site.css");
			CopyNonTemplate("site.js");
		}

		private async Task<Chapter> ProcessChapterAsync(Book book, string chapterPath, int chapterNumber, List<Language> languages, string outImagesPath, string outContentPath)
		{
			// copy all the chapter images to the output
			var imagesPath = Path.Combine(outImagesPath, chapterNumber.ToString());
			Directory.CreateDirectory(imagesPath);

			var chapterImages = Directory.EnumerateFiles(chapterPath, "*.jpg").ToList();
			chapterImages.Sort(StringComparer.OrdinalIgnoreCase);

			var imageNumber = 1;
			foreach (var img in chapterImages)
			{
				CopyFile(img, Path.Combine(imagesPath, $"{imageNumber++}.jpg"));
			}

			// read chapter content
			var baseContentPath = Path.Combine(chapterPath, "content.md");
			var contentPath = Path.Combine(chapterPath, $"content.{book.Language.Code}.md");
			var isTranslation = File.Exists(contentPath);
			if (!isTranslation)
				contentPath = baseContentPath;
			var content = File.ReadAllText(contentPath);
			var chapterContent = Markdown.Parse(content, markdownPipeline);
			var metadata = ParseMetadata(chapterContent);

			// if this is a translation, then load the tags from the base
			ChapterMetadata? baseMetadata;
			if (isTranslation)
			{
				var baseContent = File.ReadAllText(baseContentPath);
				var baseChapterContent = Markdown.Parse(baseContent, markdownPipeline);
				baseMetadata = ParseMetadata(baseChapterContent);
			}
			else
			{
				baseMetadata = metadata;
			}

			if (baseMetadata.Tags != null)
			{
				metadata.Tags = new ChapterMetadataTags
				{
					Testament = GetTranslation(book.Books, baseMetadata.Tags.Testament),
					Book = GetTranslation(book.Books, baseMetadata.Tags.Book),
					People = GetTranslation(book.People, baseMetadata.Tags.People)?.ToList(),
				};
			}

			// create chapter
			var chapter = new Chapter
			{
				Number = chapterNumber,
				Metadata = metadata,
				Pages = ParsePages(chapterContent),
				ContentPath = contentPath,
				IsTranslation = isTranslation,
			};

			var model = new ChapterPageModel
			{
				Book = book,
				Chapter = chapter,
				AllLanguages = languages,
			};

			// generate html files
			{
				var outPath = Path.Combine(outContentPath, chapter.Number.ToString());
				var dest = await ProcessTemplate("chapter.cshtml", outPath, model);
				Console.WriteLine($"Saved '{book.Language.Name}' chapter {chapterNumber} to '{dest}'.");
			}

			return chapter;
		}

		private async Task ProcessContentsIndexAsync(Book book, List<Chapter> chapters, List<Language> languages, string outImagesPath, string outContentPath)
		{
			var model = new ContentsPageModel
			{
				Book = book,
				Chapters = chapters,
				AllLanguages = languages,
			};

			// generate html files
			{
				var dest = await ProcessTemplate("contents.cshtml", outContentPath, model);
				Console.WriteLine($"Saved '{book.Language.Name}' contents to '{dest}'.");
			}
		}

		private async Task<string> ProcessTemplate(string template, string outPath, object? model)
		{
			var html = await razorEngine.CompileRenderAsync(template, model);

			if (!Directory.Exists(outPath))
				Directory.CreateDirectory(outPath);

			var dest = Path.Combine(outPath, "index.htm");

			File.WriteAllText(dest, html);

			return dest;
		}

		private List<Page> ParsePages(MarkdownDocument chapterContent)
		{
			// parse blocks from content
			var pageBlocks = new List<List<Block>>();
			pageBlocks.Add(new List<Block>());
			foreach (var item in chapterContent)
			{
				switch (item)
				{
					case YamlFrontMatterBlock y:
						// skip this
						break;
					case ThematicBreakBlock t:
						// new page
						pageBlocks.Add(new List<Block>());
						break;
					default:
						// add to the current page
						pageBlocks[pageBlocks.Count - 1].Add(item);
						break;
				}
			}

			// convert blocks into objects
			var pages = new List<Page>();
			var pageCount = 0;
			foreach (var page in pageBlocks)
			{
				var writer = new StringWriter();
				var renderer = new HtmlRenderer(writer);

				foreach (var block in page)
				{
					renderer.Render(block);
				}

				pages.Add(new Page
				{
					Number = ++pageCount,
					Content = writer.ToString()
				});
			}

			return pages;
		}

		private ChapterMetadata ParseMetadata(MarkdownDocument chapterContent)
		{
			var yamlBlock = chapterContent.OfType<YamlFrontMatterBlock>().FirstOrDefault();
			return yamlDeserializer.Deserialize<ChapterMetadata>(yamlBlock.Lines.ToString());
		}

		private void CopyNonTemplate(string path)
		{
			var nonTemplate = Path.Combine(templatesPath, path);
			if (File.Exists(nonTemplate))
				CopyFile(nonTemplate, Path.Combine(outputPath, path));
		}

		private static void CopyFile(string img, string dest)
		{
			File.Copy(img, dest, true);

			Console.WriteLine($"Copied '{img}' to '{dest}'.");
		}

		private static string? GetTranslation(Dictionary<string, string>? dic, string? key) =>
			dic != null && key != null && dic.TryGetValue(key, out var value) ? value : key;

		private static IEnumerable<string>? GetTranslation(Dictionary<string, string>? dic, IEnumerable<string>? keys) =>
			keys == null ? null : keys.Select(k => GetTranslation(dic, k)!);
	}
}
