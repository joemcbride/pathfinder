using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Timers;
using MonoMac.AppKit;
using MonoMac.Foundation;
using Outlander.Core;
using Outlander.Core.Authentication;
using Outlander.Core.Client;
using Outlander.Core.Text;

namespace Outlander.Mac.Beta
{
	public partial class MainWindowController : MonoMac.AppKit.NSWindowController
	{
		private IServiceLocator _services;

		private Bootstrapper _bootStrapper;
		private IGameServer _gameServer;
		private CommandCache _commandCache;

		private IGameStream _gameStream;
		private GameStreamListener _gameStreamListener;

		private ICommandProcessor _commandProcessor;
		private IScriptLog _scriptLog;

		private ExpTracker _expTracker;
		private HighlightSettings _highlightSettings;

		private Timer _spellTimer;
		private string _spell;
		private int _count;

		private TextViewWrapper _mainTextViewWrapper;


		#region Constructors

		// Called when created from unmanaged code
		public MainWindowController(IntPtr handle) : base(handle)
		{
			Initialize();
		}
		// Called when created directly from a XIB file
		[Export("initWithCoder:")]
		public MainWindowController(NSCoder coder) : base(coder)
		{
			Initialize();
		}
		// Call to load from the XIB/NIB file
		public MainWindowController() : base("MainWindow")
		{
			Initialize();
		}
		// Shared initialization code
		void Initialize()
		{
			_bootStrapper = new Bootstrapper();
			_commandCache = new CommandCache();
			_expTracker = new ExpTracker();

			_spellTimer = new Timer();
			_spellTimer.Interval = 1000;
			_spellTimer.Elapsed += (sender, e) => {
				_count++;
				_gameServer.GameState.Set(ComponentKeys.SpellTime, _count.ToString());
				BeginInvokeOnMainThread(() => SpellLabel.StringValue = "S: ({0}) {1}".ToFormat(_count, _spell));
			};
		}

		#endregion

		public void SwitchProfile()
		{
			var profiles = _services.Get<IProfileLoader>().Profiles();

			var ctrl = new ProfileSelectorController();
			ctrl.Window.ParentWindow = Window;
			ctrl.InitWithProfiles(
				profiles,
				_services,
				_services.Get<AppSettings>(),
				_services.Get<IProfileLoader>(),
				_services.Get<IAppSettingsLoader>(),
				() => {
					NSApplication.SharedApplication.StopModal();
				}
			);
			NSApplication.SharedApplication.RunModalForWindow(ctrl.Window);
		}

		public void Connect()
		{
			var loader = _services.Get<IProfileLoader>();
			var profile = loader.Load(_services.Get<AppSettings>().Profile);

			var ctrl = new LoginWindowController();
			ctrl.ShowSheet(
				Window,
				ConnectModel.For(profile.Account, profile.Game, profile.Character),
				_services,
				(model) => {
					Connect(model);
				},
				() => {
				}
			);
		}

		private void InitializeVitalsAndRTBars()
		{
			HealthLabel.Label = "Health 100%";
			HealthLabel.BackgroundColor = "#800000";
			HealthLabel.Value = 0;
			ManaLabel.Label = "Mana 100%";
			ManaLabel.Value = 0;
			ConcentrationLabel.BackgroundColor = "#009999";
			ConcentrationLabel.Label = "Concentration 100%";
			ConcentrationLabel.TextOffset = new PointF(55, 1);
			ConcentrationLabel.Value = 0;
			StaminaLabel.BackgroundColor = "#004000";
			StaminaLabel.Label = "Stamina 100%";
			StaminaLabel.Value = 0;
			SpiritLabel.BackgroundColor = "#400040";
			SpiritLabel.Label = "Spirit 100%";
			SpiritLabel.Value = 0;
			RTLabel.Label = string.Empty;
			RTLabel.Value = 0.0f;
			RTLabel.BackgroundColor = "#0000FF";
			RTLabel.TextOffset = new PointF(6, 2);
			RTLabel.Font = NSFont.FromFontName("Geneva", 16);
		}

