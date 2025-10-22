

using UnityEngine.UIElements;

namespace TrainingBuddy.UI
{
	public class HighScoreScreen : UILayout
	{
		protected HighScoreScreen(LayoutData layoutData, UIManager uiManager) : base(layoutData, uiManager, layoutData.HighScoreVisualTree)
		{
			_layoutData.HighScoreScreen = this;
		}

		protected override void OnLayoutBuilt(VisualElement root)
		{
			root.AddToClassList("highscore-wrapper");
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