using UnityEngine;
using UnityEngine.UIElements;
using VContainer.Unity;

namespace TrainingBuddy.UI
{
	[CreateAssetMenu(fileName = "New LayoutData", menuName = "ScriptableObjects/LayoutData")]
	public class LayoutData : ScriptableObject
	{
		[field:SerializeField] public VisualTreeAsset WelcomeScreenVisualTree { get; set; }
		[field:SerializeField] public VisualTreeAsset LoginScreenVisualTree { get; set; }
		[field:SerializeField] public VisualTreeAsset RegisterScreenVisualTree { get; set; }
		
		[field:SerializeField] public VisualTreeAsset ForgotPasswordScreenVisualTree { get; set; }
		[field:SerializeField] public VisualTreeAsset ResetPasswordScreenVisualTree { get; set; }
		[field:SerializeField] public VisualTreeAsset MainMenuVisualTree { get; set; }
		[field:SerializeField] public VisualTreeAsset ProfileScreenVisualTree { get; set; }
		[field:SerializeField] public VisualTreeAsset HighScoreVisualTree { get; set; }
		[field:SerializeField] public VisualTreeAsset RaceScreenVisualTree { get; set; }
		[field:SerializeField] public VisualTreeAsset LobbyScreenVisualTree { get; set; }
		[field:SerializeField] public VisualTreeAsset HostScreenVisualTree { get; set; }
		[field:SerializeField] public VisualTreeAsset FindLobbyScreenVisualTree { get; set; }
		
		public WelcomeScreen WelcomeScreen { get; set; }
		public LoginScreen LoginScreen { get; set; }
		public RegisterScreen RegisterScreen { get; set; }
		public ForgotPasswordScreen ForgotPasswordScreen { get; set; }
		public ResetPasswordScreen ResetPasswordScreen { get; set; }
		public MainMenu MainMenu { get; set; }
		public ProfileScreen ProfileScreen { get; set; }
		public HighScoreScreen HighScoreScreen { get; set; }
		public LobbyScreen LobbyScreen { get; set; }
		public RaceScreen RaceScreen { get; set; }
		public HostScreen HostScreen { get; set; }
		public FindLobbyScreen FindLobbyScreen { get; set; }
	}
}