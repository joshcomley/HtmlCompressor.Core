using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using HtmlCompression.Core;
using HtmlCompression.Core.Preservation;

namespace HtmlCompression.Console
{
	public class Program
	{
		public static void Main(string[] args)
		{
			var compressor = new HtmlCompressor();
			compressor.Preservers.Add(new Preserver(new Regex("<!--\\s*build.*?\\s*-->",
				RegexOptions.Singleline | RegexOptions.IgnoreCase)));
			compressor.Preservers.Add(new Preserver(new Regex("<!--\\s*endbuild.*?\\s*-->",
				RegexOptions.Singleline | RegexOptions.IgnoreCase)));
			compressor.Settings.SurroundingSpaces = SurroundingSpaces.RemoveForAllTags;
			var text = compressor.Compress(File.ReadAllText(@"D:\B\BrandlessConsulting\Projects\Hazception\Code\Web\Client.Web\src\Hazception.Client.Web.FrontEnd\wwwroot\index.html"));
			File.WriteAllText(@"D:\Temp\compressed.html", text);
		}
	}
}
