using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace HtmlCompressor.Core
{
	// Original von: https://code.google.com/p/htmlcompressor/
	// Diese Datei von https://code.google.com/p/htmlcompressor/source/browse/trunk/src/main/java/com/googlecode/htmlcompressor/compressor/HtmlCompressor.java
	// Tipps auf http://stackoverflow.com/questions/3789472/what-is-the-c-sharp-regex-equivalent-to-javas-appendreplacement-and-appendtail
	// Java-Regex auf http://www.devarticles.com/c/a/Java/Introduction-to-the-Javautilregex-Object-Model/8/

	/**
	 * Class that compresses given HTML source by removing comments, extra spaces and 
	 * line breaks while preserving content within &lt;pre>, &lt;textarea>, &lt;script> 
	 * and &lt;style> tags. 
	 * <p>Blocks that should be additionally preserved could be marked with:
	 * <br><code>&lt;!-- {{{ -->
	 * <br>&nbsp;&nbsp;&nbsp;&nbsp;...
	 * <br>&lt;!-- }}} --></code> 
	 * <br>or any number of user defined patterns. 
	 * <p>Content inside &lt;script> or &lt;style> tags could be optionally compressed using 
	 * <a href="http://developer.yahoo.com/yui/compressor/">Yahoo YUI ICompressor</a> or <a href="http://code.google.com/closure/compiler/">Google Closure Compiler</a>
	 * libraries.
	 * 
	 * @author <a href="mailto:serg472@gmail.com">Sergiy Kovalchuk</a>
	 */

	public sealed class HtmlCompressor :
	  ICompressor
	{
		public HtmlCompressorSettings Settings { get; set; }
		//public static readonly string JS_COMPRESSOR_YUI = "yui";
		//public static readonly string JS_COMPRESSOR_CLOSURE = "closure";

		public HtmlCompressor() : this(new HtmlCompressorSettings())
		{

		}

		public HtmlCompressor(HtmlCompressorSettings settings)
		{
			Settings = settings;
		}

		/**
		 * Predefined pattern that matches <code>&lt;?php ... ?></code> tags. 
		 * Could be passed inside a list to {@link #setPreservePatterns(List) setPreservePatterns} method.
		 */

		public static readonly Regex PhpTagPattern = new Regex("<\\?php.*?\\?>",
		  RegexOptions.Singleline | RegexOptions.IgnoreCase);

		/**
		 * Predefined pattern that matches <code>&lt;% ... %></code> tags. 
		 * Could be passed inside a list to {@link #setPreservePatterns(List) setPreservePatterns} method.
		 */
		public static readonly Regex ServerScriptTagPattern = new Regex("<%.*?%>", RegexOptions.Singleline);

		/**
		 * Predefined pattern that matches <code>&lt;--# ... --></code> tags. 
		 * Could be passed inside a list to {@link #setPreservePatterns(List) setPreservePatterns} method.
		 */
		public static readonly Regex ServerSideIncludePattern = new Regex("<!--\\s*#.*?-->", RegexOptions.Singleline);

		/**
		 * Predefined list of tags that are very likely to be block-level. 
		 * Could be passed to {@link #setRemoveSurroundingSpaces(string) setRemoveSurroundingSpaces} method.
		 */
		public static readonly string BlockTagsMin = "html,head,body,br,p";

		/**
		 * Predefined list of tags that are block-level by default, excluding <code>&lt;div></code> and <code>&lt;li></code> tags. 
		 * Table tags are also included.
		 * Could be passed to {@link #setRemoveSurroundingSpaces(string) setRemoveSurroundingSpaces} method.
		 */

		public static readonly string BlockTagsMax = BlockTagsMin +
								 ",h1,h2,h3,h4,h5,h6,blockquote,center,dl,fieldset,form,frame,frameset,hr,noframes,ol,table,tbody,tr,td,th,tfoot,thead,ul";

		/**
		 * Could be passed to {@link #setRemoveSurroundingSpaces(string) setRemoveSurroundingSpaces} method 
		 * to remove all surrounding spaces (not recommended).
		 */
		public static readonly string AllTags = "all";

		////YUICompressor settings
		//private bool yuiJsNoMunge = false;
		//private bool yuiJsPreserveAllSemiColons = false;
		//private bool yuiJsDisableOptimizations = false;
		//private int yuiJsLineBreak = -1;
		//private int yuiCssLineBreak = -1;

		////error reporter implementation for YUI compressor
		//private ErrorReporter yuiErrorReporter = null;

		//temp replacements for preserved blocks 
		private const string TempCondCommentBlock = "%%%~COMPRESS~COND~{0}~%%%";
		private const string TempPreBlock = "%%%~COMPRESS~PRE~{0}~%%%";
		private const string TempTextAreaBlock = "%%%~COMPRESS~TEXTAREA~{0}~%%%";
		private const string TempScriptBlock = "%%%~COMPRESS~SCRIPT~{0}~%%%";
		private const string TempStyleBlock = "%%%~COMPRESS~STYLE~{0}~%%%";
		private const string TempEventBlock = "%%%~COMPRESS~EVENT~{0}~%%%";
		private const string TempLineBreakBlock = "%%%~COMPRESS~LT~{0}~%%%";
		private const string TempSkipBlock = "%%%~COMPRESS~SKIP~{0}~%%%";
		private const string TempUserBlock = "%%%~COMPRESS~USER{0}~{1}~%%%";

		//compiled regex patterns
		private static readonly Regex EmptyPattern = new Regex("\\s");

		private static readonly Regex SkipPattern = new Regex("<!--\\s*\\{\\{\\{\\s*-->(.*?)<!--\\s*\\}\\}\\}\\s*-->",
		  RegexOptions.Singleline | RegexOptions.IgnoreCase);

		private static readonly Regex CondCommentPattern = new Regex("(<!(?:--)?\\[[^\\]]+?]>)(.*?)(<!\\[[^\\]]+]-->)",
		  RegexOptions.Singleline | RegexOptions.IgnoreCase);

		private static readonly Regex CommentPattern = new Regex("<!---->|<!--[^\\[].*?-->",
		  RegexOptions.Singleline | RegexOptions.IgnoreCase);

		private static readonly Regex IntertagPatternTagTag = new Regex(">\\s+<",
		  RegexOptions.Singleline | RegexOptions.IgnoreCase);

		private static readonly Regex IntertagPatternTagCustom = new Regex(">\\s+%%%~",
		  RegexOptions.Singleline | RegexOptions.IgnoreCase);

		private static readonly Regex IntertagPatternCustomTag = new Regex("~%%%\\s+<",
		  RegexOptions.Singleline | RegexOptions.IgnoreCase);

		private static readonly Regex IntertagPatternCustomCustom = new Regex("~%%%\\s+%%%~",
		  RegexOptions.Singleline |
		  RegexOptions.IgnoreCase);

		private static readonly Regex MultispacePattern = new Regex("\\s+", RegexOptions.Singleline | RegexOptions.IgnoreCase);

		private static readonly Regex TagEndSpacePattern = new Regex("(<(?:[^>]+?))(?:\\s+?)(/?>)",
		  RegexOptions.Singleline | RegexOptions.IgnoreCase);

		private static readonly Regex TagLastUnquotedValuePattern = new Regex("=\\s*[a-z0-9-_]+$", RegexOptions.IgnoreCase);

		private static readonly Regex TagQuotePattern = new Regex("\\s*=\\s*([\"'])([a-z0-9-_]+?)\\1(/?)(?=[^<]*?>)",
		  RegexOptions.IgnoreCase);

		private static readonly Regex PrePattern = new Regex("(<pre[^>]*?>)(.*?)(</pre>)",
		  RegexOptions.Singleline | RegexOptions.IgnoreCase);

		private static readonly Regex TaPattern = new Regex("(<textarea[^>]*?>)(.*?)(</textarea>)",
		  RegexOptions.Singleline | RegexOptions.IgnoreCase);

		private static readonly Regex ScriptPattern = new Regex("(<script[^>]*?>)(.*?)(</script>)",
		  RegexOptions.Singleline | RegexOptions.IgnoreCase);

		private static readonly Regex StylePattern = new Regex("(<style[^>]*?>)(.*?)(</style>)",
		  RegexOptions.Singleline | RegexOptions.IgnoreCase);

		private static readonly Regex TagPropertyPattern = new Regex("(\\s\\w+)\\s*=\\s*(?=[^<]*?>)", RegexOptions.IgnoreCase);

		private static readonly Regex CdataPattern = new Regex("\\s*<!\\[CDATA\\[(.*?)\\]\\]>\\s*",
		  RegexOptions.Singleline | RegexOptions.IgnoreCase);

		private static readonly Regex ScriptCdataPattern = new Regex("/\\*\\s*<!\\[CDATA\\[\\*/(.*?)/\\*\\]\\]>\\s*\\*/",
		  RegexOptions.Singleline | RegexOptions.IgnoreCase);

		private static readonly Regex DoctypePattern = new Regex("<!DOCTYPE[^>]*>",
		  RegexOptions.Singleline | RegexOptions.IgnoreCase);

		private static readonly Regex TypeAttrPattern = new Regex("type\\s*=\\s*([\\\"']*)(.+?)\\1",
		  RegexOptions.Singleline | RegexOptions.IgnoreCase);

		private static readonly Regex JsTypeAttrPattern =
		  new Regex("(<script[^>]*)type\\s*=\\s*([\"']*)(?:text|application)/javascript\\2([^>]*>)",
			RegexOptions.Singleline | RegexOptions.IgnoreCase);

		private static readonly Regex JsLangAttrPattern =
		  new Regex("(<script[^>]*)language\\s*=\\s*([\"']*)javascript\\2([^>]*>)",
			RegexOptions.Singleline | RegexOptions.IgnoreCase);

		private static readonly Regex StyleTypeAttrPattern =
		  new Regex("(<style[^>]*)type\\s*=\\s*([\"']*)text/style\\2([^>]*>)",
			RegexOptions.Singleline | RegexOptions.IgnoreCase);

		private static readonly Regex LinkTypeAttrPattern =
		  new Regex("(<link[^>]*)type\\s*=\\s*([\"']*)text/(?:css|plain)\\2([^>]*>)",
			RegexOptions.Singleline | RegexOptions.IgnoreCase);

		private static readonly Regex LinkRelAttrPattern =
		  new Regex("<link(?:[^>]*)rel\\s*=\\s*([\"']*)(?:alternate\\s+)?stylesheet\\1(?:[^>]*)>",
			RegexOptions.Singleline | RegexOptions.IgnoreCase);

		private static readonly Regex FormMethodAttrPattern = new Regex("(<form[^>]*)method\\s*=\\s*([\"']*)get\\2([^>]*>)",
		  RegexOptions.Singleline | RegexOptions.IgnoreCase);

		private static readonly Regex InputTypeAttrPattern = new Regex("(<input[^>]*)type\\s*=\\s*([\"']*)text\\2([^>]*>)",
		  RegexOptions.Singleline | RegexOptions.IgnoreCase);

		private static readonly Regex BooleanAttrPattern =
		  new Regex("(<\\w+[^>]*)(checked|selected|disabled|readonly)\\s*=\\s*([\"']*)\\w*\\3([^>]*>)",
			RegexOptions.Singleline | RegexOptions.IgnoreCase);

		private static readonly Regex EventJsProtocolPattern = new Regex("^javascript:\\s*(.+)",
		  RegexOptions.Singleline | RegexOptions.IgnoreCase);

		private static readonly Regex HttpProtocolPattern =
		  new Regex("(<[^>]+?(?:href|src|cite|action)\\s*=\\s*['\"])http:(//[^>]+?>)",
			RegexOptions.Singleline | RegexOptions.IgnoreCase);

		private static readonly Regex HttpsProtocolPattern =
		  new Regex("(<[^>]+?(?:href|src|cite|action)\\s*=\\s*['\"])https:(//[^>]+?>)",
			RegexOptions.Singleline | RegexOptions.IgnoreCase);

		private static readonly Regex RelExternalPattern =
		  new Regex("<(?:[^>]*)rel\\s*=\\s*([\"']*)(?:alternate\\s+)?external\\1(?:[^>]*)>",
			RegexOptions.Singleline | RegexOptions.IgnoreCase);

		private static readonly Regex EventPattern1 =
		  new Regex("(\\son[a-z]+\\s*=\\s*\")([^\"\\\\\\r\\n]*(?:\\\\.[^\"\\\\\\r\\n]*)*)(\")", RegexOptions.IgnoreCase);

		//unmasked: \son[a-z]+\s*=\s*"[^"\\\r\n]*(?:\\.[^"\\\r\n]*)*"

		private static readonly Regex EventPattern2 =
		  new Regex("(\\son[a-z]+\\s*=\\s*')([^'\\\\\\r\\n]*(?:\\\\.[^'\\\\\\r\\n]*)*)(')", RegexOptions.IgnoreCase);

		private static readonly Regex LineBreakPattern = new Regex("(?:[ \t]*(\\r?\\n)[ \t]*)+");

		//private static readonly Regex SurroundingSpacesMinPattern =
		//  new Regex("\\s*(</?(?:" + BlockTagsMin.Replace(",", "|") + ")(?:>|[\\s/][^>]*>))\\s*",
		//    RegexOptions.Singleline | RegexOptions.IgnoreCase);

		//private static readonly Regex SurroundingSpacesMaxPattern =
		//  new Regex("\\s*(</?(?:" + BlockTagsMax.Replace(",", "|") + ")(?:>|[\\s/][^>]*>))\\s*",
		//    RegexOptions.Singleline | RegexOptions.IgnoreCase);

		private static readonly Regex SurroundingSpacesAllPattern = new Regex("\\s*(<[^>]+>)\\s*",
		  RegexOptions.Singleline |
		  RegexOptions.IgnoreCase);

		//patterns for searching for temporary replacements
		private static readonly Regex TempCondCommentPattern = new Regex("%%%~COMPRESS~COND~(\\d+?)~%%%");
		private static readonly Regex TempPrePattern = new Regex("%%%~COMPRESS~PRE~(\\d+?)~%%%");
		private static readonly Regex TempTextAreaPattern = new Regex("%%%~COMPRESS~TEXTAREA~(\\d+?)~%%%");
		private static readonly Regex TempScriptPattern = new Regex("%%%~COMPRESS~SCRIPT~(\\d+?)~%%%");
		private static readonly Regex TempStylePattern = new Regex("%%%~COMPRESS~STYLE~(\\d+?)~%%%");
		private static readonly Regex TempEventPattern = new Regex("%%%~COMPRESS~EVENT~(\\d+?)~%%%");
		private static readonly Regex TempSkipPattern = new Regex("%%%~COMPRESS~SKIP~(\\d+?)~%%%");
		private static readonly Regex TempLineBreakPattern = new Regex("%%%~COMPRESS~LT~(\\d+?)~%%%");

		//statistics

		//javascript and css compressor implementations
		private HtmlCompressorStatistics _statistics;

		/**
		 * The main method that compresses given HTML source and returns compressed
		 * result.
		 * 
		 * @param html HTML content to compress
		 * @return compressed content.
		 */

		public string Compress(string html)
		{
			if (!Settings.Enabled || string.IsNullOrEmpty(html))
			{
				return html;
			}

			//calculate uncompressed statistics
			InitStatistics(html);

			//preserved block containers
			var condCommentBlocks = new List<string>();
			var preBlocks = new List<string>();
			var taBlocks = new List<string>();
			var scriptBlocks = new List<string>();
			var styleBlocks = new List<string>();
			var eventBlocks = new List<string>();
			var skipBlocks = new List<string>();
			var lineBreakBlocks = new List<string>();
			var userBlocks = new List<List<string>>();

			//preserve blocks
			html = PreserveBlocks(html, preBlocks, taBlocks, scriptBlocks, styleBlocks, eventBlocks, condCommentBlocks,
			  skipBlocks, lineBreakBlocks, userBlocks);

			//process pure html
			html = ProcessHtml(html);

			//process preserved blocks
			ProcessPreservedBlocks(preBlocks, taBlocks, scriptBlocks, styleBlocks, eventBlocks, condCommentBlocks, skipBlocks,
			  lineBreakBlocks, userBlocks);

			//put preserved blocks back
			html = ReturnBlocks(html, preBlocks, taBlocks, scriptBlocks, styleBlocks, eventBlocks, condCommentBlocks, skipBlocks,
			  lineBreakBlocks, userBlocks);

			//calculate compressed statistics
			EndStatistics(html);

			return html;
		}

		private void InitStatistics(string html)
		{
			//create stats
			if (Settings.GenerateStatistics)
			{
				_statistics = new HtmlCompressorStatistics();
				_statistics.SetTime(DateTime.Now.Ticks);
				_statistics.GetOriginalMetrics().SetFilesize(html.Length);

				//calculate number of empty chars
				var matcher = EmptyPattern.Matches(html);
				_statistics.GetOriginalMetrics().SetEmptyChars(_statistics.GetOriginalMetrics().GetEmptyChars() + matcher.Count);
			}
			else
			{
				_statistics = null;
			}
		}

		private void EndStatistics(string html)
		{
			//calculate compression time
			if (Settings.GenerateStatistics)
			{
				_statistics.SetTime(DateTime.Now.Ticks - _statistics.GetTime());
				_statistics.GetCompressedMetrics().SetFilesize(html.Length);

				//calculate number of empty chars
				var matcher = EmptyPattern.Matches(html);
				_statistics.GetCompressedMetrics().SetEmptyChars(_statistics.GetCompressedMetrics().GetEmptyChars() + matcher.Count);
			}
		}

		private string PreserveBlocks(
		  string html,
		  ICollection<string> preBlocks,
		  ICollection<string> taBlocks,
		  ICollection<string> scriptBlocks,
		  ICollection<string> styleBlocks,
		  ICollection<string> eventBlocks,
		  ICollection<string> condCommentBlocks,
		  ICollection<string> skipBlocks,
		  ICollection<string> lineBreakBlocks,
		  ICollection<List<string>> userBlocks)
		{
			//preserve user blocks
			if (Settings.PreservePatterns != null)
			{
				for (var p = 0; p < Settings.PreservePatterns.Count; p++)
				{
					var userBlock = new List<string>();

					var matches = Settings.PreservePatterns[p].Matches(html);
					var index = 0;
					var sb = new StringBuilder();
					var lastValue = 0;

					foreach (Match match in matches)
					{
						if (match.Groups[0].Value.Trim().Length > 0)
						{
							userBlock.Add(match.Groups[0].Value);

							sb.Append(html.Substring(lastValue, match.Index - lastValue));
							//matches.appendReplacement(sb1, string.Format(tempUserBlock, p, index++));
							sb.Append(match.Result(string.Format(TempUserBlock, p, index++)));

							lastValue = match.Index + match.Length;
						}
					}

					//matches.appendTail(sb1);
					sb.Append(html.Substring(lastValue));

					html = sb.ToString();
					userBlocks.Add(userBlock);
				}
			}

			var skipBlockIndex = 0;

			//preserve <!-- {{{ ---><!-- }}} ---> skip blocks
			if (true)
			{
				var matcher = SkipPattern.Matches(html);
				var sb = new StringBuilder();
				var lastValue = 0;

				foreach (Match match in matcher)
				{
					if (match.Groups[1].Value.Trim().Length > 0)
					{
						skipBlocks.Add(match.Groups[1].Value);

						sb.Append(html.Substring(lastValue, match.Index - lastValue));
						//matcher.appendReplacement(sb, string.Format(tempSkipBlock, skipBlockIndex++));
						sb.Append(match.Result(string.Format(TempSkipBlock, skipBlockIndex++)));

						lastValue = match.Index + match.Length;
					}
				}

				//matcher.appendTail(sb);
				sb.Append(html.Substring(lastValue));

				html = sb.ToString();
			}

			//preserve conditional comments
			if (true)
			{
				var condCommentCompressor = CreateCompressorClone();
				var matcher = CondCommentPattern.Matches(html);
				var index = 0;
				var sb = new StringBuilder();
				var lastValue = 0;

				foreach (Match match in matcher)
				{
					if (match.Groups[2].Value.Trim().Length > 0)
					{
						condCommentBlocks.Add(
						  match.Groups[1].Value + condCommentCompressor.Compress(match.Groups[2].Value) + match.Groups[3].Value);

						sb.Append(html.Substring(lastValue, match.Index - lastValue));
						//matcher.appendReplacement(sb, string.Format(tempCondCommentBlock, index++));
						sb.Append(match.Result(string.Format(TempCondCommentBlock, index++)));

						lastValue = match.Index + match.Length;
					}
				}

				//matcher.appendTail(sb);
				sb.Append(html.Substring(lastValue));

				html = sb.ToString();
			}

			//preserve inline events
			if (true)
			{
				var matcher = EventPattern1.Matches(html);
				var index = 0;
				var sb = new StringBuilder();
				var lastValue = 0;

				foreach (Match match in matcher)
				{
					if (match.Groups[2].Value.Trim().Length > 0)
					{
						eventBlocks.Add(match.Groups[2].Value);

						sb.Append(html.Substring(lastValue, match.Index - lastValue));
						//matcher.appendReplacement(sb, "$1" + string.Format(tempEventBlock, index++) + "$3");
						sb.Append(match.Result("$1" + string.Format(TempEventBlock, index++) + "$3"));

						lastValue = match.Index + match.Length;
					}
				}

				//matcher.appendTail(sb);
				sb.Append(html.Substring(lastValue));

				html = sb.ToString();
			}

			if (true)
			{
				var matcher = EventPattern2.Matches(html);
				var index = 0;
				var sb = new StringBuilder();
				var lastValue = 0;

				foreach (Match match in matcher)
				{
					if (match.Groups[2].Value.Trim().Length > 0)
					{
						eventBlocks.Add(match.Groups[2].Value);

						sb.Append(html.Substring(lastValue, match.Index - lastValue));
						//matcher.appendReplacement(sb, "$1" + string.Format(tempEventBlock, index++) + "$3");
						sb.Append(match.Result("$1" + string.Format(TempEventBlock, index++) + "$3"));

						lastValue = match.Index + match.Length;
					}
				}

				//matcher.appendTail(sb);
				sb.Append(html.Substring(lastValue));

				html = sb.ToString();
			}

			//preserve PRE tags
			if (true)
			{
				var matcher = PrePattern.Matches(html);
				var index = 0;
				var sb = new StringBuilder();
				var lastValue = 0;

				foreach (Match match in matcher)
				{
					if (match.Groups[2].Value.Trim().Length > 0)
					{
						preBlocks.Add(match.Groups[2].Value);

						sb.Append(html.Substring(lastValue, match.Index - lastValue));
						//matcher.appendReplacement(sb, "$1" + string.Format(tempPreBlock, index++) + "$3");
						sb.Append(match.Result("$1" + string.Format(TempPreBlock, index++) + "$3"));

						lastValue = match.Index + match.Length;
					}
				}

				//matcher.appendTail(sb);
				sb.Append(html.Substring(lastValue));

				html = sb.ToString();
			}

			//preserve SCRIPT tags
			if (true)
			{
				var matcher = ScriptPattern.Matches(html);
				var index = 0;
				var sb = new StringBuilder();
				var lastValue = 0;

				foreach (Match match in matcher)
				{
					//ignore empty scripts
					if (match.Groups[2].Value.Trim().Length > 0)
					{
						//check type
						var type = "";
						var typeMatcher = TypeAttrPattern.Match(match.Groups[1].Value);
						if (typeMatcher.Success)
						{
							type = typeMatcher.Groups[2].Value.ToLowerInvariant();
						}

						if (type.Length == 0 || type.Equals("text/javascript") || type.Equals("application/javascript"))
						{
							//javascript block, preserve and compress with js compressor
							scriptBlocks.Add(match.Groups[2].Value);

							sb.Append(html.Substring(lastValue, match.Index - lastValue));
							//matcher.appendReplacement(sb, "$1" + string.Format(tempScriptBlock, index++) + "$3");
							sb.Append(match.Result("$1" + string.Format(TempScriptBlock, index++) + "$3"));

							lastValue = match.Index + match.Length;
						}
						else if (type.Equals("text/x-jquery-tmpl"))
						{
							//jquery template, ignore so it gets compressed with the rest of html
						}
						else
						{
							//some custom script, preserve it inside "skip blocks" so it won't be compressed with js compressor 
							skipBlocks.Add(match.Groups[2].Value);

							sb.Append(html.Substring(lastValue, match.Index - lastValue));
							//matcher.appendReplacement(sb, "$1" + string.Format(tempSkipBlock, skipBlockIndex++) + "$3");
							sb.Append(match.Result("$1" + string.Format(TempSkipBlock, skipBlockIndex++) + "$3"));

							lastValue = match.Index + match.Length;
						}
					}
				}

				//matcher.appendTail(sb);
				sb.Append(html.Substring(lastValue));

				html = sb.ToString();
			}

			//preserve STYLE tags
			if (true)
			{
				var matcher = StylePattern.Matches(html);
				var index = 0;
				var sb = new StringBuilder();
				var lastValue = 0;

				foreach (Match match in matcher)
				{
					if (match.Groups[2].Value.Trim().Length > 0)
					{
						styleBlocks.Add(match.Groups[2].Value);

						sb.Append(html.Substring(lastValue, match.Index - lastValue));
						//matcher.appendReplacement(sb, "$1" + string.Format(tempStyleBlock, index++) + "$3");
						sb.Append(match.Result("$1" + string.Format(TempStyleBlock, index++) + "$3"));

						lastValue = match.Index + match.Length;
					}
				}

				//matcher.appendTail(sb);
				sb.Append(html.Substring(lastValue));

				html = sb.ToString();
			}

			//preserve TEXTAREA tags
			if (true)
			{
				var matcher = TaPattern.Matches(html);
				var index = 0;
				var sb = new StringBuilder();
				var lastValue = 0;

				foreach (Match match in matcher)
				{
					if (match.Groups[2].Value.Trim().Length > 0)
					{
						taBlocks.Add(match.Groups[2].Value);

						sb.Append(html.Substring(lastValue, match.Index - lastValue));
						//matcher.appendReplacement(sb, "$1" + string.Format(tempTextAreaBlock, index++) + "$3");
						sb.Append(match.Result("$1" + string.Format(TempTextAreaBlock, index++) + "$3"));

						lastValue = match.Index + match.Length;
					}
				}

				//matcher.appendTail(sb);
				sb.Append(html.Substring(lastValue));

				html = sb.ToString();
			}

			//preserve line breaks
			if (Settings.PreserveLineBreaks)
			{
				var matcher = LineBreakPattern.Matches(html);
				var index = 0;
				var sb = new StringBuilder();
				var lastValue = 0;

				foreach (Match match in matcher)
				{
					lineBreakBlocks.Add(match.Groups[1].Value);

					sb.Append(html.Substring(lastValue, match.Index - lastValue));
					//matcher.appendReplacement(sb, string.Format(tempLineBreakBlock, index++));
					sb.Append(match.Result(string.Format(TempLineBreakBlock, index++)));

					lastValue = match.Index + match.Length;
				}

				//matcher.appendTail(sb);
				sb.Append(html.Substring(lastValue));

				html = sb.ToString();
			}

			return html;
		}

		private string ReturnBlocks(
		  string html,
		  List<string> preBlocks,
		  List<string> taBlocks,
		  List<string> scriptBlocks,
		  List<string> styleBlocks,
		  List<string> eventBlocks,
		  List<string> condCommentBlocks,
		  List<string> skipBlocks,
		  List<string> lineBreakBlocks,
		  List<List<string>> userBlocks)
		{
			//put line breaks back
			if (Settings.PreserveLineBreaks)
			{
				var matcher = TempLineBreakPattern.Matches(html);
				var sb = new StringBuilder();
				var lastValue = 0;

				foreach (Match match in matcher)
				{
					var i = int.Parse(match.Groups[1].Value);
					if (lineBreakBlocks.Count > i)
					{
						sb.Append(html.Substring(lastValue, match.Index - lastValue));
						//matcher.appendReplacement(sb, lineBreakBlocks[i]);
						sb.Append(match.Result(lineBreakBlocks[i]));

						lastValue = match.Index + match.Length;
					}
				}

				//matcher.appendTail(sb);
				sb.Append(html.Substring(lastValue));

				html = sb.ToString();
			}

			//put TEXTAREA blocks back
			if (true)
			{
				var matcher = TempTextAreaPattern.Matches(html);
				var sb = new StringBuilder();
				var lastValue = 0;

				foreach (Match match in matcher)
				{
					var i = int.Parse(match.Groups[1].Value);
					if (taBlocks.Count > i)
					{
						sb.Append(html.Substring(lastValue, match.Index - lastValue));
						//matcher.appendReplacement(sb, Regex.Escape(taBlocks[i]));
						sb.Append(match.Result( /*Regex.Escape*/taBlocks[i]));

						lastValue = match.Index + match.Length;
					}
				}

				//matcher.appendTail(sb);
				sb.Append(html.Substring(lastValue));

				html = sb.ToString();
			}

			//put STYLE blocks back
			if (true)
			{
				var matcher = TempStylePattern.Matches(html);
				var sb = new StringBuilder();
				var lastValue = 0;

				foreach (Match match in matcher)
				{
					var i = int.Parse(match.Groups[1].Value);
					if (styleBlocks.Count > i)
					{
						sb.Append(html.Substring(lastValue, match.Index - lastValue));
						//matcher.appendReplacement(sb, Regex.Escape(styleBlocks[i]));
						sb.Append(match.Result( /*Regex.Escape*/styleBlocks[i]));

						lastValue = match.Index + match.Length;
					}
				}

				//matcher.appendTail(sb);
				sb.Append(html.Substring(lastValue));

				html = sb.ToString();
			}

			//put SCRIPT blocks back
			if (true)
			{
				var matcher = TempScriptPattern.Matches(html);
				var sb = new StringBuilder();
				var lastValue = 0;

				foreach (Match match in matcher)
				{
					var i = int.Parse(match.Groups[1].Value);
					if (scriptBlocks.Count > i)
					{
						sb.Append(html.Substring(lastValue, match.Index - lastValue));
						//matcher.appendReplacement(sb, Regex.Escape(scriptBlocks[i]));
						sb.Append(match.Result( /*Regex.Escape*/scriptBlocks[i]));

						lastValue = match.Index + match.Length;
					}
				}

				//matcher.appendTail(sb);
				sb.Append(html.Substring(lastValue));

				html = sb.ToString();
			}

			//put PRE blocks back
			if (true)
			{
				var matcher = TempPrePattern.Matches(html);
				var sb = new StringBuilder();
				var lastValue = 0;

				foreach (Match match in matcher)
				{
					var i = int.Parse(match.Groups[1].Value);
					if (preBlocks.Count > i)
					{
						sb.Append(html.Substring(lastValue, match.Index - lastValue));
						//matcher.appendReplacement(sb, Regex.Escape(preBlocks[i]));
						sb.Append(match.Result( /*Regex.Escape*/preBlocks[i]));

						lastValue = match.Index + match.Length;
					}
				}

				//matcher.appendTail(sb);
				sb.Append(html.Substring(lastValue));

				html = sb.ToString();
			}

			//put event blocks back
			if (true)
			{
				var matcher = TempEventPattern.Matches(html);
				var sb = new StringBuilder();
				var lastValue = 0;

				foreach (Match match in matcher)
				{
					var i = int.Parse(match.Groups[1].Value);
					if (eventBlocks.Count > i)
					{
						sb.Append(html.Substring(lastValue, match.Index - lastValue));
						//matcher.appendReplacement(sb, Regex.Escape(eventBlocks[i]));
						sb.Append(match.Result( /*Regex.Escape*/eventBlocks[i]));

						lastValue = match.Index + match.Length;
					}
				}

				//matcher.appendTail(sb);
				sb.Append(html.Substring(lastValue));

				html = sb.ToString();
			}

			//put conditional comments back
			if (true)
			{
				var matcher = TempCondCommentPattern.Matches(html);
				var sb = new StringBuilder();
				var lastValue = 0;

				foreach (Match match in matcher)
				{
					var i = int.Parse(match.Groups[1].Value);
					if (condCommentBlocks.Count > i)
					{
						sb.Append(html.Substring(lastValue, match.Index - lastValue));
						//matcher.appendReplacement(sb, Regex.Escape(condCommentBlocks[i]));
						sb.Append(match.Result( /*Regex.Escape*/condCommentBlocks[i]));

						lastValue = match.Index + match.Length;
					}
				}

				//matcher.appendTail(sb);
				sb.Append(html.Substring(lastValue));

				html = sb.ToString();
			}

			//put skip blocks back
			if (true)
			{
				var matcher = TempSkipPattern.Matches(html);
				var sb = new StringBuilder();
				var lastValue = 0;

				foreach (Match match in matcher)
				{
					var i = int.Parse(match.Groups[1].Value);
					if (skipBlocks.Count > i)
					{
						sb.Append(html.Substring(lastValue, match.Index - lastValue));
						//matcher.appendReplacement(sb, Regex.Escape(skipBlocks[i]));
						sb.Append(match.Result( /*Regex.Escape*/skipBlocks[i]));

						lastValue = match.Index + match.Length;
					}
				}

				//matcher.appendTail(sb);
				sb.Append(html.Substring(lastValue));

				html = sb.ToString();
			}

			//put user blocks back
			if (Settings.PreservePatterns != null)
			{
				for (var p = Settings.PreservePatterns.Count - 1; p >= 0; p--)
				{
					var tempUserPattern = new Regex("%%%~COMPRESS~USER" + p + "~(\\d+?)~%%%");
					var matcher = tempUserPattern.Matches(html);
					var sb = new StringBuilder();
					var lastValue = 0;

					foreach (Match match in matcher)
					{
						var i = int.Parse(match.Groups[1].Value);
						if (userBlocks.Count > p && userBlocks[p].Count > i)
						{
							sb.Append(html.Substring(lastValue, match.Index - lastValue));
							//matcher.appendReplacement(sb, Regex.Escape(userBlocks[p][i]));
							sb.Append(match.Result( /*Regex.Escape*/userBlocks[p][i]));

							lastValue = match.Index + match.Length;
						}
					}

					//matcher.appendTail(sb);
					sb.Append(html.Substring(lastValue));

					html = sb.ToString();
				}
			}

			return html;
		}

		private string ProcessHtml(string html)
		{
			//remove comments
			html = RemoveComments(html);

			//simplify doctype
			html = SimpleDoctype(html);

			//remove script attributes
			html = RemoveScriptAttributes(html);

			//remove style attributes
			html = RemoveStyleAttributes(html);

			//remove link attributes
			html = RemoveLinkAttributes(html);

			//remove form attributes
			html = RemoveFormAttributes(html);

			//remove input attributes
			html = RemoveInputAttributes(html);

			//simplify bool attributes
			html = SimpleBooleanAttributes(html);

			//remove http from attributes
			html = RemoveHttpProtocol(html);

			//remove https from attributes
			html = RemoveHttpsProtocol(html);

			//remove inter-tag spaces
			html = RemoveIntertagSpaces(html);

			//remove multi whitespace characters
			html = RemoveMultiSpaces(html);

			//remove spaces around equals sign and ending spaces
			html = RemoveSpacesInsideTags(html);

			//remove quotes from tag attributes
			html = RemoveQuotesInsideTags(html);

			//remove surrounding spaces
			html = RemoveSurroundingSpaces(html);

			return html.Trim();
		}

		private string RemoveSurroundingSpaces(string html)
		{
			//remove spaces around provided tags
			if (Settings.RemoveSurroundingSpaces != null)
			{
				Regex pattern;
				if (string.Compare(Settings.RemoveSurroundingSpaces, BlockTagsMin, StringComparison.CurrentCultureIgnoreCase) == 0
				  || string.Compare(Settings.RemoveSurroundingSpaces, BlockTagsMax, StringComparison.CurrentCultureIgnoreCase) == 0
				  || string.Compare(Settings.RemoveSurroundingSpaces, AllTags, StringComparison.CurrentCultureIgnoreCase) == 0)
				{
					pattern = SurroundingSpacesAllPattern;
				}
				else
				{
					pattern = new Regex($"\\s*(</?(?:{Settings.RemoveSurroundingSpaces.Replace(",", "|")})(?:>|[\\s/][^>]*>))\\s*",
					  RegexOptions.Singleline | RegexOptions.IgnoreCase);
				}

				var matcher = pattern.Matches(html);
				var sb = new StringBuilder();
				var lastValue = 0;

				foreach (Match match in matcher)
				{
					sb.Append(html.Substring(lastValue, match.Index - lastValue));
					//matcher.appendReplacement(sb, "$1");
					sb.Append(match.Result("$1"));

					lastValue = match.Index + match.Length;
				}

				//matcher.appendTail(sb);
				sb.Append(html.Substring(lastValue));

				html = sb.ToString();
			}
			return html;
		}

		private string RemoveQuotesInsideTags(string html)
		{
			//remove quotes from tag attributes
			if (Settings.RemoveQuotes)
			{
				var matcher = TagQuotePattern.Matches(html);
				var sb = new StringBuilder();
				var lastValue = 0;

				foreach (Match match in matcher)
				{
					//if quoted attribute is followed by "/" add extra space
					if (match.Groups[3].Value.Trim().Length == 0)
					{
						sb.Append(html.Substring(lastValue, match.Index - lastValue));
						//matcher.appendReplacement(sb, "=$2");
						sb.Append(match.Result("=$2"));

						lastValue = match.Index + match.Length;
					}
					else
					{
						sb.Append(html.Substring(lastValue, match.Index - lastValue));
						//matcher.appendReplacement(sb, "=$2 $3");
						sb.Append(match.Result("=$2 $3"));

						lastValue = match.Index + match.Length;
					}
				}

				//matcher.appendTail(sb);
				sb.Append(html.Substring(lastValue));

				html = sb.ToString();
			}
			return html;
		}

		private string RemoveSpacesInsideTags(string html)
		{
			//remove spaces around equals sign inside tags
			html = TagPropertyPattern.Replace(html, "$1=");

			//remove ending spaces inside tags
			//html = tagEndSpacePattern.Matches(html).Replace("$1$2");
			var matcher = TagEndSpacePattern.Matches(html);
			var sb = new StringBuilder();
			var lastValue = 0;

			foreach (Match match in matcher)
			{
				//keep space if attribute value is unquoted before trailing slash
				if (match.Groups[2].Value.StartsWith("/") && TagLastUnquotedValuePattern.IsMatch(match.Groups[1].Value))
				{
					sb.Append(html.Substring(lastValue, match.Index - lastValue));
					//matcher.appendReplacement(sb, "$1 $2");
					sb.Append(match.Result("$1 $2"));

					lastValue = match.Index + match.Length;
				}
				else
				{
					sb.Append(html.Substring(lastValue, match.Index - lastValue));
					//matcher.appendReplacement(sb, "$1$2");
					sb.Append(match.Result("$1$2"));

					lastValue = match.Index + match.Length;
				}
			}

			//matcher.appendTail(sb);
			sb.Append(html.Substring(lastValue));

			html = sb.ToString();

			return html;
		}

		private string RemoveMultiSpaces(string html)
		{
			//collapse multiple spaces
			if (Settings.RemoveMultiSpaces)
			{
				html = MultispacePattern.Replace(html, " ");
			}
			return html;
		}

		private string RemoveIntertagSpaces(string html)
		{
			//remove inter-tag spaces
			if (Settings.RemoveIntertagSpaces)
			{
				html = IntertagPatternTagTag.Replace(html, "><");
				html = IntertagPatternTagCustom.Replace(html, ">%%%~");
				html = IntertagPatternCustomTag.Replace(html, "~%%%<");
				html = IntertagPatternCustomCustom.Replace(html, "~%%%%%%~");
			}
			return html;
		}

		private string RemoveComments(string html)
		{
			//remove comments
			if (Settings.RemoveComments)
			{
				html = CommentPattern.Replace(html, "");
			}
			return html;
		}

		private string SimpleDoctype(string html)
		{
			//simplify doctype
			if (Settings.SimpleDoctype)
			{
				html = DoctypePattern.Replace(html, "<!DOCTYPE html>");
			}
			return html;
		}

		private string RemoveScriptAttributes(string html)
		{
			if (Settings.RemoveScriptAttributes)
			{
				//remove type from script tags
				html = JsTypeAttrPattern.Replace(html, "$1$3");

				//remove language from script tags
				html = JsLangAttrPattern.Replace(html, "$1$3");
			}
			return html;
		}

		private string RemoveStyleAttributes(string html)
		{
			//remove type from style tags
			if (Settings.RemoveStyleAttributes)
			{
				html = StyleTypeAttrPattern.Replace(html, "$1$3");
			}
			return html;
		}

		private string RemoveLinkAttributes(string html)
		{
			//remove type from link tags with rel=stylesheet
			if (Settings.RemoveLinkAttributes)
			{
				var matcher = LinkTypeAttrPattern.Matches(html);
				var sb = new StringBuilder();
				var lastValue = 0;

				foreach (Match match in matcher)
				{
					//if rel=stylesheet
					if (Matches(LinkRelAttrPattern, match.Groups[0].Value))
					{
						sb.Append(html.Substring(lastValue, match.Index - lastValue));
						//matcher.appendReplacement(sb, "$1$3");
						sb.Append(match.Result("$1$3"));

						lastValue = match.Index + match.Length;
					}
					else
					{
						sb.Append(html.Substring(lastValue, match.Index - lastValue));
						//matcher.appendReplacement(sb, "$0");
						sb.Append(match.Result("$0"));

						lastValue = match.Index + match.Length;
					}
				}

				//matcher.appendTail(sb);
				sb.Append(html.Substring(lastValue));

				html = sb.ToString();
			}
			return html;
		}

		private string RemoveFormAttributes(string html)
		{
			//remove method from form tags
			if (Settings.RemoveFormAttributes)
			{
				html = FormMethodAttrPattern.Replace(html, "$1$3");
			}
			return html;
		}

		private string RemoveInputAttributes(string html)
		{
			//remove type from input tags
			if (Settings.RemoveInputAttributes)
			{
				html = InputTypeAttrPattern.Replace(html, "$1$3");
			}
			return html;
		}

		private string SimpleBooleanAttributes(string html)
		{
			//simplify bool attributes
			if (Settings.SimpleBooleanAttributes)
			{
				html = BooleanAttrPattern.Replace(html, "$1$2$4");
			}
			return html;
		}

		private string RemoveHttpProtocol(string html)
		{
			//remove http protocol from tag attributes
			if (Settings.RemoveHttpProtocol)
			{
				var matcher = HttpProtocolPattern.Matches(html);
				var sb = new StringBuilder();
				var lastValue = 0;

				foreach (Match match in matcher)
				{
					//if rel!=external
					if (!Matches(RelExternalPattern, match.Groups[0].Value))
					{
						sb.Append(html.Substring(lastValue, match.Index - lastValue));
						//matcher.appendReplacement(sb, "$1$2");
						sb.Append(match.Result("$1$2"));

						lastValue = match.Index + match.Length;
					}
					else
					{
						sb.Append(html.Substring(lastValue, match.Index - lastValue));
						//matcher.appendReplacement(sb, "$0");
						sb.Append(match.Result("$0"));

						lastValue = match.Index + match.Length;
					}
				}

				//matcher.appendTail(sb);
				sb.Append(html.Substring(lastValue));

				html = sb.ToString();
			}
			return html;
		}

		private string RemoveHttpsProtocol(string html)
		{
			//remove https protocol from tag attributes
			if (Settings.RemoveHttpsProtocol)
			{
				var matcher = HttpsProtocolPattern.Matches(html);
				var sb = new StringBuilder();
				var lastValue = 0;

				foreach (Match match in matcher)
				{
					//if rel!=external
					if (!Matches(RelExternalPattern, match.Groups[0].Value))
					{
						sb.Append(html.Substring(lastValue, match.Index - lastValue));
						//matcher.appendReplacement(sb, "$1$2");
						sb.Append(match.Result("$1$2"));

						lastValue = match.Index + match.Length;
					}
					else
					{
						sb.Append(html.Substring(lastValue, match.Index - lastValue));
						//matcher.appendReplacement(sb, "$0");
						sb.Append(match.Result("$0"));

						lastValue = match.Index + match.Length;
					}
				}

				//matcher.appendTail(sb);
				sb.Append(html.Substring(lastValue));

				html = sb.ToString();
			}
			return html;
		}

		private static bool Matches(Regex regex, string value)
		{
			// http://stackoverflow.com/questions/4450045/difference-between-matches-and-find-in-java-regex

			var cloneRegex = new Regex(@"^" + regex + @"$", regex.Options);
			return cloneRegex.IsMatch(value);
		}

		private void ProcessPreservedBlocks(List<string> preBlocks, List<string> taBlocks, List<string> scriptBlocks,
		  List<string> styleBlocks, List<string> eventBlocks, List<string> condCommentBlocks,
		  List<string> skipBlocks, List<string> lineBreakBlocks,
		  List<List<string>> userBlocks)
		{
			ProcessPreBlocks(preBlocks);
			ProcessTextAreaBlocks(taBlocks);
			ProcessScriptBlocks(scriptBlocks);
			ProcessStyleBlocks(styleBlocks);
			ProcessEventBlocks(eventBlocks);
			ProcessCondCommentBlocks(condCommentBlocks);
			ProcessSkipBlocks(skipBlocks);
			ProcessUserBlocks(userBlocks);
			ProcessLineBreakBlocks(lineBreakBlocks);
		}

		private void ProcessPreBlocks(List<string> preBlocks)
		{
			if (Settings.GenerateStatistics)
			{
				foreach (var block in preBlocks)
				{
					_statistics.SetPreservedSize(_statistics.GetPreservedSize() + block.Length);
				}
			}
		}

		private void ProcessTextAreaBlocks(List<string> taBlocks)
		{
			if (Settings.GenerateStatistics)
			{
				foreach (var block in taBlocks)
				{
					_statistics.SetPreservedSize(_statistics.GetPreservedSize() + block.Length);
				}
			}
		}

		private void ProcessCondCommentBlocks(List<string> condCommentBlocks)
		{
			if (Settings.GenerateStatistics)
			{
				foreach (var block in condCommentBlocks)
				{
					_statistics.SetPreservedSize(_statistics.GetPreservedSize() + block.Length);
				}
			}
		}

		private void ProcessSkipBlocks(List<string> skipBlocks)
		{
			if (Settings.GenerateStatistics)
			{
				foreach (var block in skipBlocks)
				{
					_statistics.SetPreservedSize(_statistics.GetPreservedSize() + block.Length);
				}
			}
		}

		private void ProcessLineBreakBlocks(List<string> lineBreakBlocks)
		{
			if (Settings.GenerateStatistics)
			{
				foreach (var block in lineBreakBlocks)
				{
					_statistics.SetPreservedSize(_statistics.GetPreservedSize() + block.Length);
				}
			}
		}

		private void ProcessUserBlocks(List<List<string>> userBlocks)
		{
			if (Settings.GenerateStatistics)
			{
				foreach (var blockList in userBlocks)
				{
					foreach (var block in blockList)
					{
						_statistics.SetPreservedSize(_statistics.GetPreservedSize() + block.Length);
					}
				}
			}
		}

		private void ProcessEventBlocks(List<string> eventBlocks)
		{
			if (Settings.GenerateStatistics)
			{
				foreach (var block in eventBlocks)
				{
					_statistics.GetOriginalMetrics()
					  .SetInlineEventSize(_statistics.GetOriginalMetrics().GetInlineEventSize() + block.Length);
				}
			}

			if (Settings.RemoveJavaScriptProtocol)
			{
				for (var i = 0; i < eventBlocks.Count; i++)
				{
					eventBlocks[i] = RemoveJavaScriptProtocol(eventBlocks[i]);
				}
			}
			else if (Settings.GenerateStatistics)
			{
				foreach (var block in eventBlocks)
				{
					_statistics.SetPreservedSize(_statistics.GetPreservedSize() + block.Length);
				}
			}

			if (Settings.GenerateStatistics)
			{
				foreach (var block in eventBlocks)
				{
					_statistics.GetCompressedMetrics()
					  .SetInlineEventSize(_statistics.GetCompressedMetrics().GetInlineEventSize() + block.Length);
				}
			}
		}

		private string RemoveJavaScriptProtocol(string source)
		{
			//remove javascript: from inline events
			var result = EventJsProtocolPattern.Replace(source, @"$1", 1);
			//var matcher = eventJsProtocolPattern.Match(source);
			//if (matcher.Success)
			//{
			//    result = matcher.replaceFirst("$1");
			//}

			if (Settings.GenerateStatistics)
			{
				_statistics.SetPreservedSize(_statistics.GetPreservedSize() + result.Length);
			}

			return result;
		}

		private void ProcessScriptBlocks(List<string> scriptBlocks)
		{
			if (Settings.GenerateStatistics)
			{
				foreach (var block in scriptBlocks)
				{
					_statistics.GetOriginalMetrics()
					  .SetInlineScriptSize(_statistics.GetOriginalMetrics().GetInlineScriptSize() + block.Length);
				}
			}

			if (Settings.CompressJavaScript)
			{
				for (var i = 0; i < scriptBlocks.Count; i++)
				{
					scriptBlocks[i] = CompressJavaScript(scriptBlocks[i]);
				}
			}
			else if (Settings.GenerateStatistics)
			{
				foreach (var block in scriptBlocks)
				{
					_statistics.SetPreservedSize(_statistics.GetPreservedSize() + block.Length);
				}
			}

			if (Settings.GenerateStatistics)
			{
				foreach (var block in scriptBlocks)
				{
					_statistics.GetCompressedMetrics()
					  .SetInlineScriptSize(_statistics.GetCompressedMetrics().GetInlineScriptSize() + block.Length);
				}
			}
		}

		private void ProcessStyleBlocks(List<string> styleBlocks)
		{
			if (Settings.GenerateStatistics)
			{
				foreach (var block in styleBlocks)
				{
					_statistics.GetOriginalMetrics()
					  .SetInlineStyleSize(_statistics.GetOriginalMetrics().GetInlineStyleSize() + block.Length);
				}
			}

			if (Settings.CompressCss)
			{
				for (var i = 0; i < styleBlocks.Count; i++)
				{
					styleBlocks[i] = CompressCssStyles(styleBlocks[i]);
				}
			}
			else if (Settings.GenerateStatistics)
			{
				foreach (var block in styleBlocks)
				{
					_statistics.SetPreservedSize(_statistics.GetPreservedSize() + block.Length);
				}
			}

			if (Settings.GenerateStatistics)
			{
				foreach (var block in styleBlocks)
				{
					_statistics.GetCompressedMetrics()
					  .SetInlineStyleSize(_statistics.GetCompressedMetrics().GetInlineStyleSize() + block.Length);
				}
			}
		}

		private string CompressJavaScript(string source)
		{
			//set default javascript compressor
			if (Settings.JavaScriptCompressor == null)
			{
				return source;
				//YuiJavaScriptCompressor yuiJsCompressor = new YuiJavaScriptCompressor();
				//yuiJsCompressor.setNoMunge(yuiJsNoMunge);
				//yuiJsCompressor.setPreserveAllSemiColons(yuiJsPreserveAllSemiColons);
				//yuiJsCompressor.setDisableOptimizations(yuiJsDisableOptimizations);
				//yuiJsCompressor.setLineBreak(yuiJsLineBreak);

				//if (yuiErrorReporter != null)
				//{
				//    yuiJsCompressor.setErrorReporter(yuiErrorReporter);
				//}

				//javaScriptCompressor = yuiJsCompressor;
			}

			//detect CDATA wrapper
			var scriptCdataWrapper = false;
			var cdataWrapper = false;
			var matcher = ScriptCdataPattern.Match(source);
			if (matcher.Success)
			{
				scriptCdataWrapper = true;
				source = matcher.Groups[1].Value;
			}
			else if (CdataPattern.Match(source).Success)
			{
				cdataWrapper = true;
				source = matcher.Groups[1].Value;
			}

			var result = Settings.JavaScriptCompressor.Compress(source);

			if (scriptCdataWrapper)
			{
				result = $"/*<![CDATA[*/{result}/*]]>*/";
			}
			else if (cdataWrapper)
			{
				result = $"<![CDATA[{result}]]>";
			}

			return result;
		}

		private string CompressCssStyles(string source)
		{
			//set default css compressor
			if (Settings.CssCompressor == null)
			{
				return source;
				//YuiCssCompressor yuiCssCompressor = new YuiCssCompressor();
				//yuiCssCompressor.setLineBreak(yuiCssLineBreak);

				//cssCompressor = yuiCssCompressor;
			}

			//detect CDATA wrapper
			var cdataWrapper = false;
			var matcher = CdataPattern.Match(source);
			if (matcher.Success)
			{
				cdataWrapper = true;
				source = matcher.Groups[1].Value;
			}

			var result = Settings.CssCompressor.Compress(source);

			if (cdataWrapper)
			{
				result = $"<![CDATA[{result}]]>";
			}

			return result;
		}

		private HtmlCompressor CreateCompressorClone()
		{
			var clone = new HtmlCompressor(Settings);
			return clone;
		}
	}
}

