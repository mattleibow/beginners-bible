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

		[YamlMember]
		public bool HideInContents { get; set; }
	}

	public class ChapterMetadataTags
	{
		[YamlMember]
		public string? Testament { get; set; }

		[YamlMember]
		public List<string>? Books { get; set; }

		[YamlMember]
		public List<string>? People { get; set; }
	}
}
