using System.Linq;

namespace tools
{
	public class Language
	{
		private string code;

		public string Code
		{
			get => code;
			set => code = value?.ToLowerInvariant();
		}

		public string? Country
		{
			get
			{
				var parts = Code?.Split('-');
				if (parts?.Length > 1)
					return parts[1];
				return parts?.FirstOrDefault();
			}
		}

		public string Name { get; set; }
	}
}
