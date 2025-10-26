using TrainingBuddy.Managers;
using TrainingBuddy.UI.Controls;
using UnityEngine;
using UnityEngine.TextCore.Text;
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
		[SerializeField] private VisualTreeAsset _overlayAsset;
		private UniversalOverlay _universalOverlay;

		public VisualElement Header { get; private set; }
		public VisualElement Content { get; private set; }
		public VisualElement Footer { get; private set; }

		public UILayout CurrentLayout { get; private set; }
		public UniversalOverlay Overlay => _universalOverlay;

		private LayoutData _layoutData;
		private DatabaseManager _databaseManager;
		private UILayout _pendingLayout;
		private VisualElement _safeAreaContainer;
		private Rect _lastSafeArea;
		private Vector2Int _lastScreenSize;
		private ScreenOrientation _lastOrientation;
		private bool _safeAreaRegistered;
		private bool _hasStarted;

		[Inject]
		public void Construct(LayoutData layoutData, DatabaseManager databaseManager)
		{
			_layoutData = layoutData;
			_databaseManager = databaseManager;
		}
		
		private void Awake()
		{
			if (_uiDocument == null)
			{
				_uiDocument = GetComponent<UIDocument>();
			}

			if (_universalOverlay == null && !TryGetComponent(out _universalOverlay))
			{
				_universalOverlay = gameObject.AddComponent<UniversalOverlay>();
			}

			WarmupFonts();

			_universalOverlay?.Configure(_uiDocument, _overlayAsset);
		}

		public void Initialize()
		{
			_databaseManager.UIManager = this;
			
			//Instantiate Containers
			Header = _header.Instantiate();
			Content = _content.Instantiate();
			// Footer = _footer.Instantiate();

			Header.AddToClassList("layout-header");
			Content.AddToClassList("layout-content");
			// Footer.AddToClassList("layout-footer");

			SetupSafeAreaContainer();

			ChangePage(_layoutData.ProfileScreen);
		}

		private void Start()
		{
			_hasStarted = true;
			TryApplyPendingLayout();
		}

		public void ShowOverlay(string title, string message, string primaryButtonText = "OK", System.Action primaryAction = null, string secondaryButtonText = null, System.Action secondaryAction = null, bool allowBackgroundDismiss = false)
		{
			_universalOverlay?.Show(title, message, primaryButtonText, primaryAction, secondaryButtonText, secondaryAction, allowBackgroundDismiss);
		}

		public void HideOverlay()
		{
			_universalOverlay?.Hide();
		}

		public void ChangePage(UILayout layout)
		{
			if (!_hasStarted)
			{
				_pendingLayout = layout;
				return;
			}

			Content.Clear();

			if (_databaseManager.Auth == null)
			{
				Content.Add(_layoutData.LoginScreen.Layout);
				CurrentLayout = _layoutData.LoginScreen;
				AddConditionalClasses(CurrentLayout);
				return;
			}

			AddConditionalClasses(layout);
			Content.Add(Header);
			Content.Add(layout.Layout);
			CurrentLayout = layout;
			CurrentLayout.DrawLayout();
		}

		private void TryApplyPendingLayout()
		{
			if (_pendingLayout == null || !_hasStarted)
			{
				return;
			}

			var layout = _pendingLayout;
			_pendingLayout = null;
			ChangePage(layout);
		}

		private void AddConditionalClasses(UILayout layout)
		{
			ClearConditionalClasses();
			
			switch (layout)
			{
				case MainMenu:
					Header.AddToClassList("hide");
					_uiDocument.rootVisualElement.AddToClassList("show-splash-background");
					break;
				case LoginScreen:
					_uiDocument.rootVisualElement.AddToClassList("show-splash-background");
					break;
				case FindLobbyScreen:
					_uiDocument.rootVisualElement.AddToClassList("show-splash-background");
					break;
			}
		}

		private void ClearConditionalClasses()
		{
			Header.RemoveFromClassList("hide");
			_uiDocument.rootVisualElement.RemoveFromClassList("show-splash-background");
		}

		private void SetupSafeAreaContainer()
        {
            if (_uiDocument == null)
            {
                Debug.LogError("UIManager requires a UIDocument to create a safe area container.");
                return;
            }

            if (_safeAreaContainer != null)
            {
                return;
            }

            _safeAreaContainer = new VisualElement
            {
                name = "SafeAreaContainer",
            };

            _safeAreaContainer.AddToClassList("safe-area-container");
            _safeAreaContainer.style.flexGrow = 1f;
            _safeAreaContainer.style.flexShrink = 0f;
            _safeAreaContainer.style.flexDirection = FlexDirection.Column;
            _safeAreaContainer.style.justifyContent = Justify.FlexStart;
            _safeAreaContainer.style.alignItems = Align.Stretch;
            _safeAreaContainer.style.width = new Length(100f, LengthUnit.Percent);
            _safeAreaContainer.style.height = new Length(100f, LengthUnit.Percent);

            var root = _uiDocument.rootVisualElement;
            root.Add(_safeAreaContainer);

            // _safeAreaContainer.Add(Header);
            _safeAreaContainer.Add(Content);
            // _safeAreaContainer.Add(Footer);
        }

        public void UpdateStepCounter(long steps)
        {
            Header.Q<Label>("StepCounter").text = "Steps: " + steps;
        }
        
        private void WarmupFonts()
        {
	        var panelSettings = _uiDocument != null ? _uiDocument.panelSettings : null;
	        var textSettings = panelSettings != null ? panelSettings.textSettings : null;

	        if (textSettings == null)
	        {
		        return;
	        }

	        WarmupFontAsset(textSettings.defaultFontAsset);

	        var fallbackFonts = textSettings.fallbackFontAssets;
	        if (fallbackFonts == null)
	        {
		        return;
	        }

	        foreach (var fallback in fallbackFonts)
	        {
		        WarmupFontAsset(fallback);
	        }
        }

        private static void WarmupFontAsset(FontAsset fontAsset)
        {
	        if (fontAsset == null)
	        {
		        return;
	        }

	        const string WarmupCharacters = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";

	        fontAsset.TryAddCharacters(WarmupCharacters, out string _);
        }
    }
}