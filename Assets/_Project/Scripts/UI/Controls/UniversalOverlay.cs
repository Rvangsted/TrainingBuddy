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
        private VisualElement _overlaySingleRow;
        private VisualElement _overlayTwoButtonRow;
        private VisualElement _buttonSpace;
        private Label _titleLabel;
        private Label _messageLabel;
        private TextField _inputField;
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

        public void ShowWithInput(string title, string message, string placeholder, string primaryButtonText, Action<string> primaryAction, string secondaryButtonText = null, Action secondaryAction = null, PopupImage image = PopupImage.None, bool allowBackgroundDismiss = true)
        {
            Show(title, message, primaryButtonText, () => primaryAction?.Invoke(_inputField?.value ?? string.Empty), secondaryButtonText, secondaryAction, image, allowBackgroundDismiss);

            if (_inputField != null)
            {
                _inputField.value = string.Empty;
                _inputField.textEdition.placeholder = placeholder ?? string.Empty;
                _inputField.style.display = DisplayStyle.Flex;
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

            // Hide input field — only ShowWithInput makes it visible
            if (_inputField != null)
            {
                _inputField.value = string.Empty;
                _inputField.style.display = DisplayStyle.None;
            }

            // Reset image classes from any previous call
            _overlayCard?.RemoveFromClassList("has-background-image");
            _overlayCard?.RemoveFromClassList("background-worry");
            _overlayCard?.RemoveFromClassList("background-friends");

            // OverlaySingleRow has no active button in the UXML — always use the two-button row.
            // The secondary button is hidden below when secondaryButtonText is null/empty.
            _overlaySingleRow?.AddToClassList("hide-row");
            _overlayTwoButtonRow?.RemoveFromClassList("hide-row");

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

            bool hasSecondary = !string.IsNullOrEmpty(secondaryButtonText);
            if (_secondaryButton != null)
            {
                if (hasSecondary)
                {
                    _secondaryButton.text = secondaryButtonText;
                    _secondaryButton.style.display = DisplayStyle.Flex;
                }
                else
                {
                    _secondaryButton.style.display = DisplayStyle.None;
                }
            }

            if (_buttonSpace != null)
                _buttonSpace.style.display = hasSecondary ? DisplayStyle.Flex : DisplayStyle.None;

            if (_primaryButton != null)
            {
                if (hasSecondary)
                    _primaryButton.RemoveFromClassList("overlay-button--full-width");
                else
                    _primaryButton.AddToClassList("overlay-button--full-width");
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
            _overlaySingleRow = _overlayRoot.Q<VisualElement>("OverlaySingleRow");
            _overlayTwoButtonRow = _overlayRoot.Q<VisualElement>("OverlayTwoButtonRow");
            _buttonSpace = _overlayRoot.Q<VisualElement>("ButtonSpace");

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

            _inputField = new TextField();
            _inputField.name = "OverlayInputField";
            _inputField.AddToClassList("overlay-input-field");
            _inputField.style.display = DisplayStyle.None;

            // Insert directly before the button rows so it always sits above them
            if (_overlaySingleRow?.parent != null)
            {
                int idx = _overlaySingleRow.parent.IndexOf(_overlaySingleRow);
                _overlaySingleRow.parent.Insert(idx, _inputField);
            }
            else
            {
                _overlayCard?.Add(_inputField);
            }

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
