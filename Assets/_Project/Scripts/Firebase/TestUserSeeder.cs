using System.Collections;
using System.Threading.Tasks;
using Firebase;
using Firebase.Auth;
using Firebase.Database;
using Newtonsoft.Json;
using UnityEngine;

namespace TrainingBuddy.FireBase
{
    public class TestUserSeeder : MonoBehaviour
    {
        private const string DbUrl = "https://trainingbuddy-81bca-default-rtdb.europe-west1.firebasedatabase.app/";

        private static readonly JsonSerializerSettings JsonSettings =
            new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore };

        private static readonly (string userName, string sex, int dobDay, int dobMonth, int dobYear, int steps)[] Users =
        {
            ("Lars Hansen",       "Male",    4,  3, 1990,    500),
            ("Mette Jensen",      "Female", 17,  7, 1995,   1800),
            ("Anders Nielsen",    "Male",   29, 11, 1988,   4200),
            ("Sofie Christensen", "Female",  2,  5, 2000,   6500),
            ("Mikkel Pedersen",   "Male",   11,  1, 1993,   9100),
            ("Emma Larsen",       "Female", 23,  8, 1997,  12300),
            ("Jonas Møller",      "Male",    8,  6, 1985,  16700),
            ("Camilla Andersen",  "Female", 14, 12, 1992,  21000),
            ("Thomas Sørensen",   "Male",   30,  4, 1991,  28500),
            ("Ida Madsen",        "Female",  6,  9, 1998,  35000),
        };

        private string _status = "Press the button to seed 10 test users.";
        private bool _isRunning;

        private const float PanelWidth  = 340f;
        private const float PanelHeight = 120f;

        private void OnGUI()
        {
            float x = (Screen.width  - PanelWidth)  / 2f;
            float y = (Screen.height - PanelHeight) / 2f;

            GUILayout.BeginArea(new Rect(x, y, PanelWidth, PanelHeight));

            var boxStyle = new GUIStyle(GUI.skin.box)
            {
                normal = { background = MakeTex(1, 1, new Color(0, 0, 0, 0.7f)) }
            };
            GUILayout.BeginVertical(boxStyle);

            GUI.enabled = !_isRunning;
            if (GUILayout.Button(_isRunning ? "Creating…" : "Seed 10 Test Users", GUILayout.Height(45)))
            {
                Debug.Log("[TestUserSeeder] Button clicked, starting seed…");
                StartCoroutine(SeedCoroutine());
            }
            GUI.enabled = true;

            GUILayout.Space(6);

            var labelStyle = new GUIStyle(GUI.skin.label)
            {
                wordWrap  = true,
                alignment = TextAnchor.MiddleCenter,
                normal    = { textColor = Color.white }
            };
            GUILayout.Label(_status, labelStyle);

            GUILayout.EndVertical();
            GUILayout.EndArea();
        }

