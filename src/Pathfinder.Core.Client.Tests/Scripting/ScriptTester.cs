using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using Outlander.Core.Tests;
using Outlander.Core.Client.Scripting;
using Outlander.Core.Client;

namespace Outlander.Core.Client.Tests
{
	[TestFixture]
	public class ScriptTester
	{
		private Script theScript;
		private StubGameServer theGameServer;
		private StubGameState theGameState;
		private InMemoryServiceLocator theServices;
		private InMemoryScriptLog theLog;
		private IGameStream theGameStream;

		[SetUp]
		public void SetUp()
		{
			theLog = new InMemoryScriptLog();
			theGameState = new StubGameState();
			theGameServer = new StubGameServer(theGameState);
			theGameStream = new GameStream(theGameState);

			theServices = new InMemoryServiceLocator();
			theServices.Add<IGameServer>(theGameServer);
			theServices.Add<IGameState>(theGameState);
			theServices.Add<IScriptLog>(theLog);
			theServices.Add<IVariableReplacer>(new VariableReplacer());
			theServices.Add<ICommandProcessor>(new CommandProcessor(theServices, theServices.Get<IVariableReplacer>(), theLog));
			theServices.Add<IIfBlocksParser>(new IfBlocksParser());
			var waitFor = new WaitForTokenHandler(theGameState, theGameStream);
			var waitForRe = new WaitForReTokenHandler(theGameState, theGameStream);
			var matchWait = new MatchWaitTokenHandler(theGameState, theGameStream);
			theServices.Add<WaitForTokenHandler>(waitFor);
			theServices.Add<WaitForReTokenHandler>(waitForRe);
			theServices.Add<MatchWaitTokenHandler>(matchWait);
			theServices.Add<IIfBlockExecuter>(new IfBlockExecuter(waitFor, waitForRe, matchWait));
			theServices.Add<IGameStream>(theGameStream);

			theScript = new Script(theServices, Tokenizer.With(TokenDefinitionRegistry.Default()));
		}

		[Test]
		public void runs_simple_script()
		{
			const string script = "debuglevel 5\nstart:\nput %1\ngoto end\nend:";
			const string expected = "script started\ndebuglevel 5\npassing label: start\ncollect rock\ngoto end\npassing label: end\n";

			var scriptTask = theScript.Run("1", "script", script, "collect rock");

			scriptTask.Wait();

			Assert.AreEqual(expected, theLog.Builder.ToString());
		}

		[Test]
		public void assigns_argument_variables()
		{
			const string script = "start:";

			var scriptTask = theScript.Run("1", "script", script, "one", "two", "three");

			scriptTask.Wait();

			var localVars = theScript.ScriptVars;

			Assert.AreEqual("one two three", localVars["0"]);
			Assert.AreEqual("one", localVars["1"]);
			Assert.AreEqual("two", localVars["2"]);
			Assert.AreEqual("three", localVars["3"]);
		}

		[Test]
		public void cancels_script()
		{
			const string script = "start:\npause 5";

			theScript.Stop();

			var scriptTask = theScript.Run("1", "script", script);

			try
			{
				Task.WaitAll(scriptTask);
			}
			catch(AggregateException)
			{}

			Assert.IsTrue(scriptTask.IsCanceled);
		}
	}
}
