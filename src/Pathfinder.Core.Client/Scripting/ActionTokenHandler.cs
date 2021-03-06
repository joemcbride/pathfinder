using System;
using System.Linq;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using Outlander.Core.Client;
using Outlander.Core;

namespace Outlander.Core.Client
{
	public class ActionToken : Token
	{
		public string Action { get; set; }
		public string When { get; set; }
	}

	public class ActionContext
	{
		public string ScriptName { get; set; }
		public int LineNumber { get; set; }
		public string Match { get; set; }
		public ScriptContext ScriptContext { get; set; }
		public ActionToken Token { get; set; }
	}

	public class ActionTokenHandler : TokenHandler
	{
		private readonly ActionContext _actionContext;
		private readonly IDataTracker<ActionContext> _tracker;
		private PatternReporter _reportPattern;

		public ActionTokenHandler(ActionContext context, IDataTracker<ActionContext> tracker)
		{
			_actionContext = context;
			_tracker = tracker;
		}

		protected override void execute()
		{
			_reportPattern = new PatternReporter(_actionContext, _tracker);
			_reportPattern.Subscribe(Context.Get<IGameStream>());

			Context.CancelToken.Register(() => {
				_reportPattern.Unsubscribe();
			});
		}
	}

	public class PatternReporter : DataReporter<TextTag>
	{
		private readonly IDataTracker<ActionContext> _tracker;
		private readonly ActionContext _token;

		public PatternReporter(ActionContext token, IDataTracker<ActionContext> tracker)
			: base(Guid.NewGuid().ToString())
		{
			_token = token;
			_tracker = tracker;
		}

		public override void OnNext(TextTag item)
		{
			var match = Regex.Match(item.Text, _token.Token.When, RegexOptions.Multiline);

			if(match.Success)
			{
				_token.Match = match.Value;
				_tracker.Publish(_token);
			}
		}
	}
}
