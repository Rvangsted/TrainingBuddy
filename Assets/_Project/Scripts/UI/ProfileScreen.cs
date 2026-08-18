using System;
using System.Collections.Generic;
using System.Threading.Tasks;
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
		private Button _logoutButton;
		private ActivityGraph _activityGraph;
		private CircularProgressBar _levelingProgressBar;
		private Label _levelingProgressValueLabel;

		private readonly FirebaseController _firebaseController;
		private DataSnapshot _dataSnapshot;
		private long _currentStepCount;
		private readonly List<ActivityGraph.DataPoint> _historyDataPoints = new();

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

			_logoutButton = Layout.Q<Button>("LogoutButton");
			_logoutButton.RegisterCallback<ClickEvent>(OnLogout);
		}

		// ── Layout drawing ────────────────────────────────────────────────────────

		public override async void DrawLayout()
		{
			base.DrawLayout();
			_uiManager.Header.Q<Label>("SiteTitle").text = "Min Profil";

			_dataSnapshot = await _databaseManager.FetchUserData(_databaseManager.Auth.CurrentUser);

			_levelingProgressBar       = Layout.Q<CircularProgressBar>("LevelingProgressBar");
			_levelingProgressValueLabel = Layout.Q<Label>("LevelingProgressValueLabel");
			_currentStepCount = Convert.ToInt64(_dataSnapshot.Child("StepCount").Value ?? 0L);
			UpdateLevelingProgress();

			long stepCurrency = Convert.ToInt64(_dataSnapshot.Child("StepCurrency").Value ?? 0L);
			Layout.Q<Label>("LevelingBottomLeftElementLabel").text = $"{stepCurrency} mønter";

			int placementPoints = Convert.ToInt32(_dataSnapshot.Child("PlacementPoints").Value ?? 0);
			Layout.Q<Label>("LevelingBottomRightElementLabel").text = $"{placementPoints} placeringspoint";

			Layout.Q<Label>("Name").text = _dataSnapshot.Child("UserName").Value.ToString();
			int dobMonth = Convert.ToInt32(_dataSnapshot.Child("DateOfBirthMonth").Value);
			string monthAbbr = new DateTime(2000, dobMonth, 1).ToString("MMM");
			Layout.Q<Label>("DateOfBirth").text = $"{_dataSnapshot.Child("DateOfBirthDay").Value} {monthAbbr} {_dataSnapshot.Child("DateOfBirthYear").Value}";
			Layout.Q<Label>("UserID").text = $"ID {_dataSnapshot.Child("FriendCode").Value}";
			Layout.Q<VisualElement>("ProfilePicture").AddToClassList("Kvinde");

			_activityGraph = Layout.Q<ActivityGraph>("WeeklyDistanceGraph");
			_activityGraph.ValueFormatter = value => value >= 1000
				? $"{value / 1000f:0.#}k skridt"
				: $"{(int)value} skridt";

			await LoadActivityGraph();

			int totalEarned = (int)(_currentStepCount / DatabaseManager.StepsPerPoint);
			int spent = Convert.ToInt32(_dataSnapshot.Child("AccelerationPoints").Value) +
			            Convert.ToInt32(_dataSnapshot.Child("SpeedPoints").Value);
			Layout.Q<Label>("LevelingPointsLabelValue").text = Mathf.Max(0, totalEarned - spent).ToString();

			int speedPoints = Convert.ToInt32(_dataSnapshot.Child("SpeedPoints").Value);
			int accelPoints = Convert.ToInt32(_dataSnapshot.Child("AccelerationPoints").Value);
			float barMax = totalEarned > 0 ? totalEarned : 1f;

			// Speed stat
			Layout.Q<Label>("SpeedStatValue").text = $"{speedPoints} point";
			Layout.Q<LinearProgressBar>("SpeedProgressBar").Value = Mathf.Clamp01(speedPoints / barMax);
			Layout.Q<Button>("SpeedStatLowerLeft").RegisterCallback<ClickEvent>(SpeedMinus);
			Layout.Q<Button>("SpeedStatLowerRight").RegisterCallback<ClickEvent>(SpeedPlus);

			// Acceleration stat
			Layout.Q<Label>("AccelerationStatValue").text = $"{accelPoints} point";
			Layout.Q<LinearProgressBar>("AccelerationProgressBar").Value = Mathf.Clamp01(accelPoints / barMax);
			Layout.Q<Button>("AccelerationStatLowerLeft").RegisterCallback<ClickEvent>(AccelerationMinus);
			Layout.Q<Button>("AccelerationStatLowerRight").RegisterCallback<ClickEvent>(AccelerationPlus);

			DrawFriendsSection();
		}

		private async void DrawFriendsSection()
		{
			var friendsContent = Layout.Q<VisualElement>("FriendsContent");
			if (friendsContent == null) return;
			friendsContent.Clear();

			var addFriendButton = new Button { text = "Tilføj ven" };
			addFriendButton.AddToClassList("add-friend-button");
			addFriendButton.RegisterCallback<ClickEvent>(_ =>
			{
				_uiManager.ShowOverlayWithInput(
					"Send anmodning",
					"Vil du sende en venneanmodning?",
					"Venne ID...",
					"Send",
					OnSearchFriend,
					"Annuller",
					null,
					UniversalOverlay.PopupImage.Friends
				);
			});
			friendsContent.Add(addFriendButton);

			var requests = await _databaseManager.FetchIncomingRequestsAsync();
			if (requests.Count > 0)
			{
				var requestsLabel = new Label { text = "Venneanmodninger" };
				requestsLabel.AddToClassList("friends-section-label");
				requestsLabel.AddToClassList("font-title");
				friendsContent.Add(requestsLabel);

				foreach (var (requester, fromUserId) in requests)
				{
					var requestCard = new VisualElement();
					requestCard.AddToClassList("friend-request-card");

					var nameLabel = new Label { text = requester.UserName ?? string.Empty };
					nameLabel.AddToClassList("friend-request-name");
					nameLabel.AddToClassList("font-title");

					var acceptButton = new Button { text = "Accepter" };
					acceptButton.AddToClassList("friend-accept-button");
					string capturedUid = fromUserId;
					acceptButton.RegisterCallback<ClickEvent>(async _ =>
					{
						await _databaseManager.HandleFriendRequestAsync(capturedUid, true);
						DrawFriendsSection();
					});

					var denyButton = new Button { text = "Afvis" };
					denyButton.AddToClassList("friend-deny-button");
					denyButton.RegisterCallback<ClickEvent>(async _ =>
					{
						await _databaseManager.HandleFriendRequestAsync(capturedUid, false);
						DrawFriendsSection();
					});

					requestCard.Add(nameLabel);
					requestCard.Add(acceptButton);
					requestCard.Add(denyButton);
					friendsContent.Add(requestCard);
				}
			}

			var friends = await _databaseManager.FetchFriendsAsync();
			if (friends.Count == 0) return;

			var friendsLabel = new Label { text = "Venner" };
			friendsLabel.AddToClassList("friends-section-label");
			friendsLabel.AddToClassList("font-title");
			friendsContent.Add(friendsLabel);

			for (var i = 0; i < friends.Count; i++)
			{
				var friend = friends[i];

				var friendElement = new VisualElement();
				friendElement.AddToClassList("friend-element");
				if ((i + 1) % 3 == 0)
					friendElement.AddToClassList("no-margin");

				var friendImage = new VisualElement();
				friendImage.AddToClassList("friend-image");

				var friendFavoriteIcon = new VisualElement();
				friendFavoriteIcon.AddToClassList("friend-favorite-icon");

				var friendNameLabel = new Label { text = friend.UserName ?? string.Empty };
				friendNameLabel.AddToClassList("friend-name-label");
				friendNameLabel.AddToClassList("font-title");

				var friendDistanceLabel = new Label { text = string.Empty };
				friendDistanceLabel.AddToClassList("friend-distance-label");
				friendDistanceLabel.AddToClassList("font-regular");

				friendElement.Add(friendImage);
				friendElement.Add(friendFavoriteIcon);
				friendElement.Add(friendNameLabel);
				friendElement.Add(friendDistanceLabel);

				friendsContent.Add(friendElement);
			}
		}

		private async Task LoadActivityGraph()
		{
			// FetchDailyStepsAsync never includes today (see its doc comment) — history only.
			// The "today" point is rebuilt separately, live, in UpdateTodayDataPoint().
			var dailySteps = await _databaseManager.FetchDailyStepsAsync(5);

			_historyDataPoints.Clear();
			foreach (var (dateKey, steps) in dailySteps)
			{
				var date = DateTime.ParseExact(dateKey, "yyyy-MM-dd", null);
				_historyDataPoints.Add(new ActivityGraph.DataPoint(FormatDanishDate(date), steps));
			}

			UpdateTodayDataPoint();
		}

		// Re-appends a freshly computed "today" point onto the cached history and re-pushes the
		// whole set to the graph — called on every StepCountChanged, not just on page load, so
		// "I dag" actually tracks the live running total instead of freezing at whatever it was
		// when the page first drew.
		private void UpdateTodayDataPoint()
		{
			if (_activityGraph == null) return;

			long todaySteps = Math.Max(0, _currentStepCount - _databaseManager.DailyStepBase);
			var dataPoints = new List<ActivityGraph.DataPoint>(_historyDataPoints)
			{
				new ActivityGraph.DataPoint("I dag", todaySteps)
			};

			_activityGraph.SetData(dataPoints, dataPoints.Count - 1);
		}

		private static string FormatDanishDate(DateTime date)
		{
			string[] months = { "jan", "feb", "mar", "apr", "maj", "jun", "jul", "aug", "sep", "okt", "nov", "dec" };
			return $"{date.Day}. {months[date.Month - 1]}";
		}

		private void UpdateLevelingProgress()
		{
			if (_levelingProgressBar == null || _levelingProgressValueLabel == null)
				return;

			long stepsIntoBlock = _currentStepCount % DatabaseManager.StepsPerPoint;
			long stepsRemaining = DatabaseManager.StepsPerPoint - stepsIntoBlock;

			_levelingProgressBar.Value      = (float)stepsIntoBlock / DatabaseManager.StepsPerPoint;
			_levelingProgressValueLabel.text = $"{stepsRemaining}\n skridt";
		}

		// ── Event handlers ────────────────────────────────────────────────────────

		private void OnStepCountChanged(long stepCount)
		{
			_currentStepCount = stepCount;
			UpdateLevelingProgress();
			UpdateTodayDataPoint();
		}

		private void OnLogout(ClickEvent evt)
		{
			_firebaseController.FirebaseLogout();
			_uiManager.ChangePage(_layoutData.WelcomeScreen);
		}

		private async void OnSearchFriend(string friendCode)
		{
			if (string.IsNullOrWhiteSpace(friendCode)) return;

			UserData? user = await _databaseManager.GetUserByFriendCodeAsync(friendCode.Trim().ToUpper());
			if (user == null)
			{
				_uiManager.ShowOverlay("Ikke fundet", "Ingen bruger med den kode.", "OK", null, UniversalOverlay.PopupImage.None);
				return;
			}

			if (user.Value.UserID == _databaseManager.Auth?.CurrentUser?.UserId)
			{
				_uiManager.ShowOverlay("Fejl", "Du kan ikke tilføje dig selv.", "OK", null, UniversalOverlay.PopupImage.None);
				return;
			}

			string targetUserId = user.Value.UserID;
			string targetName   = user.Value.UserName;

			_uiManager.ShowOverlay(
				"Send venneanmodning",
				$"Vil du sende en venneanmodning til {targetName}?",
				"Ja",
				async () => await _databaseManager.SendFriendRequestAsync(targetUserId),
				"Nej",
				null,
				UniversalOverlay.PopupImage.Friends
			);
		}

		// ── Skill points ──────────────────────────────────────────────────────────

		private int AvailableSkillPoints()
		{
			int totalEarned = (int)(_currentStepCount / DatabaseManager.StepsPerPoint);
			int spent = Convert.ToInt32(_dataSnapshot.Child("AccelerationPoints").Value) +
			            Convert.ToInt32(_dataSnapshot.Child("SpeedPoints").Value);
			return Mathf.Max(0, totalEarned - spent);
		}

		private async void AccelerationPlus(ClickEvent evt)
		{
			if (AvailableSkillPoints() <= 0) return;

			int newValue = Convert.ToInt32(_dataSnapshot.Child("AccelerationPoints").Value) + 1;
			await _databaseManager.PatchUserFields(new Dictionary<string, object> { { "AccelerationPoints", newValue } });
			ReDrawLayout();
		}

		private async void AccelerationMinus(ClickEvent evt)
		{
			if (Convert.ToInt32(_dataSnapshot.Child("AccelerationPoints").Value) <= 0) return;

			int newValue = Convert.ToInt32(_dataSnapshot.Child("AccelerationPoints").Value) - 1;
			await _databaseManager.PatchUserFields(new Dictionary<string, object> { { "AccelerationPoints", newValue } });
			ReDrawLayout();
		}

		private async void SpeedPlus(ClickEvent evt)
		{
			if (AvailableSkillPoints() <= 0) return;

			int newValue = Convert.ToInt32(_dataSnapshot.Child("SpeedPoints").Value) + 1;
			await _databaseManager.PatchUserFields(new Dictionary<string, object> { { "SpeedPoints", newValue } });
			ReDrawLayout();
		}

		private async void SpeedMinus(ClickEvent evt)
		{
			if (Convert.ToInt32(_dataSnapshot.Child("SpeedPoints").Value) <= 0) return;

			int newValue = Convert.ToInt32(_dataSnapshot.Child("SpeedPoints").Value) - 1;
			await _databaseManager.PatchUserFields(new Dictionary<string, object> { { "SpeedPoints", newValue } });
			ReDrawLayout();
		}
	}
}