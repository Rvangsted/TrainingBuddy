package dk.trainingbuddy.game.healthconnect

import android.content.Context
import android.content.Intent
import androidx.health.connect.client.HealthConnectClient
import androidx.health.connect.client.permission.HealthPermission
import androidx.health.connect.client.records.StepsRecord
import androidx.health.connect.client.request.AggregateRequest
import androidx.health.connect.client.time.TimeRangeFilter
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.launch
import java.time.Instant

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

/**
 * Entry points called from C# via AndroidJavaClass("dk.trainingbuddy.game.healthconnect.HealthConnectBridge").
 * All @JvmStatic — a plain Kotlin `object` only exposes instance methods on its INSTANCE field to
 * Java/JNI, so CallStatic() would fail to resolve them without this annotation.
 */
object HealthConnectBridge {

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
            receiver.onResult("notInstalled")
            return
        }

        CoroutineScope(Dispatchers.Main).launch {
            try {
                val client = HealthConnectClient.getOrCreate(context)
                val granted = client.permissionController.getGrantedPermissions()
                receiver.onResult(if (granted.contains(STEPS_PERMISSION)) "available" else "permissionDenied")
            } catch (e: Exception) {
                receiver.onResult("permissionDenied")
            }
        }
    }

    @JvmStatic
    fun requestPermission(context: Context, receiver: AvailabilityReceiver) {
        pendingPermissionReceiver = receiver
        context.startActivity(Intent(context, HealthConnectPermissionActivity::class.java))
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
}