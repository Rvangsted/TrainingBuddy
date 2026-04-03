using System.Collections.Generic;
using TrainingBuddy.Managers;
using UnityEngine;
using UnityEngine.UIElements;

namespace TrainingBuddy.UI
{
	public class RaceScreen : UILayout
	{
		private const int MaxPlayers = 6;
		private const float IconWidth = 44f;

		private VisualElement _playerIconsContainer;
		private readonly List<VisualElement> _playerPins = new();
		private readonly float[] _playerProgresses = new float[MaxPlayers];
		private int _playerCount;

		private static readonly PlayerRaceData[] TestPlayers =
		{
			new PlayerRaceData("Emil", isMale:true, progress:0.82f),
			new PlayerRaceData("Marie",  isMale: false, progress: 0.79f),
			new PlayerRaceData("Jonas",  isMale: true,  progress: 0.61f),
			new PlayerRaceData("Sofie",  isMale: false, progress: 0.45f),
			new PlayerRaceData("Kasper", isMale: true,  progress: 0.44f),
			new PlayerRaceData("Emma",   isMale: false, progress: 0.20f),
		};

		protected RaceScreen(LayoutData layoutData, UIManager uiManager, DatabaseManager databaseManager) : base(layoutData, uiManager, databaseManager)
		{
			ConfigureLayoutAsset(_layoutData.RaceScreenVisualTree, "race-wrapper");
			_layoutData.RaceScreen = this;
		}

		public override void Initialize() { }

		public override void DrawLayout()
		{
			base.DrawLayout();

			_playerIconsContainer = Layout.Q<VisualElement>("PlayerIconsContainer");
			_playerIconsContainer.RegisterCallback<GeometryChangedEvent>(_ => RefreshAllPinPositions());

			SetupPlayers(TestPlayers);
		}

		/// <summary>
		/// Populates the progress bar with up to 6 players.
		/// </summary>
		public void SetupPlayers(PlayerRaceData[] players)
		{
			_playerCount = Mathf.Clamp(players.Length, 0, MaxPlayers);

			_playerIconsContainer.Clear();
			_playerPins.Clear();

			for (var i = 0; i < _playerCount; i++)
			{
				_playerProgresses[i] = Mathf.Clamp01(players[i].Progress);
				var pin = CreatePlayerPin(players[i], i);
				_playerPins.Add(pin);
				_playerIconsContainer.Add(pin);
			}

			RefreshAllPinPositions();
		}

		/// <summary>
		/// Update a single player's progress along the bar.
		/// </summary>
		/// <param name="playerIndex">0-based index.</param>
		/// <param name="progress">0 = start, 1 = finish.</param>
		public void SetPlayerProgress(int playerIndex, float progress)
		{
			if (playerIndex < 0 || playerIndex >= _playerCount)
				return;

			_playerProgresses[playerIndex] = Mathf.Clamp01(progress);
			PlacePin(playerIndex);
		}

		// ── private helpers ────────────────────────────────────────────────────

		private VisualElement CreatePlayerPin(PlayerRaceData player, int index)
		{
			var pin = new VisualElement { name = $"PlayerPin{index}" };
			pin.AddToClassList("player-pin");

			var nameLabel = new Label(player.Name);
			nameLabel.AddToClassList("player-name");
			nameLabel.AddToClassList("font-regular");

			var icon = new VisualElement();
			icon.AddToClassList("player-icon");
			icon.AddToClassList($"player-color-{index}");
			icon.AddToClassList(player.IsMale ? "avatar-male" : "avatar-female");

			pin.Add(nameLabel);
			pin.Add(icon);

			return pin;
		}

		private void PlacePin(int index)
		{
			if (index >= _playerPins.Count)
				return;

			var containerWidth = _playerIconsContainer.resolvedStyle.width;
			if (containerWidth <= 0)
				return;

			// Centre the pin (which has a fixed width matching the icon) over the progress point.
			var x = _playerProgresses[index] * containerWidth - IconWidth * 0.5f;
			x = Mathf.Clamp(x, 0f, Mathf.Max(0f, containerWidth - IconWidth));
			_playerPins[index].style.left = x;
		}

		private void RefreshAllPinPositions()
		{
			for (var i = 0; i < _playerPins.Count; i++)
				PlacePin(i);
		}

		public sealed class PlayerRaceData
		{
			public string Name { get; }
			public bool IsMale { get; }
			public float Progress { get; }

			public PlayerRaceData(string name, bool isMale, float progress)
			{
				Name = name;
				IsMale = isMale;
				Progress = progress;
			}
		}
	}
}