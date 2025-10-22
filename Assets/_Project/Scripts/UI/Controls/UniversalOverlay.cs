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
        private Label _titleLabel;
        private Label _messageLabel;
        private Button _primaryButton;
        private Button _secondaryButton;

        private Action _primaryAction;
        private Action _secondaryAction;
        private bool _allowBackgroundDismiss;

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

        public void Show(string title, string message, string primaryButtonText = "OK", Action primaryAction = null, string secondaryButtonText = null, Action secondaryAction = null, bool allowBackgroundDismiss = false)
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

            if (_primaryButton != null)
            {
                _primaryButton.text = string.IsNullOrEmpty(primaryButtonText) ? "OK" : primaryButtonText;
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

            if (_secondaryButton != null)
            {
                _secondaryButton.style.display = DisplayStyle.None;
            }

            var root = _uiDocument.rootVisualElement;
            root.Add(_overlayRoot);
            _overlayRoot.BringToFront();
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
            Hide();
            _primaryAction?.Invoke();
        }

        private void OnSecondaryClicked()
        {
            Hide();
            _secondaryAction?.Invoke();
        }
    }
}