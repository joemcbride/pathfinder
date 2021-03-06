using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using Outlander.Core.Client.Scripting;

namespace Outlander.Core.Client.Tests
{
	[TestFixture]
	public class TokenizerTests
	{
		private Tokenizer theTokenizer;

		[SetUp]
		public void SetUp()
		{
			theTokenizer = new Tokenizer(TokenDefinitionRegistry.Default().Definitions());
		}

		[Test]
		public void creates_label_token()
		{
			const string line = "somewhere:";

			var tokens = theTokenizer.Tokenize(line).ToList();
			Assert.AreEqual(1, tokens.Count);
			Assert.AreEqual("label", tokens[0].Type);
			Assert.AreEqual("somewhere", tokens[0].Value);
		}

		[Test]
		public void creates_label_swim_token()
		{
			const string line = "Swim:\r";

			var tokens = theTokenizer.Tokenize(line).ToList();
			Assert.AreEqual(1, tokens.Count);
			Assert.AreEqual("label", tokens[0].Type);
			Assert.AreEqual("Swim", tokens[0].Value);
		}

		[Test]
		public void creates_action_not_label_token()
		{
			const string line = "action var circle $1;put #var circle $1 when Circle:\\s+(\\d+)$";

			var tokens = theTokenizer.Tokenize(line).ToList();
			Assert.AreEqual(1, tokens.Count);
			var token = tokens.First().As<ActionToken>();
			Assert.NotNull(token);
			Assert.AreEqual("action", token.Type);
			Assert.AreEqual("var circle $1;put #var circle $1", token.Action);
			Assert.AreEqual("Circle:\\s+(\\d+)$", token.When);
		}

		[Test]
		public void creates_goto_token()
		{
			const string line = "goto somewhere";

			var tokens = theTokenizer.Tokenize(line).ToList();
			Assert.AreEqual(1, tokens.Count);
			Assert.AreEqual("goto", tokens[0].Type);
			Assert.AreEqual("somewhere", tokens[0].Value);
		}

		[Test]
		public void creates_waitfor_token()
		{
			const string line = "waitfor You finish playing";

			var tokens = theTokenizer.Tokenize(line).ToList();
			Assert.AreEqual(1, tokens.Count);
			Assert.AreEqual("waitfor", tokens[0].Type);
			Assert.AreEqual("You finish playing", tokens[0].Value);
		}

		[Test]
		public void creates_waitforre_token()
		{
			const string line = "waitforre You finish playing|Something else";

			var tokens = theTokenizer.Tokenize(line).ToList();
			Assert.AreEqual(1, tokens.Count);
			Assert.AreEqual("waitforre", tokens[0].Type);
			Assert.AreEqual("You finish playing|Something else", tokens[0].Value);
		}

		[Test]
		public void creates_pause_token()
		{
			const string line = "pause 0.5";

			var tokens = theTokenizer.Tokenize(line).ToList();
			Assert.AreEqual(1, tokens.Count);
			Assert.AreEqual("pause", tokens[0].Type);
			Assert.AreEqual("0.5", tokens[0].Value);
		}

		[Test]
		public void creates_pause_token_without_value()
		{
			const string line = "pause";

			var tokens = theTokenizer.Tokenize(line).ToList();
			Assert.AreEqual(1, tokens.Count);
			Assert.AreEqual("pause", tokens[0].Type);
			Assert.AreEqual("", tokens[0].Value);
		}

		[Test]
		public void creates_put_token()
		{
			const string line = "PUT collect rocks";

			var tokens = theTokenizer.Tokenize(line).ToList();
			Assert.AreEqual(1, tokens.Count);
			Assert.AreEqual("put", tokens[0].Type);
			Assert.AreEqual("collect rocks", tokens[0].Value);
		}

		[Test]
		public void creates_match_token()
		{
			const string line = "match Kick You manage to collect";

			var token = theTokenizer.Tokenize(line).Single().As<MatchToken>();
			Assert.AreEqual("match", token.Type);
			Assert.AreEqual("Kick", token.Goto);
			Assert.AreEqual("You manage to collect", token.Pattern);
		}

