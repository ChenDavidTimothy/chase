#!/usr/bin/env python3
"""Actual Unity WebGL repetitive dive recovery smoke (G shelf + H wall).
Requires the project's local server on 8766 and Chrome CDP on 9227.
Screenshots are human-review evidence; true motor state comes from Unity tests.
"""
import base64,io,json,time,urllib.request
from pathlib import Path
from PIL import Image,ImageStat
import websocket
out=Path(__file__).resolve().parent/'evidence'
out.mkdir(exist_ok=True)
p=next(x for x in json.load(urllib.request.urlopen('http://127.0.0.1:9227/json')) if x.get('type')=='page' and ':8766/' in x.get('url',''))
w=websocket.create_connection(p['webSocketDebuggerUrl'],timeout=25,suppress_origin=True)
i=0;events=[]
def cmd(method,parameters=None):
 global i
 i+=1;w.send(json.dumps({'id':i,'method':method,'params':parameters or {}}))
 while True:
  r=json.loads(w.recv())
  if r.get('method'):events.append(r)
  if r.get('id')==i:
   if 'error' in r:raise RuntimeError(r['error'])
   return r.get('result',{})
def js(expr):return cmd('Runtime.evaluate',{'expression':expr,'returnByValue':True}).get('result',{}).get('value')
def key(k,down):
 n={'g':('KeyG',71),'h':('KeyH',72),'ArrowUp':('ArrowUp',38),'v':('KeyV',86),'w':('KeyW',87)}[k];p={'type':'keyDown' if down else 'keyUp','key':k,'code':n[0],'windowsVirtualKeyCode':n[1],'nativeVirtualKeyCode':n[1]}
 if len(k)==1 and down:p['text']=k
 cmd('Input.dispatchKeyEvent',p)
def tap(k):key(k,True);time.sleep(.065);key(k,False)
def shot(name):
 blob=base64.b64decode(cmd('Page.captureScreenshot',{'format':'png','captureBeyondViewport':False})['data']);f=out/(name+'.png');f.write_bytes(blob);print('SHOT',name,len(blob),flush=True)
cmd('Page.enable');cmd('Runtime.enable');cmd('Log.enable');cmd('Network.enable');cmd('Network.setCacheDisabled',{'cacheDisabled':True})
cmd('Page.navigate',{'url':'http://127.0.0.1:8766/?build=chase-v07-rounded-dive-shelf-wall'})
for n in range(190):
 d=js('({ready:document.readyState,loading:document.querySelector("#unity-loading-bar")?.style.display})')
 if d and d['ready']=='complete' and d['loading']=='none':break
 time.sleep(.15)
else:raise RuntimeError('Not loaded')
for n in range(190):
 blob=base64.b64decode(cmd('Page.captureScreenshot',{'format':'png','captureBeyondViewport':False})['data'])
 im=Image.open(io.BytesIO(blob)).convert('RGB');width,height=im.size
 sample=im.crop((int(width*.2),int(height*.56),int(width*.84),int(height*.91)))
 if sum(ImageStat.Stat(sample).stddev)/3>11:break
 time.sleep(.22)
else:raise RuntimeError('Unity splash never finished')
cmd('Page.bringToFront');js('document.querySelector("canvas").focus({preventScroll:true});true')
js('''(function(){window.__diveKeys=[];document.addEventListener('keydown',e=>{if(['ArrowUp','g','h'].includes(e.key))window.__diveKeys.push({key:e.key,prevented:e.defaultPrevented,focus:document.activeElement.id,scrollY:scrollY})},false);return true})()''')
for stage in ['g','h']:
 for repeat in range(2):
  name=('shelf' if stage=='g' else 'wall')+'-'+str(repeat+1)
  tap(stage);time.sleep(.50)
  shot(name+'-before')
  tap('ArrowUp');time.sleep(.24);shot(name+'-launch')
  time.sleep(1.1);shot(name+'-impact')
  time.sleep(5.5);shot(name+'-after-7s')
status=js('({focus:document.activeElement.id,scrollY:scrollY,keys:window.__diveKeys})')
errors=[e['params']['entry'].get('text','') for e in events if e.get('method')=='Log.entryAdded' and e.get('params',{}).get('entry',{}).get('level')=='error']
assert not errors,errors
assert status['focus']=='unity-canvas' and status['scrollY']==0
assert sum(x['key']=='ArrowUp' and x['prevented'] for x in status['keys'])==4
(out/'browser-recovery-verification.json').write_text(json.dumps({'url':js('location.href'),'run':'fresh WebGL after v0.7 rounded-capsule release, G/H twice each, +7 seconds unassisted','state':status,'console_errors':errors,'screenshots':16},indent=2)+'\n')
print('BROWSER_REPEATED_DIVE_PASS',len(status['keys']),len(errors))
w.close()