		private void UpdateImages()
		{
			var state = _gameServer.GameState;

			NSImage standing = null;
			if(state.Get(ComponentKeys.Standing) == "1") {
				standing = NSImage.ImageNamed("standing-s.png");
			}
			if(state.Get(ComponentKeys.Kneeling) == "1") {
				standing = NSImage.ImageNamed("kneeling-s.png");
			}
			if(state.Get(ComponentKeys.Sitting) == "1") {
				standing = NSImage.ImageNamed("sitting-s.png");
			}
			if(state.Get(ComponentKeys.Prone) == "1") {
				standing = NSImage.ImageNamed("prone-s.png");
			}
			StandingImage.Image = standing;

			NSImage health = null;
			if(state.Get(ComponentKeys.Bleeding) == "1") {
				health = NSImage.ImageNamed("bleeding.png");
			}
			if(state.Get(ComponentKeys.Stunned) == "1") {
				health = NSImage.ImageNamed("stunned.png");
			}
			if(state.Get(ComponentKeys.Dead) == "1") {
				health = NSImage.ImageNamed("dead.png");
			}
			HealthImage.Image = health;

			NSImage hidden = null;
			if(state.Get(ComponentKeys.Hidden) == "1") {
				hidden = NSImage.ImageNamed("hidden.png");
			}
			HiddenImage.Image = hidden;

			NSImage grouped = null;
			if(state.Get(ComponentKeys.Joined) == "1") {
				grouped = NSImage.ImageNamed("group.png");
			}
			GroupedImage.Image = grouped;

			NSImage webbed = null;
			if(state.Get(ComponentKeys.Webbed) == "1") {
				webbed = NSImage.ImageNamed("web.png");
			}
			WebbedImage.Image = webbed;

			NSImage invisible = null;
			if(state.Get(ComponentKeys.Invisible) == "1") {
				invisible = NSImage.ImageNamed("invisible.png");
			}
			InvisibleImage.Image = invisible;
		}

