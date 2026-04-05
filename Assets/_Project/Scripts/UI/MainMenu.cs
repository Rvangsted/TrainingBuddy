using System;
using TrainingBuddy.FireBase;
using TrainingBuddy.Managers;
using UnityEngine;
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

			bool inRace = false;
			try
			{
				inRace = await _databaseManager.IsUserInActiveRaceAsync(_databaseManager.Auth.CurrentUser.UserId);
			}
			catch (Exception ex)
			{
				Debug.LogError($"IsUserInActiveRaceAsync failed: {ex.Message}");
			}

			_resumeRaceButton.style.display = inRace ? DisplayStyle.Flex : DisplayStyle.None;
			_startRaceButton.style.display  = inRace ? DisplayStyle.None : DisplayStyle.Flex;
			_joinRaceButton.style.display   = inRace ? DisplayStyle.None : DisplayStyle.Flex;

			if (!_databaseManager.StepCounterRunning)
			{
				_databaseManager.StartStepCounter();
			}

			
			
			
			// _startRaceButton = Layout.Q<Button>("StartRaceButton");
			// _participateRaceButton = Layout.Q<Button>("ParticipateRaceButton");
			// _profileButton = Layout.Q<Button>("ProfileButton");
			//
			// _startRaceButton.clicked -= CreateLobby;
			// _startRaceButton.clicked += CreateLobby;
			//
			// _participateRaceButton.clicked -= OpenFindLobby;
			// _participateRaceButton.clicked += OpenFindLobby;
			//
			// _profileButton.clicked -= OpenProfile;
			// _profileButton.clicked += OpenProfile;
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

		private async void OnProfile(ClickEvent evt)
		{
			_uiManager.ChangePage(_layoutData.ProfileScreen);
		}
		
		private void OnHighScore(ClickEvent evt)
		{
			_uiManager.ChangePage(_layoutData.HighScoreScreen);
		}
	}
}
