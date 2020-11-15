using System;
using System.CommandLine;
using System.CommandLine.Builder;
using System.CommandLine.Invocation;
using System.CommandLine.Parsing;
using System.IO;
using System.Threading.Tasks;

namespace tools
{
	class Program
	{
		static Task<int> Main(string[] args)
		{
			var rootCommand = new RootCommand("Book generator.")
			{
				new Option<DirectoryInfo>(
					"--content",
					"The directory with the content of the book.")
					{ IsRequired = true }
					.ExistingOnly(),
				new Option<DirectoryInfo>(
					"--templates",
					"The directory with the templates.")
					{ IsRequired = true }
					.ExistingOnly(),
				new Option<DirectoryInfo>(
					"--output",
					"The directory for the generated files.")
					{ IsRequired = true }
					.LegalFilePathsOnly(),
			};

			rootCommand.Handler = CommandHandler.Create<DirectoryInfo, DirectoryInfo, DirectoryInfo>(async (content, templates, output) =>
			{
				if (content == null)
				{
					Console.Error.WriteLine("The directory with the content of the book was not specified.");
					return 1;
				}
				if (templates == null)
				{
					Console.Error.WriteLine("The directory with the templates was not specified.");
					return 1;
				}
				if (output == null)
				{
					Console.Error.WriteLine("The directory for the generated files was not specified.");
					return 1;
				}

				var processor = new Processor(content.FullName, output.FullName, templates.FullName);
				await processor.ProcessAsync();

				return 0;
			});

			var parser = new CommandLineBuilder(rootCommand)
				.UseHelp()
				.UseSuggestDirective()
				.UseTypoCorrections()
				.UseVersionOption()
				.Build();

			return parser.InvokeAsync(args);
		}
	}
}
