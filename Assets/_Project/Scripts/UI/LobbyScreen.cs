using UnityEngine.UIElements;

namespace TrainingBuddy.UI
{
	public class LobbyScreen : UILayout
	{
		protected LobbyScreen(LayoutData layoutData, UIManager uiManager) : base(layoutData, uiManager, layoutData.LobbyScreenVisualTree)
		{
			_layoutData.LobbyScreen = this;
		}

		protected override void OnLayoutBuilt(VisualElement root)
		{
			root.AddToClassList("lobby-wrapper");
		}

		public override void Initialize()
		{
			base.Initialize();
		}

		public override void DrawLayout()
		{
			base.DrawLayout();
			_uiManager.Header.Q<Button>("BackButton").RegisterCallback<ClickEvent>(_ => _uiManager.ChangePage(_layoutData.MainMenu));
		}
	}
}