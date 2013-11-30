﻿using System.Linq;
using NUnit.Framework;
using Pathfinder.Core;
using Pathfinder.Core.Xml;

namespace Pathfinder.Core.Tests.Xml
{
    [TestFixture]
    public class PromptParserTester
    {
        private PromptParser theParser;
        private const string SampleData = "<prompt time=\"1366780108\">H&gt;</prompt>";

        [SetUp]
        public void Setup()
        {
            theParser = new PromptParser();
        }

        [Test]
        public void should_match_for_prompt()
        {
			Assert.True(theParser.Matches(SampleData));
        }

        [Test]
        public void should_parse_prompt()
        {
			var result = theParser.Parse(SampleData).Single();

			Assert.AreEqual("H>", result.Prompt);
        }

        [Test]
		public void should_parse_time()
        {
			var result = theParser.Parse(SampleData).Single();

			Assert.AreEqual("1366780108".UnixTimeStampToDateTime(), result.Time);
        }

		[Test]
		public void is_valid()
		{
			var result = theParser.Parse(SampleData).Single();
			Assert.True(result.IsValid());
		}

		[Test]
		public void is_not_valid()
		{
			var result = new ParseResult();
			Assert.False(result.IsValid());
		}
    }
}
