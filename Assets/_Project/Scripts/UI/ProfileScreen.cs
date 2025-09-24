

using System;
using BedtimeCore;
using Firebase.Database;
using TrainingBuddy.FireBase;
using TrainingBuddy.Managers;
using TrainingBuddy.UI.Controls;
using TrainingBuddy.UI.Effects;
using UnityEngine.UIElements;

namespace TrainingBuddy.UI
{
	public class ProfileScreen : UILayout
	{
		private Button _accPlusButton;
		private Button _accMinusButton;
		private Button _spdPlusButton;
		private Button _spdMinusButton;
		private Button _trainingButton;
		private Button _logoutButton;
		
		private readonly FirebaseController _firebaseController;
		private readonly DatabaseManager _databaseManager;

		private DataSnapshot _dataSnapshot;
		
		protected ProfileScreen(LayoutData layoutData, UIManager uiManager, FirebaseController firebaseController, DatabaseManager databaseManager) : base(layoutData, uiManager)
		{
			Layout = _layoutData.ProfileScreenVisualTree.Instantiate();
			_layoutData.ProfileScreen = this;
			_firebaseController = firebaseController;
			_databaseManager = databaseManager;
		}
		
		public override void Initialize()
		{
			// var containerWithShadow = new Shadow
			// {
			// 	name = "ContainerWithShadow",
			// 	shadowCornerRadius = 54,
			// 	shadowScale = 1,
			// 	shadowOffsetX = 0,
			// 	shadowOffsetY = 0,
			// };
			// containerWithShadow.AddToClassList("drop-shadow");
			//
			// var gradientContainer = new GradientElement
			// {
			// 	name = "GradientContainer",
			// };
			// gradientContainer.AddToClassList("gradient-container");
			
			// var button = new LocalizedButton
			// {
			// 	name = "RaceButton",
			// 	key = "interface_button_race",
			// };
			// var glow = new Glow();
			// glow.AddToClassList("button-glow");
			// button.AddToClassList("button-large");
			// button.Add(glow);
			
			var smallBoxshadowSettings = new ShadowSettings
			{
				CornerRadius = 70,
				ShadowScale = .9f,
				ShadowOffsetX = 0,
				ShadowOffsetY = 0,
			};
			
			var largeBoxshadowSettings = new ShadowSettings
			{
				CornerRadius = 70,
				ShadowScale = 1f,
				ShadowOffsetX = 0,
				ShadowOffsetY = 15,
			};
			
			
			
			// box.AddToClassList("box-content");
			// box.AddToClassList($"box-size-{size}");
			

			// var titleBox = ShadowBox("Title", smallBoxshadowSettings);
			var titleBox = new VisualElement() { name = "UserName" };
			titleBox.AddToClassList("box-header");
			
			var box = new VisualElement() { name = $"StatsContent" };
			box.AddToClassList($"box-size-large");

			var test = new CircularProgressBar { name = "ProgressBar", Value = 0.7f };
			test.AddToClassList($"circular-progress");
			
			box.Add(test);

			// var contentBox = ShadowBox("Stats", largeBoxshadowSettings, BoxSize.large);
			// var contentBox2 = ShadowBox("Stats2", largeBoxshadowSettings, BoxSize.large);
			// var participateRaceButton = ShadowButton("ParticipateRaceButton", "interface_button_participate_race", shadowSettings);
			// var profileButton = ShadowButton("ProfileButton", "interface_button_profile", shadowSettings);
			// var privacyButton = TextButton("PrivacyButton", "interface_button_privacy", "<u>", "</u>");
			//
			// gradientContainer.Content.Add(startRaceButton);
			// gradientContainer.Content.Add(participateRaceButton);
			// gradientContainer.Content.Add(profileButton);
			//
			// containerWithShadow.Add(gradientContainer);
			
			Layout.Q<VisualElement>("ProfileScreen").Add(titleBox);
			Layout.Q<VisualElement>("ProfileScreen").Add(box);
			// Layout.Q<VisualElement>("ProfileScreen").Add(test);
			// Layout.Q<VisualElement>("ProfileScreen").Add(contentBox2);
			// Layout.Q<VisualElement>("MainMenu").Add(participateRaceButton);
			// Layout.Q<VisualElement>("MainMenu").Add(profileButton);
			// Layout.Q<VisualElement>("MainMenu").Add(privacyButton);
		}

