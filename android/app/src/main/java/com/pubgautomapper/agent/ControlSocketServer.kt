package com.pubgautomapper.agent

import android.net.LocalServerSocket
import android.net.LocalSocket
import java.io.BufferedReader
import java.io.InputStreamReader

class ControlSocketServer(private val service: ControlAccessibilityService) : Runnable {
    @Volatile private var running = true
    private var server: LocalServerSocket? = null

    fun stop() {
        running = false
        try { server?.close() } catch (_: Exception) {}
    }

    override fun run() {
        try {
            server = LocalServerSocket("pubg_automapper")
            while (running) {
                val socket = server?.accept() ?: break
                socket.use { handle(it) }
            }
        } catch (_: Exception) { }
    }

    private fun handle(socket: LocalSocket) {
        val input = BufferedReader(InputStreamReader(socket.inputStream))
        val output = socket.outputStream.bufferedWriter()
        input.forEachLine { line ->
            val trimmed = line.trim()
            val ok = when {
                trimmed == "PING" -> true
                trimmed == "RESET" -> service.resetGestures()
                trimmed.startsWith("TAP ") -> {
                    val p = trimmed.split(' ')
                    p.size == 3 && service.tap(p[1].toFloatOrNull() ?: Float.NaN, p[2].toFloatOrNull() ?: Float.NaN)
                }
                trimmed.startsWith("SWIPE ") -> {
                    val p = trimmed.split(' ')
                    p.size == 6 && service.swipe(
                        p[1].toFloatOrNull() ?: Float.NaN,
                        p[2].toFloatOrNull() ?: Float.NaN,
                        p[3].toFloatOrNull() ?: Float.NaN,
                        p[4].toFloatOrNull() ?: Float.NaN,
                        p[5].toLongOrNull() ?: 120L
                    )
                }
                trimmed.startsWith("FRAME ") -> service.applyFrame(trimmed.removePrefix("FRAME "))
                else -> false
            }
            output.write(if (ok) "OK\n" else "ERR\n")
            output.flush()
        }
    }
}
