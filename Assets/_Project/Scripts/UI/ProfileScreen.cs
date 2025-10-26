using System;
using System.Collections.Generic;
using BedtimeCore;
using Firebase.Database;
using TrainingBuddy.FireBase;
using TrainingBuddy.Managers;
using TrainingBuddy.UI.Controls;
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

		private DataSnapshot _dataSnapshot;

		private CircularProgressBar _levelingProgressBar;
		private Label _levelingProgressValueLabel;
		private float _stepsRequiredForNextLevel;
		private long _currentStepCount;
		
		protected ProfileScreen(LayoutData layoutData, UIManager uiManager, FirebaseController firebaseController, DatabaseManager databaseManager) : base(layoutData, uiManager, databaseManager)
		{
			ConfigureLayoutAsset(_layoutData.ProfileScreenVisualTree, "profile-wrapper");
			_layoutData.ProfileScreen = this;
			_firebaseController = firebaseController;
		}
		
		public override void Initialize()
		{
			base.Initialize();
			_databaseManager.StepCountChanged -= OnStepCountChanged;
			_databaseManager.StepCountChanged += OnStepCountChanged;
		}

		public override async void DrawLayout()
		{
			_dataSnapshot = await _databaseManager.FetchUserData(_databaseManager.Auth.CurrentUser);
			
			if (_layoutDrawn)
			{
				return;
			}
				
			Layout.Q<Label>("Name").text = _dataSnapshot.Child("UserName").Value.ToString();
			Layout.Q<Label>("DateOfBirth").text = _dataSnapshot.Child("Email").Value.ToString();
			Layout.Q<Label>("UserID").text = _dataSnapshot.Child("UserID").Value.ToString();
			Layout.Q<VisualElement>("ProfilePicture").AddToClassList(_dataSnapshot.Child("Sex").Value.ToString()); 
			
			DrawStatsSection();
			DrawActivitySection();
			DrawFriendsSection();
			
			_uiManager.Header.Q<Button>("BackButton").RegisterCallback<ClickEvent>(_ => _uiManager.ChangePage(_layoutData.MainMenu));
			_uiManager.Header.Q<Label>("SiteTitle").text = "Min Profil";
			
			base.DrawLayout();
        }
		
		private async void DrawStatsSection()
		{
			var levelingProgressContainer = Layout.Q<VisualElement>("LevelingMiddleContainer");
			levelingProgressContainer?.Clear();
			
			Layout.Q<Label>("LevelingPointsLabelValue").text = _dataSnapshot.Child("SkillPoints").Value.ToString();
			Layout.Q<Button>("LevelingPointsContainer").RegisterCallback<ClickEvent>(OnTraining);

			var levelingProgressWrapper = new VisualElement();
			levelingProgressWrapper.AddToClassList("leveling-progress-wrapper");

			_stepsRequiredForNextLevel = Convert.ToInt32(_dataSnapshot.Child("Level").Value) * 10000f;
			_currentStepCount = Convert.ToInt64(_dataSnapshot.Child("StepCount").Value);

			var circularProgressBar = new CircularProgressBar
			{
				name = "LevelingProgressBar",
				Value = _stepsRequiredForNextLevel <= 0f
					? 1f
					: Mathf.Clamp01((float)_currentStepCount / _stepsRequiredForNextLevel),
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
			_levelingProgressBar = circularProgressBar;

			var levelingProgressContent = new VisualElement();
			levelingProgressContent.AddToClassList("leveling-progress-content");

			var stepsToGo = Mathf.Max(0, Mathf.CeilToInt(_stepsRequiredForNextLevel - _currentStepCount));
			var levelingProgressValueLabel = new Label
			{
				name = "LevelingProgressValueLabel",
				text = $"{stepsToGo}\n skridt",
			};
			levelingProgressValueLabel.AddToClassList("leveling-progress-value-label");
			levelingProgressValueLabel.AddToClassList("font-title");
			_levelingProgressValueLabel = levelingProgressValueLabel;
			
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
			levelingProgressContainer?.Add(levelingProgressWrapper);

			UpdateLevelingProgress();
			
			// SpeedStatUpperMiddle
			var speedProgressWrapper = Layout.Q<VisualElement>("SpeedStatUpperMiddle");
			speedProgressWrapper?.Clear();

			var speedProgressBar = new LinearProgressBar
			{
				name = "SpeedProgressBar", 
				Value = (float)Convert.ToInt32(_dataSnapshot.Child("SpeedPoints").Value) / ((Convert.ToInt32(_dataSnapshot.Child("Level").Value) - 1) * 10),
				TrackColor = new Color(0.88f, 0.88f, 0.88f, 1f),
				ProgressColor = new Color(0.76f, 1f, 0f, 1f),
				KnobColor = new Color(0.68f, 0.94f, 0f, 1f),
				InnerKnobColor = new Color(0.88f, 0.88f, 0.88f, 1f),
				
				LineThickness = 35f,
				KnobSize = 25f,
				InnerKnobSize = 6f,
			};
			speedProgressBar.AddToClassList($"linear-progress-bar");
			speedProgressWrapper?.Add(speedProgressBar);
			Layout.Q<Label>("SpeedStatValue").text = $"{_dataSnapshot.Child("SpeedPoints").Value} point";
			Layout.Q<Button>("SpeedStatLowerLeft").RegisterCallback<ClickEvent>(SpeedMinus);
			Layout.Q<Button>("SpeedStatLowerRight").RegisterCallback<ClickEvent>(SpeedPlus);
			
			var accelerationProgressWrapper = Layout.Q<VisualElement>("AccelerationStatUpperMiddle");
			accelerationProgressWrapper?.Clear();

			var accelerationProgressBar = new LinearProgressBar
			{
				name = "AccelerationProgressBar", 
				Value = (float)Convert.ToInt32(_dataSnapshot.Child("AccelerationPoints").Value) / ((Convert.ToInt32(_dataSnapshot.Child("Level").Value) - 1) * 10),
				TrackColor = new Color(0.88f, 0.88f, 0.88f, 1f),
				ProgressColor = new Color(0.76f, 1f, 0f, 1f),
				KnobColor = new Color(0.68f, 0.94f, 0f, 1f),
				InnerKnobColor = new Color(0.88f, 0.88f, 0.88f, 1f),
				
				LineThickness = 35f,
				KnobSize = 25f,
				InnerKnobSize = 6f,
			};
			accelerationProgressBar.AddToClassList($"linear-progress-bar");
			accelerationProgressWrapper?.Add(accelerationProgressBar);
			Layout.Q<Label>("AccelerationStatValue").text = $"{_dataSnapshot.Child("AccelerationPoints").Value} point";
			Layout.Q<Button>("AccelerationStatLowerLeft").RegisterCallback<ClickEvent>(AccelerationMinus);
			Layout.Q<Button>("AccelerationStatLowerRight").RegisterCallback<ClickEvent>(AccelerationPlus);
		}

		private void OnStepCountChanged(long stepCount)
		{
			_currentStepCount = stepCount;
			UpdateLevelingProgress();
		}

		private void UpdateLevelingProgress()
		{
			if (_levelingProgressBar == null || _levelingProgressValueLabel == null)
			{
				return;
			}

			float progress = 1f;
			if (_stepsRequiredForNextLevel > 0f)
			{
				progress = Mathf.Clamp01((float)_currentStepCount / _stepsRequiredForNextLevel);
			}
			_levelingProgressBar.Value = progress;

			var stepsToGo = 0;
			if (_stepsRequiredForNextLevel > 0f)
			{
				stepsToGo = Mathf.Max(0, Mathf.CeilToInt(_stepsRequiredForNextLevel - _currentStepCount));
			}

			_levelingProgressValueLabel.text = $"{stepsToGo}\n skridt";
		}

		private void DrawFriendsSection()
		{
			var friendsContent = Layout.Q<VisualElement>("FriendsContent");
			friendsContent?.Clear();

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
			
				friendsContent?.Add(friendElement);
			}
		}

		private void DrawActivitySection()
        {
            var activityContainer = Layout.Q<VisualElement>("ActivityContainer");
            activityContainer?.Clear();
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
				SpeedPoints = Convert.ToInt32(_dataSnapshot.Child("SpeedPoints").Value) + 1,
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
				SpeedPoints = Convert.ToInt32(_dataSnapshot.Child("SpeedPoints").Value) - 1,
			});
			ReDrawLayout();
		}
		
		private async void OnTraining(ClickEvent evt)
		{
			await _databaseManager.InvestInTraining(_layoutData);
			ReDrawLayout();
		}
		
	}
}