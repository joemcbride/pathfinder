using System;
using System.Text.RegularExpressions;

namespace Outlander.Core.Client
{
	public class SendCommandTokenHandler : TokenHandler
	{
		protected override void execute()
		{
			var commandProcessor = Context.Get<ICommandProcessor>();

			commandProcessor.Process(Token.Value, Context);

			TaskSource.SetResult(new CompletionEventArgs());
		}
	}
}
