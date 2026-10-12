#!/usr/bin/env python3
"""Production WebGL smoke: focused native Chrome keys, gameplay images and error log.
Requires WebGL server 8765, Chrome debugger port 9226 and websocket-client.
"""
import base64,json,time,urllib.request,os,io
from pathlib import Path
import websocket
from PIL import Image,ImageStat
root=Path(__file__).resolve().parents[2]
out=root/'docs/chase-v0.9.0/evidence/production'
out.mkdir(exist_ok=True,parents=True)
cdp=int(os.getenv('CHASE_CDP_PORT','9226'))
pages=json.load(urllib.request.urlopen(f'http://127.0.0.1:{cdp}/json'))
p=next(x for x in pages if x.get('type')=='page' and ':8765/' in x.get('url',''))
ws=websocket.create_connection(p['webSocketDebuggerUrl'],timeout=16,suppress_origin=True)
i=0;events=[]
def cmd(m,params=None):
 global i
 i+=1;ws.send(json.dumps({'id':i,'method':m,'params':params or {}}))
 while True:
  value=json.loads(ws.recv())
  if value.get('method'):events.append(value)
  if value.get('id')==i:
   if 'error' in value:raise RuntimeError(value['error'])
   return value.get('result',{})
def js(expr):return cmd('Runtime.evaluate',{'expression':expr,'returnByValue':True}).get('result',{}).get('value')
def key(name,down):
 codes={'r':('KeyR',82),'w':('KeyW',87),'ArrowUp':('ArrowUp',38),'ArrowDown':('ArrowDown',40),' ':('Space',32)}
 c,n=codes[name];params={'type':'keyDown' if down else 'keyUp','key':name,'code':c,'windowsVirtualKeyCode':n,'nativeVirtualKeyCode':n}
 if down and len(name)==1:params['text']=name
 cmd('Input.dispatchKeyEvent',params)
def tap(name,delay=.07):
 key(name,True);time.sleep(delay);key(name,False)
def shot(name):
 path=out/(name+'.png')
 path.write_bytes(base64.b64decode(cmd('Page.captureScreenshot',{'format':'png','captureBeyondViewport':False})['data']))
 print('GAME_SCREEN',name,path.stat().st_size,flush=True)
cmd('Page.enable');cmd('Runtime.enable');cmd('Log.enable')
cmd('Network.enable')
cmd('Network.setCacheDisabled',{'cacheDisabled':True})
cmd('Page.navigate',{'url':'http://127.0.0.1:8765/?build=chase-v090-modular-production'})
cmd('Page.bringToFront')
for attempt in range(200):
 data=js('({ready:document.readyState,loader:document.querySelector("#unity-loading-bar")?.style.display,rect:document.querySelector("canvas")?.getBoundingClientRect().toJSON()})')
 if data and data['ready']=='complete' and data['loader']=='none':break
 time.sleep(.12)
else:raise RuntimeError('Game failed to load')
# Do not confuse a hidden loading bar with a rendered game scene: Unity's
# splash screen persists for several seconds afterward on cold browser loads.
render_ready=None
for attempt in range(180):
 pixels=base64.b64decode(cmd('Page.captureScreenshot',
     {'format':'png','captureBeyondViewport':False})['data'])
 frame=Image.open(io.BytesIO(pixels)).convert('RGB')
 w,h=frame.size
 lower=frame.crop((int(w*.2),int(h*.56),int(w*.84),int(h*.91)))
 deviation=sum(ImageStat.Stat(lower).stddev)/3
 render_ready={'attempt':attempt,'stddev':round(deviation,3)}
 if deviation>11:break
 time.sleep(.28)
else:raise RuntimeError('Production WebGL still on splash/blank screen')
print('PRODUCTION_RENDER_READY',render_ready,flush=True)
js('''(function(){document.querySelector('canvas').focus({preventScroll:true});
 window.__supermanKeys=[];
 if(!window.__prodSmokeListenerInstalled){window.__prodSmokeListenerInstalled=true;
 document.addEventListener('keydown',e=>{
 if(['ArrowUp','ArrowDown',' '].includes(e.key))window.__supermanKeys.push({key:e.key,prevented:e.defaultPrevented,focused:document.activeElement.id,scrollY:scrollY});
 },false);}
 return true})()''')
shot('production-fresh-arena')
tap('r');time.sleep(3.27)
tap('ArrowUp');time.sleep(.18);shot('production-real-headfirst');time.sleep(.29);shot('production-real-landing');time.sleep(.62);shot('production-real-recovery')
tap('ArrowDown');tap(' ')
status=js('({scrollX:scrollX,scrollY:scrollY,focus:document.activeElement.id,loading:document.querySelector("#unity-loading-bar")?.style.display,events:window.__supermanKeys,rect:document.querySelector("canvas").getBoundingClientRect().toJSON()})')
errors=[e['params']['entry'].get('text','') for e in events if e.get('method')=='Log.entryAdded' and e.get('params',{}).get('entry',{}).get('level')=='error']
print('BROWSER_STATE',status,flush=True)
print('BROWSER_ERRORS',errors,flush=True)
assert not errors
assert status['loading']=='none' and status['focus']=='unity-canvas'
assert status['scrollX']==status['scrollY']==0
assert len(status['events'])>=3 and all(e['prevented'] and e['focused']=='unity-canvas' for e in status['events'])
report={'url':js('location.href'),'loaded':data,'render_ready':render_ready,'post_test':status,'console_error_entries':errors}
(out/'browser-production-verification.json').write_text(json.dumps(report,indent=2)+'\n')
print('PRODUCTION_WEBGL_BROWSER_PASS')
ws.close()
