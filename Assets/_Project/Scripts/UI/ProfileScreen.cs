using System;
using System.Globalization;
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
                private const float StepsPerKilometer = 1312f;
                private const float WeeklyDistanceGoalKm = 80f;
                private const int StepGoal = 10000;

                private readonly FirebaseController _firebaseController;
                private readonly DatabaseManager _databaseManager;

                private DataSnapshot _dataSnapshot;

                private Label _profileNameLabel;
                private Label _profileEmailLabel;
                private Label _skillPointsLabel;
                private Label _weeklyDistanceLabel;
                private Label _weeklyGoalLabel;
                private CircularProgressBar _weeklyDistanceProgress;
                private Label _levelLabel;
                private Label _experienceValueLabel;
                private Label _stepsValueLabel;
                private SliderInt _speedSlider;
                private SliderInt _accelerationSlider;
                private Label _speedPointsLabel;
                private Label _accelerationPointsLabel;
                private ProgressBar _experienceBar;
                private ProgressBar _stepsBar;
                private Label _experienceProgressLabel;
                private Label _stepsProgressLabel;
                private Button _trainingButton;
                private Button _logoutButton;

                private bool _uiBound;
                private bool _eventsRegistered;

                protected ProfileScreen(LayoutData layoutData, UIManager uiManager, FirebaseController firebaseController, DatabaseManager databaseManager) : base(layoutData, uiManager)
                {
                        Layout = _layoutData.ProfileScreenVisualTree.Instantiate();
                        _layoutData.ProfileScreen = this;
                        _firebaseController = firebaseController;
                        _databaseManager = databaseManager;
                }

                public override void Initialize()
                {
                        EnsureUIReferences();
                        SetStaticTexts();
                }

                public override async void DrawLayout()
                {
                        base.DrawLayout();

                        EnsureUIReferences();
                        SetStaticTexts();

                        if (!_eventsRegistered)
                        {
                                var backButton = _uiManager.Header.Q<Button>("BackButton");
                                backButton?.RegisterCallback<ClickEvent>(_ => _uiManager.ChangePage(_layoutData.MainMenu));

                                _trainingButton?.RegisterCallback<ClickEvent>(OnTraining);
                                _logoutButton?.RegisterCallback<ClickEvent>(OnLogout);

                                _eventsRegistered = true;
                        }

                        if (_databaseManager.Auth?.CurrentUser == null)
                        {
                                PopulateDefaults();
                                return;
                        }

                        try
                        {
                                _dataSnapshot = await _databaseManager.FetchUserData(_databaseManager.Auth.CurrentUser);
                        }
                        catch (Exception exception)
                        {
                                $"FetchUserData failed with {exception}".Log();
                                PopulateDefaults();
                                return;
                        }

                        if (_dataSnapshot == null || !_dataSnapshot.Exists)
                        {
                                PopulateDefaults();
                                return;
                        }

                        UpdateFromSnapshot();
                }

                private void EnsureUIReferences()
                {
                        if (_uiBound)
                        {
                                return;
                        }

                        _profileNameLabel = Layout.Q<Label>("ProfileName");
                        _profileEmailLabel = Layout.Q<Label>("ProfileEmail");
                        _skillPointsLabel = Layout.Q<Label>("SkillPointsValue");
                        _weeklyDistanceLabel = Layout.Q<Label>("WeeklyDistanceValue");
                        _weeklyGoalLabel = Layout.Q<Label>("WeeklyGoalLabel");
                        _weeklyDistanceProgress = Layout.Q<CircularProgressBar>("WeeklyDistanceProgress");
                        _levelLabel = Layout.Q<Label>("LevelValue");
                        _experienceValueLabel = Layout.Q<Label>("ExperienceValue");
                        _stepsValueLabel = Layout.Q<Label>("StepsValue");
                        _speedSlider = Layout.Q<SliderInt>("SpeedSlider");
                        _accelerationSlider = Layout.Q<SliderInt>("AccelerationSlider");
                        _speedPointsLabel = Layout.Q<Label>("SpeedPointsLabel");
                        _accelerationPointsLabel = Layout.Q<Label>("AccelerationPointsLabel");
                        _experienceBar = Layout.Q<ProgressBar>("ExperienceBar");
                        _stepsBar = Layout.Q<ProgressBar>("StepsBar");
                        _experienceProgressLabel = Layout.Q<Label>("ExperienceProgressLabel");
                        _stepsProgressLabel = Layout.Q<Label>("StepsProgressLabel");
                        _trainingButton = Layout.Q<Button>("TrainingButton");
                        _logoutButton = Layout.Q<Button>("LogoutButton");

                        if (_speedSlider != null)
                        {
                                _speedSlider.lowValue = 0;
                                _speedSlider.highValue = 20;
                                _speedSlider.SetValueWithoutNotify(0);
                                _speedSlider.focusable = false;
                                _speedSlider.pickingMode = PickingMode.Ignore;
                        }

                        if (_accelerationSlider != null)
                        {
                                _accelerationSlider.lowValue = 0;
                                _accelerationSlider.highValue = 20;
                                _accelerationSlider.SetValueWithoutNotify(0);
                                _accelerationSlider.focusable = false;
                                _accelerationSlider.pickingMode = PickingMode.Ignore;
                        }

                        _experienceBar?.SetValueWithoutNotify(0);
                        _stepsBar?.SetValueWithoutNotify(0);

                        _uiBound = true;
                }

                private void SetStaticTexts()
                {
                        if (_weeklyGoalLabel != null)
                        {
                                _weeklyGoalLabel.text = $"Mål: {WeeklyDistanceGoalKm.ToString("F0", CultureInfo.InvariantCulture)} KM";
                        }
                }

                private void PopulateDefaults()
                {
                        var culture = CultureInfo.InvariantCulture;

                        if (_profileNameLabel != null)
                        {
                                _profileNameLabel.text = "BRUGER";
                        }

                        if (_profileEmailLabel != null)
                        {
                                _profileEmailLabel.text = "Din e-mail";
                        }

                        if (_skillPointsLabel != null)
                        {
                                _skillPointsLabel.text = "0";
                        }

                        if (_weeklyDistanceLabel != null)
                        {
                                _weeklyDistanceLabel.text = "0 KM";
                        }

                        if (_weeklyDistanceProgress != null)
                        {
                                _weeklyDistanceProgress.Value = 0f;
                        }

                        if (_levelLabel != null)
                        {
                                _levelLabel.text = "1";
                        }

                        if (_experienceValueLabel != null)
                        {
                                _experienceValueLabel.text = "0";
                        }

                        if (_stepsValueLabel != null)
                        {
                                _stepsValueLabel.text = "0";
                        }

                        if (_speedSlider != null)
                        {
                                _speedSlider.highValue = 20;
                                _speedSlider.SetValueWithoutNotify(0);
                        }

                        if (_accelerationSlider != null)
                        {
                                _accelerationSlider.highValue = 20;
                                _accelerationSlider.SetValueWithoutNotify(0);
                        }

                        if (_speedPointsLabel != null)
                        {
                                _speedPointsLabel.text = "0 pt";
                        }

                        if (_accelerationPointsLabel != null)
                        {
                                _accelerationPointsLabel.text = "0 pt";
                        }

                        if (_experienceBar != null)
                        {
                                _experienceBar.title = string.Empty;
                                _experienceBar.SetValueWithoutNotify(0);
                        }

                        if (_stepsBar != null)
                        {
                                _stepsBar.title = string.Empty;
                                _stepsBar.SetValueWithoutNotify(0);
                        }

                        if (_experienceProgressLabel != null)
                        {
                                _experienceProgressLabel.text = "0 / 0 XP";
                        }

                        if (_stepsProgressLabel != null)
                        {
                                _stepsProgressLabel.text = $"0 / {StepGoal.ToString("N0", culture)} skridt";
                        }
                }

                private void UpdateFromSnapshot()
                {
                        var culture = CultureInfo.InvariantCulture;
                        var authUser = _databaseManager.Auth.CurrentUser;

                        var userName = _dataSnapshot.Child("UserName").Value?.ToString();
                        if (string.IsNullOrWhiteSpace(userName))
                        {
                                userName = authUser?.DisplayName;
                        }

                        if (string.IsNullOrWhiteSpace(userName))
                        {
                                userName = "Bruger";
                        }

                        if (_profileNameLabel != null)
                        {
                                _profileNameLabel.text = userName.ToUpperInvariant();
                        }

                        if (_profileEmailLabel != null)
                        {
                                _profileEmailLabel.text = authUser?.Email ?? string.Empty;
                        }

                        var skillPoints = GetIntValue(_dataSnapshot, "SkillPoints");
                        var speedPoints = GetIntValue(_dataSnapshot, "SpeedPoints");
                        var accelerationPoints = GetIntValue(_dataSnapshot, "AccelerationPoints");
                        var level = Math.Max(GetIntValue(_dataSnapshot, "Level", 1), 1);
                        var experience = GetIntValue(_dataSnapshot, "ExperiencePoints");
                        var stepCount = GetIntValue(_dataSnapshot, "StepCount");
                        var stepSnapshot = GetIntValue(_dataSnapshot, "StepCountSnapshot");
                        var totalSteps = Math.Max(stepCount + stepSnapshot, 0);

                        if (_skillPointsLabel != null)
                        {
                                _skillPointsLabel.text = skillPoints.ToString(culture);
                        }

                        if (_levelLabel != null)
                        {
                                _levelLabel.text = level.ToString(culture);
                        }

                        if (_experienceValueLabel != null)
                        {
                                _experienceValueLabel.text = experience.ToString("N0", culture);
                        }

                        if (_stepsValueLabel != null)
                        {
                                _stepsValueLabel.text = totalSteps.ToString("N0", culture);
                        }

                        if (_speedPointsLabel != null)
                        {
                                _speedPointsLabel.text = $"{speedPoints.ToString(culture)} pt";
                        }

                        if (_accelerationPointsLabel != null)
                        {
                                _accelerationPointsLabel.text = $"{accelerationPoints.ToString(culture)} pt";
                        }

                        var totalAvailablePoints = Math.Max(speedPoints + accelerationPoints + skillPoints, 1);

                        if (_speedSlider != null)
                        {
                                _speedSlider.highValue = totalAvailablePoints;
                                _speedSlider.SetValueWithoutNotify(Mathf.Clamp(speedPoints, _speedSlider.lowValue, totalAvailablePoints));
                        }

                        if (_accelerationSlider != null)
                        {
                                _accelerationSlider.highValue = totalAvailablePoints;
                                _accelerationSlider.SetValueWithoutNotify(Mathf.Clamp(accelerationPoints, _accelerationSlider.lowValue, totalAvailablePoints));
                        }

                        var activeSteps = Math.Max(stepCount, 0);
                        var distanceKm = activeSteps / StepsPerKilometer;

                        if (_weeklyDistanceLabel != null)
                        {
                                _weeklyDistanceLabel.text = $"{distanceKm.ToString("F0", culture)} KM";
                        }

                        if (_weeklyGoalLabel != null)
                        {
                                _weeklyGoalLabel.text = $"Mål: {WeeklyDistanceGoalKm.ToString("F0", culture)} KM";
                        }

                        if (_weeklyDistanceProgress != null)
                        {
                                _weeklyDistanceProgress.Value = Mathf.Clamp01(distanceKm / WeeklyDistanceGoalKm);
                        }

                        UpdateExperienceProgress(level, experience, culture);
                        UpdateStepProgress(activeSteps, culture);
                }

                private void UpdateExperienceProgress(int level, int experience, CultureInfo culture)
                {
                        if (_experienceBar == null || _experienceProgressLabel == null)
                        {
                                return;
                        }

                        var currentLevel = Mathf.Max(level, 1);
                        var expNeededToCurrentLevel = (currentLevel - 1) * 10000 * currentLevel / 2;
                        var expNeededToNextLevel = currentLevel * 10000 * (currentLevel + 1) / 2;
                        var maxExp = Mathf.Max(expNeededToNextLevel - expNeededToCurrentLevel, 1);
                        var currentExp = Mathf.Clamp(experience - expNeededToCurrentLevel, 0, maxExp);

                        _experienceBar.title = string.Empty;
                        _experienceBar.SetValueWithoutNotify(currentExp / (float)maxExp * 100f);
                        _experienceProgressLabel.text = $"{currentExp.ToString("N0", culture)} / {maxExp.ToString("N0", culture)} XP";
                }

                private void UpdateStepProgress(int activeSteps, CultureInfo culture)
                {
                        if (_stepsBar == null || _stepsProgressLabel == null)
                        {
                                return;
                        }

                        var normalizedProgress = Mathf.Clamp01(activeSteps / (float)StepGoal);
                        _stepsBar.title = string.Empty;
                        _stepsBar.SetValueWithoutNotify(normalizedProgress * 100f);
                        _stepsProgressLabel.text = $"{activeSteps.ToString("N0", culture)} / {StepGoal.ToString("N0", culture)} skridt";
                }

                private static int GetIntValue(DataSnapshot snapshot, string childName, int defaultValue = 0)
                {
                        var child = snapshot?.Child(childName);
                        if (child == null || child.Value == null)
                        {
                                return defaultValue;
                        }

                        try
                        {
                                return Convert.ToInt32(child.Value);
                        }
                        catch (Exception)
                        {
                                return defaultValue;
                        }
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
