using System;
using System.Linq;
using NUnit.Framework;
using Outlander.Core;

namespace Outlander.Core.Container.Tests
{
	[TestFixture]
	public class IoCTester
	{
		private SimpleContainer theContainer;

		[SetUp]
		public void SetUp()
		{
			theContainer = new SimpleContainer();
			theContainer.PerRequest<ILog, NullLog>();

			IoC.BuildUp = theContainer.BuildUp;
			IoC.GetInstance = theContainer.GetInstance;
			IoC.GetAllInstances = theContainer.GetAllInstances;
		}

		[Test]
		public void should_get_log()
		{
			var logger = IoC.Get<ILog>();
			Assert.NotNull(logger);
			Assert.AreEqual(typeof(NullLog), logger.GetType());
		}

		[Test]
		public void should_get_game_server()
		{
			theContainer.PerRequest<IGameState, SimpleGameState>();
			theContainer.PerRequest<IGameServer, SimpleGameServer>();

			var server = IoC.Get<IGameServer>();
			Assert.NotNull(server);
		}
	}
}
