#region

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using BedtimeCore;
using Firebase.Database;
using Newtonsoft.Json;
using TrainingBuddy.FireBase;
using TrainingBuddy.Managers;
using TrainingBuddy.UI.Controls;
using UnityEngine;
using UnityEngine.UIElements;

#endregion

namespace TrainingBuddy.UI
{
	public class ProfileScreen : UILayout
	{
		private const int ExperiencePerLevel = 1000;
		private const int StatPointProgressMax = 100;
		private const int pointsPerLevel = 10;
		private readonly DatabaseManager _databaseManager;

		private readonly FirebaseController _firebaseController;
		private LinearProgressBar _accelerationProgressBar;
		private Label _accelerationStatValueLabel;
		private Button _accMinusButton;
		private Button _accPlusButton;

		private ActivityGraph _activityGraph;

		private DataSnapshot _dataSnapshot;
		private Label _dateOfBirthLabel;

		private bool _eventsRegistered;
		private Label _levelingBottomLeftLabel;
		private Label _levelingBottomRightLabel;
		private Label _levelingPointsLabel;
		private Label _levelingProgressValueLabel;

		private CircularProgressBar _levelProgressBar;
		private Button _logoutButton;

		private Label _nameLabel;
		private Button _spdMinusButton;
		private Button _spdPlusButton;
		private LinearProgressBar _speedProgressBar;
		private Label _speedStatValueLabel;
		private Button _trainingButton;
		private Label _userIdLabel;

		protected ProfileScreen(LayoutData layoutData, UIManager uiManager, FirebaseController firebaseController, DatabaseManager databaseManager) : base(layoutData, uiManager, layoutData.ProfileScreenVisualTree)
		{
			_layoutData.ProfileScreen = this;
			_firebaseController = firebaseController;
			_databaseManager = databaseManager;
		}

		protected override void OnLayoutBuilt(VisualElement root)
		{
			root.AddToClassList("profile-wrapper");
		}

		public override void Initialize()
		{
			base.Initialize();
			CacheProfileElements();
			InitializeStatsSection();
			InitializeActivitySection();
			InitializeFriendsSection();
		}

		public override async void DrawLayout()
		{
			base.DrawLayout();

			if (!_eventsRegistered)
			{
				_uiManager.Header.Q<Button>("BackButton")
				          .RegisterCallback<ClickEvent>(_ => _uiManager.ChangePage(_layoutData.MainMenu));
				_logoutButton?.RegisterCallback<ClickEvent>(OnLogout);
				_eventsRegistered = true;
			}

			await PopulateProfileAsync();
		}

		private void CacheProfileElements()
		{
			_nameLabel = Layout.Q<Label>("Name");
			_userIdLabel = Layout.Q<Label>("UserID");
			_dateOfBirthLabel = Layout.Q<Label>("DateOfBirth");
			_levelingPointsLabel = Layout.Q<Label>("LevelingPointsLabel");
			_levelingBottomLeftLabel = Layout.Q<Label>("LevelingBottomLeftElementLabel");
			_levelingBottomRightLabel = Layout.Q<Label>("LevelingBottomRightElementLabel");
			_speedStatValueLabel = Layout.Q<Label>("SpeedStatValue");
			_accelerationStatValueLabel = Layout.Q<Label>("AccelerationStatValue");
			_logoutButton = Layout.Q<Button>("LogoutButton");
		}

		private void InitializeStatsSection()
		{
			var levelingProgressWrapper = new VisualElement();
			levelingProgressWrapper.AddToClassList("leveling-progress-wrapper");

			_levelProgressBar = new CircularProgressBar
			{
				name = "LevelingProgressBar",
				Value = 0.7f,
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
			_levelProgressBar.AddToClassList("leveling-progress-bar");

			var levelingProgressContent = new VisualElement();
			levelingProgressContent.AddToClassList("leveling-progress-content");

			_levelingProgressValueLabel = new Label { name = "LevelingProgressValueLabel", text = "64 KM", };
			_levelingProgressValueLabel.AddToClassList("leveling-progress-value-label");
			_levelingProgressValueLabel.AddToClassList("font-title");

			var levelingProgressSubtitleLabel = new Label { name = "LevelingProgressSubtitleLabel", text = "Til næste niveau", };
			levelingProgressSubtitleLabel.AddToClassList("leveling-progress-subtitle-label");
			levelingProgressSubtitleLabel.AddToClassList("font-regular");

			levelingProgressContent.Add(_levelingProgressValueLabel);
			levelingProgressContent.Add(levelingProgressSubtitleLabel);

			levelingProgressWrapper.Add(_levelProgressBar);
			levelingProgressWrapper.Add(levelingProgressContent);
			Layout.Q<VisualElement>("LevelingMiddleContainer")
			      .Add(levelingProgressWrapper);

			// SpeedStatUpperMiddle
			_speedProgressBar = new LinearProgressBar
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
			_speedProgressBar.AddToClassList("linear-progress-bar");
			Layout.Q<VisualElement>("SpeedStatUpperMiddle")
			      .Add(_speedProgressBar);

			_accelerationProgressBar = new LinearProgressBar
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
			_accelerationProgressBar.AddToClassList("linear-progress-bar");
			Layout.Q<VisualElement>("AccelerationStatUpperMiddle")
			      .Add(_accelerationProgressBar);
			Layout.Q<VisualElement>("LevelingMiddleContainer")
			      .Add(levelingProgressWrapper);
		}

		private void InitializeFriendsSection()
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

				var friendNameLabel = new Label { name = "FriendName", text = "Name", };
				friendNameLabel.AddToClassList("friend-name-label");
				friendNameLabel.AddToClassList("font-title");

				var friendDistanceLabel = new Label { name = "FriendDistance", text = "50 km", };
				friendDistanceLabel.AddToClassList("friend-distance-label");
				friendDistanceLabel.AddToClassList("font-regular");

				friendElement.Add(friendImage);
				friendElement.Add(friendFavoriteIcon);
				friendElement.Add(friendNameLabel);
				friendElement.Add(friendDistanceLabel);

				Layout.Q<VisualElement>("FriendsContent")
				      .Add(friendElement);
			}
		}