		public override void AwakeFromNib()
		{
			base.AwakeFromNib();

			InitializeVitalsAndRTBars();

			Window.Title = "Outlander";

			_gameServer = _bootStrapper.Build();
			_services = _bootStrapper.ServiceLocator();

			var appSettings = _services.Get<AppSettings>();
			var homeDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Documents/Outlander");
			appSettings.HomeDirectory = homeDir;

			_services.Get<IAppDirectoriesBuilder>().Build();
			_services.Get<IAppSettingsLoader>().Load();

			UpdateImages();

			_commandProcessor = _services.Get<ICommandProcessor>();
			_scriptLog = _services.Get<IScriptLog>();
			_services.Get<IRoundtimeHandler>().Changed += (sender, e) => {
				SetRoundtime(e);
			};

			_highlightSettings = _services.Get<HighlightSettings>();
			_mainTextViewWrapper = new TextViewWrapper(MainTextView, _highlightSettings);

			_scriptLog.Info += (sender, e) => {
				string log;
				if(e.LineNumber > -1)
				{
					log = "[{0}({1})]:  {2}\n".ToFormat(e.Name, e.LineNumber, e.Data);
				}
				else
				{
					log = "[{0}]:  {1}\n".ToFormat(e.Name, e.Data);
				}

				BeginInvokeOnMainThread(()=> {

					var hasLineFeed = MainTextView.TextStorage.Value.EndsWith("\n");

					if(!hasLineFeed)
						log = "\n" + log;

					var tag = TextTag.For(log, "#0066CC");

					_mainTextViewWrapper.Append(tag);
				});
			};

			_scriptLog.NotifyStarted += (sender, e) => {

				var log = "[{0}]: {1} - script started\n".ToFormat(e.Name, e.Started.ToString("G"));

				BeginInvokeOnMainThread(()=> {

					var hasLineFeed = MainTextView.TextStorage.Value.EndsWith("\n");

					if(!hasLineFeed)
						log = "\n" + log;

					var tag = TextTag.For(log, "ADFF2F");
					_mainTextViewWrapper.Append(tag);
				});
			};

			_scriptLog.NotifyAborted += (sender, e) => {
				var log = "[{0}]: total runtime {1:hh\\:mm\\:ss} - {2} seconds\n".ToFormat(e.Name, e.Runtime, Math.Round(e.Runtime.TotalSeconds, 2));

				BeginInvokeOnMainThread(()=> {

					var hasLineFeed = MainTextView.TextStorage.Value.EndsWith("\n");

					if(!hasLineFeed)
						log = "\n" + log;

					var tag = TextTag.For(log, "ADFF2F");
					_mainTextViewWrapper.Append(tag);
				});
			};

			var notifyLogger = new NotificationLogger();
			notifyLogger.OnError = (err) => {
				BeginInvokeOnMainThread(()=> {
					LogSystem(err.Message + "\n\n");
				});
			};

			var compositeLogger = _services.Get<ILog>().As<CompositeLog>();
			compositeLogger.Add(notifyLogger);

			_gameServer.GameState.Tags = (tags) => {
				tags.Apply(t => {
					t.As<AppTag>().IfNotNull(appInfo => {
						BeginInvokeOnMainThread(() => {
							Window.Title = string.Format("{0}: {1} - {2}", appInfo.Game, appInfo.Character, "Outlander");
						});
					});

					t.As<StreamTag>().IfNotNull(streamTag => {
						if(!string.IsNullOrWhiteSpace(streamTag.Id) && streamTag.Id.Equals("logons")) {

							var text = "[{0}]{1}".ToFormat(DateTime.Now.ToString("HH:mm"), streamTag.Text);
							var highlights = _services.Get<Highlights>().For(TextTag.For(text));

							BeginInvokeOnMainThread(()=> {
								highlights.Apply(h => {
									h.Mono = true;
									Append(h, ArrivalsTextView);
								});
							});
						}

						if(!string.IsNullOrWhiteSpace(streamTag.Id) && streamTag.Id.Equals("thoughts")) {

							var text = "[{0}]: {1}".ToFormat(DateTime.Now.ToString("HH:mm"), streamTag.Text);
							var highlights = _services.Get<Highlights>().For(TextTag.For(text));

							BeginInvokeOnMainThread(()=> {
								highlights.Apply(h => {
									Append(h, ThoughtsTextView);
								});
							});
						}

						if(!string.IsNullOrWhiteSpace(streamTag.Id) && streamTag.Id.Equals("death")) {

							var text = "[{0}]{1}".ToFormat(DateTime.Now.ToString("HH:mm"), streamTag.Text);
							var highlights = _services.Get<Highlights>().For(TextTag.For(text));

							BeginInvokeOnMainThread(()=> {
								highlights.Apply(h => {
									Append(h, DeathsTextView);
								});
							});
						}
					});

					t.As<SpellTag>().IfNotNull(s => {
						BeginInvokeOnMainThread(() => {
							_spell = s.Spell;
							_count = 0;
							SpellLabel.StringValue = "S: {0}".ToFormat(s.Spell);

							if(!string.Equals(_spell, "None"))
							{
								_gameServer.GameState.Set(ComponentKeys.SpellTime, "0");
								_spellTimer.Start();
							}
							else
							{
								_spellTimer.Stop();
								_gameServer.GameState.Set(ComponentKeys.SpellTime, "0");
							}
						});
					});

					t.As<VitalsTag>().IfNotNull(v => {
						UpdateVitals();
					});

					var ids = new string[]
					{
						ComponentKeys.RoomTitle,
						ComponentKeys.RoomDescription,
						ComponentKeys.RoomObjects,
						ComponentKeys.RoomPlayers,
						ComponentKeys.RoomExists
					};

					t.As<ComponentTag>().IfNotNull(c => {
						if(!ids.Contains(c.Id))
							return;
						var builder = new StringBuilder();
						_gameServer.GameState.Get(ComponentKeys.RoomTitle).IfNotNullOrEmpty(s=>builder.AppendLine(s));
						_gameServer.GameState.Get(ComponentKeys.RoomDescription).IfNotNullOrEmpty(s=>builder.AppendLine(s));
						_gameServer.GameState.Get(ComponentKeys.RoomObjectsH).IfNotNullOrEmpty(s=>builder.AppendLine(s));
						_gameServer.GameState.Get(ComponentKeys.RoomPlayers).IfNotNullOrEmpty(s=>builder.AppendLine(s));
						_gameServer.GameState.Get(ComponentKeys.RoomExists).IfNotNullOrEmpty(s=>builder.AppendLine(s));

						BeginInvokeOnMainThread(()=> {
							LogRoom(builder.ToString(), RoomTextView);
						});
					});

					BeginInvokeOnMainThread(()=> {
						UpdateImages();

						LeftHandLabel.StringValue = string.Format("L: {0}", _gameServer.GameState.Get(ComponentKeys.LeftHand));
						RightHandLabel.StringValue = string.Format("R: {0}", _gameServer.GameState.Get(ComponentKeys.RightHand));
					});

					_services.Get<IAppSettingsLoader>().SaveVariables();
				});
			};

			_gameServer.GameState.Exp = (exp) => {

				_expTracker.Update(exp);

				var skills = _expTracker.SkillsWithExp().ToList();
				var tags = skills
						.OrderBy(x => x.Name)
						.Select(x =>
						{
							var color = x.IsNew ? _highlightSettings.Get(HighlightKeys.Whisper).Color : string.Empty;
							return TextTag.For(x.Display() + "\n", color, true);
						}).ToList();
				var now = DateTime.Now;
				tags.Add(TextTag.For("Last updated: {0:hh\\:mm\\:ss tt}\n".ToFormat(now), string.Empty, true));
				if(_expTracker.StartedTracking.HasValue)
					tags.Add(TextTag.For("Tracking for: {0:hh\\:mm\\:ss}\n".ToFormat(now - _expTracker.StartedTracking.Value), string.Empty, true));

				BeginInvokeOnMainThread(()=> {
					ReplaceText(tags, ExpTextView);
				});
			};

			_gameStream = _services.Get<IGameStream>();
			_gameStreamListener = new GameStreamListener(tag => {
				BeginInvokeOnMainThread(()=>{
					if(tag.Filtered)
						return;
					Log(tag);
				});
			});
			_gameStreamListener.Subscribe(_gameStream);
		}

