#!/usr/bin/env python3
"""Capture the REAL WebGL gym Superman dive in Chrome through trusted keyboard input.

Requires locally served Builds/ChaseAnimationGymWebGL on port 8766, Chrome with
--remote-debugging-port=9227, Python websocket-client and Pillow. In the gym,
G teleports the athlete to an aligned 3m shelf approach without bypassing any
movement physics. Up launches the same shared motor as the production match.
Screenshots and measured differences are saved to docs/chase-v0.5.1/evidence.
"""
import base64
import io
import hashlib
import json
import os
import time
import urllib.request
from pathlib import Path
from PIL import Image, ImageChops, ImageStat
import websocket

root=Path(__file__).resolve().parents[2]
side_view=os.getenv('CHASE_SIDE_VIEW','0')=='1'
directory=root/'docs/chase-v0.5.1/evidence' / ('sideview' if side_view else '.')
directory.mkdir(parents=True,exist_ok=True)
cdp=int(os.getenv('CHASE_CDP_PORT','9227'))
game=os.getenv('CHASE_GAME_PORT','8766')
pages=json.load(urllib.request.urlopen(f'http://127.0.0.1:{cdp}/json'))
page=next(x for x in pages if x.get('type')=='page' and f':{game}/' in x.get('url',''))
ws=websocket.create_connection(page['webSocketDebuggerUrl'],suppress_origin=True,timeout=20)
seq=0
logs=[]
def request(method,params=None):
    global seq
    seq+=1
    ws.send(json.dumps({'id':seq,'method':method,'params':params or {}}))
    while True:
        item=json.loads(ws.recv())
        if item.get('method'):
            logs.append(item)
        if item.get('id')==seq:
            if 'error' in item:
                raise RuntimeError(item['error'])
            return item.get('result',{})
def evaluate(expression):
    result=request('Runtime.evaluate',{'expression':expression,'returnByValue':True})
    if 'exceptionDetails' in result:
        raise RuntimeError(str(result['exceptionDetails']))
    return result.get('result',{}).get('value')
def key(name,is_down):
    code,n={'g':('KeyG',71),'r':('KeyR',82),'w':('KeyW',87),'a':('KeyA',65),'d':('KeyD',68),'s':('KeyS',83),'v':('KeyV',86),'ArrowUp':('ArrowUp',38)}[name]
    data={'type':'keyDown' if is_down else 'keyUp','key':name,'code':code,
          'windowsVirtualKeyCode':n,'nativeVirtualKeyCode':n}
    if len(name)==1 and is_down:
        data['text']=name
    request('Input.dispatchKeyEvent',data)
def tap(name):
    key(name,True)
    time.sleep(.055)
    key(name,False)
def capture(name):
    result=request('Page.captureScreenshot',{'format':'png','captureBeyondViewport':False})
    blob=base64.b64decode(result['data'])
    file=directory/f'{name}.png'
    file.write_bytes(blob)
    print('SCREENSHOT',name,file.stat().st_size,hashlib.sha256(blob).hexdigest()[:12],flush=True)
    return file

request('Page.enable')
request('Runtime.enable')
request('Log.enable')
request('Page.bringToFront')
for attempt in range(200):
    loaded=evaluate('({ready:document.readyState,loading:document.querySelector("#unity-loading-bar")?.style.display,canvas:document.querySelector("canvas")?.getBoundingClientRect().toJSON()})')
    if loaded and loaded['ready']=='complete' and loaded['loading']=='none':
        break
    time.sleep(.15)
else:
    raise RuntimeError('WebGL gym never completed loading')

# Hiding the Unity loader does NOT imply the game has rendered its first frame.
# On a cold Chrome reload Unity often shows its logo/splash for several seconds
# after loader.style.display is "none". Catch this rather than collecting
# convincing-looking but false positive screenshot differences.
render_test = None
for attempt in range(180):
    preview = base64.b64decode(request('Page.captureScreenshot',
        {'format':'png','captureBeyondViewport':False})['data'])
    frame = Image.open(io.BytesIO(preview)).convert('RGB')
    w,h=frame.size
    lower_scene = frame.crop((int(w*.2),int(h*.56),int(w*.84),int(h*.91)))
    deviation = sum(ImageStat.Stat(lower_scene).stddev)/3
    render_test = {'attempt':attempt,'lower_scene_rgb_stddev':round(deviation,3)}
    if deviation>11.0:
        break
    time.sleep(.28)