		private void InitializeActivitySection()
		{
			var activityContainer = Layout.Q<VisualElement>("ActivityContainer");
			if (activityContainer == null)
			{
				return;
			}

			_activityGraph = new ActivityGraph { name = "WeeklyDistanceGraph", ValueFormatter = value => $"{value:0.#} KM", };

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
			_activityGraph?.SetData(dataPoints, highlightIndex);
		}

		private async Task PopulateProfileAsync()
		{
			if (_databaseManager?.Auth?.CurrentUser == null)
			{
				return;
			}

			try
			{
				var snapshot = await _databaseManager.FetchUserData(_databaseManager.Auth.CurrentUser);
				if (snapshot == null || !snapshot.Exists)
				{
					"No user data found for profile screen.".Log();
					return;
				}

				var userData = JsonConvert.DeserializeObject<UserData>(snapshot.GetRawJsonValue(), _databaseManager.JsonSettings);
				UpdateProfileHeader(userData);
				UpdateStats(userData);

				_dataSnapshot = snapshot;
			}
			catch (Exception exception)
			{
				$"Failed to populate profile screen. {exception}".Log();
			}
		}

		private void UpdateProfileHeader(UserData userData)
		{
			if (_nameLabel != null && !string.IsNullOrWhiteSpace(userData.UserName))
			{
				_nameLabel.text = userData.UserName;
			}

			if (_userIdLabel != null && !string.IsNullOrWhiteSpace(userData.UserID))
			{
				_userIdLabel.text = $"ID #{userData.UserID}";
			}

			if (_dateOfBirthLabel != null && !string.IsNullOrWhiteSpace(userData.Email))
			{
				_dateOfBirthLabel.text = userData.Email;
			}
		}

		private void UpdateStats(UserData userData)
		{
			var level = userData.Level ?? 1;
			var experience = userData.ExperiencePoints ?? 0;
			var skillPoints = userData.SkillPoints ?? 0;
			var accelerationPoints = userData.AccelerationPoints ?? 0;
			var speedPoints = userData.SpeedPoints ?? 0;
			var steps = userData.StepCount ?? 0;

			if (_levelProgressBar != null)
			{
				_levelProgressBar.Value = CalculateProgress(experience, ExperiencePerLevel);
				$"{_levelProgressBar.Value}".Log();
			}

			if (_levelingProgressValueLabel != null)
			{
				_levelingProgressValueLabel.text = $"Lvl {level}";
			}

			if (_levelingPointsLabel != null)
			{
				_levelingPointsLabel.text = $"{skillPoints} point";
			}

			if (_levelingBottomLeftLabel != null)
			{
				_levelingBottomLeftLabel.text = $"{steps} skridt";
			}

			if (_levelingBottomRightLabel != null)
			{
				_levelingBottomRightLabel.text = $"{experience} XP";
			}

			if (_speedProgressBar != null)
			{
				_speedProgressBar.Value = CalculateProgress(speedPoints, StatPointProgressMax);
			}

			if (_speedStatValueLabel != null)
			{
				_speedStatValueLabel.text = $"{speedPoints} point";
			}

			if (_accelerationProgressBar != null)
			{
				_accelerationProgressBar.Value = CalculateProgress(accelerationPoints, StatPointProgressMax);
			}

			if (_accelerationStatValueLabel != null)
			{
				_accelerationStatValueLabel.text = $"{accelerationPoints} point";
			}
		}

		private static float CalculateProgress(int value, float max)
		{
			if (max <= 0f)
			{
				return 0f;
			}

			return Mathf.Clamp01(value / max);
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