using System.Collections;
using System.Threading.Tasks;
using NUnit.Framework;
using TrainingBuddy.Testing;
using UnityEngine;

namespace Testing
{
	public class Test_Firebase
	{
		[Test]
		public async Task FirebaseDependencies()
		{
			var firebaseInit = new MockFirebaseController();
			Assert.IsTrue(await firebaseInit.CheckDependencies());
		}
		
		[Test]
		public async Task FirebaseAuthentication()
		{
			var firebaseInit = new MockFirebaseController();
			Assert.IsTrue(await firebaseInit.FirebaseLogin("admin@trainingbuddy.dk", "smjo3y2kZRfk7jN^@wGN4z8K^"));
		}
	}
}