		private void Connect(ConnectModel model)
		{
			if(string.IsNullOrWhiteSpace(model.Game)
				|| string.IsNullOrWhiteSpace(model.Account)
				|| string.IsNullOrWhiteSpace(model.Password)
				|| string.IsNullOrWhiteSpace(model.Character))
			{
				LogSystem("Please enter all information\n");
				return;
			}

			LogSystem("Authenticating...\n");

			var token = _gameServer.Authenticate(model.Game, model.Account, model.Password, model.Character);
			if(token != null)
			{
				LogSystem("Authenticated...\n");
				_gameServer.Connect(token);
			}
			else
			{
				LogSystem("Unable to authenticate.\n");
			}
		}

		private void UpdateVitals()
		{
			BeginInvokeOnMainThread(()=> {
				HealthLabel.Value = _gameServer.GameState.Get(ComponentKeys.Health).AsFloat();
				HealthLabel.Label = "Health {0}%".ToFormat(HealthLabel.Value);

				ManaLabel.Value = _gameServer.GameState.Get(ComponentKeys.Mana).AsFloat();
				ManaLabel.Label = "Mana {0}%".ToFormat(ManaLabel.Value);

				StaminaLabel.Value = _gameServer.GameState.Get(ComponentKeys.Stamina).AsFloat();
				StaminaLabel.Label = "Stamina {0}%".ToFormat(StaminaLabel.Value);

				ConcentrationLabel.Value = _gameServer.GameState.Get(ComponentKeys.Concentration).AsFloat();
				ConcentrationLabel.Label = "Concentration {0}%".ToFormat(ConcentrationLabel.Value);

				SpiritLabel.Value = _gameServer.GameState.Get(ComponentKeys.Spirit).AsFloat();
				SpiritLabel.Label = "Spirit {0}%".ToFormat(SpiritLabel.Value);
			});
		}