		public override async void DrawLayout()
		{
			base.DrawLayout();
			_uiManager.Header.Q<Button>("BackButton").RegisterCallback<ClickEvent>(_ => _uiManager.ChangePage(_layoutData.MainMenu));
			//
			// _accPlusButton = Layout.Q<Button>("AccPlus");
			// _accPlusButton.RegisterCallback<ClickEvent>(AccelerationPlus);
			//
			// _accMinusButton = Layout.Q<Button>("AccMinus");
			// _accMinusButton.RegisterCallback<ClickEvent>(AccelerationMinus);
			//
			// _spdPlusButton = Layout.Q<Button>("SpdPlus");
			// _spdPlusButton.RegisterCallback<ClickEvent>(SpeedPlus);
			//
			// _spdMinusButton = Layout.Q<Button>("SpdMinus");
			// _spdMinusButton.RegisterCallback<ClickEvent>(SpeedMinus);
			//
			// _trainingButton = Layout.Q<Button>("TrainingButton");
			// _trainingButton.RegisterCallback<ClickEvent>(OnTraining);
			//
			// _logoutButton = Layout.Q<Button>("LogoutButton");
			// _logoutButton.RegisterCallback<ClickEvent>(OnLogout);
			//
			// _dataSnapshot = await _databaseManager.FetchUserData(_databaseManager.Auth.CurrentUser);
			//
			// Layout.Q<Label>("Username").text = _dataSnapshot.Child("UserName").Value.ToString();
			//
			// Layout.Q<Label>("AccNumber").text = _dataSnapshot.Child("AccelerationPoints").Value.ToString();
			// Layout.Q<Label>("SpdNumber").text = _dataSnapshot.Child("SpeedPoints").Value.ToString();
			//
			// Layout.Q<Label>("SkillPoints").text = $"Skill Points: {_dataSnapshot.Child("SkillPoints").Value}";
			//
			// var expInt = Convert.ToInt32(_dataSnapshot.Child("ExperiencePoints").Value);
			// var userLevel = Convert.ToInt32(_dataSnapshot.Child("Level").Value);
			//
			// int expNeededToCurrentLevel = (userLevel - 1) * 10000 * userLevel / 2;
			// int expNeededToNextLevel = userLevel * 10000 * (userLevel + 1) / 2;
			// int maxExp = expNeededToNextLevel - expNeededToCurrentLevel;
			//
			// Layout.Q<Label>("Level").text = $"Level: {userLevel}";
			// Layout.Q<ProgressBar>("ExperienceBar").title = $"{expInt - expNeededToCurrentLevel}/{maxExp} XP";
			// Layout.Q<ProgressBar>("ExperienceBar").value = (expInt - expNeededToCurrentLevel) / (float) maxExp * 100;
		}

		private async void AccelerationPlus(ClickEvent evt)
		{
			if (Convert.ToInt32(_dataSnapshot.Child("SkillPoints").Value) <= 0)
			{
				return;
			}
			
			await _databaseManager.UpdateUser(_databaseManager.Auth.CurrentUser, new UserData
			{
				SkillPoints = Convert.ToInt32(_dataSnapshot.Child("SkillPoints").Value) - 1,
				AccelerationPoints = Convert.ToInt32(_dataSnapshot.Child("AccelerationPoints").Value) + 1
			});
			ReDrawLayout();
		}
		
		private async void AccelerationMinus(ClickEvent evt)
		{
			if (Convert.ToInt32(_dataSnapshot.Child("AccelerationPoints").Value) <= 0)
			{
				return;
			}
			
			await _databaseManager.UpdateUser(_databaseManager.Auth.CurrentUser, new UserData
			{
				SkillPoints = Convert.ToInt32(_dataSnapshot.Child("SkillPoints").Value) + 1,
				AccelerationPoints = Convert.ToInt32(_dataSnapshot.Child("AccelerationPoints").Value) - 1
			});
			ReDrawLayout();
		}
		
		private async void SpeedPlus(ClickEvent evt)
		{
			if (Convert.ToInt32(_dataSnapshot.Child("SkillPoints").Value) <= 0)
			{
				return;
			}
			
			await _databaseManager.UpdateUser(_databaseManager.Auth.CurrentUser, new UserData
			{
				SkillPoints = Convert.ToInt32(_dataSnapshot.Child("SkillPoints").Value) - 1,
				SpeedPoints = Convert.ToInt32(_dataSnapshot.Child("SpeedPoints").Value) + 1
			});
			ReDrawLayout();
		}
		
		private async void SpeedMinus(ClickEvent evt)
		{
			if (Convert.ToInt32(_dataSnapshot.Child("SpeedPoints").Value) <= 0)
			{
				return;
			}
			
			await _databaseManager.UpdateUser(_databaseManager.Auth.CurrentUser, new UserData
			{
				SkillPoints = Convert.ToInt32(_dataSnapshot.Child("SkillPoints").Value) + 1,
				SpeedPoints = Convert.ToInt32(_dataSnapshot.Child("SpeedPoints").Value) - 1
			});
			ReDrawLayout();
		}
		
		private async void OnTraining(ClickEvent evt)
		{
			await _databaseManager.InvestInTraining(_layoutData);
			ReDrawLayout();
		}

		private void OnLogout(ClickEvent evt)
		{
			_firebaseController.FirebaseLogout();
			_uiManager.ChangePage(_layoutData.LoginScreen);
		}
	}
}