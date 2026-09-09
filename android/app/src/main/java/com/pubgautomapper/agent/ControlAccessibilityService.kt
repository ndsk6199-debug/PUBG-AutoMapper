package com.pubgautomapper.agent

import android.accessibilityservice.AccessibilityService
import android.accessibilityservice.GestureDescription
import android.graphics.Path
import android.os.Handler
import android.os.Looper
import android.view.accessibility.AccessibilityEvent
import org.json.JSONArray
import org.json.JSONObject

class ControlAccessibilityService : AccessibilityService() {
    private var socketThread: Thread? = null
    private var socketServer: ControlSocketServer? = null
    private val mainHandler = Handler(Looper.getMainLooper())
    private val active = LinkedHashMap<String, ActivePointer>()

    private data class ActivePointer(
        val x: Float,
        val y: Float,
        val stroke: GestureDescription.StrokeDescription
    )

    override fun onServiceConnected() {
        super.onServiceConnected()
        socketServer = ControlSocketServer(this)
        socketThread = Thread(socketServer, "control-socket").apply { start() }
    }

    override fun onDestroy() {
        socketServer?.stop()
        socketThread?.interrupt()
        active.clear()
        super.onDestroy()
    }

    override fun onAccessibilityEvent(event: AccessibilityEvent?) = Unit
    override fun onInterrupt() = Unit

    fun tap(x: Float, y: Float, durationMs: Long = 60L): Boolean {
        if (!finite(x, y)) return false
        val path = Path().apply { moveTo(x, y) }
        val stroke = GestureDescription.StrokeDescription(path, 0L, durationMs.coerceAtLeast(1L))
        return dispatchGesture(GestureDescription.Builder().addStroke(stroke).build(), null, null)
    }

    fun swipe(x1: Float, y1: Float, x2: Float, y2: Float, durationMs: Long = 120L): Boolean {
        if (!finite(x1, y1, x2, y2)) return false
        val path = Path().apply { moveTo(x1, y1); lineTo(x2, y2) }
        val stroke = GestureDescription.StrokeDescription(path, 0L, durationMs.coerceAtLeast(1L))
        return dispatchGesture(GestureDescription.Builder().addStroke(stroke).build(), null, null)
    }

    /**
     * FRAME is the low-latency control primitive.
     * Multiple pointers are dispatched together. Existing pointers are continued
     * with StrokeDescription.continueStroke(), allowing joystick + look + buttons
     * to coexist without the desktop spawning an adb process for each event.
     */
    fun applyFrame(json: String): Boolean {
        return try {
            val root = JSONObject(json)
            val duration = root.optLong("durationMs", 60L).coerceIn(20L, 100L)
            val arr: JSONArray = root.optJSONArray("pointers") ?: JSONArray()
            val wanted = LinkedHashMap<String, Pair<Float, Float>>()

            for (i in 0 until arr.length()) {
                val item = arr.getJSONObject(i)
                val id = item.getString("id")
                val x = item.getDouble("x").toFloat()
                val y = item.getDouble("y").toFloat()
                if (finite(x, y)) wanted[id] = x to y
            }

            val latch = java.util.concurrent.CountDownLatch(1)
            var dispatched = false

            mainHandler.post {
                try {
                    val builder = GestureDescription.Builder()

                    // Finalize pointers that disappeared from this frame.
                    for ((id, old) in active.toMap()) {
                        if (!wanted.containsKey(id)) {
                            val endPath = Path().apply { moveTo(old.x, old.y) }
                            builder.addStroke(old.stroke.continueStroke(endPath, 0L, 1L, false))
                        }
                    }

                    val next = LinkedHashMap<String, ActivePointer>()
                    for ((id, pos) in wanted) {
                        val x = pos.first
                        val y = pos.second
                        val old = active[id]
                        val stroke = if (old != null) {
                            val path = Path().apply { moveTo(old.x, old.y); lineTo(x, y) }
                            old.stroke.continueStroke(path, 0L, duration, true)
                        } else {
                            val path = Path().apply { moveTo(x, y) }
                            GestureDescription.StrokeDescription(path, 0L, duration, true)
                        }
                        builder.addStroke(stroke)
                        next[id] = ActivePointer(x, y, stroke)
                    }

                    dispatched = dispatchGesture(builder.build(), object : GestureResultCallback() {
                        override fun onCancelled(gestureDescription: GestureDescription?) {
                            active.clear()
                        }
                        override fun onCompleted(gestureDescription: GestureDescription?) {
                            // Usually not reached while the pointers use willContinue=true.
                        }
                    }, null)

                    if (dispatched) {
                        active.clear()
                        active.putAll(next)
                    }
                } finally {
                    latch.countDown()
                }
            }

            latch.await(500, java.util.concurrent.TimeUnit.MILLISECONDS)
            dispatched
        } catch (_: Exception) {
            false
        }
    }

    fun resetGestures(): Boolean {
        mainHandler.post { active.clear() }
        return true
    }

    private fun finite(vararg values: Float) = values.all { it.isFinite() }
}
