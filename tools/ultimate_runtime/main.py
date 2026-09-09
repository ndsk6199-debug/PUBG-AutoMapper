from __future__ import annotations
import ctypes
import json
import re
import socket
import subprocess
import threading
import time
from pathlib import Path
from pynput import keyboard, mouse

BASE = Path(__file__).resolve().parent
CFG = json.loads((BASE / "config.json").read_text(encoding="utf-8"))
PROFILE = json.loads((BASE / "profile.json").read_text(encoding="utf-8"))

class AgentConnection:
    PORT = 27184

    def __init__(self, adb_path: str, serial: str):
        self.adb = adb_path
        self.serial = serial
        self.sock: socket.socket | None = None
        self.lock = threading.Lock()

    def _run_adb(self, *args, timeout=8):
        return subprocess.run([self.adb, *map(str, args)], capture_output=True, text=True,
                              timeout=timeout,
                              creationflags=getattr(subprocess, "CREATE_NO_WINDOW", 0))

    def connect(self):
        self._run_adb("-s", self.serial, "forward", "--remove", f"tcp:{self.PORT}")
        p = self._run_adb("-s", self.serial, "forward", f"tcp:{self.PORT}", f"tcp:{self.PORT}")
        if p.returncode:
            raise RuntimeError(p.stderr.strip() or "Could not create ADB forward tunnel")
        last_error = None
        for _ in range(20):
            try:
                self.sock = socket.create_connection(("127.0.0.1", self.PORT), timeout=1)
                self.sock.settimeout(2)
                if self.command("PING") == "OK":
                    return
                last_error = "Agent PING returned ERR"
            except OSError as exc:
                last_error = exc
                time.sleep(0.25)
        raise RuntimeError(f"Android Input Agent did not open/respond on port {self.PORT}: {last_error}")

    def command(self, line: str) -> str:
        with self.lock:
            if self.sock is None:
                raise RuntimeError("Agent not connected")
            self.sock.sendall((line + "\n").encode("utf-8"))
            data = bytearray()
            while not data.endswith(b"\n"):
                chunk = self.sock.recv(256)
                if not chunk:
                    raise RuntimeError("Android Input Agent closed the connection")
                data.extend(chunk)
            return data.decode("utf-8", errors="replace").strip()

    def frame(self, pointers: list[dict], duration_ms: int = 50) -> bool:
        payload = json.dumps({"durationMs": duration_ms, "pointers": pointers}, separators=(",", ":"))
        return self.command("FRAME " + payload) == "OK"

    def tap(self, x: float, y: float):
        return self.command(f"TAP {x:.2f} {y:.2f}") == "OK"

    def swipe(self, x1: float, y1: float, x2: float, y2: float, duration: int):
        return self.command(f"SWIPE {x1:.2f} {y1:.2f} {x2:.2f} {y2:.2f} {duration}") == "OK"

    def reset(self):
        try: self.command("RESET")
        except Exception: pass

    def close(self):
        self.reset()
        try:
            if self.sock: self.sock.close()
        finally:
            self.sock = None
        self._run_adb("-s", self.serial, "forward", "--remove", f"tcp:{self.PORT}")

