using System;
using TrainingBuddy.FireBase;
using TrainingBuddy.Managers;
using TrainingBuddy.UI.Controls;
using UnityEngine;
using UnityEngine.Localization.Settings;
using UnityEngine.UIElements;

namespace TrainingBuddy.UI
{
	public class MainMenu : UILayout
	{
		private Button _startRaceButton;
		private Button _resumeRaceButton;
		private Button _joinRaceButton;
		private Button _profileButton;
		private Button _highscoreButton;
		private Button _privacyButton;
		
		protected MainMenu(LayoutData layoutData, UIManager uiManager, DatabaseManager databaseManager) : base(layoutData, uiManager, databaseManager)
		{
			ConfigureLayoutAsset(_layoutData.MainMenuVisualTree, "main-menu-wrapper");
			_layoutData.MainMenu = this;
		}
		
		public override void Initialize()
		{
			_startRaceButton = Layout.Q<Button>("StartRaceButton");
			_resumeRaceButton = Layout.Q<Button>("ResumeRaceButton");
			_joinRaceButton = Layout.Q<Button>("JoinRaceButton");
			_profileButton = Layout.Q<Button>("ProfileButton");
			_highscoreButton = Layout.Q<Button>("HighScoreButton");
			_privacyButton = Layout.Q<Button>("PrivacyButton");

			_startRaceButton.RegisterCallback<ClickEvent>(OnCreateLobby);
			_joinRaceButton.RegisterCallback<ClickEvent>(OnFindLobby);
			_resumeRaceButton.RegisterCallback<ClickEvent>(OnResumeLobby);
			_profileButton.RegisterCallback<ClickEvent>(OnProfile);
			_highscoreButton.RegisterCallback<ClickEvent>(OnHighScore);
			_privacyButton.RegisterCallback<ClickEvent>(_ => Debug.Log("Privacy Button clicked"));
		}

		public override async void DrawLayout()
		{
			base.DrawLayout();

			_resumeRaceButton.style.display = DisplayStyle.None;
			_startRaceButton.style.display  = DisplayStyle.None;
			_joinRaceButton.style.display   = DisplayStyle.None;

			bool inRace     = false;
			bool inProgress = false;
			try
			{
				string activeRaceId = await _databaseManager.GetActiveRaceIdAsync();
				inRace = activeRaceId != null;
				if (inRace)
				{
					string status = await _databaseManager.GetRaceStatusAsync(activeRaceId);
					inProgress = string.Equals(status, "in_progress", StringComparison.OrdinalIgnoreCase);
				}
			}
			catch (Exception ex)
			{
				Debug.LogError($"Race status check failed: {ex.Message}");
			}

			_resumeRaceButton.style.display = inRace ? DisplayStyle.Flex : DisplayStyle.None;
			_startRaceButton.style.display  = inRace ? DisplayStyle.None : DisplayStyle.Flex;
			_joinRaceButton.style.display   = inRace ? DisplayStyle.None : DisplayStyle.Flex;

			if (inRace)
			{
				string key  = inProgress ? "watch_race" : "button_resume_race";
				string text = LocalizationSettings.StringDatabase.GetLocalizedString(key);
				_resumeRaceButton.text = !string.IsNullOrEmpty(text) ? text : key;
			}

			if (!_databaseManager.StepCounterRunning)
			{
				await _databaseManager.StartStepCounter();
			}
		}
		
		private async void OnCreateLobby(ClickEvent evt)
		{
			await _databaseManager.CreateLobby(new RaceData
			{
				RaceName = $"{_databaseManager.Auth.CurrentUser.DisplayName}'s Race",
				HostName = _databaseManager.Auth.CurrentUser.DisplayName,
				Longitude = 0,
				Latitude = 0,
				Status = 0,
			});
			
			_uiManager.ChangePage(_layoutData.HostScreen);
		}
		
		private void OnFindLobby(ClickEvent evt)
		{
			_uiManager.ChangePage(_layoutData.FindLobbyScreen);
		}
		
		private async void OnResumeLobby(ClickEvent evt)
		{
			bool isHost = await _databaseManager.IsUserHostingActiveRaceAsync(_databaseManager.Auth.CurrentUser.UserId);
			_uiManager.ChangePage(isHost ? _layoutData.HostScreen : _layoutData.LobbyScreen);
		}

		private void OnProfile(ClickEvent evt)
		{
			_uiManager.ChangePage(_layoutData.ProfileScreen);
		}
		
		private void OnHighScore(ClickEvent evt)
		{
			_uiManager.ChangePage(_layoutData.HighScoreScreen);
		}
	}
}
