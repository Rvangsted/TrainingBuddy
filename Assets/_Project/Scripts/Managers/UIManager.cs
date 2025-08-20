using TrainingBuddy.Managers;
using UnityEngine;
using UnityEngine.UIElements;
using VContainer;
using VContainer.Unity;

namespace TrainingBuddy.UI
{
	public class UIManager : MonoBehaviour, IInitializable
	{
		[SerializeField] private UIDocument _uiDocument;
		[SerializeField] private VisualTreeAsset _header;
		[SerializeField] private VisualTreeAsset _content;
		[SerializeField] private VisualTreeAsset _footer;
		
		public VisualElement Header { get; private set; }
		public VisualElement Content { get; private set; }
		public VisualElement Footer { get; private set; }
		
		public UILayout CurrentLayout { get; private set; }

		private LayoutData _layoutData;
		private DatabaseManager _databaseManager;

		[Inject]
		public void Construct(LayoutData layoutData, DatabaseManager databaseManager)
		{
			_layoutData = layoutData;
			_databaseManager = databaseManager;
		}
		
		public void Initialize()
		{
			_databaseManager.UIManager = this;
			
			//Instantiate Containers
			Header = _header.Instantiate();
			Content = _content.Instantiate();
			Footer = _footer.Instantiate();

			Header.AddToClassList("layout-header");
			Content.AddToClassList("layout-content");
			Footer.AddToClassList("layout-footer");
			
			// Assemble UI
			_uiDocument.rootVisualElement.Add(Header);
			_uiDocument.rootVisualElement.Add(Content);
			_uiDocument.rootVisualElement.Add(Footer);

			ChangePage(_layoutData.ProfileScreen);
		}

		public void ChangePage(UILayout layout)
		{
			Content.Clear();

			if (_databaseManager.Auth == null)
			{
				Content.Add(_layoutData.LoginScreen.Layout);
				CurrentLayout = _layoutData.LoginScreen;
				AddConditionalClasses(CurrentLayout);
				return;
			}

			AddConditionalClasses(layout);
			Content.Add(layout.Layout);
			CurrentLayout = layout;
			CurrentLayout.DrawLayout();
		}

		private void AddConditionalClasses(UILayout layout)
		{
			ClearConditionalClasses();
			
			switch (layout)
			{
				case MainMenu:
					Header.AddToClassList("hide-back-button");
					_uiDocument.rootVisualElement.AddToClassList("show-splash-background");
					break;
				case LoginScreen:
					_uiDocument.rootVisualElement.AddToClassList("show-splash-background");
					break;
			}
		}

		private void ClearConditionalClasses()
		{
			Header.RemoveFromClassList("hide-back-button");
			_uiDocument.rootVisualElement.RemoveFromClassList("show-splash-background");
		}

		public void UpdateStepCounter(long steps)
		{
			Header.Q<Label>("StepCounter").text = "Steps: " + steps;
		}
	}
}