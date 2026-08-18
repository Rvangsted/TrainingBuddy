using System;
using System.Collections.Generic;
using TrainingBuddy.FireBase;
using TrainingBuddy.Managers;
using TrainingBuddy.UI.Controls;
using UnityEngine;
using UnityEngine.UIElements;

namespace TrainingBuddy.UI
{
	public class RaceScreen : UILayout
	{
		private const int MaxPlayers = 5;
		private const float IconWidth = 44f;

		private VisualElement _playerIconsContainer;
		private VisualElement _runnersContainer;
		private readonly List<VisualElement> _playerPins = new();
		private readonly List<RunnerLaneElement> _runnerLanes = new();
		private readonly float[] _playerProgresses = new float[MaxPlayers];
		private int _playerCount;

		// ── Pending data (set before ChangePage) ──────────────────────────────
		private PlayerRaceData[] _pendingPlayers;
		private RaceSimulation _pendingSimulation;
		private string _pendingRaceId;

		// ── Live simulation state ─────────────────────────────────────────────
		private PlayerRaceData[] _activePlayers;
		private bool[] _playerFinished;
		private float _raceTime;
		private bool _isRaceRunning;
		private string _activeRaceId;

		// ── TEST LOOP (fallback when no simulation data is provided) ──────────
		private bool _testLoopActive;
		private float _testProgress;
		private const float TestLoopLapDuration = 5f;

		private static readonly PlayerRaceData[] TestPlayers =
		{
			new("Emil",   isMale: true,  laneIndex: 0, finishTime: 57f, accelerationBias: 0.7f),
			new("Marie",  isMale: false, laneIndex: 1, finishTime: 60f, accelerationBias: 0.4f),
			new("Jonas",  isMale: true,  laneIndex: 2, finishTime: 62f, accelerationBias: 0.2f),
			new("Sofie",  isMale: false, laneIndex: 3, finishTime: 59f, accelerationBias: 0.5f),
			new("Kasper", isMale: true,  laneIndex: 4, finishTime: 63f, accelerationBias: 0.1f),
		};

		protected RaceScreen(LayoutData layoutData, UIManager uiManager, DatabaseManager databaseManager) : base(layoutData, uiManager, databaseManager)
		{
			ConfigureLayoutAsset(_layoutData.RaceScreenVisualTree, "race-wrapper");
			_layoutData.RaceScreen = this;
		}

		public override void Initialize() { }

		/// <summary>Call before ChangePage to supply a live simulation from Firebase.</summary>
		public void PrepareWithSimulation(RaceSimulation simulation, string raceId)
		{
			_pendingSimulation = simulation;
			_pendingRaceId     = raceId;
			_pendingPlayers    = null;
			_layoutDrawn       = false;
		}

		/// <summary>Legacy: call before ChangePage to supply static player data (used by test/editor flows).</summary>
		public void PrepareWithPlayers(PlayerRaceData[] players)
		{
			_pendingPlayers    = players;
			_pendingSimulation = null;
			_layoutDrawn       = false;
		}

		public override void DrawLayout()
		{
			base.DrawLayout();

			_playerIconsContainer = Layout.Q<VisualElement>("PlayerIconsContainer");
			_playerIconsContainer.RegisterCallback<GeometryChangedEvent>(_ => RefreshAllPinPositions());

			_runnersContainer = Layout.Q<VisualElement>("RunnersContainer");

			Layout.RegisterCallback<DetachFromPanelEvent>(_ =>
			{
				StopAllRunnerAnimations();
				_isRaceRunning   = false;
				_testLoopActive  = false;
			});

			if (_pendingSimulation != null)
			{
				_activeRaceId = _pendingRaceId;
				StartSimulatedRace(_pendingSimulation);
				_pendingSimulation = null;
				_pendingRaceId     = null;
			}
			else
			{
				var players = _pendingPlayers ?? TestPlayers;
				SetupPlayers(players);
				SetupRunnerLanes(players);
				_pendingPlayers = null;
				StartProgressTestLoop();
			}
		}

		// ── Simulation playback ────────────────────────────────────────────────

