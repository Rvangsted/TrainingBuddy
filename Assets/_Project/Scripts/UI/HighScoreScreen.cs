using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using TrainingBuddy.FireBase;
using TrainingBuddy.Managers;
using UnityEngine.UIElements;

namespace TrainingBuddy.UI
{
	public class HighScoreScreen : UILayout
	{
		protected HighScoreScreen(LayoutData layoutData, UIManager uiManager, DatabaseManager databaseManager) : base(layoutData, uiManager, databaseManager)
		{
			ConfigureLayoutAsset(_layoutData.HighScoreVisualTree, "highscore-wrapper");
			_layoutData.HighScoreScreen = this;
		}

		public override void Initialize()
		{
		}

		public override async void DrawLayout()
		{
			base.DrawLayout();
			List<LeaderboardEntry> entries = await _databaseManager.FetchLeaderboardAsync();
			PopulateLeaderboard(entries);
		}

		private void PopulateLeaderboard(IEnumerable<LeaderboardEntry> entries)
		{
			if (Layout == null)
			{
				return;
			}

			var sortedEntries = entries
				.Select(e => new LeaderboardEntryData(e.UserName, e.PlacementPoints, SexToAvatarClass(e.Sex)))
				.OrderByDescending(e => e.Points)
				.ThenBy(e => e.Name, StringComparer.OrdinalIgnoreCase)
				.ToList();

			if (sortedEntries.Count == 0)
			{
				return;
			}

			ApplyPodiumEntry("FirstPlace", sortedEntries.ElementAtOrDefault(0), 1);
			ApplyPodiumEntry("SecondPlace", sortedEntries.ElementAtOrDefault(1), 2);
			ApplyPodiumEntry("ThirdPlace", sortedEntries.ElementAtOrDefault(2), 3);

			var leaderboardList = Layout.Q<ScrollView>("LeaderboardList");
			if (leaderboardList == null)
			{
				return;
			}

			leaderboardList.contentContainer.Clear();

			for (var index = 3; index < sortedEntries.Count; index++)
			{
				leaderboardList.contentContainer.Add(CreateLeaderboardRow(sortedEntries[index], index + 1));
			}
		}

		private void ApplyPodiumEntry(string prefix, LeaderboardEntryData entry, int rank)
		{
			if (entry == null)
			{
				return;
			}

			var avatarElement = Layout.Q<VisualElement>($"{prefix}Avatar");
			avatarElement?.RemoveFromClassList("avatar-man");
			avatarElement?.RemoveFromClassList("avatar-woman");
			avatarElement?.RemoveFromClassList("avatar-runner");
			avatarElement?.AddToClassList(entry.AvatarClass);

			var rankLabel = Layout.Q<Label>($"{prefix}Rank");
			if (rankLabel != null)
			{
				rankLabel.text = rank.ToString(CultureInfo.InvariantCulture);
			}

			var nameLabel = Layout.Q<Label>($"{prefix}Name");
			if (nameLabel != null)
			{
				nameLabel.text = entry.Name.ToUpperInvariant();
			}

			var distanceLabel = Layout.Q<Label>($"{prefix}Distance");
			if (distanceLabel != null)
			{
				distanceLabel.text = entry.Points.ToString("N0", new CultureInfo("da-DK"));
			}
		}

		private VisualElement CreateLeaderboardRow(LeaderboardEntryData entry, int rank)
		{
			var row = new VisualElement();
			row.AddToClassList("leaderboard-row");

			var rankLabel = new Label(rank.ToString(CultureInfo.InvariantCulture));
			rankLabel.AddToClassList("leaderboard-rank");
			rankLabel.AddToClassList("font-regular");

			var avatar = new VisualElement();
			avatar.AddToClassList("leaderboard-avatar");
			avatar.AddToClassList(entry.AvatarClass);

			var nameLabel = new Label(entry.Name.ToUpperInvariant());
			nameLabel.AddToClassList("leaderboard-name");
			nameLabel.AddToClassList("font-title");

			var distanceLabel = new Label(entry.Points.ToString("N0", new CultureInfo("da-DK")));
			distanceLabel.AddToClassList("leaderboard-distance");
			distanceLabel.AddToClassList("font-regular");

			row.Add(rankLabel);
			row.Add(avatar);
			row.Add(nameLabel);
			row.Add(distanceLabel);

			return row;
		}

		private static string SexToAvatarClass(string sex)
		{
			return sex?.ToLowerInvariant() switch
			{
				"male" => "avatar-man",
				"female" => "avatar-woman",
				_ => "avatar-runner"
			};
		}

		private sealed class LeaderboardEntryData
		{
			public LeaderboardEntryData(string name, int points, string avatarClass)
			{
				Name = name ?? string.Empty;
				Points = points;
				AvatarClass = avatarClass;
			}

			public string Name { get; }
			public int Points { get; }
			public string AvatarClass { get; }
		}
	}
}