else:
    raise RuntimeError('WebGL stayed on Unity splash or blank canvas; did not render gameplay')
print('RENDERED_GAMEPLAY_READY',render_test,flush=True)

# The focused Unity canvas, not Chrome navigation or JavaScript synthetic events,
# receives the actual CDP input dispatch. The key listener observes browser
# defaultPrevention after Unity handles the key event.
evaluate('''(function(){
  window.__diveInputs=[];
  document.querySelector("canvas").focus({preventScroll:true});
  if(!window.__chaseDiveInputListenerInstalled){
    window.__chaseDiveInputListenerInstalled=true;
    document.addEventListener('keydown',e=>{
      if(['ArrowUp','w','a','d','s'].includes(e.key))window.__diveInputs.push({key:e.key,
        prevented:e.defaultPrevented,focus:document.activeElement.id,scroll:scrollY});
    },false);
  }
  return true;
})()''')
assert evaluate('document.activeElement.id')=='unity-canvas'
if side_view:
    tap('v')
    time.sleep(.9)
tap('g')
time.sleep(.70)
idle=capture('00-shelf-ready-idle')
key('w',True)
time.sleep(.22)
tap('ArrowUp')
start=time.monotonic()
frames=[('01-takeoff',.08),('02-superman-flight',.25),('03-shelf-crossing',.39),
        ('04-chest-contact',.60),('05-prone-impact',.75),('06-getup',1.03),
        ('07-recovery',1.25),('08-standing-idle',2.05)]
shots={}
for name,offset in frames:
    until=start+offset
    if time.monotonic()<until:
        time.sleep(until-time.monotonic())
    shots[name]=capture(name)
    if name=='01-takeoff':key('a',True)
    if name=='02-superman-flight':key('a',False);key('d',True)
    if name=='03-shelf-crossing':key('d',False);key('s',True)
    if name=='04-chest-contact':key('s',False)
key('w',False)
end=evaluate('({focus:document.activeElement.id,scrollX:scrollX,scrollY:scrollY,inputs:window.__diveInputs,loading:document.querySelector("#unity-loading-bar")?.style.display,rect:document.querySelector("canvas")?.getBoundingClientRect().toJSON()})')
print('BROWSER_FINAL',end,flush=True)
assert end['focus']=='unity-canvas' and end['scrollX']==0 and end['scrollY']==0
assert any(e['key']=='ArrowUp' for e in end['inputs']) and all(e['focus']=='unity-canvas' for e in end['inputs']),end['inputs']
errors=[m['params']['entry'].get('text','') for m in logs if m.get('method')=='Log.entryAdded' and m.get('params',{}).get('entry',{}).get('level')=='error']
assert not errors,errors

def metric(before,after):
    a=Image.open(before).convert('RGB')
    b=Image.open(after).convert('RGB')
    assert a.size==b.size
    rect=(int(a.width*.28),int(a.height*.30),int(a.width*.74),int(a.height*.82))
    difference=ImageChops.difference(a.crop(rect),b.crop(rect))
    changed=sum(max(pixel)>32 for pixel in difference.getdata())
    return {'changed_pixels_gt_32':changed,'mean_absolute_rgb':round(sum(ImageStat.Stat(difference).mean)/3,3),'crop':rect}
metrics={name:metric(idle,path) for name,path in shots.items()}
print('PIXEL_DIFFERENCES',metrics,flush=True)
# Webcam/canvas motion alone does not prove pose correctness. The screenshots
# must be examined alongside the Unity imported-vertex/collider tests and HUD.
assert metrics['02-superman-flight']['changed_pixels_gt_32']>1000
assert metrics['05-prone-impact']['changed_pixels_gt_32']>1000
report={'browser':page['url'],'loaded':loaded,'render_ready':render_test,'final':end,'browser_errors':errors,
        'pixel_differences':metrics,'capture_origin':'actual Chrome W held + Up launch; A,D,S adversarial input midflight', 'side_camera':side_view ,
        'notes':'Screenshot differences document rendered activity; PlayMode verified real state and full-body geometry.'}
(directory/'browser-superman-verification.json').write_text(json.dumps(report,indent=2)+'\n')
print('PROJECTILE_W_UP_BROWSER_PASS',len(shots),'screenshots',flush=True)
ws.close()
