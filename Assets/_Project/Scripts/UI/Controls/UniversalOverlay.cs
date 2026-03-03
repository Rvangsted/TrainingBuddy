using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace TrainingBuddy.UI.Controls
{
    [DisallowMultipleComponent]
    public class UniversalOverlay : MonoBehaviour
    {
        [SerializeField] private UIDocument _uiDocument;
        [SerializeField] private VisualTreeAsset _overlayAsset;

        private VisualElement _overlayRoot;
        private VisualElement _overlayBackground;
        private VisualElement _overlaySafeContent;
        private VisualElement _overlayCard;
        private VisualElement _overlayContent;
        private VisualElement _overlaySingleRow;
        private VisualElement _overlayTwoButtonRow;
        private Label _titleLabel;
        private Label _messageLabel;
        private Button _primaryButton;
        private Button _secondaryButton;

        private Action _primaryAction;
        private Action _secondaryAction;
        private bool _allowBackgroundDismiss;
        private Rect _lastSafeArea;
        private Vector2Int _lastScreenSize;
        private ScreenOrientation _lastOrientation;
        private bool _safeAreaRegistered;

        private const float OverlayPadding = 24f;

        public bool IsVisible => _overlayRoot != null && _overlayRoot.style.display.value == DisplayStyle.Flex;

        public void Configure(UIDocument uiDocument, VisualTreeAsset overlayAsset)
        {
            _uiDocument = uiDocument;
            _overlayAsset = overlayAsset;

            if (isActiveAndEnabled)
            {
                Initialize();
            }
        }

        private void Awake()
        {
            if (_uiDocument == null)
            {
                _uiDocument = GetComponent<UIDocument>();
            }

            Initialize();
        }

        private void OnEnable()
        {
            Initialize();
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

        private void OnDestroy()
        {
            if (_overlayBackground != null)
            {
                _overlayBackground.UnregisterCallback<ClickEvent>(OnBackgroundClicked);
            }

            if (_primaryButton != null)
            {
                _primaryButton.clicked -= OnPrimaryClicked;
            }

            if (_secondaryButton != null)
            {
                _secondaryButton.clicked -= OnSecondaryClicked;
            }
        }

        public void Show(string title, string message, string primaryButtonText = null, Action primaryAction = null, string secondaryButtonText = null, Action secondaryAction = null, PopupImage image = PopupImage.None , bool allowBackgroundDismiss = true)
        {
            if (_overlayAsset == null)
            {
                Debug.LogWarning("UniversalOverlay is missing a VisualTreeAsset reference.");
                return;
            }

            Initialize();

            if (_overlayRoot == null)
            {
                return;
            }

            _allowBackgroundDismiss = allowBackgroundDismiss;
            _primaryAction = primaryAction;
            _secondaryAction = secondaryAction;

            if (_titleLabel != null)
            {
                _titleLabel.text = title;
            }

            if (_messageLabel != null)
            {
                _messageLabel.text = message;
            }

            if (primaryButtonText != null && secondaryButtonText != null)
            {
	            // Show two button row
	            _overlaySingleRow.AddToClassList("hide-row");
            }

            if (primaryButtonText != null && secondaryButtonText == null)
            {
	            // Show One button Row
	            _overlayTwoButtonRow.AddToClassList("hide-row");
            }

            if (image != PopupImage.None)
            {
	            _overlayCard.AddToClassList("has-background-image");
	            
	            switch (image)
	            {
		            case PopupImage.Worry:
			            _overlayCard.AddToClassList("background-worry");
			        break;
		            case PopupImage.Friends:
			            _overlayCard.AddToClassList("background-friends");
			        break;
	            }
            }

            if (_primaryButton != null)
            {
                _primaryButton.text = primaryButtonText;
            }

            if (_secondaryButton != null)
            {
                if (string.IsNullOrEmpty(secondaryButtonText))
                {
                    _secondaryButton.style.display = DisplayStyle.None;
                }
                else
                {
                    _secondaryButton.text = secondaryButtonText;
                    _secondaryButton.style.display = DisplayStyle.Flex;
                }
            }

            _overlayRoot.style.display = DisplayStyle.Flex;
            _overlayRoot.BringToFront();
            _primaryButton?.Focus();
        }

        public void Hide()
        {
            if (_overlayRoot == null)
            {
                return;
            }

            _overlayRoot.style.display = DisplayStyle.None;
            _primaryAction = null;
            _secondaryAction = null;
            _allowBackgroundDismiss = false;
        }

        private void Initialize()
        {
            if (_overlayRoot != null)
            {
                return;
            }

            if (_overlayAsset == null || _uiDocument == null)
            {
                return;
            }

            _overlayRoot = _overlayAsset.Instantiate();
            _overlayRoot.name = "UniversalOverlay";
            _overlayRoot.style.display = DisplayStyle.None;
            _overlayRoot.pickingMode = PickingMode.Position;
            _overlayRoot.StretchToParentSize();

            _overlayBackground = _overlayRoot.Q<VisualElement>("OverlayBackground");
            _overlaySafeContent = _overlayRoot.Q<VisualElement>("OverlaySafeContent");
            _overlayCard = _overlayRoot.Q<VisualElement>("OverlayCard");
            _overlayContent = _overlayRoot.Q<VisualElement>("OverlayContent");
            _overlaySingleRow = _overlayRoot.Q<VisualElement>("OverlaySingleRow");
            _overlayTwoButtonRow = _overlayRoot.Q<VisualElement>("OverlayTwoButtonRow");

            if (_overlaySafeContent != null)
            {
                _overlaySafeContent.pickingMode = PickingMode.Ignore;
            }

            if (_overlayCard != null)
            {
                _overlayCard.pickingMode = PickingMode.Position;
            }
            
            _titleLabel = _overlayRoot.Q<Label>("OverlayTitle");
            _messageLabel = _overlayRoot.Q<Label>("OverlayMessage");
            
            _primaryButton = _overlayRoot.Q<Button>("OverlayPrimaryButton");
            _secondaryButton = _overlayRoot.Q<Button>("OverlaySecondaryButton");
            
            if (_overlayBackground != null)
            {
                _overlayBackground.pickingMode = PickingMode.Position;
                _overlayBackground.RegisterCallback<ClickEvent>(OnBackgroundClicked);
            }
            
            if (_primaryButton != null)
            {
                _primaryButton.clicked += OnPrimaryClicked;
            }
            
            if (_secondaryButton != null)
            {
                _secondaryButton.clicked += OnSecondaryClicked;
            }

            var root = _uiDocument.rootVisualElement;
            root.Add(_overlayRoot);
            _overlayRoot.BringToFront();

            RegisterSafeAreaCallbacks();
            ApplySafeAreaInsets(force: true);
        }

        private void OnBackgroundClicked(ClickEvent evt)
        {
            if (_allowBackgroundDismiss)
            {
                Hide();
            }

            evt.StopPropagation();
        }

        private void OnPrimaryClicked()
        {
            var action = _primaryAction;
            action?.Invoke();
            Hide();
        }

        private void OnSecondaryClicked()
        {
            var action = _secondaryAction;
            action?.Invoke();
            Hide();
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
            var safeContent = _overlaySafeContent ?? _overlayRoot;
            if (safeContent == null)
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

            var left = Mathf.Max(0f, safeArea.xMin * widthScale) + OverlayPadding;
            var right = Mathf.Max(0f, (screenSize.x - safeArea.xMax) * widthScale) + OverlayPadding;
            var bottom = Mathf.Max(0f, safeArea.yMin * heightScale) + OverlayPadding;
            var top = Mathf.Max(0f, (screenSize.y - safeArea.yMax) * heightScale) + OverlayPadding;

            safeContent.style.paddingLeft = left;
            safeContent.style.paddingRight = right;
            safeContent.style.paddingBottom = bottom;
            safeContent.style.paddingTop = top;

            _lastSafeArea = safeArea;
            _lastScreenSize = screenSize;
            _lastOrientation = orientation;
        }
        
        public enum PopupImage {
	        Friends,
	        Worry,
	        None,
        }
    }
}
