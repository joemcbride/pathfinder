using System;
using System.Threading;
using Outlander.Core.Client.Scripting;
using Outlander.Core;

namespace Outlander.Core.Client
{
	public class ScriptContext
	{
		private readonly IServiceLocator _services;
		private readonly ISimpleDictionary<string, string> _localvars;

		public ScriptContext(string id, string name, CancellationToken cancelToken, IServiceLocator services, ISimpleDictionary<string, string> localVars)
		{
			Id = id;
			Name = name;
			CancelToken = cancelToken;
			_services = services;
			_localvars = localVars;
			MatchWait = new MatchWait();
			GoSubStack = new GoSubStack();
		}

		public string Id { get; set; }
		public string Name { get; private set; }
		public int LineNumber { get; set; }
		public MatchWait MatchWait { get; private set; }
		public IGoSubStack GoSubStack { get; private set;}
		public string[] CurrentArgs { get; set; }
		public CancellationToken CancelToken { get; private set; }
		public int DebugLevel { get; set; }

		public T Get<T>()
		{
			return _services.Get<T>();
		}

		public ISimpleDictionary<string, string> LocalVars
		{
			get
			{
				return _localvars;
			}
		}

		public ISimpleDictionary<string, string> GlobalVar
		{
			get
			{
				return _services.Get<IGameState>().GlobalVars();
			}
		}
	}
}
