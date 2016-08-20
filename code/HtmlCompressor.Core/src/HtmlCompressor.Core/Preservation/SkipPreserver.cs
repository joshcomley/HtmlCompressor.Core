using System.Text.RegularExpressions;

namespace HtmlCompression.Core.Preservation
{
	/// <summary>
	/// Preserve <!-- {{{ ---><!-- }}} ---> skip blocks
	/// </summary>
	public class SkipPreserver : Preserver
	{
		public SkipPreserver(Regex pattern) : base(pattern)
		{
			ExpandReplacement = false;
		}

		protected override bool AssertMatch(Match match)
		{
			return match.Groups[1].Value.Trim().Length > 0;
		}
	}
}