		private void SendCommand(string command)
		{
			_commandCache.Add(command);

			if(!command.StartsWith("#")) {

				command = _commandProcessor.Eval(command);

				var prompt = _gameServer.GameState.Get(ComponentKeys.Prompt) + " " + command + "\n";

				var hasLineFeed = _mainTextViewWrapper.EndsWithNewline();

				if(!hasLineFeed)
					prompt = "\n" + prompt;

				Log(TextTag.For(prompt));
			}

			_commandProcessor.Process(command, echo: false);
		}

		private void LogRoom(string text, NSTextView textView)
		{
			var defaultSettings = new DefaultSettings();
			var defaultColor = _highlightSettings.Get(HighlightKeys.Default).Color;

			//textView.TextStorage.BeginEditing();
			textView.TextStorage.SetString("".CreateString(defaultColor.ToNSColor(), defaultSettings.Font));
			//textView.TextStorage.EndEditing();

			var highlights = _services.Get<Highlights>();
			highlights.For(TextTag.For(text)).Apply(t => Append(t, textView));
		}

		private void Log(TextTag text)
		{
			var prompt = _gameServer.GameState.Get(ComponentKeys.Prompt);

			if (string.Equals(text.Text.Trim(), prompt)
				&& _mainTextViewWrapper.EndsWith(prompt, true))
				return;

			var highlights = _services.Get<Highlights>();
			highlights.For(text).Apply(t => _mainTextViewWrapper.Append(t));
		}

		private void LogSystem(string text)
		{
			LogSystem(text, "#ffbb00");
		}

		private void LogSystem(string text, string color)
		{
			text = "[{0}]: {1}".ToFormat(DateTime.Now.ToString("HH:mm"), text);
			_mainTextViewWrapper.Append(TextTag.For(text, color, true));
		}

		private void Append(TextTag tag, NSTextView textView)
		{
			var text = tag.Text.Replace("&lt;", "<").Replace("&gt;", ">");

			var defaultSettings = new DefaultSettings();

			var defaultColor = _highlightSettings.Get(HighlightKeys.Default).Color;

			var color = !string.IsNullOrWhiteSpace(tag.Color) ? tag.Color : defaultColor;
			var font = tag.Mono ? defaultSettings.MonoFont : defaultSettings.Font;

			var scroll = textView.EnclosingScrollView.VerticalScroller.FloatValue == 1.0f;

			//textView.TextStorage.BeginEditing();
			textView.TextStorage.Append(text.CreateString(color.ToNSColor(), font));
			//textView.TextStorage.EndEditing();

			if(scroll)
				textView.ScrollRangeToVisible(new NSRange(textView.Value.Length, 0));
		}

		private void ReplaceText(IEnumerable<TextTag> tags, NSTextView textView)
		{
			var defaultSettings = new DefaultSettings();
			var defaultColor = _highlightSettings.Get(HighlightKeys.Default).Color;

			//textView.TextStorage.BeginEditing();
			textView.TextStorage.SetString("".CreateString(defaultColor.ToNSColor()));
			tags.Apply(tag => {
				var color = !string.IsNullOrWhiteSpace(tag.Color) ? tag.Color : defaultColor;
				var font = tag.Mono ? defaultSettings.MonoFont : defaultSettings.Font;
				textView.TextStorage.Append(tag.Text.CreateString(color.ToNSColor(), font));
			});
			//textView.TextStorage.EndEditing();
		}

