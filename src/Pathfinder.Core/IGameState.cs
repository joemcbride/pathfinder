using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Outlander.Core.Authentication;
using Outlander.Core.Text;
using Outlander.Core;

namespace Outlander.Core
{
	public delegate void TextLogHandler(string text);

	public interface IGameState
	{
		string Get(string key);
		void Set(string key, string value);
		void Read(string data);
		void Echo(string text);
		ISimpleDictionary<string, string> GlobalVars();

		event TextLogHandler TextLog;
		DataTracker<string> TextTracker { get; }

		DataTracker<IEnumerable<Tag>> TagTracker { get; }

		Action<IEnumerable<Tag>> Tags { get; set; }
		Action<SkillExp> Exp { get; set; }
	}

	public class SimpleGameState : IGameState
	{
		private readonly IGameParser _parser;
		private readonly List<string> _filters = new List<string>();
		private readonly SimpleDictionary<string, string> _components = new SimpleDictionary<string, string>();
		private readonly IRoundtimeHandler _roundtimeHandler;

		public event TextLogHandler TextLog = delegate { };
		public DataTracker<string> TextTracker { get; private set; }

		public DataTracker<IEnumerable<Tag>> TagTracker { get; private set; }
		public Action<IEnumerable<Tag>> Tags { get; set; }
		public Action<SkillExp> Exp { get; set; }

		public SimpleGameState(IGameParser parser, IRoundtimeHandler roundtimeHandler)
		{
			_parser = parser;
			_roundtimeHandler = roundtimeHandler;
			_components.Set(ComponentKeys.Prompt, ">");

			TextTracker = new DataTracker<string>();
			TagTracker = new DataTracker<IEnumerable<Tag>>();
		}

		public string Get(string key)
		{
			return _components.Get(key);
		}

		public void Set(string key, string value)
		{
			_components.Set(key, value);
		}

		public void Read(string data)
		{
			var result = _parser.Parse(Chunk.For(data));
			ApplyTags(result.Tags);
			RenderData(result);
		}

		public void Echo(string text)
		{
			Log(text);
		}

		public ISimpleDictionary<string, string> GlobalVars()
		{
			return _components;
		}

		public void RenderData(ReadResult result)
		{
			if (result.Chunk == null || string.IsNullOrWhiteSpace(result.Chunk.Text) || string.IsNullOrWhiteSpace(result.Chunk.Text.Trim()))
				return;

			Log(result.Chunk.Text);
		}

		public void ApplyTags(IEnumerable<Tag> tags)
		{
			tags = tags.ToList();

			tags.OfType<ComponentTag>().Apply(c => {
				if(c.IsExp) {
					var skill = SkillExp.For(c);
					var ranksId = c.Id + ".Ranks";
					// don't set ranks to empty if a value already exists
					if(!_components.HasKey(ranksId) || !string.IsNullOrWhiteSpace(skill.Ranks))
					{
						_components.Set(ranksId, skill.Ranks);
					}
					_components.Set(c.Id + ".LearningRate", skill.LearningRate.Id.ToString());
					_components.Set(c.Id + ".LearningRateName", skill.LearningRate.Description);
					if(Exp != null)
					{
						Exp(skill);
					}
				} else if(string.Equals(ComponentKeys.RoomObjects, c.Id)) {
					_components.Set(c.Id,Regex.Replace(c.Value, "<[^>]*>", string.Empty));
					_components.Set(ComponentKeys.RoomObjectsH, c.Value);
					var monsterMatch = Regex.Matches(c.Value, RegexPatterns.MonsterBold);
					_components.Set(ComponentKeys.MonsterCount, monsterMatch.Count.ToString());
					_components.Set(ComponentKeys.MonsterList, string.Join(", ", monsterMatch.OfType<Match>().Select(x=>x.Groups[2].Value).ToArray()));
				} else {
					_components.Set(c.Id, c.Value);
				}
			});

			tags.OfType<RoomNameTag>().Apply(t => {
				_components.Set(ComponentKeys.RoomName, t.Name.Trim());
			});

			tags.OfType<VitalsTag>().Apply(t => {
				_components.Set(t.Name, t.Value.ToString());
			});

			tags.OfType<IndicatorTag>().Apply(t => {
				_components.Set(t.Id, t.Value);
			});

			tags.OfType<LeftHandTag>().Apply(t => {
				_components.Set(ComponentKeys.LeftHand, t.Name);
				_components.Set(ComponentKeys.LeftHandId, t.Id);
				_components.Set(ComponentKeys.LeftHandNoun, t.Noun);
			});

			tags.OfType<RightHandTag>().Apply(t => {
				_components.Set(ComponentKeys.RightHand, t.Name);
				_components.Set(ComponentKeys.RightHandId, t.Id);
				_components.Set(ComponentKeys.RightHandNoun, t.Noun);
			});

			tags.OfType<SpellTag>().Apply(t => {
				_components.Set(ComponentKeys.Spell, t.Spell);
			});

			tags.OfType<PromptTag>().Apply(t => {
				_components.Set(ComponentKeys.Prompt, t.Prompt);
				_components.Set(ComponentKeys.GameTime, t.GameTime);
				_components.Set(ComponentKeys.GameTimeUpdate, DateTime.UtcNow.DateTimeToUnixTimestamp().ToString());
			});

			tags.OfType<RoundtimeTag>().Apply(t => {
				var gameTime = _components.Get(ComponentKeys.GameTime).UnixTimeStampToDateTime();
				var gameTimeLastUpdate = _components.Get(ComponentKeys.GameTimeUpdate).UnixTimeStampToDateTime();
				var diff = t.RoundTime - gameTime;
				var realDiff = diff.Subtract(DateTime.Now - gameTimeLastUpdate);
				_roundtimeHandler.Set(realDiff.Seconds);
			});

			tags.OfType<AppTag>().Apply(t => {
				_components.Set(ComponentKeys.CharacterName, t.Character);
				_components.Set(ComponentKeys.Game, t.Game);
			});

			var tagsEv = Tags;
			if(tagsEv != null && tags != null){
				tagsEv(tags);
			}

			TagTracker.Publish(tags);
		}

		private void Log(string data)
		{
			var textLog = TextLog;
			if(textLog != null)
			{
				textLog(Filter(data));
			}

			TextTracker.Publish(Filter(data));
		}

		private string Filter(string data)
		{
			if (string.IsNullOrEmpty(data))
				return data;

			data = Regex.Replace(data, "[\r\n]{3,}", "\n\n");

			_filters.Apply(x => data = Regex.Replace(data, x, string.Empty));

			return data;
		}
	}
}
