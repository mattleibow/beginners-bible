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
				books[book.Language.Code] = book;
			}

			// extract all the languages
			var languages = books.Values
				.Select(b => b.Language)
				.OrderBy(b => b.Name)
				.ToList();

			// process each book
			foreach (var book in books.Values)
			{
				var chapterPaths = Directory.EnumerateDirectories(rootPath);
				var chapterNumber = 1;
				foreach (var chapterPath in chapterPaths)
				{
					var outImagesPath = Path.Combine(outputPath, "images");
					var outContentPath = Path.Combine(outputPath, book.Language.Code);

					await ProcessChapterAsync(book, chapterPath, chapterNumber++, languages, outImagesPath, outContentPath);
				}
			}

			// copy the rest of the files
			CopyNonTemplate("site.css");
			CopyNonTemplate("site.js");
		}

		private async Task ProcessChapterAsync(Book book, string chapterPath, int chapterNumber, List<Language> languages, string outImagesPath, string outContentPath)
		{
			// copy all the chapter images to the output
			var imagesPath = Path.Combine(outImagesPath, chapterNumber.ToString());
			if (Directory.Exists(imagesPath))
				Directory.Delete(imagesPath, true);
			Directory.CreateDirectory(imagesPath);
			var chapterImages = Directory.EnumerateFiles(chapterPath, "*.jpg");
			var imageNumber = 1;
			foreach (var img in chapterImages)
			{
				var dest = Path.Combine(imagesPath, $"{imageNumber++}.jpg");
				File.Copy(img, dest, true);

				Console.WriteLine($"Copied '{img}' to '{dest}'.");
			}

			// read chapter content
			var contentPath = Path.Combine(chapterPath, $"content.{book.Language.Code}.md");
			if (!File.Exists(contentPath))
				contentPath = Path.Combine(chapterPath, "content.md");
			var content = File.ReadAllText(contentPath);
			var chapterContent = Markdown.Parse(content, markdownPipeline);

			// create chapter
			var chapter = new Chapter
			{
				Number = chapterNumber,
				Metadata = ParseMetadata(chapterContent),
				Pages = ParsePages(chapterContent),
				ContentPath = contentPath,
			};

			var model = new ChapterPageModel
			{
				Book = book,
				Chapter = chapter,
				AllLanguages = languages,
			};

			// generate html files
			{
				var html = await razorEngine.CompileRenderAsync("chapter.cshtml", model);
				var outPath = Path.Combine(outContentPath, chapter.Number.ToString());
				if (!Directory.Exists(outPath))
					Directory.CreateDirectory(outPath);
				var dest = Path.Combine(outPath, "index.html");
				File.WriteAllText(dest, html);

				Console.WriteLine($"Saved '{book.Language.Name}' chapter {chapterNumber} to '{dest}'.");
			}
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
			{
				var dest = Path.Combine(outputPath, path);
				File.Copy(nonTemplate, dest, true);

				Console.WriteLine($"Copied '{nonTemplate}' to '{dest}'.");
			}
		}
	}
}
