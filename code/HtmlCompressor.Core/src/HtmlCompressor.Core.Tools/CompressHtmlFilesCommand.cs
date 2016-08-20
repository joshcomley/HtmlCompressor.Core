using System.IO;

namespace HtmlCompression.Core.Tools
{
	public class CompressHtmlFilesCommand
	{
		private readonly string _publishFolder;
		private HtmlCompressor _htmlCompressor;

		public CompressHtmlFilesCommand(string publishFolder)
		{
			_publishFolder = publishFolder;
		}

		public int Run()
		{
			_htmlCompressor = new HtmlCompressor
			{
				Settings =
					{
						SurroundingSpaces = SurroundingSpaces.RemoveForAllTags,
						RemoveIntertagSpaces = true,
						RemoveMultiSpaces = true
					}
			};
			foreach (var file in Directory.EnumerateFiles(_publishFolder, "*.html"))
			{
				CompressHtml(file);
			}
			foreach (var file in Directory.EnumerateFiles(_publishFolder, "*.htm"))
			{
				CompressHtml(file);
			}
			return 0;
		}

		private void CompressHtml(string file)
		{
			var html = File.ReadAllText(file);
			html = _htmlCompressor.Compress(html);
			File.WriteAllText(file, html);
		}
	}
}