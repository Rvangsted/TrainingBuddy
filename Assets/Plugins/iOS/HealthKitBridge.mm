// Native HealthKit bridge for TrainingBuddy's step counter. Entry points are extern "C" so they
// can be called from C# via [DllImport("__Internal")], the same convention Unity's own Input
// System uses for its CoreMotion pedometer bridge (Library/PackageCache/.../iOS/iOSStepCounter.mm).
// See Assets/_Project/Docs/StepCounter_HealthPlatform_Migration_Scope.md for the overall plan and
// Assets/_Project/Scripts/Managers/HealthKitStepProvider.cs for the C# side of this bridge.

#import <HealthKit/HealthKit.h>
#import <UIKit/UIKit.h>

typedef void (*HealthKitIntCallback)(int requestId, int value);
typedef void (*HealthKitStepsCallback)(int requestId, long long steps, int success);

static HKHealthStore *GetHealthStore()
{
    static HKHealthStore *store = nil;
    if (!store)
    {
        store = [[HKHealthStore alloc] init];
    }
    return store;
}

extern "C" {

int _HealthKit_IsAvailable()
{
    return [HKHealthStore isHealthDataAvailable] ? 1 : 0;
}

void _HealthKit_RequestAuthorization(int requestId, HealthKitIntCallback callback)
{
    HKQuantityType *stepType = [HKQuantityType quantityTypeForIdentifier:HKQuantityTypeIdentifierStepCount];
    NSSet *readTypes = [NSSet setWithObject:stepType];

    [GetHealthStore() requestAuthorizationToShareTypes:nil
                                              readTypes:readTypes
                                             completion:^(BOOL success, NSError *error) {
        dispatch_async(dispatch_get_main_queue(), ^{
            callback(requestId, success ? 1 : 0);
        });
    }];
}

void _HealthKit_QueryStepsSince(int requestId, long long sinceUnixMillis, HealthKitStepsCallback callback)
{
    HKQuantityType *stepType = [HKQuantityType quantityTypeForIdentifier:HKQuantityTypeIdentifierStepCount];
    NSDate *start = [NSDate dateWithTimeIntervalSince1970:(sinceUnixMillis / 1000.0)];
    NSDate *end = [NSDate date];
    NSPredicate *predicate = [HKQuery predicateForSamplesWithStartDate:start endDate:end options:HKQueryOptionStrictStartDate];

    HKStatisticsQuery *query = [[HKStatisticsQuery alloc] initWithQuantityType:stepType
                                                        quantitySamplePredicate:predicate
                                                                        options:HKStatisticsOptionCumulativeSum
                                                              completionHandler:^(HKStatisticsQuery *q, HKStatistics *result, NSError *error) {
        double steps = 0;
        BOOL success = (error == nil);
        if (success && result)
        {
            HKQuantity *sum = result.sumQuantity;
            if (sum)
            {
                steps = [sum doubleValueForUnit:[HKUnit countUnit]];
            }
        }
        dispatch_async(dispatch_get_main_queue(), ^{
            callback(requestId, (long long)steps, success ? 1 : 0);
        });
    }];

    [GetHealthStore() executeQuery:query];
}

int _HealthKit_OpenSettings()
{
    NSURL *url = [NSURL URLWithString:UIApplicationOpenSettingsURLString];
    if (!url || ![[UIApplication sharedApplication] canOpenURL:url])
    {
        return 0;
    }

    dispatch_async(dispatch_get_main_queue(), ^{
        [[UIApplication sharedApplication] openURL:url options:@{} completionHandler:nil];
    });
    return 1;
}

}