		private void StartSimulatedRace(RaceSimulation simulation)
		{
			// Sort participants by lane so runner index == lane index == path index
			var sorted = new List<RaceSimulationParticipant>(simulation.Participants);
			sorted.Sort((a, b) => a.Lane.CompareTo(b.Lane));

			_activePlayers = new PlayerRaceData[sorted.Count];
			for (int i = 0; i < sorted.Count; i++)
			{
				var p = sorted[i];
				_activePlayers[i] = new PlayerRaceData(
					p.DisplayName,
					!string.Equals(p.Sex, "Female", StringComparison.OrdinalIgnoreCase),
					laneIndex:        p.Lane,
					finishTime:       p.FinishTime,
					accelerationBias: p.AccelerationBias,
					userId:           p.UserId
				);
			}

			SetupPlayers(_activePlayers);
			SetupRunnerLanes(_activePlayers);

			_playerFinished = new bool[_activePlayers.Length];
			_raceTime       = 0f;
			_isRaceRunning  = true;
		}

		/// <summary>Called from UIManager.Update() every frame while this screen is active.</summary>
		public void TickRace(float deltaTime)
		{
			if (_isRaceRunning && _activePlayers != null)
			{
				_raceTime += deltaTime;
				bool allFinished = true;
				for (int i = 0; i < _activePlayers.Length; i++)
				{
					if (_playerFinished[i]) continue;

					_runnerLanes[i].EnsureAnimating();
					float progress = GetProgress(_raceTime, _activePlayers[i].FinishTime, _activePlayers[i].AccelerationBias);
					SetPlayerProgress(i, progress);

					if (progress >= 1f)
					{
						_playerFinished[i] = true;
						_runnerLanes[i].StopAnimation();
						_runnerLanes[i].style.display = DisplayStyle.None;
					}
					else
					{
						allFinished = false;
					}
				}

				if (allFinished)
				{
					_isRaceRunning = false;
					AnnounceWinner();
				}
			}
			else if (_testLoopActive)
			{
				TickTestLoop(deltaTime);
			}
		}

		private static float GetProgress(float raceTime, float finishTime, float accelBias)
		{
			float t     = Mathf.Clamp01(raceTime / finishTime);
			// accelBias=1 → convex curve (fast start), accelBias=0 → linear
			float power = Mathf.Lerp(1f, 0.55f, accelBias);
			return Mathf.Pow(t, power);
		}

		private void AnnounceWinner()
		{
			// Winner = lowest finish time
			var winner = _activePlayers[0];
			for (int i = 1; i < _activePlayers.Length; i++)
			{
				if (_activePlayers[i].FinishTime < winner.FinishTime)
					winner = _activePlayers[i];
			}

			string currentUserId     = _databaseManager.Auth?.CurrentUser?.UserId;
			bool isCurrentUserWinner = currentUserId != null && winner.UserId == currentUserId;
			string message           = isCurrentUserWinner ? "Du vandt løbet!" : $"{winner.Name} vinder løbet!";

			// Client-side display only — computed from data already in memory so the popup can
			// show a number instantly, with no wait on MarkRaceWatchedAsync's DB round-trip.
			// MarkRaceWatchedAsync still does the authoritative award using the same rank/table
			// (PlacementPointsTable, PlacementPoints_Scope.md) once every participant has watched.
			if (currentUserId != null)
			{
				var ranked = new List<PlayerRaceData>(_activePlayers);
				ranked.Sort((a, b) => a.FinishTime.CompareTo(b.FinishTime));
				int rank = ranked.FindIndex(p => p.UserId == currentUserId) + 1;
				if (rank > 0)
				{
					int points = PlacementPointsTable.GetPoints(rank);
					// Rich text (UI Toolkit Label supports this out of the box) so the award
					// reads as a distinct line rather than an afterthought — see
					// PlacementPoints_Scope.md "UI". Stays inline in the same popup rather than
					// a toast: this is the direct outcome of the race already on screen, not an
					// unrelated background event.
					message += $"\n<b><size=120%><color=#AF59FF>+{points} placeringspoint</color></size></b>";
				}
			}

			string raceId = _activeRaceId;
			_uiManager.ShowOverlay(
				"Løbet er slut!",
				message,
				"Tilbage til hovedmenu",
				async () =>
				{
					if (raceId != null)
						await _databaseManager.MarkRaceWatchedAsync(raceId);
					_uiManager.ChangePage(_layoutData.MainMenu);
				},
				UniversalOverlay.PopupImage.None,
				false
			);
		}

		// ── Player/runner setup ────────────────────────────────────────────────

