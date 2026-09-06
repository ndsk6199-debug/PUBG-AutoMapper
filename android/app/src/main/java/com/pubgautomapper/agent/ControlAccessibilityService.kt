package com.pubgautomapper.agent

import android.accessibilityservice.AccessibilityService
import android.accessibilityservice.GestureDescription
import android.graphics.Path
import android.view.accessibility.AccessibilityEvent

class ControlAccessibilityService : AccessibilityService() {
    private var socketThread: Thread? = null
    private var socketServer: ControlSocketServer? = null

    override fun onServiceConnected() {
        super.onServiceConnected()
        socketServer = ControlSocketServer(this)
        socketThread = Thread(socketServer, "control-socket").apply { start() }
    }

    override fun onDestroy() {
        socketServer?.stop()
        socketThread?.interrupt()
        super.onDestroy()
    }

    override fun onAccessibilityEvent(event: AccessibilityEvent?) = Unit
    override fun onInterrupt() = Unit

    fun tap(x: Float, y: Float, durationMs: Long = 60L): Boolean {
        if (!x.isFinite() || !y.isFinite()) return false
        val path = Path().apply { moveTo(x, y) }
        val stroke = GestureDescription.StrokeDescription(path, 0L, durationMs.coerceAtLeast(1L))
        return dispatchGesture(GestureDescription.Builder().addStroke(stroke).build(), null, null)
    }

    fun swipe(x1: Float, y1: Float, x2: Float, y2: Float, durationMs: Long = 120L): Boolean {
        if (!listOf(x1, y1, x2, y2).all { it.isFinite() }) return false
        val path = Path().apply { moveTo(x1, y1); lineTo(x2, y2) }
        val stroke = GestureDescription.StrokeDescription(path, 0L, durationMs.coerceAtLeast(1L))
        return dispatchGesture(GestureDescription.Builder().addStroke(stroke).build(), null, null)
    }
}
