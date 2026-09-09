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

    private data class ActivePointer(var x: Float, var y: Float, var stroke: GestureDescription.StrokeDescription)

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
     * Apply a frame of simultaneously-active touch pointers.
     * Each pointer is continued across frames with StrokeDescription.continueStroke().
     * The desktop sends the next frame before the previous stroke's duration expires.
     */
    fun applyFrame(json: String): Boolean {
        return try {
            val root = JSONObject(json)
            val duration = root.optLong("durationMs", 70L).coerceIn(15L, 120L)
            val wanted = LinkedHashMap<String, Pair<Float, Float>>()
            val arr: JSONArray = root.optJSONArray("pointers") ?: JSONArray()
            for (i in 0 until arr.length()) {
                val o = arr.getJSONObject(i)
                val id = o.getString("id")
                val x = o.getDouble("x").toFloat()
                val y = o.getDouble("y").toFloat()
                if (finite(x, y)) wanted[id] = x to y
            }

            val latch = java.util.concurrent.CountDownLatch(1)
            var result = false
            mainHandler.post {
                try {
                    val builder = GestureDescription.Builder()

                    // Release pointers no longer requested.
                    for ((id, old) in active.toMap()) {
                        if (!wanted.containsKey(id)) {
                            val path = Path().apply { moveTo(old.x, old.y) }
                            builder.addStroke(old.stroke.continueStroke(path, 0L, 1L, false))
                        }
                    }

                    // Continue or create each requested pointer.
                    val nextPointers = LinkedHashMap<String, ActivePointer>()
                    for ((id, pos) in wanted) {
                        val x = pos.first
                        val y = pos.second
                        val previous = active[id]
                        val stroke = if (previous != null) {
                            val path = Path().apply { moveTo(previous.x, previous.y); lineTo(x, y) }
                            previous.stroke.continueStroke(path, 0L, duration, true)
                        } else {
                            val path = Path().apply { moveTo(x, y) }
                            GestureDescription.StrokeDescription(path, 0L, duration, true)
                        }
                        builder.addStroke(stroke)
                        nextPointers[id] = ActivePointer(x, y, stroke)
                    }

                    val gesture = builder.build()
                    result = dispatchGesture(gesture, object : GestureResultCallback() {
                        override fun onCompleted(gestureDescription: GestureDescription?) {
                            active.clear()
                            active.putAll(nextPointers)
                        }
                        override fun onCancelled(gestureDescription: GestureDescription?) {
                            // The next frame can start fresh after cancellation.
                            active.clear()
                        }
                    }, null)
                } finally {
                    latch.countDown()
                }
            }
            latch.await(500, java.util.concurrent.TimeUnit.MILLISECONDS)
            result
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
