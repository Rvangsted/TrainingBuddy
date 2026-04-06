using System.Collections.Generic;
using BedtimeCore;
using TrainingBuddy.Managers;
using TrainingBuddy.UI.Controls;
using UnityEngine.UIElements;

namespace TrainingBuddy.UI
{
	public class HostScreen : UILayout
	{
		private readonly List<Button> _hostButtons = new();
		private string _activeRaceId;
		private List<(string displayName, bool isHost, long joinedAt, string sex, string userId)> _participants = new();

		protected HostScreen(LayoutData layoutData, UIManager uiManager, DatabaseManager databaseManager) : base(layoutData, uiManager, databaseManager)
		{
			ConfigureLayoutAsset(_layoutData.HostScreenVisualTree, "host-wrapper");
			_layoutData.HostScreen = this;
		}

		public override void Initialize()
		{
		}

		public override async void DrawLayout()
		{
			base.DrawLayout();
			_uiManager.Header.Q<Label>("SiteTitle").text = "Start løb";

			var startButton = Layout.Q<Button>("StartButton");
			startButton.clicked += async () => await StartRace();

			_activeRaceId = await _databaseManager.GetActiveRaceIdAsync();
			var currentUserId = _databaseManager.Auth.CurrentUser?.UserId;
			_participants = await _databaseManager.FetchCurrentRaceParticipantsAsync();

			var capacity     = await _databaseManager.FetchRaceCapacityAsync(_activeRaceId);
			var inviteButton = Layout.Q<Button>("InviteButton");
			if (inviteButton != null)
				inviteButton.style.display = (capacity > 0 && _participants.Count >= capacity)
					? DisplayStyle.None
					: DisplayStyle.Flex;

			var entries = _participants.ConvertAll(p => new HostEntryData(
				p.displayName,
				p.isHost ? "Host" : "Deltager",
				string.Empty,
				string.Equals(p.sex, "Female", System.StringComparison.OrdinalIgnoreCase) ? "avatar_female" : "avatar_male",
				p.userId == currentUserId,
				p.userId
			));
			PopulateHostCards(entries);
		}

		private async System.Threading.Tasks.Task StartRace()
		{
			try
			{
				var simulation = await _databaseManager.StartRaceAsync(_activeRaceId);
				_layoutData.RaceScreen.PrepareWithSimulation(simulation, _activeRaceId);
				_uiManager.ChangePage(_layoutData.RaceScreen);
			}
			catch (System.Exception ex)
			{
				$"Failed to start race: {ex.Message}".LogError();
				_uiManager.ShowOverlay("Kan ikke starte", ex.Message, "OK", () => { });
			}
		}

		private void PopulateHostCards(IEnumerable<HostEntryData> entries)
		{
			if (Layout == null)
			{
				return;
			}

			var hostList = Layout.Q<VisualElement>("HostList");
			if (hostList == null)
			{
				return;
			}

			hostList.Clear();
			_hostButtons.Clear();

			var index = 0;
			foreach (var entry in entries)
			{
				hostList.Add(CreateHostCard(entry, index));
				index++;
			}
		}

		private VisualElement CreateHostCard(HostEntryData entry, int index)
		{
			var row = new VisualElement
			{
				name = $"HostCard{index + 1:00}"
			};
			row.AddToClassList("host-card-row");

			var card = new VisualElement();
			card.AddToClassList("host-card");

			var content = new VisualElement();
			content.AddToClassList("host-card-content");

			var titleLabel = new Label(entry.Title.ToUpperInvariant());
			titleLabel.AddToClassList("host-card-title");
			titleLabel.AddToClassList("font-title");

			var hostLabel = new Label($"Host: {entry.Host}");
			hostLabel.AddToClassList("host-card-host");
			hostLabel.AddToClassList("font-regular");

			var startLabel = new Label($"Starter: {entry.StartText}");
			startLabel.AddToClassList("host-card-start");
			startLabel.AddToClassList("font-regular");

			content.Add(titleLabel);
			content.Add(hostLabel);
			content.Add(startLabel);
			card.Add(content);

			var avatar = new VisualElement();
			avatar.AddToClassList("host-card-avatar");
			avatar.AddToClassList(entry.AvatarClass);

			var joinButton = entry.IsCurrentUser
				? new Button(() => CancelRace()) { name = $"JoinHostButton{index + 1:00}" }
				: new Button(() => KickPlayer(entry.UserId, entry.Title)) { name = $"JoinHostButton{index + 1:00}" };
			joinButton.AddToClassList("host-card-button");
			if (entry.IsCurrentUser)
				joinButton.AddToClassList("host-card-button--host");

			_hostButtons.Add(joinButton);

			row.Add(card);
			row.Add(avatar);
			row.Add(joinButton);

			return row;
		}

		private void KickPlayer(string userId, string displayName)
		{
			_uiManager.ShowOverlay(
				"Fjern deltager",
				$"Er du sikker på, at du vil fjerne {displayName} fra løbet?",
				"Fortryd",
				() => { },
				"Fjern deltager",
				async () =>
				{
					try
					{
						await _databaseManager.KickParticipantAsync(_activeRaceId, userId);
						_uiManager.HideOverlay();
						DrawLayout();
					}
					catch (System.Exception ex)
					{
						$"Failed to kick participant: {ex.Message}".LogError();
					}
				},
				UniversalOverlay.PopupImage.None,
				false
			);
		}

		private void CancelRace()
		{
			_uiManager.ShowOverlay(
				"Aflys løb",
				"Er du sikker på, at du vil aflyse løbet? Alle deltagere vil blive fjernet.",
				"Fortryd",
				() => { },
				"Aflys løb",
				async () =>
				{
					try
					{
						await _databaseManager.CancelRaceAsync(_activeRaceId);
						_uiManager.ChangePage(_layoutData.MainMenu);
					}
					catch (System.Exception ex)
					{
						$"Failed to cancel race: {ex.Message}".LogError();
					}
				},
				UniversalOverlay.PopupImage.None,
				false
			);
		}

		private sealed class HostEntryData
		{
			public HostEntryData(string title, string host, string startText, string avatarClass, bool isCurrentUser, string userId)
			{
				Title = title;
				Host = host;
				StartText = startText;
				AvatarClass = avatarClass;
				IsCurrentUser = isCurrentUser;
				UserId = userId;
			}

			public string Title { get; }
			public string Host { get; }
			public string StartText { get; }
			public string AvatarClass { get; }
			public bool IsCurrentUser { get; }
			public string UserId { get; }
		}
	}
}
