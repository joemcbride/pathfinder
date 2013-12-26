using System;
using System.Linq;
using NUnit.Framework;
using Pathfinder.Core.Tests;
using Pathfinder.Core.Client.Scripting;

namespace Pathfinder.Core.Client.Tests
{
	[TestFixture]
	public class ScriptRunnerTester
	{
		private ScriptRunner theRunner;
		private StubScript theScript;
		private StubScriptLoader theLoader;
		private StubGameServer theGameServer;
		private StubGameState theGameState;
		private IVariableReplacer theReplacer;
		private InMemoryScriptLog theLogger;
		private InMemoryServiceLocator theServices;

		[SetUp]
		public void SetUp()
		{
			theScript = new StubScript();
			theLoader = new StubScriptLoader();
			theLogger = new InMemoryScriptLog();

			theGameState = new StubGameState();
			theGameServer = new StubGameServer(theGameState);

			theReplacer = new VariableReplacer();

			theServices = new InMemoryServiceLocator();

			theServices.Add<IGameServer>(theGameServer);
			theServices.Add<IGameState>(theGameState);
			theServices.Add<IScriptLog>(theLogger);
			theServices.Add<ICommandProcessor>(new CommandProcessor(theServices, theReplacer, theLogger));
			theServices.Add<IVariableReplacer>(theReplacer);
			theServices.Add<IIfBlocksParser>(new IfBlocksParser());

			theRunner = new ScriptRunner(theServices, theLoader, theLogger);

			theRunner.Create = () => theScript;
		}

		[Test]
		public void runs_the_script()
		{
			const string scriptData = "start:\nput %0\npause 1\ngoto start";

			theLoader.AddData("myscript", scriptData);

			var token = new ScriptToken();
			token.Name = "myscript";
			token.Args = new string[]{ "one", "two", "three four" };

			var task = theRunner.Run(token);
			task.Wait();

			Assert.AreEqual(token.Name, theScript.Name);
			Assert.True(token.Args.SequenceEqual(theScript.Args));
			Assert.AreEqual(scriptData, theScript.Script);
		}

		[Test]
		public void does_not_try_to_run_script_if_cannot_load()
		{
			var token = new ScriptToken();
			token.Name = "myscript";
			token.Args = new string[]{ "one", "two", "three four" };

			var task = theRunner.Run(token);

			Assert.IsNull(task);
			Assert.IsNull(theScript.Script);
			Assert.IsNull(theScript.Args);
		}

		[Test]
		public void run_integrated_script()
		{
			theRunner.Create = theRunner.DefaultCreate;

			const string scriptData = "start:\ngoto end\nend:";
			const string expected = "myscript started\npassing label: start\ngoto end\npassing label: end\nmyscript finished\n";

			theLoader.AddData("myscript", scriptData);

			var token = new ScriptToken();
			token.Name = "myscript";
			token.Args = new string[]{ "one", "two", "three four" };

			var task = theRunner.Run(token);

			task.Wait();

			Assert.AreEqual(expected, theLogger.Builder.ToString());
			Assert.AreEqual(0, theRunner.Scripts().Count());
		}

		[Test]
		public void run_integrated_if_script()
		{
			theRunner.Create = theRunner.DefaultCreate;

			const string scriptData = "\nstart:\n\tif (\"%1\" == \"one\") then\n\t\techo one\n\telse if (\"%2\" == \"two\") then\n\t\techo two\n\telse\n\t\techo three\n\n\tgoto end\n\nend:";
			const string expected = "myscript started\npassing label: start\nif (\"one\" == \"one\")\nif result true\necho one\ngoto end\npassing label: end\nmyscript finished\n";

			theLoader.AddData("myscript", scriptData);

			var token = new ScriptToken();
			token.Name = "myscript";
			token.Args = new string[]{ "one", "two", "three four" };

			var task = theRunner.Run(token);

			task.Wait();

			Assert.AreEqual(expected, theLogger.Builder.ToString());
			Assert.AreEqual(0, theRunner.Scripts().Count());
		}

		[Test]
		public void run_integrated_if_script_2()
		{
			theRunner.Create = theRunner.DefaultCreate;

			const string scriptData = "\nstart:\n\tif (\"%1\" == \"one\") then\n\t\techo one\n\telse if (\"%2\" == \"two\") then\n\t\techo two\n\telse {\n\t\techo three\n\t\techo four\n\t}\n\n\tgoto end\n\nend:";
			const string expected = "myscript started\npassing label: start\nif (\"nope\" == \"one\")\nif result false\nif (\"nope\" == \"two\")\nif result false\necho three\necho four\ngoto end\npassing label: end\nmyscript finished\n";

			theLoader.AddData("myscript", scriptData);

			var token = new ScriptToken();
			token.Name = "myscript";
			token.Args = new string[]{ "nope", "nope", "three four" };

			var task = theRunner.Run(token);

			task.Wait();

			Assert.AreEqual(expected, theLogger.Builder.ToString());
			Assert.AreEqual(0, theRunner.Scripts().Count());
		}
	}
}
