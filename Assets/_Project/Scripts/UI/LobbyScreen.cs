using System.Collections.Generic;
using BedtimeCore;
using TrainingBuddy.Managers;
using TrainingBuddy.UI.Controls;
using UnityEngine.UIElements;

namespace TrainingBuddy.UI
{
	public class LobbyScreen : UILayout
	{
		private readonly List<Button> _lobbyButtons = new();
		private string _activeRaceId;

		protected LobbyScreen(LayoutData layoutData, UIManager uiManager, DatabaseManager databaseManager) : base(layoutData, uiManager, databaseManager)
		{
			ConfigureLayoutAsset(_layoutData.LobbyScreenVisualTree, "lobby-wrapper");
			_layoutData.LobbyScreen = this;
		}

		public override void Initialize()
		{
		}

		public override async void DrawLayout()
		{
			base.DrawLayout();
			_uiManager.Header.Q<Label>("SiteTitle").text = "Start løb";

			_activeRaceId = await _databaseManager.GetActiveRaceIdAsync();
			var currentUserId = _databaseManager.Auth.CurrentUser?.UserId;
			var participants = await _databaseManager.FetchCurrentRaceParticipantsAsync();
			var entries = participants.ConvertAll(p => new LobbyEntryData(
				p.displayName,
				p.isHost ? "Host" : "Deltager",
				string.Empty,
				string.Equals(p.sex, "Female", System.StringComparison.OrdinalIgnoreCase) ? "avatar_female" : "avatar_male",
				p.userId == currentUserId
			));
			PopulateLobbyCards(entries);
		}

		private void PopulateLobbyCards(List<LobbyEntryData> entries)
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

			row.Add(card);
			row.Add(avatar);

			if (entry.IsCurrentUser)
			{
				var joinButton = new Button(() => LeaveLobby(index))
				{
					name = $"JoinLobbyButton{index + 1:00}"
				};
				joinButton.AddToClassList("lobby-card-button");
				_lobbyButtons.Add(joinButton);
				row.Add(joinButton);
			}

			return row;
		}

		private void LeaveLobby(int btnNum)
		{
			_uiManager.ShowOverlay(
				"Forlad løb",
				"Er du sikker på, at du vil forlade løbet?",
				"Fortryd",
				() => { },
				"Forlad løb",
				async () =>
				{
					try
					{
						await _databaseManager.LeaveRaceAsync(_activeRaceId);
						_uiManager.HideOverlay();
						_uiManager.ChangePage(_layoutData.MainMenu);
					}
					catch (System.Exception ex)
					{
						$"Failed to leave race: {ex.Message}".LogError();
					}
				},
				UniversalOverlay.PopupImage.None,
				false
			);
		}

		private sealed class LobbyEntryData
		{
			public LobbyEntryData(string title, string host, string startText, string avatarClass, bool isCurrentUser)
			{
				Title = title;
				Host = host;
				StartText = startText;
				AvatarClass = avatarClass;
				IsCurrentUser = isCurrentUser;
			}

			public string Title { get; }
			public string Host { get; }
			public string StartText { get; }
			public string AvatarClass { get; }
			public bool IsCurrentUser { get; }
		}
	}
}
