using System.Threading.Tasks;

namespace tools
{
	class Program
	{
		static async Task Main(string[] args)
		{
			var rootPath = @"C:\Projects\beginners-bible\content";
			var outputPath = @"C:\Projects\beginners-bible\output";
			var templatesPath = @"C:\Projects\beginners-bible\templates";

			var processor = new Processor(rootPath, outputPath, templatesPath);
			await processor.ProcessAsync();
		}
	}
}