		[Test]
		public void creates_match_token_2()
		{
			const string line = "match Wait1 ...wait something";

			var token = theTokenizer.Tokenize(line).Single().As<MatchToken>();
			Assert.AreEqual("match", token.Type);
			Assert.AreEqual("Wait1", token.Goto);
			Assert.AreEqual("...wait something", token.Pattern);
		}

		[Test]
		public void creates_match_token_3()
		{
			const string line = "MATCH braid You get";

			var token = theTokenizer.Tokenize(line).Single().As<MatchToken>();
			Assert.AreEqual("match", token.Type);
			Assert.AreEqual("braid", token.Goto);
			Assert.AreEqual("You get", token.Pattern);
		}

		[Test]
		public void creates_match_token_trims_end()
		{
			const string line = "MATCH braid You get\r";

			var token = theTokenizer.Tokenize(line).Single().As<MatchToken>();
			Assert.AreEqual("match", token.Type);
			Assert.AreEqual("braid", token.Goto);
			Assert.AreEqual("You get", token.Pattern);
		}

		[Test]
		public void creates_matchre_token()
		{
			const string line = "matchre CheckEXP You take a step back|Now what did the|I could not find";
			const string pattern = "You take a step back|Now what did the|I could not find";

			var token = theTokenizer.Tokenize(line).Single().As<MatchToken>();
			Assert.AreEqual("matchre", token.Type);
			Assert.AreEqual("CheckEXP", token.Goto);
			Assert.AreEqual(pattern, token.Pattern);
		}

		[Test]
		public void creates_matchre_token_trims_end()
		{
			const string line = "matchre CheckEXP You take a step back|Now what did the|I could not find\r";
			const string pattern = "You take a step back|Now what did the|I could not find";

			var token = theTokenizer.Tokenize(line).Single().As<MatchToken>();
			Assert.AreEqual("matchre", token.Type);
			Assert.AreEqual("CheckEXP", token.Goto);
			Assert.AreEqual(pattern, token.Pattern);
		}

		[Test]
		public void creates_if_token()
		{
			const string line = "if ($Outdoorsmanship.LearningRate >= %maxexp) then goto END";

			var token = theTokenizer.Tokenize(line).Single().As<IfToken>();
			Assert.AreEqual("if", token.Type);
			Assert.AreEqual("($Outdoorsmanship.LearningRate >= %maxexp) then goto END", token.Value);
		}

		[Test]
		public void creates_if_num_token()
		{
			const string line = "if_1 goto something";

			var token = theTokenizer.Tokenize(line).Single().As<IfToken>();
			Assert.AreEqual("if_", token.Type);
			Assert.AreEqual("1", token.Blocks.IfEval);
			Assert.AreEqual("goto something", token.Blocks.IfBlock);
		}

		[Test]
		public void creates_if_num_2_token()
		{
			const string line = "if_13 goto somewhere";

			var token = theTokenizer.Tokenize(line).Single().As<IfToken>();
			Assert.AreEqual("if_", token.Type);
			Assert.AreEqual("13", token.Blocks.IfEval);
			Assert.AreEqual("goto somewhere", token.Blocks.IfBlock);
		}

		[Test]
		public void creates_var_token()
		{
			const string line = "var something another";

			var token = theTokenizer.Tokenize(line).Single();
			Assert.AreEqual("var", token.Type);
			Assert.AreEqual("something another", token.Value);
		}

		[Test]
		public void creates_var_token_from_setvariable()
		{
			const string line = "setvariable something another";

			var token = theTokenizer.Tokenize(line).Single();
			Assert.AreEqual("var", token.Type);
			Assert.AreEqual("something another", token.Value);
		}

		[Test]
		public void creates_unvar_token()
		{
			const string line = "unvar something";

			var token = theTokenizer.Tokenize(line).Single();
			Assert.AreEqual("unvar", token.Type);
			Assert.AreEqual("unvar something", token.Text);
			Assert.AreEqual("something", token.Value);
		}

