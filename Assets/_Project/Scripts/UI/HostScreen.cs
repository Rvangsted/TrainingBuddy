using System;
using TrainingBuddy.FireBase;
using TrainingBuddy.Managers;
using UnityEngine.UIElements;

namespace TrainingBuddy.UI
{
	public class HostScreen : UILayout
	{
		private Button _createButton;
		
		private readonly DatabaseManager _databaseManager;
		
		protected HostScreen(LayoutData layoutData, UIManager uiManager, DatabaseManager databaseManager) : base(layoutData, uiManager)
		{
			ConfigureLayoutAsset(_layoutData.HostScreenVisualTree, "host-wrapper");
			_layoutData.HostScreen = this;
			_databaseManager = databaseManager;
		}
		
		public override void Initialize()
		{
			// throw new System.NotImplementedException();
		}

		public override void DrawLayout()
		{
			base.DrawLayout();
			_uiManager.Header.Q<Button>("BackButton").RegisterCallback<ClickEvent>(_ => _uiManager.ChangePage(_layoutData.RaceMenuScreen));
			
			_createButton = Layout.Q<Button>("CreateButton");
			_createButton.RegisterCallback<ClickEvent>(CreateLobby);
		}
		
		private async void CreateLobby(ClickEvent evt)
		{
			await _databaseManager.CreateLobby(new RaceData
			{
				RaceName = Layout.Q<TextField>("RaceName").value,
				HostName = _databaseManager.Auth.CurrentUser.DisplayName,
				HostID = _databaseManager.Auth.CurrentUser.UserId,
				Longitude = 0,
				Latitude = 0,
				Status = 0,
				Timestamp = DateTime.Now.ToString("dd-MM-yyyy HH:mm:ss")
			});
			// ReDrawLayout();
		}
	}
}