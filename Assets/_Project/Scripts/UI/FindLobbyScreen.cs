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

		public override async void DrawLayout()
		{
			base.DrawLayout();
			var container = Layout.Q<VisualElement>("Container");
			container?.Clear();
			_lobbyButtons.Clear();

			var lobbies = await _databaseManager.NearbyRaces(10);
			
			var listSize = lobbies.Count > 3 ? 3 : lobbies.Count;
			for (var i = 0; i < listSize; i++)
			{
				_lobbyButtons.Add(new Button
				{
					name = $"{lobbies[i].Child("title").Value}_" + i,
					text = $"{lobbies[i].Child("title").Value}",
				});
				_lobbyButtons[i].AddToClassList("button-large");
				container?.Add(_lobbyButtons[i]);
				int localIncrement = i;
				_lobbyButtons[i].RegisterCallback<ClickEvent>(async evt => await _databaseManager.SubmitJoinRequestAsync(lobbies[localIncrement].Key));
			}
			
			_uiManager.Header.Q<Label>("SiteTitle").text = "Tætteste Race";
		}
		
		private async void JoinLobby(ClickEvent evt, int btnNum)
		{
			$"BUTTON NR: {btnNum}".Log();
		}
	}
}
