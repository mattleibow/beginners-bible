using YamlDotNet.Serialization;

namespace tools
{
	public class ChapterMetadata
	{
		[YamlMember]
		public string Title { get; set; }

		[YamlMember]
		public string Bible { get; set; }
	}
}
