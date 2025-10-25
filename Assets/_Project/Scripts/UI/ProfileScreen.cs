using System;
using System.Collections.Generic;
using BedtimeCore;
using Firebase.Database;
using TrainingBuddy.FireBase;
using TrainingBuddy.Managers;
using TrainingBuddy.UI.Controls;
using TrainingBuddy.UI.Effects;
using UnityEngine;
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

		private ActivityGraph _activityGraph;
		
		private readonly FirebaseController _firebaseController;
		private readonly DatabaseManager _databaseManager;

		private DataSnapshot _dataSnapshot;
		
		protected ProfileScreen(LayoutData layoutData, UIManager uiManager, FirebaseController firebaseController, DatabaseManager databaseManager) : base(layoutData, uiManager)
		{
			Layout = _layoutData.ProfileScreenVisualTree.Instantiate();
			Layout.AddToClassList("profile-wrapper");
			_layoutData.ProfileScreen = this;
			_firebaseController = firebaseController;
			_databaseManager = databaseManager;
		}
		
		public override void Initialize()
		{
			//InitializeStatsSection();
			//InitializeActivitySection();
			//InitializeFriendsSection();
		}

		public override async void DrawLayout()
		{
			_dataSnapshot = await _databaseManager.FetchUserData(_databaseManager.Auth.CurrentUser);
			base.DrawLayout();
				
			Layout.Q<Label>("Name").text = _dataSnapshot.Child("UserName").Value.ToString();
			Layout.Q<Label>("DateOfBirth").text = _dataSnapshot.Child("Email").Value.ToString();
			Layout.Q<Label>("UserID").text = _dataSnapshot.Child("UserID").Value.ToString();
			Layout.Q<VisualElement>("ProfilePicture").AddToClassList(_dataSnapshot.Child("Sex").Value.ToString());
			
			DrawStatsSection();
			DrawActivitySection();
			DrawFriendsSection();
			
			_uiManager.Header.Q<Button>("BackButton").RegisterCallback<ClickEvent>(_ => _uiManager.ChangePage(_layoutData.MainMenu));
        }
		
		private void DrawStatsSection()
		{
			var stepsToGo = 10000 - Convert.ToInt32(_dataSnapshot.Child("StepCount").Value);
			var levelingProgressWrapper = new VisualElement();
			levelingProgressWrapper.AddToClassList("leveling-progress-wrapper");
			
			var circularProgressBar = new CircularProgressBar
			{
				name = "LevelingProgressBar", 
				Value = Convert.ToInt32(_dataSnapshot.Child("StepCount").Value) / 10000f,
				TrackColor = new Color(0.88f, 0.88f, 0.88f, 1f),
				ProgressColor = new Color(0.76f, 1f, 0f, 1f),
				KnobColor = new Color(0.68f, 0.94f, 0f, 1f),
				InnerKnobColor = new Color(0.88f, 0.88f, 0.88f, 1f),
				ArrowColor = new Color(0, 0f, 0f, 1f),
				
				LineThickness = 30f,
				StartAngle = 270f,
				KnobSize = 20f,
				InnerKnobSize = 6f,
				ShowKnob = true,
				ShowArrow = true,
				ArrowLength = 18f,
				ArrowWidth = 18f,
				ArrowOffset = 6f,
			};
			circularProgressBar.AddToClassList($"leveling-progress-bar");

			var levelingProgressContent = new VisualElement();
			levelingProgressContent.AddToClassList("leveling-progress-content");
			
			var levelingProgressValueLabel = new Label
			{
				name = "LevelingProgressValueLabel",
				text = $"{(10000 - Convert.ToInt32(_dataSnapshot.Child("StepCount").Value)).ToString()} skridt",
			};
			levelingProgressValueLabel.AddToClassList("leveling-progress-value-label");
			levelingProgressValueLabel.AddToClassList("font-title");
			
			var levelingProgressSubtitleLabel = new Label
			{
				name = "LevelingProgressSubtitleLabel",
				text = "Til næste niveau",
			};
			levelingProgressSubtitleLabel.AddToClassList("leveling-progress-subtitle-label");
			levelingProgressSubtitleLabel.AddToClassList("font-regular");
			
			levelingProgressContent.Add(levelingProgressValueLabel);
			levelingProgressContent.Add(levelingProgressSubtitleLabel);
			
			levelingProgressWrapper.Add(circularProgressBar);
			levelingProgressWrapper.Add(levelingProgressContent);
			Layout.Q<VisualElement>("LevelingMiddleContainer").Add(levelingProgressWrapper);
			
			// SpeedStatUpperMiddle
			var speedProgressBar = new LinearProgressBar
			{
				name = "SpeedProgressBar", 
				Value = 0.7f,
				TrackColor = new Color(0.88f, 0.88f, 0.88f, 1f),
				ProgressColor = new Color(0.76f, 1f, 0f, 1f),
				KnobColor = new Color(0.68f, 0.94f, 0f, 1f),
				InnerKnobColor = new Color(0.88f, 0.88f, 0.88f, 1f),
				
				LineThickness = 35f,
				KnobSize = 25f,
				InnerKnobSize = 6f,
			};
			speedProgressBar.AddToClassList($"linear-progress-bar");
			Layout.Q<VisualElement>("SpeedStatUpperMiddle").Add(speedProgressBar);
			
			var accelerationProgressBar = new LinearProgressBar
			{
				name = "AccelerationProgressBar", 
				Value = 0.7f,
				TrackColor = new Color(0.88f, 0.88f, 0.88f, 1f),
				ProgressColor = new Color(0.76f, 1f, 0f, 1f),
				KnobColor = new Color(0.68f, 0.94f, 0f, 1f),
				InnerKnobColor = new Color(0.88f, 0.88f, 0.88f, 1f),
				
				LineThickness = 35f,
				KnobSize = 25f,
				InnerKnobSize = 6f,
			};
			accelerationProgressBar.AddToClassList($"linear-progress-bar");
			Layout.Q<VisualElement>("AccelerationStatUpperMiddle").Add(accelerationProgressBar);
			Layout.Q<VisualElement>("LevelingMiddleContainer").Add(levelingProgressWrapper);
		}

		private void DrawFriendsSection()
		{
			for (var i = 1; i <= 6; i++)
			{
				var friendElement = new VisualElement();
				friendElement.AddToClassList("friend-element");
				if (i % 3 == 0)
				{
					friendElement.AddToClassList("no-margin");
				}
			
				var friendImage = new VisualElement();
				friendImage.AddToClassList("friend-image");
			
				var friendFavoriteIcon = new VisualElement();
				friendFavoriteIcon.AddToClassList("friend-favorite-icon");
			
				var friendNameLabel = new Label
				{
					name = "FriendName",
					text = "Name",
				};
				friendNameLabel.AddToClassList("friend-name-label");
				friendNameLabel.AddToClassList("font-title");
			
				var friendDistanceLabel = new Label
				{
					name = "FriendDistance",
					text = "50 km",
				};
				friendDistanceLabel.AddToClassList("friend-distance-label");
				friendDistanceLabel.AddToClassList("font-regular");
			
				friendElement.Add(friendImage);
				friendElement.Add(friendFavoriteIcon);
				friendElement.Add(friendNameLabel);
				friendElement.Add(friendDistanceLabel);
			
				Layout.Q<VisualElement>("FriendsContent").Add(friendElement);
			}
		}

		private void DrawActivitySection()
        {
            var activityContainer = Layout.Q<VisualElement>("ActivityContainer");
            if (activityContainer == null)
            {
				return;
            }

            _activityGraph = new ActivityGraph
            {
				name = "WeeklyDistanceGraph", 
				ValueFormatter = value => $"{value:0.#} KM",
            };

            activityContainer.Add(_activityGraph);

            var sampleData = new List<ActivityGraph.DataPoint>
            {
                new("28. april", 2.8f),
                new("29. april", 3.6f),
                new("30. april", 2.9f),
                new("I dag", 5.0f),
                new("1. maj", 2.4f),
            };

            _activityGraph.SetData(sampleData, 3);
        }

        public void UpdateActivityData(IEnumerable<ActivityGraph.DataPoint> dataPoints, int highlightIndex)
        {
            _activityGraph?.SetData(dataPoints);
        }

		private void OnLogout(ClickEvent evt)
		{
			_firebaseController.FirebaseLogout();
			_uiManager.ChangePage(_layoutData.LoginScreen);
		}
		
		// public override async void DrawLayout()
		// {
		// 	base.DrawLayout();
		// 	_uiManager.Header.Q<Button>("BackButton").RegisterCallback<ClickEvent>(_ => _uiManager.ChangePage(_layoutData.MainMenu));
		// 	
		// 	_accPlusButton = Layout.Q<Button>("AccPlus");
		// 	_accPlusButton.RegisterCallback<ClickEvent>(AccelerationPlus);
		// 	
		// 	_accMinusButton = Layout.Q<Button>("AccMinus");
		// 	_accMinusButton.RegisterCallback<ClickEvent>(AccelerationMinus);
		// 	
		// 	_spdPlusButton = Layout.Q<Button>("SpdPlus");
		// 	_spdPlusButton.RegisterCallback<ClickEvent>(SpeedPlus);
		// 	
		// 	_spdMinusButton = Layout.Q<Button>("SpdMinus");
		// 	_spdMinusButton.RegisterCallback<ClickEvent>(SpeedMinus);
		// 	
		// 	_trainingButton = Layout.Q<Button>("TrainingButton");
		// 	_trainingButton.RegisterCallback<ClickEvent>(OnTraining);
		// 	
		// 	_logoutButton = Layout.Q<Button>("LogoutButton");
		// 	_logoutButton.RegisterCallback<ClickEvent>(OnLogout);
		// 	
		// 	_dataSnapshot = await _databaseManager.FetchUserData(_databaseManager.Auth.CurrentUser);
		// 	
		// 	Layout.Q<Label>("Username").text = _dataSnapshot.Child("UserName").Value.ToString();
		// 	
		// 	Layout.Q<Label>("AccNumber").text = _dataSnapshot.Child("AccelerationPoints").Value.ToString();
		// 	Layout.Q<Label>("SpdNumber").text = _dataSnapshot.Child("SpeedPoints").Value.ToString();
		// 	
		// 	Layout.Q<Label>("SkillPoints").text = $"Skill Points: {_dataSnapshot.Child("SkillPoints").Value}";
		// 	
		// 	var expInt = Convert.ToInt32(_dataSnapshot.Child("ExperiencePoints").Value);
		// 	var userLevel = Convert.ToInt32(_dataSnapshot.Child("Level").Value);
		// 	
		// 	int expNeededToCurrentLevel = (userLevel - 1) * 10000 * userLevel / 2;
		// 	int expNeededToNextLevel = userLevel * 10000 * (userLevel + 1) / 2;
		// 	int maxExp = expNeededToNextLevel - expNeededToCurrentLevel;
		// 	
		// 	Layout.Q<Label>("Level").text = $"Level: {userLevel}";
		// 	Layout.Q<ProgressBar>("ExperienceBar").title = $"{expInt - expNeededToCurrentLevel}/{maxExp} XP";
		// 	Layout.Q<ProgressBar>("ExperienceBar").value = (expInt - expNeededToCurrentLevel) / (float) maxExp * 100;
		// }
		//
		// private async void AccelerationPlus(ClickEvent evt)
		// {
		// 	if (Convert.ToInt32(_dataSnapshot.Child("SkillPoints").Value) <= 0)
		// 	{
		// 		return;
		// 	}
		// 	
		// 	await _databaseManager.UpdateUser(_databaseManager.Auth.CurrentUser, new UserData
		// 	{
		// 		SkillPoints = Convert.ToInt32(_dataSnapshot.Child("SkillPoints").Value) - 1,
		// 		AccelerationPoints = Convert.ToInt32(_dataSnapshot.Child("AccelerationPoints").Value) + 1
		// 	});
		// 	ReDrawLayout();
		// }
		//
		// private async void AccelerationMinus(ClickEvent evt)
		// {
		// 	if (Convert.ToInt32(_dataSnapshot.Child("AccelerationPoints").Value) <= 0)
		// 	{
		// 		return;
		// 	}
		// 	
		// 	await _databaseManager.UpdateUser(_databaseManager.Auth.CurrentUser, new UserData
		// 	{
		// 		SkillPoints = Convert.ToInt32(_dataSnapshot.Child("SkillPoints").Value) + 1,
		// 		AccelerationPoints = Convert.ToInt32(_dataSnapshot.Child("AccelerationPoints").Value) - 1
		// 	});
		// 	ReDrawLayout();
		// }
		//
		// private async void SpeedPlus(ClickEvent evt)
		// {
		// 	if (Convert.ToInt32(_dataSnapshot.Child("SkillPoints").Value) <= 0)
		// 	{
		// 		return;
		// 	}
		// 	
		// 	await _databaseManager.UpdateUser(_databaseManager.Auth.CurrentUser, new UserData
		// 	{
		// 		SkillPoints = Convert.ToInt32(_dataSnapshot.Child("SkillPoints").Value) - 1,
		// 		SpeedPoints = Convert.ToInt32(_dataSnapshot.Child("SpeedPoints").Value) + 1
		// 	});
		// 	ReDrawLayout();
		// }
		//
		// private async void SpeedMinus(ClickEvent evt)
		// {
		// 	if (Convert.ToInt32(_dataSnapshot.Child("SpeedPoints").Value) <= 0)
		// 	{
		// 		return;
		// 	}
		// 	
		// 	await _databaseManager.UpdateUser(_databaseManager.Auth.CurrentUser, new UserData
		// 	{
		// 		SkillPoints = Convert.ToInt32(_dataSnapshot.Child("SkillPoints").Value) + 1,
		// 		SpeedPoints = Convert.ToInt32(_dataSnapshot.Child("SpeedPoints").Value) - 1
		// 	});
		// 	ReDrawLayout();
		// }
		//
		// private async void OnTraining(ClickEvent evt)
		// {
		// 	await _databaseManager.InvestInTraining(_layoutData);
		// 	ReDrawLayout();
		// }
		//
		// private void OnLogout(ClickEvent evt)
		// {
		// 	_firebaseController.FirebaseLogout();
		// 	_uiManager.ChangePage(_layoutData.LoginScreen);
		// }
		
	}
}