		[Test]
		public void creates_hasvar_token()
		{
			const string line = "hasvar something newvar";

			var token = theTokenizer.Tokenize(line).Single();
			Assert.AreEqual("hasvar", token.Type);
			Assert.AreEqual("hasvar something newvar", token.Text);
			Assert.AreEqual("something newvar", token.Value);
		}

		[Test]
		public void creates_move_token()
		{
			const string line = "move something somewhere";

			var token = theTokenizer.Tokenize(line).Single();
			Assert.AreEqual("move", token.Type);
			Assert.AreEqual("something somewhere", token.Value);
		}

		[Test]
		public void creates_nextroom_token()
		{
			const string line = "nextroom something somewhere";

			var token = theTokenizer.Tokenize(line).Single();
			Assert.AreEqual("nextroom", token.Type);
			Assert.AreEqual("something somewhere", token.Value);
		}

		[Test]
		public void creates_action_token()
		{
			const string line = "action do something when some condition";

			var token = theTokenizer.Tokenize(line).Single().As<ActionToken>();
			Assert.AreEqual("do something", token.Action);
			Assert.AreEqual("some condition", token.When);
		}

		[Test]
		public void creates_send_token()
		{
			const string line = "send stuff";

			var token = theTokenizer.Tokenize(line).Single();
			Assert.AreEqual("send", token.Type);
			Assert.AreEqual("stuff", token.Value);
		}

		[Test]
		public void creates_echo_token()
		{
			const string line = "echo something";

			var token = theTokenizer.Tokenize(line).Single();
			Assert.AreEqual("echo", token.Type);
			Assert.AreEqual("something\n", token.Value);
		}

		[Test]
		public void creates_echo_empty_token()
		{
			const string line = "echo";

			var token = theTokenizer.Tokenize(line).Single();
			Assert.AreEqual("echo", token.Type);
			Assert.AreEqual("\n", token.Value);
		}

		[Test]
		public void creates_echo_empty_ignore_case_token()
		{
			const string line = "ECHO";

			var token = theTokenizer.Tokenize(line).Single();
			Assert.AreEqual("echo", token.Type);
			Assert.AreEqual("\n", token.Value);
		}

		[Test]
		public void creates_debuglevel_token()
		{
			const string line = "debuglevel 5";

			var token = theTokenizer.Tokenize(line).Single();
			Assert.AreEqual("debuglevel", token.Type);
			Assert.AreEqual("5", token.Value);
		}

		[Test]
		public void creates_parse_token()
		{
			const string line = "parse something";

			var token = theTokenizer.Tokenize(line).Single();
			Assert.AreEqual("parse", token.Type);
			Assert.AreEqual("something", token.Value);
		}

		[Test]
		public void creates_containsre_token()
		{
			const string line = "containsre something other";

			var token = theTokenizer.Tokenize(line).Single();
			Assert.AreEqual("containsre", token.Type);
			Assert.AreEqual("something other", token.Value);
		}

		[Test]
		public void creates_gosub_token()
		{
			const string line = "gosub label";

			var token = theTokenizer.Tokenize(line).Single().As<GotoToken>();
			Assert.AreEqual("gosub", token.Type);
			Assert.AreEqual("label", token.Value);
			Assert.AreEqual("label", token.Label);
		}

		[Test]
		public void creates_gosub_token_with_arguments()
		{
			const string line = "gosub label one two";

			var token = theTokenizer.Tokenize(line).Single().As<GotoToken>();
			Assert.AreEqual("gosub", token.Type);
			Assert.AreEqual("label one two", token.Value);
			Assert.AreEqual("label", token.Label);
			Assert.True(token.Args.SequenceEqual(new string[]{ "one", "two" }));
		}

		[Test]
		public void creates_gosub_token_with_quoted_arguments()
		{
			const string line = "gosub label one \"two three\" four \"five six\"";

			var token = theTokenizer.Tokenize(line).Single().As<GotoToken>();
			Assert.AreEqual("gosub", token.Type);
			Assert.AreEqual("label one \"two three\" four \"five six\"", token.Value);
			Assert.AreEqual("label", token.Label);
			Assert.True(token.Args.SequenceEqual(new string[]{ "one", "two three", "four", "five six" }));
		}
	}
}
