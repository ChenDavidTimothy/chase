#!/usr/bin/env python3
"""Verify rendered animation deformation in the separately served CHASE WebGL gym.

Run the gym server with gzip headers on localhost:8766, and launch Chrome with
--remote-debugging-port=9224. Override via CHASE_CDP_PORT and CHASE_GAME_PORT.
Requires Python websocket-client and Pillow on the development host.
This sends real browser key events and saves the observed pixels as evidence.
"""
import os
import json,urllib.request,websocket,base64,time
from pathlib import Path
from PIL import Image,ImageChops,ImageStat
cdp_port = int(os.getenv('CHASE_CDP_PORT', '9224'))
game_port = os.getenv('CHASE_GAME_PORT', '8766')
root = Path(__file__).resolve().parents[2]
output_dir = root / 'docs/chase-v0.4/animation-repair-evidence'
output_dir.mkdir(parents=True, exist_ok=True)
page=next(x for x in json.load(urllib.request.urlopen(f'http://127.0.0.1:{cdp_port}/json')) if x.get('type')=='page' and f':{game_port}/' in x.get('url',''))
ws=websocket.create_connection(page['webSocketDebuggerUrl'],timeout=15,suppress_origin=True)
i=0;messages=[]
def cmd(m,p=None):
 global i
 i+=1;ws.send(json.dumps({'id':i,'method':m,'params':p or {}}))
 while True:
  x=json.loads(ws.recv())
  if 'method' in x:messages.append(x)
  if x.get('id')==i:
   if 'error' in x:raise RuntimeError(x['error'])
   return x.get('result',{})
def js(expr):return cmd('Runtime.evaluate',{'expression':expr,'returnByValue':True}).get('result',{}).get('value')
def key(k,down):
 mp={'Shift':('ShiftLeft',16),'ArrowDown':('ArrowDown',40),'w':('KeyW',87),'r':('KeyR',82)}
 code,n=mp[k];a={'type':'keyDown' if down else 'keyUp','key':k,'code':code,'windowsVirtualKeyCode':n,'nativeVirtualKeyCode':n}
 if down and k in ('w','r'):a['text']=k
 if k=='Shift':a['modifiers']=8 if down else 0
 cmd('Input.dispatchKeyEvent',a)
def shot(n):
 out=output_dir/(n+'.png')
 out.write_bytes(base64.b64decode(cmd('Page.captureScreenshot',{'format':'png','captureBeyondViewport':False})['data']))
 print('SCREENSHOT',n,out.stat().st_size)
 return out
cmd('Page.enable');cmd('Runtime.enable');cmd('Log.enable');cmd('Page.bringToFront')
for count in range(100):
 ready=js('({loaded:document.querySelector("#unity-loading-bar")?.style.display,rect:document.querySelector("canvas")?.getBoundingClientRect().toJSON()})')
 if ready and ready['loaded']=='none':break
 time.sleep(.12)
js('document.querySelector("canvas").focus({preventScroll:true});true');time.sleep(.45)
print('GYM_READY',ready)
base=shot('gym-browser-idle')
key('Shift',True);time.sleep(.25);crouch=shot('gym-browser-crouch');key('Shift',False);time.sleep(.30)
idle=shot('gym-browser-idle-after-crouch')
key('ArrowDown',True);time.sleep(.18);slide=shot('gym-browser-slide');key('ArrowDown',False);time.sleep(.80)
postSlide=shot('gym-browser-idle-after-slide')
key('w',True);time.sleep(.18);runA=shot('gym-browser-run-A');time.sleep(.34);runB=shot('gym-browser-run-B');key('w',False);time.sleep(.4)
back=shot('gym-browser-idle-after-running')
def metric(left,right):
 a=Image.open(left).convert('RGB');b=Image.open(right).convert('RGB')
 # Measure the character zone only. The actor stands in the lower-middle of
 # the fixed follow camera, and stationary actions cannot alter floor physics.
 area=(int(a.width*.25),int(a.height*.32),int(a.width*.70),int(a.height*.82))
 d=ImageChops.difference(a.crop(area),b.crop(area))
 count=sum(1 for rgb in d.getdata() if max(rgb)>25)
 return {'changedPixelsGt25':count,'meanAbsoluteRgb':round(sum(ImageStat.Stat(d).mean)/3,3),'crop':area,'fullSize':a.size}
for name,x,y in [('idle_to_crouch',base,crouch),('idle_to_slide',idle,slide),('slide_to_idle',slide,postSlide),('run_time_A_vs_B',runA,runB),('idle_roundtrip',base,idle)]:
 print('VISUAL_DIFFERENCE',name,metric(x,y))
if metric(base,crouch)['changedPixelsGt25']<140:raise AssertionError('Crouch pose still looks neutral in browser')
if metric(idle,slide)['changedPixelsGt25']<140:raise AssertionError('Slide pose still looks neutral in browser')
errors=[x['params']['entry'].get('text','') for x in messages if x.get('method')=='Log.entryAdded' and x.get('params',{}).get('entry',{}).get('level')=='error']
assert not errors, errors
assert metric(base,idle)['changedPixelsGt25']==0, 'Character did not exactly return to Idle after crouch'
assert metric(idle,postSlide)['changedPixelsGt25']<100, 'Character did not return to Idle after slide recovery'
assert js('window.scrollY')==0, 'Browser scrolled during character movement'
report={name:metric(x,y) for name,x,y in [('idle_to_crouch',base,crouch),('idle_to_slide',idle,slide),('slide_to_idle',slide,postSlide),('run_A_vs_B',runA,runB),('idle_roundtrip',base,idle)]}
report['browserErrors']=errors
report['sourceUrl']=page['url']
(output_dir/'webgl-pose-metrics.json').write_text(json.dumps(report,indent=2)+'\n')
print('BROWSER_ERRORS',errors[:10]);print('PASS_BROWSER_ANIMATION_POSE_CHANGES')
ws.close()
