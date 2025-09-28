using UnityEngine.UIElements;

namespace TrainingBuddy.UI
{
	public class RaceScreen : UILayout
	{
		protected RaceScreen(LayoutData layoutData, UIManager uiManager) : base(layoutData, uiManager)
		{
			Layout = _layoutData.RaceScreenVisualTree.Instantiate();
			Layout.AddToClassList("race-wrapper");
			_layoutData.RaceScreen = this;
		}
		
		public override void Initialize()
		{
			// throw new System.NotImplementedException();
		}

		public override void DrawLayout()
		{
			base.DrawLayout();
			_uiManager.Header.Q<Button>("BackButton").RegisterCallback<ClickEvent>(_ => _uiManager.ChangePage(_layoutData.RaceMenuScreen));
		}
	}
}