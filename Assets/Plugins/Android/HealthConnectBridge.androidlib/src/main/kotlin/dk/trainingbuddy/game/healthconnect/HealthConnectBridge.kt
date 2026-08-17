package dk.trainingbuddy.game.healthconnect

import android.content.ActivityNotFoundException
import android.content.Context
import android.content.Intent
import android.health.connect.HealthConnectManager
import android.os.Build
import android.util.Log
import androidx.health.connect.client.HealthConnectClient
import androidx.health.connect.client.permission.HealthPermission
import androidx.health.connect.client.records.StepsRecord
import androidx.health.connect.client.request.AggregateGroupByPeriodRequest
import androidx.health.connect.client.request.AggregateRequest
import androidx.health.connect.client.time.TimeRangeFilter
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.launch
import java.time.Instant
import java.time.LocalDate
import java.time.Period
import java.time.ZoneId

/**
 * Implemented on the C# side via AndroidJavaProxy, the same way
 * com.yasirkula.unity.RuntimePermissionsReceiver is consumed by PermissionCallback.cs elsewhere
 * in this project.
 */
interface AvailabilityReceiver {
    fun onResult(status: String) // "available" | "permissionDenied" | "notInstalled"
}

interface StepsReceiver {
    fun onResult(steps: Long, success: Boolean)
}

interface DailyStepsReceiver {
    fun onResult(dateKeys: Array<String>, steps: LongArray, success: Boolean)
}

/**
 * Entry points called from C# via AndroidJavaClass("dk.trainingbuddy.game.healthconnect.HealthConnectBridge").
 * All @JvmStatic — a plain Kotlin `object` only exposes instance methods on its INSTANCE field to
 * Java/JNI, so CallStatic() would fail to resolve them without this annotation.
 */
object HealthConnectBridge {

    private const val TAG = "HealthConnectBridge"
    private const val PROVIDER_PACKAGE = "com.google.android.apps.healthdata"
    internal val STEPS_PERMISSION: String = HealthPermission.getReadPermission(StepsRecord::class)

    // Set immediately before launching HealthConnectPermissionActivity and consumed once by its
    // result callback. Single-slot is safe: Unity never issues a second permission request before
    // the transparent activity from the first one has finished.
    internal var pendingPermissionReceiver: AvailabilityReceiver? = null

    @JvmStatic
    fun isProviderInstalled(context: Context): Boolean {
        return HealthConnectClient.getSdkStatus(context, PROVIDER_PACKAGE) == HealthConnectClient.SDK_AVAILABLE
    }

    @JvmStatic
    fun checkAvailability(context: Context, receiver: AvailabilityReceiver) {
        if (!isProviderInstalled(context)) {
            Log.i(TAG, "checkAvailability: Health Connect provider not installed")
            receiver.onResult("notInstalled")
            return
        }

        CoroutineScope(Dispatchers.Main).launch {
            try {
                val client = HealthConnectClient.getOrCreate(context)
                val granted = client.permissionController.getGrantedPermissions()
                val hasSteps = granted.contains(STEPS_PERMISSION)
                Log.i(TAG, "checkAvailability: grantedPermissions=$granted hasStepsPermission=$hasSteps")
                receiver.onResult(if (hasSteps) "available" else "permissionDenied")
            } catch (e: Exception) {
                Log.e(TAG, "checkAvailability: failed to read granted permissions", e)
                receiver.onResult("permissionDenied")
            }
        }
    }

    @JvmStatic
    fun requestPermission(context: Context, receiver: AvailabilityReceiver) {
        Log.i(TAG, "requestPermission: launching HealthConnectPermissionActivity for $STEPS_PERMISSION")
        pendingPermissionReceiver = receiver
        context.startActivity(Intent(context, HealthConnectPermissionActivity::class.java))
    }

    // Deep-links into Health Connect's own permission UI for this app, instead of the OS
    // "App info" screen — that screen has no Health Connect section at all, which is why the old
    // fallback button looked like it did nothing. The target intent differs by OS version: Android
    // 14+ folded Health Connect into the platform permission framework (ACTION_MANAGE_HEALTH_PERMISSIONS,
    // android.health.connect.HealthConnectManager), while 13 and below still route through the
    // standalone Health Connect app's own settings action (HealthConnectClient.ACTION_HEALTH_CONNECT_SETTINGS).
    @JvmStatic
    fun openHealthConnectSettings(context: Context): Boolean {
        val intent = if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.UPSIDE_DOWN_CAKE) {
            Intent(HealthConnectManager.ACTION_MANAGE_HEALTH_PERMISSIONS)
                .putExtra(Intent.EXTRA_PACKAGE_NAME, context.packageName)
        } else {
            Intent(HealthConnectClient.ACTION_HEALTH_CONNECT_SETTINGS)
        }
        return try {
            context.startActivity(intent)
            true
        } catch (e: ActivityNotFoundException) {
            Log.e(TAG, "openHealthConnectSettings: no activity resolved $intent", e)
            false
        }
    }

    @JvmStatic
    fun getStepsSince(context: Context, sinceEpochMillis: Long, receiver: StepsReceiver) {
        CoroutineScope(Dispatchers.Main).launch {
            try {
                val client = HealthConnectClient.getOrCreate(context)
                val response = client.aggregate(
                    AggregateRequest(
                        metrics = setOf(StepsRecord.COUNT_TOTAL),
                        timeRangeFilter = TimeRangeFilter.between(
                            Instant.ofEpochMilli(sinceEpochMillis),
                            Instant.now()
                        )
                    )
                )
                receiver.onResult(response[StepsRecord.COUNT_TOTAL] ?: 0L, true)
            } catch (e: Exception) {
                receiver.onResult(0L, false)
            }
        }
    }

    // Calendar-based aggregation (aggregateGroupByPeriod), distinct from getStepsSince's
    // fixed-duration aggregate() — this is what actually buckets by the device's local calendar
    // day rather than a raw elapsed-time window, which is the whole point of asking Health
    // Connect instead of hand-rolling day boundaries in UTC.
    @JvmStatic
    fun getDailyStepsSince(context: Context, days: Int, receiver: DailyStepsReceiver) {
        CoroutineScope(Dispatchers.Main).launch {
            try {
                val client = HealthConnectClient.getOrCreate(context)
                val zone = ZoneId.systemDefault()
                val endDay = LocalDate.now(zone)
                val startDay = endDay.minusDays(days.toLong())

                val response = client.aggregateGroupByPeriod(
                    AggregateGroupByPeriodRequest(
                        metrics = setOf(StepsRecord.COUNT_TOTAL),
                        timeRangeFilter = TimeRangeFilter.between(startDay.atStartOfDay(), endDay.atStartOfDay()),
                        period = Period.ofDays(1)
                    )
                )

                val dateKeys = Array(response.size) { i -> response[i].startTime.toLocalDate().toString() }
                val steps = LongArray(response.size) { i -> response[i].result[StepsRecord.COUNT_TOTAL] ?: 0L }
                receiver.onResult(dateKeys, steps, true)
            } catch (e: Exception) {
                Log.e(TAG, "getDailyStepsSince: failed to aggregate daily steps", e)
                receiver.onResult(emptyArray(), LongArray(0), false)
            }
        }
    }
}