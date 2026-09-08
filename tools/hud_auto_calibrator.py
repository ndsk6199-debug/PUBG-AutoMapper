from __future__ import annotations
import argparse, json, math
from pathlib import Path
import cv2
import numpy as np

ZONES = {
    "Joystick": (0.18, 0.67, 0.18, 0.24),
    "Fire": (0.85, 0.75, 0.20, 0.28),
    "Aim Down Sights": (0.88, 0.48, 0.22, 0.30),
    "Jump": (0.92, 0.67, 0.18, 0.26),
    "Crouch": (0.84, 0.88, 0.20, 0.20),
    "Prone": (0.92, 0.90, 0.18, 0.18),
    "Reload": (0.78, 0.84, 0.24, 0.24),
    "Map": (0.92, 0.12, 0.18, 0.22),
    "Weapon 1": (0.44, 0.92, 0.20, 0.14),
    "Weapon 2": (0.55, 0.92, 0.20, 0.14),
    "First-Person View": (0.24, 0.84, 0.18, 0.18),
    "Inventory": (0.09, 0.90, 0.18, 0.18),
}

def crop(img, prior, scale=1.25):
    h,w=img.shape[:2]; cx,cy,rw,rh=prior
    x0=max(0,int((cx-rw*scale/2)*w)); y0=max(0,int((cy-rh*scale/2)*h))
    x1=min(w,int((cx+rw*scale/2)*w)); y1=min(h,int((cy+rh*scale/2)*h))
    return img[y0:y1,x0:x1],(x0,y0)

def circles(img, invert=False):
    g=cv2.cvtColor(img,cv2.COLOR_BGR2GRAY)
    g=cv2.GaussianBlur(g,(7,7),1.4)
    if invert:g=255-g
    c=cv2.HoughCircles(g,cv2.HOUGH_GRADIENT,dp=1.2,minDist=28,param1=90,param2=36,minRadius=16,maxRadius=100)
    return [] if c is None else [tuple(map(int,v)) for v in np.round(c[0]).astype(int)]

def choose(img, name, prior, invert=False):
    h,w=img.shape[:2]; c,off=crop(img,prior,1.35); best=None
    for x,y,r in circles(c,invert):
        gx,gy=x+off[0],y+off[1]; nx,ny=gx/w,gy/h
        dist=math.hypot(nx-prior[0],ny-prior[1])
        if dist>0.22: continue
        score=.78*max(0,1-dist/.22)+.22*min(1,r/70)
        if best is None or score>best[0]: best=(score,gx,gy,r)
    if best is None:return None
    s,x,y,r=best
    return {"x":x/w,"y":y/h,"confidence":round(max(0,min(1,s)),3),"method":"hough-circle"}

def detect(image):
    img=cv2.imread(str(image))
    if img is None: raise FileNotFoundError(image)
    out={}
    out["Joystick"]=choose(img,"Joystick",ZONES["Joystick"],True)
    for n,p in ZONES.items():
        if n=="Joystick":continue
        out[n]=choose(img,n,p,False)
    out={k:v for k,v in out.items() if v}
    return {
        "schemaVersion":2,"game":"PUBG Mobile",
        "source":{"image":Path(image).name,"width":img.shape[1],"height":img.shape[0]},
        "coordinateSystem":{"type":"normalized","origin":"top-left","xRange":[0,1],"yRange":[0,1]},
        "orientation":"landscape","detections":out,
        "calibration":{"mode":"automatic","confidenceThreshold":0.55,
                        "needsReview":[k for k,v in out.items() if v["confidence"]<0.55]}
    }

def main():
    ap=argparse.ArgumentParser(description="Automatic PUBG Mobile HUD coordinate detector")
    ap.add_argument("image"); ap.add_argument("-o","--output",default="hud_profile_auto.json"); ap.add_argument("--preview",default="hud_preview.png")
    a=ap.parse_args(); result=detect(a.image)
    Path(a.output).write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding="utf-8")
    img=cv2.imread(a.image); h,w=img.shape[:2]
    for name,p in result["detections"].items():
        x,y=int(p["x"]*w),int(p["y"]*h)
        cv2.circle(img,(x,y),22,(0,255,0),2); cv2.putText(img,name,(x+8,y-8),cv2.FONT_HERSHEY_SIMPLEX,.45,(0,255,0),1,cv2.LINE_AA)
    cv2.imwrite(a.preview,img)
    print(json.dumps(result,ensure_ascii=False,indent=2))

if __name__=="__main__": main()