		public void SetupPlayers(PlayerRaceData[] players)
		{
			_playerCount = Mathf.Clamp(players.Length, 0, MaxPlayers);

			_playerIconsContainer.Clear();
			_playerPins.Clear();

			for (var i = 0; i < _playerCount; i++)
			{
				_playerProgresses[i] = 0f;
				var pin = CreatePlayerPin(players[i], i);
				_playerPins.Add(pin);
				_playerIconsContainer.Add(pin);
			}

			RefreshAllPinPositions();
		}

		public void SetPlayerProgress(int playerIndex, float progress)
		{
			if (playerIndex < 0 || playerIndex >= _playerCount)
				return;

			_playerProgresses[playerIndex] = Mathf.Clamp01(progress);
			PlacePin(playerIndex);
			SetRunnerProgress(playerIndex, _playerProgresses[playerIndex]);
		}

		// ── Private helpers ────────────────────────────────────────────────────

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

		public void SetRunnerProgress(int index, float progress)
		{
			if (index < 0 || index >= _runnerLanes.Count) return;
			_runnerLanes[index].SetProgress(progress);
		}

		private void SetupRunnerLanes(PlayerRaceData[] players)
		{
			StopAllRunnerAnimations();
			_runnersContainer.Clear();
			_runnerLanes.Clear();

			var count = Mathf.Clamp(players.Length, 0, MaxPlayers);
			for (var i = 0; i < count; i++)
			{
				var player = players[i];
				var frames = player.IsMale ? _uiManager.MaleRunnerFrames : _uiManager.FemaleRunnerFrames;

				var lane = new RunnerLaneElement { name = $"RunnerLane{i}" };
				lane.AddToClassList($"runner-lane-{i}");
				var genderClass = player.IsMale ? "runner-male" : "runner-female";
				var paths = _uiManager.RunnerPaths;
				var path = paths != null && i < paths.Length ? paths[i] : null;
				lane.Configure(path?.X, path?.Y, frames, _uiManager.RunnerFramesPerSecond, genderClass,
					path?.StartSize, path?.EndSize, path?.SizeCurve);
				lane.SetName(player.Name);
				lane.SetProgress(0f);

				_runnerLanes.Add(lane);
				_runnersContainer.Add(lane);
			}
		}

		// ── Test loop (fallback / editor preview) ─────────────────────────────

		public void StartProgressTestLoop()
		{
			_testProgress   = 0f;
			_testLoopActive = true;
		}

		public void StopProgressTestLoop()
		{
			_testLoopActive = false;
		}

		private void TickTestLoop(float deltaTime)
		{
			_testProgress = (_testProgress + deltaTime / TestLoopLapDuration) % 1f;
			for (var i = 0; i < _runnerLanes.Count; i++)
			{
				_runnerLanes[i].EnsureAnimating();
				var t = (_testProgress + i * 0.15f) % 1f;
				SetRunnerProgress(i, t);
			}
		}

		private void StopAllRunnerAnimations()
		{
			foreach (var lane in _runnerLanes)
				lane.StopAnimation();
		}

		private void PlacePin(int index)
		{
			if (index >= _playerPins.Count)
				return;

			var containerWidth = _playerIconsContainer.resolvedStyle.width;
			if (containerWidth <= 0)
				return;

			var x = _playerProgresses[index] * containerWidth - IconWidth * 0.5f;
			x = Mathf.Clamp(x, 0f, Mathf.Max(0f, containerWidth - IconWidth));
			_playerPins[index].style.left = x;
		}

		private void RefreshAllPinPositions()
		{
			for (var i = 0; i < _playerPins.Count; i++)
				PlacePin(i);
		}

		// ── Data class ─────────────────────────────────────────────────────────

		public sealed class PlayerRaceData
		{
			public string Name             { get; }
			public bool   IsMale           { get; }
			public int    LaneIndex        { get; }
			public float  FinishTime       { get; }
			public float  AccelerationBias { get; }
			public string UserId           { get; }

			public PlayerRaceData(string name, bool isMale, int laneIndex = 0, float finishTime = 60f, float accelerationBias = 0f, string userId = null)
			{
				Name             = name;
				IsMale           = isMale;
				LaneIndex        = laneIndex;
				FinishTime       = finishTime;
				AccelerationBias = accelerationBias;
				UserId           = userId;
			}
		}
	}
}