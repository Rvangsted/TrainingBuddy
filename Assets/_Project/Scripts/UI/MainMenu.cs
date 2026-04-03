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
			_privacyButton = Layout.Q<Button>("PrivacyButton");
			_startRaceButton = Layout.Q<Button>("StartRaceButton");
			_joinRaceButton = Layout.Q<Button>("JoinRaceButton");
			_profileButton = Layout.Q<Button>("ProfileButton");
			_highscoreButton = Layout.Q<Button>("HighScoreButton");
			
			// var shadowSettings = new ShadowSettings
			// {
			// 	CornerRadius = 54,
			// 	ShadowScale = .9f,
			// 	ShadowOffsetX = 0,
			// 	ShadowOffsetY = 10,
			// };
			//
			// var startRaceButton = ShadowButton("StartRaceButton", "button_start_race", shadowSettings);
			// var participateRaceButton = ShadowButton("ParticipateRaceButton", "button_participate_race", shadowSettings);
			// var profileButton = ShadowButton("ProfileButton", "button_profile", shadowSettings);
			//
			// Layout.Q<VisualElement>("MainMenu").Add(startRaceButton);
			// Layout.Q<VisualElement>("MainMenu").Add(participateRaceButton);
			// Layout.Q<VisualElement>("MainMenu").Add(profileButton);

			_startRaceButton.RegisterCallback<ClickEvent>(OnCreateLobby);
			_joinRaceButton.RegisterCallback<ClickEvent>(OnFindLobby);
			_profileButton.RegisterCallback<ClickEvent>(OnProfile);
			_highscoreButton.RegisterCallback<ClickEvent>(OnHighScore);
			_privacyButton.RegisterCallback<ClickEvent>(_ => Debug.Log("Privacy Button clicked"));
		}

		public override void DrawLayout()
		{
			base.DrawLayout();
			_uiManager.Header.Q<Label>("SiteTitle").text = "Leaderboard";
			
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
			// var _userDataSnapshot = await _databaseManager.FetchUserData(_databaseManager.Auth.CurrentUser);
			// var longitude = Convert.ToInt32(_userDataSnapshot.Child("Longitude").Value);
			// var latitude = Convert.ToInt32(_userDataSnapshot.Child("Latitude").Value);
			// await _databaseManager.CreateLobby(new RaceData
			// {
			// 	RaceName = $"{_databaseManager.Auth.CurrentUser.DisplayName}'s Race",
			// 	HostName = _databaseManager.Auth.CurrentUser.DisplayName,
			// 	Longitude = longitude,
			// 	Latitude = latitude,
			// 	Status = 0,
			// });
			
			_uiManager.ChangePage(_layoutData.RaceScreen);
		}
		
		private void OnFindLobby(ClickEvent evt)
		{
			_uiManager.ChangePage(_layoutData.FindLobbyScreen);
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
