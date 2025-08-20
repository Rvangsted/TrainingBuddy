

using System;
using BedtimeCore;
using Firebase.Database;
using TrainingBuddy.FireBase;
using TrainingBuddy.Managers;
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

		public override async void DrawLayout()
		{
			base.DrawLayout();
			_uiManager.Header.Q<Button>("BackButton").RegisterCallback<ClickEvent>(_ => _uiManager.ChangePage(_layoutData.MainMenu));
			
			_accPlusButton = Layout.Q<Button>("AccPlus");
			_accPlusButton.RegisterCallback<ClickEvent>(AccelerationPlus);
			
			_accMinusButton = Layout.Q<Button>("AccMinus");
			_accMinusButton.RegisterCallback<ClickEvent>(AccelerationMinus);
			
			_spdPlusButton = Layout.Q<Button>("SpdPlus");
			_spdPlusButton.RegisterCallback<ClickEvent>(SpeedPlus);
			
			_spdMinusButton = Layout.Q<Button>("SpdMinus");
			_spdMinusButton.RegisterCallback<ClickEvent>(SpeedMinus);
			
			_trainingButton = Layout.Q<Button>("TrainingButton");
			_trainingButton.RegisterCallback<ClickEvent>(OnTraining);
			
			_logoutButton = Layout.Q<Button>("LogoutButton");
			_logoutButton.RegisterCallback<ClickEvent>(OnLogout);
			
			_dataSnapshot = await _databaseManager.FetchUserData(_databaseManager.Auth.CurrentUser);
			
			Layout.Q<Label>("Username").text = _dataSnapshot.Child("UserName").Value.ToString();
			
			Layout.Q<Label>("AccNumber").text = _dataSnapshot.Child("AccelerationPoints").Value.ToString();
			Layout.Q<Label>("SpdNumber").text = _dataSnapshot.Child("SpeedPoints").Value.ToString();
			
			Layout.Q<Label>("SkillPoints").text = $"Skill Points: {_dataSnapshot.Child("SkillPoints").Value}";
			
			var expInt = Convert.ToInt32(_dataSnapshot.Child("ExperiencePoints").Value);
			var userLevel = Convert.ToInt32(_dataSnapshot.Child("Level").Value);
			
			int expNeededToCurrentLevel = (userLevel - 1) * 10000 * userLevel / 2;
			int expNeededToNextLevel = userLevel * 10000 * (userLevel + 1) / 2;
			int maxExp = expNeededToNextLevel - expNeededToCurrentLevel;
			
			Layout.Q<Label>("Level").text = $"Level: {userLevel}";
			Layout.Q<ProgressBar>("ExperienceBar").title = $"{expInt - expNeededToCurrentLevel}/{maxExp} XP";
			Layout.Q<ProgressBar>("ExperienceBar").value = (expInt - expNeededToCurrentLevel) / (float) maxExp * 100;
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