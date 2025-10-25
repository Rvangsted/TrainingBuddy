using System;
using System.Collections.Generic;
using BedtimeCore;
using TrainingBuddy.FireBase;
using TrainingBuddy.Managers;
using UnityEngine.UIElements;

namespace TrainingBuddy.UI
{
	public class FindLobbyScreen : UILayout
	{
		private Button _createButton;
		
		private List<Button> _lobbyButtons = new ();
		
		protected FindLobbyScreen(LayoutData layoutData, UIManager uiManager, DatabaseManager databaseManager) : base(layoutData, uiManager, databaseManager)
		{
			ConfigureLayoutAsset(_layoutData.FindLobbyScreenVisualTree, "find-lobby-wrapper");
			_layoutData.FindLobbyScreen = this;
		}
		
		public override void Initialize()
		{
			// throw new System.NotImplementedException();
		}

		public override void DrawLayout()
		{
			base.DrawLayout();
			_uiManager.Header.Q<Button>("BackButton").RegisterCallback<ClickEvent>(_ => _uiManager.ChangePage(_layoutData.RaceMenuScreen));
			
			for (var i = 0; i < 3; i++)
			{
				_lobbyButtons.Add(new Button
				{
					name = "Test_" + i,
					text = "Test " + i
				});
				_lobbyButtons[i].AddToClassList("button-large");
				Layout.Q<VisualElement>("Container").Add(_lobbyButtons[i]);
				int localIncrement = i;
				_lobbyButtons[i].RegisterCallback<ClickEvent>(evt => JoinLobby(evt, localIncrement));
			}
			
			// _createButton = Layout.Q<Button>("JoinButton");
			// _createButton.RegisterCallback<ClickEvent>(JoinLobby);
		}
		
		private async void JoinLobby(ClickEvent evt, int btnNum)
		{
			$"BUTTON NR: {btnNum}".Log();
		}
	}
}