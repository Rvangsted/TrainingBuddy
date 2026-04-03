using System.Collections.Generic;
using BedtimeCore;
using TrainingBuddy.Managers;
using UnityEngine.UIElements;

namespace TrainingBuddy.UI
{
	public class LobbyScreen : UILayout
	{
		private readonly LobbyEntryData[] _testEntries = CreateTestEntries();
		private readonly List<Button> _lobbyButtons = new();

		protected LobbyScreen(LayoutData layoutData, UIManager uiManager, DatabaseManager databaseManager) : base(layoutData, uiManager, databaseManager)
		{
			ConfigureLayoutAsset(_layoutData.LobbyScreenVisualTree, "lobby-wrapper");
			_layoutData.LobbyScreen = this;
		}

		public override void Initialize()
		{
		}

		public override void DrawLayout()
		{
			base.DrawLayout();
			PopulateLobbyCards(_testEntries);
			_uiManager.Header.Q<Label>("SiteTitle").text = "Start løb";
		}

		private void PopulateLobbyCards(IEnumerable<LobbyEntryData> entries)
		{
			if (Layout == null)
			{
				return;
			}

			var lobbyList = Layout.Q<ScrollView>("LobbyList");
			if (lobbyList == null)
			{
				return;
			}

			lobbyList.contentContainer.Clear();
			_lobbyButtons.Clear();

			var index = 0;
			foreach (var entry in entries)
			{
				lobbyList.contentContainer.Add(CreateLobbyCard(entry, index));
				index++;
			}
		}

		private VisualElement CreateLobbyCard(LobbyEntryData entry, int index)
		{
			var row = new VisualElement
			{
				name = $"LobbyCard{index + 1:00}"
			};
			row.AddToClassList("lobby-card-row");

			var card = new VisualElement();
			card.AddToClassList("lobby-card");

			var content = new VisualElement();
			content.AddToClassList("lobby-card-content");

			var titleLabel = new Label(entry.Title.ToUpperInvariant());
			titleLabel.AddToClassList("lobby-card-title");
			titleLabel.AddToClassList("font-title");

			var hostLabel = new Label($"Host: {entry.Host}");
			hostLabel.AddToClassList("lobby-card-host");
			hostLabel.AddToClassList("font-regular");

			var startLabel = new Label($"Starter: {entry.StartText}");
			startLabel.AddToClassList("lobby-card-start");
			startLabel.AddToClassList("font-regular");

			content.Add(titleLabel);
			content.Add(hostLabel);
			content.Add(startLabel);
			card.Add(content);

			var avatar = new VisualElement();
			avatar.AddToClassList("lobby-card-avatar");
			avatar.AddToClassList(entry.AvatarClass);

			var joinButton = new Button(() => JoinLobby(index))
			{
				name = $"JoinLobbyButton{index + 1:00}"
			};
			joinButton.AddToClassList("lobby-card-button");

			_lobbyButtons.Add(joinButton);

			row.Add(card);
			row.Add(avatar);
			row.Add(joinButton);

			return row;
		}

		private void JoinLobby(int btnNum)
		{
			$"BUTTON NR: {btnNum}".Log();
		}

		private static LobbyEntryData[] CreateTestEntries()
		{
			return new[]
			{
				new LobbyEntryData("L\u00F8beklubben", "Peter Mikkelsen", "Om 10 minutter", "avatar_male"),
				new LobbyEntryData("Sprinterne", "Marie", "Er igang", "avatar_female"),
				new LobbyEntryData("Morgenholdet", "Sofie", "Om 5 minutter", "avatar_male"),
				new LobbyEntryData("Byparken 5K", "Jonas", "Om 18 minutter", "avatar_female"),
				new LobbyEntryData("Intervalholdet", "Nanna", "Er igang", "avatar_male"),
				new LobbyEntryData("Aftenl\u00F8berne", "Kasper", "Om 25 minutter", "avatar_female"),
				new LobbyEntryData("Tempo Team", "Mikkel", "Om 8 minutter", "avatar_male"),
				new LobbyEntryData("Weekendracet", "Camilla", "Om 12 minutter", "avatar_female")
			};
		}

		private sealed class LobbyEntryData
		{
			public LobbyEntryData(string title, string host, string startText, string avatarClass)
			{
				Title = title;
				Host = host;
				StartText = startText;
				AvatarClass = avatarClass;
			}

			public string Title { get; }
			public string Host { get; }
			public string StartText { get; }
			public string AvatarClass { get; }
		}
	}
}
