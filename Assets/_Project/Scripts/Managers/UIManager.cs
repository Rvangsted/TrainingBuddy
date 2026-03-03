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

		private System.Action _backAction;

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

			InitializeBackButton();

			Header.AddToClassList("layout-header");
			Content.AddToClassList("layout-content");
			// Footer.AddToClassList("layout-footer");

			SetupSafeAreaContainer();

			ChangePage(_layoutData.ProfileScreen);
		}

		private void Start()
		{
			_hasStarted = true;
			ApplySafeAreaInsets(force: true);
			TryApplyPendingLayout();
		}

		private void OnEnable()
		{
			RegisterSafeAreaCallbacks();
			ApplySafeAreaInsets(force: true);
		}

		private void OnDisable()
		{
			UnregisterSafeAreaCallbacks();
		}

		private void Update()
		{
			ApplySafeAreaInsets();
		}
		
		public void ShowOverlay(string title, string message, string primaryButtonText, System.Action primaryAction, UniversalOverlay.PopupImage image = UniversalOverlay.PopupImage.None, bool allowBackgroundDismiss = true)
		{
			_universalOverlay?.Show(title, message, primaryButtonText, primaryAction, null, null, image, allowBackgroundDismiss);
		}

		public void ShowOverlay(string title, string message, string primaryButtonText, System.Action primaryAction, string secondaryButtonText, System.Action secondaryAction, UniversalOverlay.PopupImage image = UniversalOverlay.PopupImage.None, bool allowBackgroundDismiss = true)
		{
			_universalOverlay?.Show(title, message, primaryButtonText, primaryAction, secondaryButtonText, secondaryAction, image, allowBackgroundDismiss);
		}
		
		public void HideOverlay()
		{
			_universalOverlay?.Hide();
		}

		private void InitializeBackButton()
		{
			var backButton = Header.Q<Button>("BackButton");
			backButton.clicked += () => _backAction?.Invoke();
		}

		public void SetBackAction(System.Action action)
		{
			_backAction = action;
		}

		public void ChangePage(UILayout layout)
		{
			if (!_hasStarted)
			{
				_pendingLayout = layout;
				return;
			}

			Content.Clear();

			if (_databaseManager.Auth == null && !IsGuestAllowedLayout(layout))
			{
				Content.Add(_layoutData.WelcomeScreen.Layout);
				CurrentLayout = _layoutData.WelcomeScreen;
				ApplyDefaultBackAction(CurrentLayout);
				AddConditionalClasses(CurrentLayout);
				return;
			}

			AddConditionalClasses(layout);
			Content.Add(Header);
			Content.Add(layout.Layout);
			CurrentLayout = layout;
			ApplyDefaultBackAction(CurrentLayout);
			CurrentLayout.DrawLayout();
		}

		private static bool IsGuestAllowedLayout(UILayout layout) =>
			layout is WelcomeScreen or RegisterScreen or LoginScreen or ForgotPasswordScreen or ResetPasswordScreen;

		private void ApplyDefaultBackAction(UILayout layout)
		{
			_backAction = layout switch
			{
				LoginScreen => () => ChangePage(_layoutData.WelcomeScreen),
				RegisterScreen => () => ChangePage(_layoutData.WelcomeScreen),
				ForgotPasswordScreen => () => ChangePage(_layoutData.LoginScreen),
				ResetPasswordScreen => () => ChangePage(_layoutData.WelcomeScreen),
				ProfileScreen => () => ChangePage(_layoutData.MainMenu),
				FindLobbyScreen => () => ChangePage(_layoutData.MainMenu),
				HighScoreScreen => () => ChangePage(_layoutData.MainMenu),
				LobbyScreen => () => ChangePage(_layoutData.MainMenu),
				_ => null,
			};
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
				case WelcomeScreen:
					Header.AddToClassList("hide");
					break;
				case RegisterScreen:
					Header.AddToClassList("simple");
					_uiDocument.rootVisualElement.AddToClassList("show-emma-background");
					_uiDocument.rootVisualElement.AddToClassList("show-bottom-container");
					break;
				case LoginScreen:
					Header.AddToClassList("simple");
					_uiDocument.rootVisualElement.AddToClassList("show-emma-background");
					_uiDocument.rootVisualElement.AddToClassList("show-bottom-container");
					break;
				case ForgotPasswordScreen:
					Header.AddToClassList("simple");
					_uiDocument.rootVisualElement.AddToClassList("show-emma-background");
					_uiDocument.rootVisualElement.AddToClassList("show-bottom-container");
					break;
				case ResetPasswordScreen:
					Header.AddToClassList("simple");
					_uiDocument.rootVisualElement.AddToClassList("show-emma-background");
					_uiDocument.rootVisualElement.AddToClassList("show-bottom-container");
					break;
				case FindLobbyScreen:
					_uiDocument.rootVisualElement.AddToClassList("show-splash-background");
					break;
			}
		}

		private void ClearConditionalClasses()
		{
			Header.RemoveFromClassList("hide");
			Header.RemoveFromClassList("simple");
			_uiDocument.rootVisualElement.RemoveFromClassList("show-emma-background");
			_uiDocument.rootVisualElement.RemoveFromClassList("show-splash-background");
			_uiDocument.rootVisualElement.RemoveFromClassList("show-bottom-container");
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
			RegisterSafeAreaCallbacks();
			ApplySafeAreaInsets(force: true);

            // _safeAreaContainer.Add(Header);
            _safeAreaContainer.Add(Content);
            // _safeAreaContainer.Add(Footer);
        }

		private void RegisterSafeAreaCallbacks()
		{
			if (_safeAreaRegistered || _uiDocument == null)
			{
				return;
			}

			var root = _uiDocument.rootVisualElement;
			if (root == null)
			{
				return;
			}

			root.RegisterCallback<GeometryChangedEvent>(OnRootGeometryChanged);
			_safeAreaRegistered = true;
		}

		private void UnregisterSafeAreaCallbacks()
		{
			if (!_safeAreaRegistered || _uiDocument == null)
			{
				return;
			}

			var root = _uiDocument.rootVisualElement;
			if (root == null)
			{
				return;
			}

			root.UnregisterCallback<GeometryChangedEvent>(OnRootGeometryChanged);
			_safeAreaRegistered = false;
		}

		private void OnRootGeometryChanged(GeometryChangedEvent evt)
		{
			ApplySafeAreaInsets(force: true);
		}

		private void ApplySafeAreaInsets(bool force = false)
		{
			if (_safeAreaContainer == null)
			{
				return;
			}

			var safeArea = Screen.safeArea;
			var screenSize = new Vector2Int(Screen.width, Screen.height);
			var orientation = Screen.orientation;

			if (!force &&
			    safeArea == _lastSafeArea &&
			    screenSize == _lastScreenSize &&
			    orientation == _lastOrientation)
			{
				return;
			}

			var root = _uiDocument != null ? _uiDocument.rootVisualElement : null;
			var panelWidth = root != null ? root.resolvedStyle.width : 0f;
			var panelHeight = root != null ? root.resolvedStyle.height : 0f;

			var widthScale = (panelWidth > 0f && screenSize.x > 0) ? panelWidth / screenSize.x : 1f;
			var heightScale = (panelHeight > 0f && screenSize.y > 0) ? panelHeight / screenSize.y : 1f;

			var left = Mathf.Max(0f, safeArea.xMin * widthScale);
			var right = Mathf.Max(0f, (screenSize.x - safeArea.xMax) * widthScale);
			var bottom = Mathf.Max(0f, safeArea.yMin * heightScale);
			var top = Mathf.Max(0f, (screenSize.y - safeArea.yMax) * heightScale);

			_safeAreaContainer.style.paddingLeft = left;
			_safeAreaContainer.style.paddingRight = right;
			_safeAreaContainer.style.paddingBottom = bottom;
			_safeAreaContainer.style.paddingTop = top;

			_lastSafeArea = safeArea;
			_lastScreenSize = screenSize;
			_lastOrientation = orientation;
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
