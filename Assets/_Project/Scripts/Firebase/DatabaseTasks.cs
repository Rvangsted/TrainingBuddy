using System.Threading.Tasks;
using BedtimeCore;
using BedtimeCore.Utility;
using Firebase.Database;
using Firebase.Extensions;
using Newtonsoft.Json;
using TrainingBuddy.Managers;
using UnityEngine;
using VContainer;

namespace TrainingBuddy.FireBase
{
	public interface IDatabaseTasks
	{
		public void CreateUser(UserData user);
		public void UpdateUser();
		public void DeleteUser();
	}
	
	public class DatabaseTasks : MonoBehaviour, IDatabaseTasks
	{
		[Inject] private IDatabaseManager _databaseManager;
		
		[InspectorButton]
		public async void CreateUser(UserData user)
		{
			string json = JsonConvert.SerializeObject(user, _databaseManager.JsonSettings);
			
			Task DBTask = _databaseManager.DatabaseReference.Child("users").Child(user.UserID).SetRawJsonValueAsync(json);
			await DBTask;

			if (DBTask.IsFaulted)
			{
				$"CreateUser operation failed with {DBTask.Exception}".Log();
			} 
			else if (DBTask.IsCompleted)
			{
				//TODO: Handle the success???
			}
		}

		[InspectorButton]
		public async void UpdateUser()
		{
			UserData currentUserdata;
			
			await _databaseManager.DatabaseReference.Child("Users").Child("Test_Kt0lGSmAoXaL58luud61cGHY3Zs2").GetValueAsync().ContinueWithOnMainThread(async task => {
                if (task.IsFaulted) {
	                $"UpdateUsers Read operation failed with {task.Exception}".Log();
                }
                else if (task.IsCompleted) {
	                DataSnapshot snapshot = task.Result;
	                
	                currentUserdata = JsonConvert.DeserializeObject<UserData>(snapshot.GetRawJsonValue(), _databaseManager.JsonSettings);
	                
	                // Test Data
	                var user = new UserData
	                {
		                AccelerationPoints = 5,
		                SpeedPoints = 2,
		                StepCount = 2,
	                };
	                
	                user.UserName ??= currentUserdata.UserName;
	                user.UserID ??= currentUserdata.UserID;
	                user.Email ??= currentUserdata.Email;
	                user.AccelerationPoints ??= currentUserdata.AccelerationPoints;
	                user.SpeedPoints ??= currentUserdata.SpeedPoints;
	                user.StepCount ??= currentUserdata.StepCount;
	                user.StepCountSnapshot ??= currentUserdata.StepCountSnapshot;
	                
	                string json = JsonConvert.SerializeObject(user, _databaseManager.JsonSettings);
			
	                Task DBTask = _databaseManager.DatabaseReference.Child("Users").Child("Test").SetRawJsonValueAsync(json);
	                await DBTask;

	                if (DBTask.IsFaulted)
	                {
		                $"UpdateUser Write operation failed with {DBTask.Exception}".Log();
	                } 
	                else if (DBTask.IsCompleted)
	                {
		                //TODO: Handle the success???
	                }
                }
            });
		}
		
		[InspectorButton]
		public async void DeleteUser()
		{
			Task DBTask = _databaseManager.DatabaseReference.Child("Users").Child("Test").RemoveValueAsync();
			await DBTask;
			
			if (DBTask.IsFaulted)
			{
				$"DeleteUser operation failed with {DBTask.Exception}".Log();
			} 
			else if (DBTask.IsCompleted)
			{
				//TODO: Handle the success???
			}
		}
	}
}