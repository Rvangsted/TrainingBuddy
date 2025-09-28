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
		private VisualElement _safeAreaContainer;

		private Rect _lastSafeArea = Rect.zero;
		private Vector2Int _lastScreenSize = Vector2Int.zero;
		private ScreenOrientation _lastOrientation = ScreenOrientation.AutoRotation;

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

			_safeAreaContainer = new VisualElement
			{
				name = "SafeAreaContainer",
			};
			_safeAreaContainer.AddToClassList("safe-area-container");
			_safeAreaContainer.style.flexGrow = 1f;
			_safeAreaContainer.style.flexDirection = FlexDirection.Column;
			_safeAreaContainer.style.width = new Length(100, LengthUnit.Percent);
			_safeAreaContainer.style.height = new Length(100, LengthUnit.Percent);

			// Assemble UI
			_uiDocument.rootVisualElement.Add(_safeAreaContainer);
			// _safeAreaContainer.Add(Header);
			_safeAreaContainer.Add(Content);
			// _safeAreaContainer.Add(Footer);

			ApplySafeArea();

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

		private void Update()
		{
			if (_safeAreaContainer == null)
			{
				return;
			}

			if (_lastSafeArea != Screen.safeArea ||
			    _lastScreenSize.x != Screen.width ||
			    _lastScreenSize.y != Screen.height ||
			    _lastOrientation != Screen.orientation)
			{
				ApplySafeArea();
			}
		}

		private void ApplySafeArea()
		{
			var safeArea = Screen.safeArea;
			var left = safeArea.xMin;
			var right = Screen.width - safeArea.xMax;
			var bottom = safeArea.yMin;
			var top = Screen.height - safeArea.yMax;

			_safeAreaContainer.style.paddingLeft = left;
			_safeAreaContainer.style.paddingRight = right;
			_safeAreaContainer.style.paddingBottom = bottom;
			_safeAreaContainer.style.paddingTop = top;

			_lastSafeArea = safeArea;
			_lastScreenSize = new Vector2Int(Screen.width, Screen.height);
			_lastOrientation = Screen.orientation;
		}
	}
}