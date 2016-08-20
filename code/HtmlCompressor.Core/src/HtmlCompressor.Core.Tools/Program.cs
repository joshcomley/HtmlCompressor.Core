using Microsoft.Extensions.CommandLineUtils;

namespace HtmlCompression.Core.Tools
{
	public class Program
	{
		public static void Main(string[] args)
		{
			var app = new CommandLineApplication
			{
				Name = "dotnet compress-html",
				FullName = "ASP.NET Core HTML compressor",
				Description = "HTML Compressor for ASP.NET Core applications",
			};
			app.HelpOption("-h|--help");
			var publishFolderOption = app.Option("-p|--publish-folder", "The path to the publish output folder",
				CommandOptionType.SingleValue);
			app.OnExecute(() =>
			{
				var publishFolder = publishFolderOption.Value();

				if (publishFolder == null)
				{
					app.ShowHelp();
					return 2;
				}
				return new CompressHtmlFilesCommand(publishFolder).Run();
			});
		}
	}
}