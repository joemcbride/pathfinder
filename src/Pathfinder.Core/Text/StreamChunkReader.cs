using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Pathfinder.Core.Text
{
	public class StreamChunkReader : IChunkReader
	{
		private ChunkReader<StreamTag> _reader;

		public StreamChunkReader()
		{
			_reader = new ChunkReader<StreamTag>("<pushStream", "<popStream", true);
			_reader.Append = (builder, result, tag) => {

				if(string.Equals(tag.Id, "assess"))
				{
					builder.Append(tag.Value);
				}

				result.AddTag(tag);
				return 0;
			};
		}

		public ReadResult Read(Chunk chunk)
		{
			return _reader.Read(chunk);
		}
	}
}