		private long _rtMax = 0;

		private void SetRoundtime(RoundtimeArgs args)
		{
			if(args.Roundtime < 0)
			{
				args.Roundtime = 0;
			}

			if(args.Reset)
				_rtMax = args.Roundtime;

			BeginInvokeOnMainThread(() => {
				RTLabel.Value = ((float)args.Roundtime / (float)_rtMax) * 100.0f;
				RTLabel.Label = "{0}".ToFormat(args.Roundtime);
				if(args.Roundtime == 0)
					RTLabel.Label = string.Empty;
				if(args.Roundtime < 10)
				{
					RTLabel.TextOffset = new PointF(6, 2);
				}
				else
				{
					RTLabel.TextOffset = new PointF(12, 2);
				}
			});
		}

		public override void KeyUp(NSEvent theEvent)
		{
			base.KeyUp(theEvent);

			var keys = KeyUtil.GetKeys(theEvent);
			if(keys == NSKey.Return && !string.IsNullOrWhiteSpace(CommandTextField.StringValue)) {
				var command = CommandTextField.StringValue;
				CommandTextField.StringValue = string.Empty;
				SendCommand(command);
			}

			if(keys == NSKey.UpArrow)
			{
				_commandCache.MovePrevious();
				CommandTextField.StringValue = _commandCache.Current;
			}

			if(keys == NSKey.DownArrow)
			{
				_commandCache.MoveNext();
				CommandTextField.StringValue = _commandCache.Current;
			}
		}

		//strongly typed window accessor
		public new MainWindow Window {
			get {
				return (MainWindow)base.Window;
			}
		}
	}

	public class TextViewWrapper
	{
		private readonly NSTextView _textView;
		private readonly HighlightSettings _highlightSettings;

		public TextViewWrapper(NSTextView textView, HighlightSettings highlightSettings)
		{
			_textView = textView;
			_highlightSettings = highlightSettings;
		}

		public bool EndsWithNewline()
		{
			return EndsWith("\n");
		}

		public bool EndsWith(string value, bool trim = false)
		{
			var val = trim ? _textView.TextStorage.Value.Trim() : _textView.TextStorage.Value;
			return val.EndsWith(value);
		}

		public void Append(TextTag tag)
		{
			_textView.BeginInvokeOnMainThread(() => {
				var text = tag.Text.Replace("&lt;", "<").Replace("&gt;", ">");

				var defaultSettings = new DefaultSettings();

				var defaultColor = _highlightSettings.Get(HighlightKeys.Default).Color;

				var color = !string.IsNullOrWhiteSpace(tag.Color) ? tag.Color : defaultColor;
				var font = tag.Mono ? defaultSettings.MonoFont : defaultSettings.Font;

				var scroll = _textView.EnclosingScrollView.VerticalScroller.FloatValue == 1.0f;

				//textView.TextStorage.BeginEditing();
				_textView.TextStorage.Append(text.CreateString(color.ToNSColor(), font));
				//textView.TextStorage.EndEditing();

				if(scroll)
					_textView.ScrollRangeToVisible(new NSRange(_textView.Value.Length, 0));
			});
		}

		public void ReplaceText(IEnumerable<TextTag> tags)
		{
			_textView.BeginInvokeOnMainThread(() => {

				var defaultSettings = new DefaultSettings();
				var defaultColor = _highlightSettings.Get(HighlightKeys.Default).Color;

				//textView.TextStorage.BeginEditing();
				_textView.TextStorage.SetString("".CreateString(defaultColor.ToNSColor()));
				tags.Apply(tag => {
					var color = !string.IsNullOrWhiteSpace(tag.Color) ? tag.Color : defaultColor;
					var font = tag.Mono ? defaultSettings.MonoFont : defaultSettings.Font;
					_textView.TextStorage.Append(tag.Text.CreateString(color.ToNSColor(), font));
				});
				//textView.TextStorage.EndEditing();
			});
		}
	}
}
