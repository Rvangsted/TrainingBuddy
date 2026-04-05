using System;
using TrainingBuddy.FireBase;
using TrainingBuddy.Managers;
using TrainingBuddy.UI;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using VContainer;
using VContainer.Unity;

namespace TrainingBuddy
{
	public class GlobalScope : LifetimeScope
	{
		[SerializeField] private UIManager _uiManager;
		[SerializeField] private LayoutData _layoutData;

		private void Initialize()
		{
			autoInjectGameObjects.AddRange(gameObject.scene.GetRootGameObjects());
		}

		protected override void Awake()
		{
			if (autoRun)
			{
				Initialize();
			}
			base.Awake();
		}
		
		protected override void Configure(IContainerBuilder builder)
		{
			builder.Register<FirebaseController>(Lifetime.Singleton).AsImplementedInterfaces().AsSelf();
			builder.Register<DatabaseManager>(Lifetime.Singleton).AsImplementedInterfaces().AsSelf();
			builder.Register<DatabaseTasks>(Lifetime.Singleton).AsImplementedInterfaces().AsSelf();
			builder.Register<GameManager>(Lifetime.Singleton).AsImplementedInterfaces().AsSelf();

			builder.RegisterInstance(_layoutData);
			
			builder.RegisterComponentInNewPrefab(_uiManager, Lifetime.Singleton).AsImplementedInterfaces().AsSelf();
			
			builder.RegisterBuildCallback(resolver =>
			{
				var canvasManager = resolver.Resolve<UIManager>();
				SceneManager.MoveGameObjectToScene(canvasManager.gameObject, gameObject.scene);
				resolver.InjectGameObject(canvasManager.gameObject);

				resolver.Resolve<LayoutData>();
			});
			
			builder.Register<WelcomeScreen>(Lifetime.Singleton).AsImplementedInterfaces().AsSelf();
			builder.Register<LoginScreen>(Lifetime.Singleton).AsImplementedInterfaces().AsSelf();
			builder.Register<RegisterScreen>(Lifetime.Singleton).AsImplementedInterfaces().AsSelf();
			builder.Register<ForgotPasswordScreen>(Lifetime.Singleton).AsImplementedInterfaces().AsSelf();
			builder.Register<ResetPasswordScreen>(Lifetime.Singleton).AsImplementedInterfaces().AsSelf();
			builder.Register<MainMenu>(Lifetime.Singleton).AsImplementedInterfaces().AsSelf();
			builder.Register<ProfileScreen>(Lifetime.Singleton).AsImplementedInterfaces().AsSelf();
			builder.Register<HighScoreScreen>(Lifetime.Singleton).AsImplementedInterfaces().AsSelf();
			builder.Register<RaceScreen>(Lifetime.Singleton).AsImplementedInterfaces().AsSelf();
			builder.Register<LobbyScreen>(Lifetime.Singleton).AsImplementedInterfaces().AsSelf();
			builder.Register<FindLobbyScreen>(Lifetime.Singleton).AsImplementedInterfaces().AsSelf();
			builder.Register<HostScreen>(Lifetime.Singleton).AsImplementedInterfaces().AsSelf();
		}
	}
} 