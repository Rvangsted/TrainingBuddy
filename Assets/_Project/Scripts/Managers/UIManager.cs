using TrainingBuddy.Managers;
using TrainingBuddy.UI.Controls;
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
		[SerializeField] private VisualTreeAsset _overlayAsset;
		private UniversalOverlay _universalOverlay;

		public VisualElement Header { get; private set; }
		public VisualElement Content { get; private set; }
		public VisualElement Footer { get; private set; }

		public UILayout CurrentLayout { get; private set; }
		public UniversalOverlay Overlay => _universalOverlay;

		private LayoutData _layoutData;
		private DatabaseManager _databaseManager;
		private VisualElement _safeAreaContainer;
		private Rect _lastSafeArea;
		private Vector2Int _lastScreenSize;
		private ScreenOrientation _lastOrientation;
		private bool _safeAreaRegistered;

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
			Content.Clear();

			if (_databaseManager.Auth == null)
			{
				_layoutData.LoginScreen.EnsureLayoutBuilt();
				Content.Add(_layoutData.LoginScreen.Layout);
				CurrentLayout = _layoutData.LoginScreen;
				AddConditionalClasses(CurrentLayout);
				ApplySafeArea(true);
				return;
			}

			AddConditionalClasses(layout);
			Content.Add(Header);
			layout.EnsureLayoutBuilt();
			Content.Add(layout.Layout);
			CurrentLayout = layout;
			CurrentLayout.DrawLayout();
			ApplySafeArea(true);
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

            if (!_safeAreaRegistered)
            {
                root.RegisterCallback<GeometryChangedEvent>(OnRootGeometryChanged);
                _safeAreaRegistered = true;
            }

            ApplySafeArea(true);
        }

        private void OnRootGeometryChanged(GeometryChangedEvent evt)
        {
            ApplySafeArea(true);
        }

        private void Update()
        {
            if (_safeAreaContainer == null)
            {
                return;
            }

            if (HasScreenChanged())
            {
                ApplySafeArea();
            }
        }

        private bool HasScreenChanged()
        {
            var currentSafeArea = CalculateEffectiveSafeArea();
            var currentResolution = new Vector2Int(Screen.width, Screen.height);
            var currentOrientation = Screen.orientation;

            return currentSafeArea != _lastSafeArea ||
                   currentResolution != _lastScreenSize ||
                   currentOrientation != _lastOrientation;
        }

        private void ApplySafeArea(bool force = false)
        {
            if (_safeAreaContainer == null)
            {
                return;
            }

            var safeArea = CalculateEffectiveSafeArea();
            var currentResolution = new Vector2Int(Screen.width, Screen.height);
            var currentOrientation = Screen.orientation;

            if (!force && safeArea == _lastSafeArea && currentResolution == _lastScreenSize && currentOrientation == _lastOrientation)
            {
                return;
            }

            _lastSafeArea = safeArea;
            _lastScreenSize = currentResolution;
            _lastOrientation = currentOrientation;

            var rootSize = GetRootDimensions();
            var scaleX = rootSize.x <= 0f ? 1f : rootSize.x / Screen.width;
            var scaleY = rootSize.y <= 0f ? 1f : rootSize.y / Screen.height;

            _safeAreaContainer.style.paddingLeft = Mathf.Max(0f, safeArea.xMin * scaleX);
            _safeAreaContainer.style.paddingRight = Mathf.Max(0f, (Screen.width - safeArea.xMax) * scaleX);
            _safeAreaContainer.style.paddingBottom = Mathf.Max(0f, safeArea.yMin * scaleY);
            _safeAreaContainer.style.paddingTop = Mathf.Max(0f, (Screen.height - safeArea.yMax) * scaleY);
        }

        private Vector2 GetRootDimensions()
        {
            if (_uiDocument == null)
            {
                return new Vector2(Screen.width, Screen.height);
            }

            var root = _uiDocument.rootVisualElement;
            var width = root.resolvedStyle.width;
            var height = root.resolvedStyle.height;

            if (float.IsNaN(width) || Mathf.Approximately(width, 0f))
            {
                width = root.worldBound.width;
            }

            if (float.IsNaN(height) || Mathf.Approximately(height, 0f))
            {
                height = root.worldBound.height;
            }

            if (Mathf.Approximately(width, 0f))
            {
                width = Screen.width;
            }

            if (Mathf.Approximately(height, 0f))
            {
                height = Screen.height;
            }

            return new Vector2(width, height);
        }

        private void OnDestroy()
        {
            if (_uiDocument != null && _safeAreaRegistered)
            {
	            if (_uiDocument.rootVisualElement != null)
	            {
					_uiDocument.rootVisualElement.UnregisterCallback<GeometryChangedEvent>(OnRootGeometryChanged);
	            }
                _safeAreaRegistered = false;
            }
        }

        public void UpdateStepCounter(long steps)
        {
            Header.Q<Label>("StepCounter").text = "Steps: " + steps;
        }

        private Rect CalculateEffectiveSafeArea()
        {
            var safeArea = Screen.safeArea;

            if (safeArea.width <= 0f || safeArea.height <= 0f)
            {
                return new Rect(0f, 0f, Screen.width, Screen.height);
            }

            var cutouts = Screen.cutouts;
            if (cutouts != null && cutouts.Length > 0)
            {
                const float edgeTolerance = 1f;

                for (var i = 0; i < cutouts.Length; i++)
                {
                    var cutout = cutouts[i];

                    if (cutout.width <= 0f || cutout.height <= 0f)
                    {
                        continue;
                    }

                    // Android devices can expose multiple notches/punch holes via Screen.cutouts.
                    // Shrink the safe area further when a cutout touches a screen edge so that
                    // content never overlaps device specific hardware features.
                    if (cutout.x <= edgeTolerance)
                    {
                        safeArea.xMin = Mathf.Max(safeArea.xMin, cutout.xMax);
                    }

                    if (cutout.y <= edgeTolerance)
                    {
                        safeArea.yMin = Mathf.Max(safeArea.yMin, cutout.yMax);
                    }

                    if (cutout.xMax >= Screen.width - edgeTolerance)
                    {
                        safeArea.xMax = Mathf.Min(safeArea.xMax, cutout.xMin);
                    }

                    if (cutout.yMax >= Screen.height - edgeTolerance)
                    {
                        safeArea.yMax = Mathf.Min(safeArea.yMax, cutout.yMin);
                    }
                }
            }

            safeArea.xMin = Mathf.Clamp(safeArea.xMin, 0f, Screen.width);
            safeArea.yMin = Mathf.Clamp(safeArea.yMin, 0f, Screen.height);
            safeArea.xMax = Mathf.Clamp(safeArea.xMax, 0f, Screen.width);
            safeArea.yMax = Mathf.Clamp(safeArea.yMax, 0f, Screen.height);

            if (safeArea.xMax < safeArea.xMin || safeArea.yMax < safeArea.yMin)
            {
                return new Rect(0f, 0f, Screen.width, Screen.height);
            }

            return safeArea;
        }
    }
}