﻿using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace HtmlCompressor.Core
{
	/// <summary>
	/// This is the only public class you should use. Although there are other classes
	/// in the "Internal" namespaces, these are unchanged to keep up the changes when a new
	/// Java version of Google's htmlcompressor library comes out. Therefore, use this interface
	/// only.
	/// </summary>
	/// <remarks>The classes in "Internal" still are public to have them accessible for
	/// the Unit Tests, which also are copied/adapted from the original Java sources.</remarks>
	public sealed class HtmlContentCompressor
	{
		private readonly Internal.HtmlCompressor _compressor = new Internal.HtmlCompressor();

		public string Compress(string html)
		{
			return _compressor.Compress(html);
		}

		public void AddPreservePattern(params Regex[] regexes)
		{
			var org = _compressor.GetPreservePatterns();

			var preservePatterns = new List<Regex>();
			if (org != null) preservePatterns.AddRange(org);
			preservePatterns.AddRange(regexes);

			_compressor.SetPreservePatterns(preservePatterns);
		}
	}
}