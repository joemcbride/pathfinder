using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Linq;
using System.Threading.Tasks;
using System.Threading;
using Pathfinder.Core.Client.Scripting;

namespace Pathfinder.Core.Client
{
	public class ScriptContext
	{
		private readonly IServiceLocator _services;
		private readonly ISimpleDictionary<string, string> _localvars;

		public ScriptContext(string name, CancellationToken cancelToken, IServiceLocator services, ISimpleDictionary<string, string> localVars)
		{
			Name = name;
			CancelToken = cancelToken;
			_services = services;
			_localvars = localVars;
			MatchWait = new MatchWait();
		}

		public string Name { get; private set; }
		public int LineNumber { get; set; }
		public MatchWait MatchWait { get; private set; }
		public CancellationToken CancelToken { get; private set; }

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