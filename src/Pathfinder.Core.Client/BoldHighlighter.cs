using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Outlander.Core.Client
{
	public class SimpleHighlighter : IHighlighter
	{
		private readonly string _pattern;
		//private readonly string _key;

		private Action<TextTag, Match> Modify { get; set; }

		public SimpleHighlighter(string pattern, string key, string color, bool mono)
		{
			_pattern = "(" + pattern + ")";

			//_key = key;

			Modify = (tag, match) => {
				tag.Text = match.Groups[0].Value;
				tag.Color = color;
				tag.Mono = mono;
				tag.Matched = true;
			};
		}

		public IEnumerable<TextTag> Highlight(TextTag text)
		{
			var tags = new List<TextTag>();

			var start = 0;

			var matches = Regex.Matches(text.Text, _pattern, RegexOptions.Multiline | RegexOptions.IgnoreCase);
			foreach(Match match in matches)
			{
				TextTag.For(text.Text.Substring(start, match.Index - start), text).IfNotNull(tags.Add);
				start = match.Index + match.Length;
				var matchedTag = TextTag.For(match.Groups[1].Value, text);
				Modify(matchedTag, match);
				matchedTag.IfNotNull(tags.Add);
			}

			if(start < text.Text.Length)
			{
				TextTag.For(text.Text.Substring(start, text.Text.Length - start), text).IfNotNull(tags.Add);
			}

			return tags;
		}

		public static SimpleHighlighter For(string pattern, string color, string key = "", bool mono = false)
		{
			return new SimpleHighlighter(pattern, key, color, mono);
		}
	}

	public class BoldHighlighter : IHighlighter
	{
		private HighlightSettings _settings;
		public Action<TextTag, Match> Modify { get; set; }

		public BoldHighlighter(HighlightSettings settings)
		{
			_settings = settings;

			Modify = (tag, match) => {
				var setting = _settings.Get(HighlightKeys.Bold);
				tag.Text = match.Groups[2].Value;
				tag.Color = setting.Color;
				tag.Mono = setting.Mono;
				tag.Matched = true;
			};
		}

		public IEnumerable<TextTag> Highlight(TextTag text)
		{
			var tags = new List<TextTag>();

			var start = 0;

			var matches = Regex.Matches(text.Text, RegexPatterns.MonsterBold, RegexOptions.Singleline);
			foreach(Match match in matches)
			{
				TextTag.For(text.Text.Substring(start, match.Index - start), text).IfNotNull(tags.Add);
				start = match.Index + match.Length;
				var matchedTag = TextTag.For(match.Groups[1].Value, text);
				Modify(matchedTag, match);
				matchedTag.IfNotNull(tags.Add);
			}

			if(start < text.Text.Length)
			{
				TextTag.For(text.Text.Substring(start, text.Text.Length - start), text).IfNotNull(tags.Add);
			}

			return tags;
		}
	}
}
