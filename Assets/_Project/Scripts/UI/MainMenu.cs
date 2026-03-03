using System;
using BedtimeCore;
using TrainingBuddy.FireBase;
using TrainingBuddy.Managers;
using TrainingBuddy.UI.Controls;
using TrainingBuddy.UI.Effects;
using UnityEngine.UIElements;

namespace TrainingBuddy.UI
{
	public class MainMenu : UILayout
	{
		private Button _startRaceButton;
		private Button _participateRaceButton;
		private Button _profileButton;
		
		protected MainMenu(LayoutData layoutData, UIManager uiManager, DatabaseManager databaseManager) : base(layoutData, uiManager, databaseManager)
		{
			ConfigureLayoutAsset(_layoutData.MainMenuVisualTree, "main-menu-wrapper");
			_layoutData.MainMenu = this;
		}
		
		public override void Initialize()
		{
			var shadowSettings = new ShadowSettings
			{
				CornerRadius = 54,
				ShadowScale = .9f,
				ShadowOffsetX = 0,
				ShadowOffsetY = 10,
			};

			var startRaceButton = ShadowButton("StartRaceButton", "button_start_race", shadowSettings);
			var participateRaceButton = ShadowButton("ParticipateRaceButton", "button_participate_race", shadowSettings);
			var profileButton = ShadowButton("ProfileButton", "button_profile", shadowSettings);
			var privacyButton = TextButton("PrivacyButton", "button_privacy", "<u>", "</u>");
			
			Layout.Q<VisualElement>("MainMenu").Add(startRaceButton);
			Layout.Q<VisualElement>("MainMenu").Add(participateRaceButton);
			Layout.Q<VisualElement>("MainMenu").Add(profileButton);
			Layout.Q<VisualElement>("MainMenu").Add(privacyButton);
		}

		public override void DrawLayout()
		{
			base.DrawLayout();
			if (!_databaseManager.StepCounterRunning)
			{
				_databaseManager.StartStepCounter();
			}
			
			_startRaceButton = Layout.Q<Button>("StartRaceButton");
			_participateRaceButton = Layout.Q<Button>("ParticipateRaceButton");
			_profileButton = Layout.Q<Button>("ProfileButton");
			
			_startRaceButton.clicked -= CreateLobby;
			_startRaceButton.clicked += CreateLobby;

			_participateRaceButton.clicked -= OpenFindLobby;
			_participateRaceButton.clicked += OpenFindLobby;

			_profileButton.clicked -= OpenProfile;
			_profileButton.clicked += OpenProfile;
		}
		
		private void OpenFindLobby()
		{
			_uiManager.ChangePage(_layoutData.FindLobbyScreen);
		}

		private void OpenProfile()
		{
			_uiManager.ChangePage(_layoutData.ProfileScreen);
		}

		private async void CreateLobby()
		{
			var _userDataSnapshot = await _databaseManager.FetchUserData(_databaseManager.Auth.CurrentUser);
			var longitude = Convert.ToInt32(_userDataSnapshot.Child("Longitude").Value);
			var latitude = Convert.ToInt32(_userDataSnapshot.Child("Latitude").Value);
			await _databaseManager.CreateLobby(new RaceData
			{
				RaceName = $"{_databaseManager.Auth.CurrentUser.DisplayName}'s Race",
				HostName = _databaseManager.Auth.CurrentUser.DisplayName,
				Longitude = longitude,
				Latitude = latitude,
				Status = 0,
			});
			
			_uiManager.ChangePage(_layoutData.LobbyScreen);
		}
	}
}
