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

/** Shell-launched privileged input agent with stable multi-pointer frame state. */
public final class PrivilegedInputMain {
    private static final int PORT = 27184;
    private static final int INJECT_ASYNC = 0;
    private static Method injectMethod;
    private static Object inputManager;
    private static final LinkedHashMap<Integer, Point> active = new LinkedHashMap<>();
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
                    socket.setTcpNoDelay(true);
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
        try { return (Boolean) injectMethod.invoke(inputManager, event, INJECT_ASYNC); }
        finally { event.recycle(); }
    }

    private static void handle(Socket socket) throws Exception {
        BufferedReader in = new BufferedReader(new InputStreamReader(socket.getInputStream()));
        BufferedWriter out = new BufferedWriter(new OutputStreamWriter(socket.getOutputStream()));
        String line;
        while ((line=in.readLine()) != null) {
            String cmd=line.trim();
            boolean ok;
            if (cmd.equals("PING")) ok=true;
            else if (cmd.equals("RESET")) { reset(); ok=true; }
            else if (cmd.startsWith("FRAME ")) ok=applyFrame(cmd.substring(6));
            else if (cmd.startsWith("TAP ")) ok=applyTap(cmd.substring(4));
            else if (cmd.startsWith("SWIPE ")) ok=applySwipe(cmd.substring(6));
            else ok=false;
            out.write(ok ? "OK\n" : "ERR\n");
            out.flush();
        }
    }

    private static boolean applyTap(String payload) {
        try {
            String[] p=payload.trim().split("\\s+");
            if (p.length != 2) return false;
            float x=Float.parseFloat(p[0]), y=Float.parseFloat(p[1]);
            if (!Float.isFinite(x)||!Float.isFinite(y)) return false;
            if (!active.isEmpty()) reset();
            LinkedHashMap<Integer,Point> down=new LinkedHashMap<>();
            down.put(4,new Point(x,y,1f));
            long now=android.os.SystemClock.uptimeMillis();
            downTime=now;
            if (!emit(now,MotionEvent.ACTION_DOWN,down)) return false;
            active.clear(); active.putAll(down);
            LinkedHashMap<Integer,Point> empty=new LinkedHashMap<>();
            if (!emit(now+20,MotionEvent.ACTION_UP,active)) return false;
            active.clear(); downTime=0L;
            return true;
        } catch(Throwable t){ reset(); return false; }
    }

    private static boolean applySwipe(String payload) {
        try {
            String[] p=payload.trim().split("\\s+");
            if (p.length != 5) return false;
            float x1=Float.parseFloat(p[0]), y1=Float.parseFloat(p[1]);
            float x2=Float.parseFloat(p[2]), y2=Float.parseFloat(p[3]);
            long duration=Math.max(1L,Math.min(1000L,Long.parseLong(p[4])));
            if (!Float.isFinite(x1)||!Float.isFinite(y1)||!Float.isFinite(x2)||!Float.isFinite(y2)) return false;
            if (!active.isEmpty()) reset();
            long start=android.os.SystemClock.uptimeMillis();
            downTime=start;
            LinkedHashMap<Integer,Point> down=new LinkedHashMap<>();
            down.put(4,new Point(x1,y1,1f));
            if (!emit(start,MotionEvent.ACTION_DOWN,down)) return false;
            active.clear(); active.putAll(down);
            long end=start+duration;
            LinkedHashMap<Integer,Point> moved=new LinkedHashMap<>();
            moved.put(4,new Point(x2,y2,1f));
            if (!emit(end,MotionEvent.ACTION_MOVE,moved)) return false;
            if (!emit(end+1,MotionEvent.ACTION_UP,moved)) return false;
            active.clear(); downTime=0L;
            return true;
        } catch(Throwable t){ reset(); return false; }
    }

    private static boolean applyFrame(String payload) {
        try {
            LinkedHashMap<Integer,Point> next=new LinkedHashMap<>();
            if (!payload.isEmpty()) {
                for(String token:payload.split(";")) {
                    String[] p=token.split(":");
                    if(p.length!=4) return false;
                    int id=Integer.parseInt(p[0]);
                    if(id<0||id>15) return false;
                    float x=Float.parseFloat(p[1]), y=Float.parseFloat(p[2]), pressure=Float.parseFloat(p[3]);
                    if(!Float.isFinite(x)||!Float.isFinite(y)||!Float.isFinite(pressure)) return false;
                    next.put(id,new Point(x,y,pressure));
                }
            }
            long now=android.os.SystemClock.uptimeMillis();
            if(active.isEmpty() && next.isEmpty()) return true;
            if(active.isEmpty()) {
                downTime=now;
                Iterator<Map.Entry<Integer,Point>> it=next.entrySet().iterator();
                Map.Entry<Integer,Point> first=it.next();
                LinkedHashMap<Integer,Point> one=new LinkedHashMap<>(); one.put(first.getKey(),first.getValue());
                if(!emit(now,MotionEvent.ACTION_DOWN,one)) return false;
                active.put(first.getKey(),first.getValue());
                while(it.hasNext()) {
                    Map.Entry<Integer,Point> e=it.next();
                    LinkedHashMap<Integer,Point> current=new LinkedHashMap<>(active); current.put(e.getKey(),e.getValue());
                    int idx=indexOf(current.keySet(),e.getKey());
                    if(!emit(now,MotionEvent.ACTION_POINTER_DOWN | (idx<<MotionEvent.ACTION_POINTER_INDEX_SHIFT),current)) return false;
                    active.put(e.getKey(),e.getValue());
                }
                return emit(now,MotionEvent.ACTION_MOVE,active);
            }
            if(next.isEmpty()) {
                ArrayList<Integer> ids=new ArrayList<>(active.keySet());
                while(ids.size()>1) {
                    int id=ids.remove(ids.size()-1);
                    LinkedHashMap<Integer,Point> current=new LinkedHashMap<>(active);
                    int idx=indexOf(current.keySet(),id);
                    if(!emit(now,MotionEvent.ACTION_POINTER_UP | (idx<<MotionEvent.ACTION_POINTER_INDEX_SHIFT),current)) return false;
                    active.remove(id);
                }
                if(!active.isEmpty()&&!emit(now,MotionEvent.ACTION_UP,active)) return false;
                active.clear(); downTime=0L; return true;
            }
            for(Integer id:new ArrayList<>(active.keySet())) {
                if(!next.containsKey(id)) {
                    LinkedHashMap<Integer,Point> current=new LinkedHashMap<>(active);
                    if(current.size()==1) { if(!emit(now,MotionEvent.ACTION_UP,current)) return false; }
                    else { int idx=indexOf(current.keySet(),id); if(!emit(now,MotionEvent.ACTION_POINTER_UP | (idx<<MotionEvent.ACTION_POINTER_INDEX_SHIFT),current)) return false; }
                    active.remove(id);
                }
            }
            for(Map.Entry<Integer,Point> e:next.entrySet()) {
                if(!active.containsKey(e.getKey())) {
                    LinkedHashMap<Integer,Point> current=new LinkedHashMap<>(active); current.put(e.getKey(),e.getValue());
                    if(current.size()==1) { downTime=now; if(!emit(now,MotionEvent.ACTION_DOWN,current)) return false; }
                    else { int idx=indexOf(current.keySet(),e.getKey()); if(!emit(now,MotionEvent.ACTION_POINTER_DOWN | (idx<<MotionEvent.ACTION_POINTER_INDEX_SHIFT),current)) return false; }
                    active.put(e.getKey(),e.getValue());
                }
            }
            active.clear(); active.putAll(next);
            return emit(now,MotionEvent.ACTION_MOVE,active);
        } catch(Throwable t){ reset(); return false; }
    }

    private static int indexOf(Collection<Integer> ids,int target){ int i=0; for(Integer id:ids){ if(id==target) return i; i++; } return -1; }

    private static boolean emit(long eventTime,int action,Map<Integer,Point> points)throws Exception{
        if(points.isEmpty()) return false;
        MotionEvent.PointerProperties[] props=new MotionEvent.PointerProperties[points.size()];
        MotionEvent.PointerCoords[] coords=new MotionEvent.PointerCoords[points.size()];
        int i=0;
        for(Map.Entry<Integer,Point> e:points.entrySet()){
            MotionEvent.PointerProperties pp=new MotionEvent.PointerProperties(); pp.id=e.getKey(); pp.toolType=MotionEvent.TOOL_TYPE_FINGER; props[i]=pp;
            MotionEvent.PointerCoords pc=new MotionEvent.PointerCoords(); pc.x=e.getValue().x; pc.y=e.getValue().y; pc.pressure=Math.max(0f,Math.min(1f,e.getValue().pressure)); pc.size=1f; coords[i]=pc; i++;
        }
        MotionEvent ev=MotionEvent.obtain(downTime==0L?eventTime:downTime,eventTime,action,points.size(),props,coords,0,0,1f,1f,0,0,InputDevice.SOURCE_TOUCHSCREEN,0);
        return inject(ev);
    }

    private static void reset(){
        try{ if(!active.isEmpty()) emit(android.os.SystemClock.uptimeMillis(),MotionEvent.ACTION_CANCEL,active); }catch(Throwable ignored){}
        active.clear(); downTime=0L;
    }
}
