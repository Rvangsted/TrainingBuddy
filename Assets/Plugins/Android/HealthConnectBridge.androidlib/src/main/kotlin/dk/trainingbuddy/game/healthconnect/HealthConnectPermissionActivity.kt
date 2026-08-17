package dk.trainingbuddy.game.healthconnect

import android.os.Bundle
import android.util.Log
import androidx.activity.ComponentActivity
import androidx.health.connect.client.PermissionController

/**
 * Transparent, single-purpose activity. Health Connect's permission request goes through the
 * AndroidX Activity Result API, which requires registerForActivityResult() to run inside
 * onCreate() of a ComponentActivity — Unity's UnityPlayerActivity doesn't do this, so the request
 * is routed through this throwaway activity instead of modifying the app's main one.
 */
class HealthConnectPermissionActivity : ComponentActivity() {

    private val requestPermissions =
        registerForActivityResult(PermissionController.createRequestPermissionResultContract()) { granted ->
            Log.i(
                "HealthConnectBridge",
                "HealthConnectPermissionActivity result: granted=$granted " +
                    "(requested ${HealthConnectBridge.STEPS_PERMISSION}) — if steps isn't in the granted " +
                    "set, the Health Connect consent screen's Steps toggle wasn't switched on before confirming"
            )
            val receiver = HealthConnectBridge.pendingPermissionReceiver
            HealthConnectBridge.pendingPermissionReceiver = null
            receiver?.onResult(if (granted.contains(HealthConnectBridge.STEPS_PERMISSION)) "available" else "permissionDenied")
            finish()
        }

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        requestPermissions.launch(setOf(HealthConnectBridge.STEPS_PERMISSION))
    }
}