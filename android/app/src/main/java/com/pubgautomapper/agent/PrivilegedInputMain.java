package com.pubgautomapper.agent;

import android.hardware.input.InputManager;
import android.view.InputDevice;
import android.view.InputEvent;
import android.view.MotionEvent;

import java.io.*;
import java.net.ServerSocket;
import java.net.Socket;
import java.lang.reflect.Method;
import java.util.*;

/**
 * Privileged touch injector launched with adb shell + app_process.
 * It keeps a pointer set across FRAME messages and injects raw multi-pointer
 * MotionEvent streams through InputManager. This is deliberately separate from
 * the normal AccessibilityService because AccessibilityService gestures have
 * limitations when pointers need to be added/removed while other pointers stay held.
 */
public final class PrivilegedInputMain {
    private static final int PORT = 27184;
    private static final int INJECT_ASYNC = 0;
    private static Method injectMethod;
    private static Object inputManager;
    private static final Map<Integer, Point> active = new LinkedHashMap<>();
    private static long downTime = 0L;

    private static final class Point {
        final float x, y, pressure;
        Point(float x, float y, float pressure) { this.x=x; this.y=y; this.pressure=pressure; }
    }

    public static void main(String[] args) throws Exception {
        initInputManager();
        try (ServerSocket server = new ServerSocket(PORT, 1)) {
            server.setReuseAddress(true);
            while (true) {
                try (Socket socket = server.accept()) {
                    handle(socket);
                } catch (Throwable ignored) {
                    reset();
                }
            }
        }
    }

    private static void initInputManager() throws Exception {
        Class<?> cls = Class.forName("android.hardware.input.InputManager");
        Method getInstance = cls.getDeclaredMethod("getInstance");
        getInstance.setAccessible(true);
        inputManager = getInstance.invoke(null);
        injectMethod = cls.getMethod("injectInputEvent", InputEvent.class, int.class);
        injectMethod.setAccessible(true);
    }

    private static boolean inject(MotionEvent event) throws Exception {
        try {
            return (Boolean) injectMethod.invoke(inputManager, event, INJECT_ASYNC);
        } finally {
            event.recycle();
        }
    }

    private static void handle(Socket socket) throws Exception {
        socket.setTcpNoDelay(true);
        BufferedReader in = new BufferedReader(new InputStreamReader(socket.getInputStream()));
        BufferedWriter out = new BufferedWriter(new OutputStreamWriter(socket.getOutputStream()));
        String line;
        while ((line = in.readLine()) != null) {
            String cmd = line.trim();
            boolean ok;
            if (cmd.equals("PING")) {
                ok = true;
            } else if (cmd.equals("RESET")) {
                reset(); ok = true;
            } else if (cmd.startsWith("FRAME ")) {
                ok = applyFrame(cmd.substring(6));
            } else {
                ok = false;
            }
            out.write(ok ? "OK\n" : "ERR\n");
            out.flush();
        }
    }

    private static boolean applyFrame(String payload) {
        try {
            Map<Integer, Point> next = new LinkedHashMap<>();
            if (!payload.isEmpty()) {
                for (String token : payload.split(";")) {
                    String[] p = token.split(":");
                    if (p.length != 4) return false;
                    int id = Integer.parseInt(p[0]);
                    if (id < 0 || id > 15) return false;
                    float x = Float.parseFloat(p[1]);
                    float y = Float.parseFloat(p[2]);
                    float pressure = Float.parseFloat(p[3]);
                    if (!Float.isFinite(x) || !Float.isFinite(y) || !Float.isFinite(pressure)) return false;
                    next.put(id, new Point(x, y, pressure));
                }
            }

            long now = android.os.SystemClock.uptimeMillis();
            if (active.isEmpty() && !next.isEmpty()) {
                downTime = now;
                emit(now, MotionEvent.ACTION_DOWN, next);
                active.clear(); active.putAll(next);
                return true;
            }
            if (!active.isEmpty() && next.isEmpty()) {
                emit(now, MotionEvent.ACTION_UP, active);
                active.clear();
                downTime = 0L;
                return true;
            }

            Set<Integer> removed = new LinkedHashSet<>(active.keySet());
            removed.removeAll(next.keySet());
            Set<Integer> added = new LinkedHashSet<>(next.keySet());
            added.removeAll(active.keySet());

            // Remove pointers first, highest/current index is used for action index.
            for (Integer id : removed) {
                LinkedHashMap<Integer, Point> temp = new LinkedHashMap<>(active);
                int idx = indexOf(temp.keySet(), id);
                emit(now, MotionEvent.ACTION_POINTER_UP | (idx << MotionEvent.ACTION_POINTER_INDEX_SHIFT), temp);
                temp.remove(id);
                active.clear(); active.putAll(temp);
            }

            // Add pointers one at a time.
            for (Integer id : added) {
                LinkedHashMap<Integer, Point> temp = new LinkedHashMap<>(active);
                temp.put(id, next.get(id));
                int idx = indexOf(temp.keySet(), id);
                emit(now, MotionEvent.ACTION_POINTER_DOWN | (idx << MotionEvent.ACTION_POINTER_INDEX_SHIFT), temp);
                active.clear(); active.putAll(temp);
            }

            // Update positions for all currently active pointers.
            active.clear(); active.putAll(next);
            if (!active.isEmpty()) emit(now, MotionEvent.ACTION_MOVE, active);
            return true;
        } catch (Throwable t) {
            reset();
            return false;
        }
    }

    private static int indexOf(Collection<Integer> ids, int target) {
        int i=0; for (Integer id: ids) { if (id == target) return i; i++; }
        return 0;
    }

    private static void emit(long eventTime, int action, Map<Integer, Point> points) throws Exception {
        int count = points.size();
        if (count <= 0) return;
        MotionEvent.PointerProperties[] props = new MotionEvent.PointerProperties[count];
        MotionEvent.PointerCoords[] coords = new MotionEvent.PointerCoords[count];
        int i=0;
        for (Map.Entry<Integer,Point> e : points.entrySet()) {
            MotionEvent.PointerProperties pp = new MotionEvent.PointerProperties();
            pp.id = e.getKey();
            pp.toolType = MotionEvent.TOOL_TYPE_FINGER;
            props[i] = pp;
            MotionEvent.PointerCoords pc = new MotionEvent.PointerCoords();
            pc.x = e.getValue().x;
            pc.y = e.getValue().y;
            pc.pressure = Math.max(0.0f, Math.min(1.0f, e.getValue().pressure));
            pc.size = 1.0f;
            coords[i] = pc;
            i++;
        }
        MotionEvent ev = MotionEvent.obtain(
                downTime == 0L ? eventTime : downTime,
                eventTime,
                action,
                count,
                props,
                coords,
                0, 0,
                1.0f, 1.0f,
                0, 0,
                InputDevice.SOURCE_TOUCHSCREEN,
                0
        );
        inject(ev);
    }

    private static void reset() {
        try {
            if (!active.isEmpty()) {
                long now = android.os.SystemClock.uptimeMillis();
                emit(now, MotionEvent.ACTION_CANCEL, active);
            }
        } catch (Throwable ignored) { }
        active.clear();
        downTime = 0L;
    }
}
