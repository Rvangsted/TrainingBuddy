using System.Collections.Generic;
using System.Linq;
using BedtimeCore;
using TrainingBuddy.FireBase;
using TrainingBuddy.Managers;
using TrainingBuddy.UI.Controls;
using UnityEngine.UIElements;

namespace TrainingBuddy.UI
{
	public class FindLobbyScreen : UILayout
	{
		private enum StatusFilter { All, Open, InProgress }

		private List<RaceListEntry> _allRaces = new();
		private StatusFilter _statusFilter = StatusFilter.All;
		private string _searchText = string.Empty;
		private bool _eventsWired;

		protected FindLobbyScreen(LayoutData layoutData, UIManager uiManager, DatabaseManager databaseManager) : base(layoutData, uiManager, databaseManager)
		{
			ConfigureLayoutAsset(_layoutData.FindLobbyScreenVisualTree, "find-lobby-wrapper");
			_layoutData.FindLobbyScreen = this;
		}

		public override void Initialize()
		{
		}

		public override async void DrawLayout()
		{
			base.DrawLayout();
			_uiManager.Header.Q<Label>("SiteTitle").text = "Deltag i løb";

			WireEvents();

			_allRaces = await _databaseManager.FetchRaceListAsync();
			ApplyFilter();
		}

		private void WireEvents()
		{
			if (_eventsWired || Layout == null)
			{
				return;
			}

			_eventsWired = true;

			var searchField = Layout.Q<LocalizedTextInput>("SearchField");
			if (searchField != null)
			{
				searchField.RegisterValueChangedCallback(evt =>
				{
					_searchText = evt.newValue ?? string.Empty;
					ApplyFilter();
				});
			}

			var filterButton = Layout.Q<Button>("FilterButton");
			if (filterButton != null)
			{
				UpdateFilterButtonLabel(filterButton);
				filterButton.clicked += () =>
				{
					_statusFilter = _statusFilter switch
					{
						StatusFilter.All => StatusFilter.Open,
						StatusFilter.Open => StatusFilter.InProgress,
						_ => StatusFilter.All,
					};
					UpdateFilterButtonLabel(filterButton);
					ApplyFilter();
				};
			}
		}

		private void UpdateFilterButtonLabel(Button filterButton)
		{
			filterButton.text = _statusFilter switch
			{
				StatusFilter.Open => "Åbne",
				StatusFilter.InProgress => "I gang",
				_ => "Alle",
			};
		}

		private void ApplyFilter()
		{
			IEnumerable<RaceListEntry> filtered = _allRaces
				.Where(r => r.Status == "open" && r.ParticipantCount < r.Capacity);

			filtered = _statusFilter switch
			{
				StatusFilter.Open => filtered.Where(r => r.Status == "open"),
				StatusFilter.InProgress => filtered.Where(r => r.Status == "in_progress"),
				_ => filtered,
			};

			if (!string.IsNullOrWhiteSpace(_searchText))
			{
				filtered = filtered.Where(r => r.Title.Contains(_searchText, System.StringComparison.OrdinalIgnoreCase));
			}

			PopulateLobbyCards(filtered.ToList());
		}

		private void PopulateLobbyCards(IReadOnlyList<RaceListEntry> entries)
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

			if (entries.Count == 0)
			{
				var emptyLabel = new Label("Ingen løb fundet");
				emptyLabel.AddToClassList("lobby-empty-label");
				emptyLabel.AddToClassList("font-regular");
				lobbyList.contentContainer.Add(emptyLabel);
				return;
			}

			for (var i = 0; i < entries.Count; i++)
			{
				lobbyList.contentContainer.Add(CreateLobbyCard(entries[i], i));
			}
		}

		private VisualElement CreateLobbyCard(RaceListEntry entry, int index)
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

			var hostLabel = new Label($"Host: {entry.HostName}");
			hostLabel.AddToClassList("lobby-card-host");
			hostLabel.AddToClassList("font-regular");

			string statusText = entry.Status switch
			{
				"open" => "Åben",
				"in_progress" => "I gang",
				"completed" => "Afsluttet",
				"cancelled" => "Aflyst",
				_ => entry.Status,
			};
			var statusLabel = new Label(statusText);
			statusLabel.AddToClassList("lobby-card-start");
			statusLabel.AddToClassList("font-regular");

			content.Add(titleLabel);
			content.Add(hostLabel);
			content.Add(statusLabel);
			card.Add(content);

			var avatar = new VisualElement();
			avatar.AddToClassList("lobby-card-avatar");
			string avatarClass = string.Equals(entry.HostSex, "Female", System.StringComparison.OrdinalIgnoreCase)
				? "avatar_female"
				: "avatar_male";
			avatar.AddToClassList(avatarClass);

			string raceId = entry.RaceId;
			var joinButton = new Button(() => JoinLobby(raceId))
			{
				name = $"JoinLobbyButton{index + 1:00}"
			};
			joinButton.AddToClassList("lobby-card-button");

			row.Add(card);
			row.Add(avatar);
			row.Add(joinButton);

			return row;
		}

		private async void JoinLobby(string raceId)
		{
			try
			{
				await _databaseManager.JoinRaceDirectlyAsync(raceId);
				$"Joined race: {raceId}".Log();
				_uiManager.ChangePage(_layoutData.LobbyScreen);
			}
			catch (System.Exception ex)
			{
				$"Failed to join race {raceId}: {ex.Message}".LogError();
			}
		}
	}
}
