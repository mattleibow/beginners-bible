using System;
using System.Threading.Tasks;
using RazorLight;

namespace generator
{
	class Program
	{
		static async Task Main(string[] args)
		{
			Console.WriteLine("Hello World!");

			var engine = new RazorLightEngineBuilder()
				.UseFileSystemProject(@"C:\Projects\beginners-bible\templates")
				.UseMemoryCachingProvider()
				.Build();

			var model = new
			{
				Name = "John Doe"
			};

			string result = await engine.CompileRenderAsync("chapter.cshtml", model);
		}
	}
}