        private IEnumerator SeedCoroutine()
        {
            _isRunning = true;
            _status = "Initializing Firebase…";

            var depTask = FirebaseApp.CheckAndFixDependenciesAsync();
            yield return new WaitUntil(() => depTask.IsCompleted);

            if (depTask.Result != DependencyStatus.Available)
            {
                _status = $"Firebase init failed: {depTask.Result}";
                _isRunning = false;
                yield break;
            }

            FirebaseAuth auth = FirebaseAuth.DefaultInstance;
            DatabaseReference db = FirebaseDatabase.GetInstance(DbUrl).RootReference;

            int ok = 0, fail = 0;

            for (int i = 0; i < Users.Length; i++)
            {
                var (userName, sex, dobDay, dobMonth, dobYear, steps) = Users[i];
                string email    = $"testuser{i + 1}@example.com";
                string password = "TestPass123!";

                _status = $"Creating {i + 1}/10: {userName}…";
                Debug.Log($"[TestUserSeeder] {_status}");

                // Register
                Task<AuthResult> registerTask = auth.CreateUserWithEmailAndPasswordAsync(email, password);
                yield return new WaitUntil(() => registerTask.IsCompleted);

                if (registerTask.IsFaulted)
                {
                    Debug.LogError($"[TestUserSeeder] Failed for {userName}: {registerTask.Exception?.GetBaseException().Message}");
                    fail++;
                    continue;
                }

                string userId = registerTask.Result.User.UserId;

                // Set display name
                Task profileTask = registerTask.Result.User.UpdateUserProfileAsync(new UserProfile { DisplayName = userName });
                yield return new WaitUntil(() => profileTask.IsCompleted);

                // Write to database (mirrors DatabaseManager.CreateUser — writes user + indexes atomically)
                string friendCode = GenerateFriendCode(userId);
                var userData = new UserData
                {
                    UserName           = userName,
                    Sex                = sex,
                    UserID             = userId,
                    FriendCode         = friendCode,
                    Email              = email,
                    DateOfBirthDay     = dobDay,
                    DateOfBirthMonth   = dobMonth,
                    DateOfBirthYear    = dobYear,
                    AccelerationPoints = 0,
                    SpeedPoints        = 0,
                    StepCount          = steps,
                    StepCountSnapshot  = 0,
                    UserLevel          = 1,
                };

                // Generate 5 days of random step history
                var rng = new System.Random(i * 12345 + 67890);
                string today = System.DateTime.UtcNow.ToString("yyyy-MM-dd");
                int dailyTotal = 0;
                var dailyEntries = new System.Collections.Generic.Dictionary<string, int>();
                for (int d = 4; d >= 0; d--)
                {
                    string date = System.DateTime.UtcNow.AddDays(-d).ToString("yyyy-MM-dd");
                    int daySteps = rng.Next(1500, 9500);
                    dailyEntries[date] = daySteps;
                    dailyTotal += daySteps;
                }
                int totalSteps    = steps + dailyTotal;
                int todaySteps    = dailyEntries[today];
                int dailyStepBase = totalSteps - todaySteps;

                userData.StepCount     = totalSteps;
                userData.StepCurrency  = totalSteps;
                userData.DailyStepBase = dailyStepBase;
                userData.DailyStepDate = today;

                string json = JsonConvert.SerializeObject(userData, JsonSettings);
                var userDict = JsonConvert.DeserializeObject<System.Collections.Generic.Dictionary<string, object>>(json, JsonSettings);

                // Nest dailySteps inside userDict so the whole user write is one atomic path
                var dailyStepsDict = new System.Collections.Generic.Dictionary<string, object>();
                foreach (var kvp in dailyEntries)
                    dailyStepsDict[kvp.Key] = kvp.Value;
                userDict["dailySteps"] = dailyStepsDict;

                var updates = new System.Collections.Generic.Dictionary<string, object>
                {
                    [$"users/{userId}"]              = userDict,
                    [$"friendCodes/{friendCode}"]    = userId,
                    [$"usernames/{userName}"]        = userId,
                };

                Task dbTask = db.UpdateChildrenAsync(updates);
                yield return new WaitUntil(() => dbTask.IsCompleted);

                if (dbTask.IsFaulted)
                {
                    Debug.LogError($"[TestUserSeeder] DB write failed for {userName}: {dbTask.Exception?.GetBaseException().Message}");
                    fail++;
                    continue;
                }

                Debug.Log($"[TestUserSeeder] Created {userName} ({userData.FriendCode}) — {steps:N0} steps");
                ok++;
            }

            auth.SignOut();
            _status = $"Done. {ok} created, {fail} failed.\nPassword: TestPass123!";
            Debug.Log($"[TestUserSeeder] {_status}");
            _isRunning = false;
        }

        private static string GenerateFriendCode(string userId)
        {
            const string alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
            ulong hash = 14695981039346656037UL;
            foreach (char c in userId)
            {
                hash ^= c;
                hash *= 1099511628211UL;
            }

            var code = new char[7];
            for (int i = 0; i < 7; i++)
            {
                code[i] = alphabet[(int)(hash & 31)];
                hash >>= 5;
            }
            return new string(code);
        }

        private static Texture2D MakeTex(int width, int height, Color col)
        {
            var tex = new Texture2D(width, height);
            tex.SetPixel(0, 0, col);
            tex.Apply();
            return tex;
        }
    }
}