class Mapper:
    def __init__(self):
        self.serial = CFG["device_serial"]
        self.adb_path = CFG["adb_path"]
        self.rotation = int(CFG.get("rotation", 1))
        self.width, self.height = self.get_screen_size()
        self.adb = AgentConnection(self.adb_path, self.serial)
        self.adb.connect()
        self.running = True
        self.mouse_capture = False
        self.held: set[str] = set()
        self.buttons: set[str] = set()
        self.lock = threading.Lock()
        self.look_dx = self.look_dy = 0.0
        self.look_nx = float(CFG["mouse_look"]["anchor_x"])
        self.look_ny = float(CFG["mouse_look"]["anchor_y"])
        self.mouse_lock = threading.Lock()
        self.last_mouse = None
        self.cursor = mouse.Controller()
        self.capture_pos = None
        self.hud = PROFILE["hud"]
        self.j = CFG["joystick"]
        self.m = CFG["mouse_look"]
        self.keys = CFG["keys"]
        self.kb = keyboard.Listener(on_press=self.key_down, on_release=self.key_up)
        self.ms = mouse.Listener(on_move=self.mouse_move, on_click=self.mouse_click)

    def get_screen_size(self):
        p = subprocess.run([self.adb_path, "-s", self.serial, "shell", "wm", "size"],
                           capture_output=True, text=True, timeout=6,
                           creationflags=getattr(subprocess, "CREATE_NO_WINDOW", 0))
        if p.returncode:
            raise RuntimeError(p.stderr.strip() or "Could not read Android screen size")
        m = re.findall(r"(\d+)x(\d+)", p.stdout)
        if not m:
            raise RuntimeError("Could not parse Android screen size")
        return tuple(map(int, m[-1]))

    def to_px(self, nx: float, ny: float):
        nx = max(0.0, min(1.0, float(nx)))
        ny = max(0.0, min(1.0, float(ny)))
        if self.width >= self.height:
            return nx * self.width, ny * self.height
        if self.rotation == 3:
            rx, ry = 1.0 - ny, nx
        elif self.rotation == 2:
            rx, ry = 1.0 - nx, 1.0 - ny
        else:
            rx, ry = ny, 1.0 - nx
        return rx * self.width, ry * self.height

    def point(self, name: str):
        v = self.hud[name]
        return self.to_px(v["x"], v["y"])

    def action_tap(self, name: str):
        if name not in self.hud: return
        x, y = self.point(name)
        try: self.adb.tap(x, y)
        except Exception as exc: print("Agent tap:", exc)

    def key_name(self, key):
        ch = getattr(key, "char", None)
        if ch: return ch.lower()
        return {keyboard.Key.space:"space", keyboard.Key.shift:"shift", keyboard.Key.tab:"tab",
                keyboard.Key.f8:"f8", keyboard.Key.esc:"escape"}.get(key)

    def key_down(self, key):
        name = self.key_name(key)
        if name == "f8": self.toggle_capture(); return
        if name in {"w","a","s","d"}:
            with self.lock: self.held.add(name)
            return
        action = self.keys.get(name)
        if action: self.action_tap(action)
        elif name == "escape": self.running = False

    def key_up(self, key):
        name = self.key_name(key)
        if name in {"w","a","s","d"}:
            with self.lock: self.held.discard(name)

    def mouse_click(self, x, y, button, pressed):
        if button == mouse.Button.left: action="fire"
        elif button == mouse.Button.right: action="ads"
        else: return
        with self.lock:
            if pressed: self.buttons.add(action)
            else: self.buttons.discard(action)

    def mouse_move(self, x, y):
        if not self.mouse_capture: return
        with self.mouse_lock:
            if self.last_mouse is None:
                self.last_mouse=(x,y); return
            self.look_dx += x-self.last_mouse[0]
            self.look_dy += y-self.last_mouse[1]
            self.last_mouse=(x,y)
        if self.capture_pos is not None:
            try: self.cursor.position=self.capture_pos
            except Exception: pass

    def toggle_capture(self):
        self.mouse_capture = not self.mouse_capture
        with self.mouse_lock:
            self.look_dx = self.look_dy = 0.0
            self.last_mouse = None
        self.look_nx = self.m["anchor_x"]
        self.look_ny = self.m["anchor_y"]
        if self.mouse_capture:
            self.capture_pos=self.cursor.position
            self.last_mouse=self.capture_pos
            ctypes.windll.user32.ShowCursor(False)
            print("Mouse Look: ON (F8 to release)")
        else:
            ctypes.windll.user32.ShowCursor(True)
            print("Mouse Look: OFF")

    def build_pointers(self):
        pointers=[]
        with self.lock:
            keys=set(self.held); buttons=set(self.buttons)

        if keys:
            dx=int("d" in keys)-int("a" in keys)
            dy=int("s" in keys)-int("w" in keys)
            mag=(dx*dx+dy*dy)**0.5
            if mag: dx,dy=dx/mag,dy/mag
            nx=max(.01,min(.99,self.j["x"]+dx*self.j["radius"]))
            ny=max(.01,min(.99,self.j["y"]+dy*self.j["radius"]))
            x,y=self.to_px(nx,ny)
            pointers.append({"id":0,"x":round(x,2),"y":round(y,2),"pressure":1.0})

        if self.mouse_capture:
            with self.mouse_lock:
                dx,dy=self.look_dx,self.look_dy
                self.look_dx=self.look_dy=0.0
            dx=max(-self.m["max_delta"],min(self.m["max_delta"],dx))
            dy=max(-self.m["max_delta"],min(self.m["max_delta"],dy))
            self.look_nx += (dx*self.m["scale"])/max(self.width,1)
            self.look_ny += (dy*self.m["scale"])/max(self.height,1)
            self.look_nx=max(.02,min(.98,self.look_nx))
            self.look_ny=max(.02,min(.98,self.look_ny))
            x,y=self.to_px(self.look_nx,self.look_ny)
            pointers.append({"id":1,"x":round(x,2),"y":round(y,2),"pressure":1.0})

        for action in buttons:
            if action not in self.hud: continue
            pid=2 if action=="fire" else 3
            x,y=self.point(action)
            pointers.append({"id":pid,"x":round(x,2),"y":round(y,2),"pressure":1.0})
        return pointers

    def frame_loop(self):
        hz=max(10,min(30,int(CFG.get("frame_hz",20))))
        while self.running:
            try: self.adb.frame(self.build_pointers(),int(CFG.get("frame_duration_ms",50)))
            except Exception as exc:
                print("Agent frame:",exc); self.running=False; break
            time.sleep(1.0/hz)

    def run(self):
        print("PUBG-AutoMapper FINAL runtime")
        print(f"Device: {self.serial} | raw screen: {self.width}x{self.height}")
        print("WASD | LMB Fire | RMB ADS | Space/C/Z/R | 1/2 | M | Tab | Shift")
        print("F8 = Mouse Look capture/release | ESC = stop")
        print("Android Input Agent: connected")
        self.kb.start(); self.ms.start()
        threading.Thread(target=self.frame_loop,daemon=True).start()
        try:
            while self.running: time.sleep(.2)
        finally:
            self.kb.stop(); self.ms.stop(); ctypes.windll.user32.ShowCursor(True); self.adb.close()

if __name__=="__main__":
    try: Mapper().run()
    except Exception as exc:
        print("ERROR:",exc)
        input("Press Enter to exit...")
