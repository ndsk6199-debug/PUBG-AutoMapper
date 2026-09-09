from __future__ import annotations
import json
import socket
import subprocess
import threading
import time
import ctypes
from pathlib import Path
from pynput import keyboard, mouse

BASE = Path(__file__).resolve().parent
CFG = json.loads((BASE / "config.json").read_text(encoding="utf-8"))
PROFILE = json.loads((BASE / "profile.json").read_text(encoding="utf-8"))

class AgentConnection:
    def __init__(self, adb_path: str, serial: str, port: int = 27183):
        self.adb = adb_path
        self.serial = serial
        self.port = port
        self.sock: socket.socket | None = None
        self.lock = threading.Lock()

    def connect(self):
        p = subprocess.run([self.adb, "-s", self.serial, "reverse", f"tcp:{self.port}", "localabstract:pubg_automapper"],
                           capture_output=True, text=True, timeout=8,
                           creationflags=getattr(subprocess, "CREATE_NO_WINDOW", 0))
        if p.returncode:
            raise RuntimeError(p.stderr.strip() or "adb reverse failed")
        self.sock = socket.create_connection(("127.0.0.1", self.port), timeout=3)
        self.sock.settimeout(3)
        if self.command("PING") != "OK":
            raise RuntimeError("Android agent did not answer PING")

    def command(self, line: str) -> str:
        with self.lock:
            if self.sock is None:
                raise RuntimeError("Agent not connected")
            self.sock.sendall((line + "\n").encode("utf-8"))
            data = bytearray()
            while not data.endswith(b"\n"):
                chunk = self.sock.recv(128)
                if not chunk:
                    raise RuntimeError("Android agent closed the connection")
                data.extend(chunk)
            return data.decode("utf-8", errors="replace").strip()

    def frame(self, pointers: list[dict], duration_ms: int = 60) -> bool:
        payload = json.dumps({"durationMs": duration_ms, "pointers": pointers}, separators=(",", ":"))
        return self.command("FRAME " + payload) == "OK"

    def reset(self):
        try: self.command("RESET")
        except Exception: pass

    def close(self):
        self.reset()
        try:
            if self.sock: self.sock.close()
        except Exception: pass
        self.sock = None

class Mapper:
    def __init__(self):
        self.serial = CFG["device_serial"]
        self.adb = AgentConnection(CFG["adb_path"], self.serial)
        self.adb.connect()
        self.running = True
        self.mouse_capture = False
        self.held = set()
        self.buttons = set()
        self.lock = threading.Lock()
        self.look_dx = self.look_dy = 0.0
        self.mouse_lock = threading.Lock()
        self.last_mouse = None
        self.cursor = mouse.Controller()
        self.capture_pos = None
        self.hud = PROFILE["hud"]
        self.j = CFG["joystick"]
        self.m = CFG["mouse_look"]
        self.kb = keyboard.Listener(on_press=self.key_down, on_release=self.key_up)
        self.ms = mouse.Listener(on_move=self.mouse_move, on_click=self.mouse_click)

    def p(self, name):
        v = self.hud[name]
        return {"x": float(v["x"]), "y": float(v["y"])}

    def set_mouse_hidden(self, hidden: bool):
        # ShowCursor is process/global; restore on shutdown.
        ctypes.windll.user32.ShowCursor(not hidden)

    def toggle_capture(self):
        self.mouse_capture = not self.mouse_capture
        if self.mouse_capture:
            self.capture_pos = self.cursor.position
            self.last_mouse = self.capture_pos
            self.set_mouse_hidden(True)
            print("Mouse Look: ON (F8 to release)")
        else:
            self.set_mouse_hidden(False)
            self.last_mouse = None
            print("Mouse Look: OFF")

    def key_name(self, key):
        ch = getattr(key, "char", None)
        if ch: return ch.lower()
        return {
            keyboard.Key.space:"space", keyboard.Key.shift:"shift",
            keyboard.Key.tab:"tab", keyboard.Key.f8:"f8", keyboard.Key.esc:"escape"
        }.get(key)

    def key_down(self, key):
        name=self.key_name(key)
        if name == "f8":
            self.toggle_capture(); return
        if name in {"w","a","s","d"}:
            with self.lock: self.held.add(name); return
        action = CFG["keys"].get(name)
        if action:
            try:
                self.adb.frame([{"id":"tap", **self.p(action)}], 40)
                self.adb.reset()
            except Exception as e: print("Agent:",e)
        elif name == "escape":
            self.running=False

    def key_up(self, key):
        name=self.key_name(key)
        if name in {"w","a","s","d"}:
            with self.lock: self.held.discard(name)

    def mouse_click(self,x,y,button,pressed):
        with self.lock:
            if button == mouse.Button.left: key="fire"
            elif button == mouse.Button.right: key="ads"
            else: return
            if pressed: self.buttons.add(key)
            else: self.buttons.discard(key)

    def mouse_move(self,x,y):
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

    def build_pointers(self):
        pointers=[]
        with self.lock:
            keys=set(self.held); buttons=set(self.buttons)
        if keys:
            dx=int("d" in keys)-int("a" in keys)
            dy=int("s" in keys)-int("w" in keys)
            mag=(dx*dx+dy*dy)**0.5
            if mag: dx,dy=dx/mag,dy/mag
            pointers.append({"id":"joy","x":self.j["x"]+dx*self.j["radius"],"y":self.j["y"]+dy*self.j["radius"]})
        if self.mouse_capture:
            with self.mouse_lock:
                dx,dy=self.look_dx,self.look_dy
                self.look_dx=self.look_dy=0
            maxd=self.m["max_delta"]
            dx=max(-maxd,min(maxd,dx)); dy=max(-maxd,min(maxd,dy))
            sx=max(.02,min(.98,self.m["anchor_x"]+dx*self.m["scale"]))
            sy=max(.02,min(.98,self.m["anchor_y"]+dy*self.m["scale"]))
            pointers.append({"id":"look","x":sx,"y":sy})
        for b in buttons:
            pointers.append({"id":b, **self.p(b)})
        return pointers

    def frame_loop(self):
        interval = 1 / max(10, min(30, int(CFG["frame_hz"])))
        while self.running:
            try:
                pointers=self.build_pointers()
                self.adb.frame(pointers, CFG["frame_duration_ms"])
            except Exception as e:
                print("Agent frame:",e)
                self.running=False
                break
            time.sleep(interval)

    def run(self):
        print("PUBG-AutoMapper ULTIMATE runtime")
        print(f"Android agent: {self.serial}")
        print("WASD | LMB Fire | RMB ADS | Space | C | Z | R | 1 | 2 | M | Tab | Shift")
        print("F8 = Mouse Look capture/release | ESC = stop")
        self.kb.start(); self.ms.start()
        threading.Thread(target=self.frame_loop, daemon=True).start()
        try:
            while self.running: time.sleep(.2)
        finally:
            self.kb.stop(); self.ms.stop(); self.set_mouse_hidden(False); self.adb.close()

if __name__ == "__main__":
    try: Mapper().run()
    except Exception as e:
        print("ERROR:",e)
        input("Press Enter to exit...")
