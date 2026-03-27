
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using TrainingBuddy.Managers;
using UnityEngine.UIElements;

namespace TrainingBuddy.UI
{
	public class HighScoreScreen : UILayout
	{
		private readonly LeaderboardEntryData[] _testEntries = CreateTestEntries();

		protected HighScoreScreen(LayoutData layoutData, UIManager uiManager, DatabaseManager databaseManager) : base(layoutData, uiManager, databaseManager)
		{
			ConfigureLayoutAsset(_layoutData.HighScoreVisualTree, "highscore-wrapper");
			_layoutData.HighScoreScreen = this;
		}
		
		public override void Initialize()
		{
			
			// throw new System.NotImplementedException();
		}

		public override void DrawLayout()
		{
			base.DrawLayout();
			PopulateLeaderboard(_testEntries);
		}

		private void PopulateLeaderboard(IEnumerable<LeaderboardEntryData> entries)
		{
			if (Layout == null)
			{
				return;
			}

			var sortedEntries = entries
				.OrderByDescending(entry => entry.DistanceKm)
				.ThenBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase)
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
				distanceLabel.text = FormatDistance(entry.DistanceKm);
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

			var distanceLabel = new Label(FormatDistance(entry.DistanceKm));
			distanceLabel.AddToClassList("leaderboard-distance");
			distanceLabel.AddToClassList("font-regular");

			row.Add(rankLabel);
			row.Add(avatar);
			row.Add(nameLabel);
			row.Add(distanceLabel);

			return row;
		}

		private string FormatDistance(float distanceKm)
		{
			return $"{distanceKm:0.#} km";
		}

		private static LeaderboardEntryData[] CreateTestEntries()
		{
			var firstNames = new[]
			{
				"Emma", "Lucas", "Freja", "Noah", "Sofie", "Oscar", "Clara", "Elias", "Anna", "Malthe",
				"Ida", "Oliver", "Nora", "William", "Alma", "Theo", "Liva", "Felix", "Josefine", "Aksel"
			};

			var lastNames = new[]
			{
				"Nielsen", "Jensen", "Hansen", "Pedersen", "Andersen", "Christensen", "Larsen", "Sorensen",
				"Rasmussen", "Jorgensen", "Madsen", "Kristensen", "Olsen", "Thomsen", "Poulsen"
			};

			var avatarClasses = new[] { "avatar-man", "avatar-woman", "avatar-runner" };
			var random = new Random(20260323);
			var entries = new LeaderboardEntryData[50];

			for (var index = 0; index < entries.Length; index++)
			{
				var firstName = firstNames[random.Next(firstNames.Length)];
				var lastNameInitial = lastNames[random.Next(lastNames.Length)][0];
				var distance = (float)Math.Round(20 + (random.NextDouble() * 180), 1);
				var avatarClass = avatarClasses[random.Next(avatarClasses.Length)];

				entries[index] = new LeaderboardEntryData(
					$"{firstName} {lastNameInitial}.",
					distance,
					avatarClass);
			}

			return entries;
		}

		private sealed class LeaderboardEntryData
		{
			public LeaderboardEntryData(string name, float distanceKm, string avatarClass)
			{
				Name = name;
				DistanceKm = distanceKm;
				AvatarClass = avatarClass;
			}

			public string Name { get; }
			public float DistanceKm { get; }
			public string AvatarClass { get; }
		}
	}
}
