using System;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

namespace Pathfinder.Core.Text
{
	public interface IChunkReader
	{
		ReadResult Read(Chunk chunk);
	}

	public class ChunkReader<TTag> : IChunkReader where TTag : Tag, new()
	{
		private ReadState _state = new ReadState();

		private readonly string _startTag;
		private readonly string _endTag;
		private readonly string _endsWithTag;
		private readonly bool _checkEndTag;

		public Func<StringBuilder, ReadResult, TTag, int> Append = null;

		public ChunkReader(string startTag, string endTag)
			: this(startTag, endTag, false)
		{
		}

		public ChunkReader(string startTag, string endTag, bool checkEndTag)
		{
			_startTag = startTag;
			_endTag = endTag;
			_checkEndTag = checkEndTag;
			_endsWithTag = ">";

			if(_checkEndTag)
			{
				_endsWithTag = "/>";
			}
		}

		public ReadResult Read(Chunk chunk)
		{
			var result = new ReadResult();

			var builder = new StringBuilder();

			var text = chunk.Text;

			for (int i = 0; i < text.Length; i++) {
				if (text[i] == '<') {
					_state.Tracking = true;
				}

				if (_state.Tracking) {
					_state.Text.Append(text[i]);
				} else {
					builder.Append(text[i]);
				}

				var currentText = _state.Text.ToString();

				if (_checkEndTag)
				{
					if (_state.Tracking && currentText.Length == 2 && currentText.StartsWith("</")) {
						_state.Tracking = false;
						_state.WaitForEnd = false;
						_state.WaitForPop = false;
						_state.Text.Clear();
						builder.Append(currentText);
					}
				}

				if (_state.Tracking
					&& (currentText.Length == 3 && !currentText.StartsWith(_startTag.Substring(0, 3))
						|| (currentText.Length >= _startTag.Length && !currentText.StartsWith(_startTag))))
				{
					_state.Tracking = false;
					builder.Append(_state.Text.ToString());
					_state.Text.Clear();
				} else if (currentText.StartsWith(_startTag)) {
					_state.WaitForPop = true;
				}

				if (_state.WaitForEnd && currentText.EndsWith(_endsWithTag)) {
					_state.WaitForEnd = false;
					_state.WaitForPop = false;
					_state.Tracking = false;
					_state.Text.Clear();

					var tag = Tag.For<TTag>(currentText);

					if (Append != null) {
						var move = Append(builder, result, tag);
						i += move;
					} else {
						result.AddTag(tag);
					}
				}

				if (_state.WaitForPop && currentText.EndsWith(_endTag)) {
					_state.WaitForEnd = true;
				}
			}

			if (builder.Length > 0)
			{
				result.Chunk = Chunk.For(builder.ToString());
			}

			if (_state.Text.Length > 0)
				result.Stop = true;

			return result;
		}
	}

	public class ReadState
	{
		public StringBuilder Text = new StringBuilder();
		public bool Tracking = false;
		public bool WaitForPop = false;
		public bool WaitForEnd = false;
	}
}