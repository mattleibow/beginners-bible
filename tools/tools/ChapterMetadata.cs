using System.Collections.Generic;
using YamlDotNet.Serialization;

namespace tools
{
	public class ChapterMetadata
	{
		[YamlMember]
		public string Title { get; set; }

		[YamlMember]
		public string Bible { get; set; }

		[YamlMember]
		public ChapterMetadataTags? Tags { get; set; }
	}

	public class ChapterMetadataTags
	{
		[YamlMember]
		public string? Testament { get; set; }

		[YamlMember]
		public string? Book { get; set; }

		[YamlMember]
		public List<string>? People { get; set; }
	}
}
