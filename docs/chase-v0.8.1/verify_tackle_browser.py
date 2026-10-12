#!/usr/bin/env python3
"""Real Chrome/WebGL football tackle verification, not synthetic DOM events."""
import base64,io,json,time,urllib.request
from pathlib import Path
from PIL import Image,ImageStat,ImageChops
import websocket
root=Path(__file__).resolve().parents[2]
out=root/'docs/chase-v0.8.1/evidence/tackle'
out.mkdir(parents=True,exist_ok=True)
pages=json.load(urllib.request.urlopen('http://127.0.0.1:9227/json'))
page=next(x for x in pages if x.get('type')=='page' and ':8766/' in x.get('url',''))
ws=websocket.create_connection(page['webSocketDebuggerUrl'],timeout=22,suppress_origin=True)
i=0
events=[]
def command(m,params=None):
 global i
 i+=1;ws.send(json.dumps({'id':i,'method':m,'params':params or {}}))
 while True:
  obj=json.loads(ws.recv())
  if obj.get('method'):events.append(obj)
  if obj.get('id')==i:
   if 'error' in obj:raise RuntimeError(obj['error'])
   return obj.get('result',{})
def js(expr):
 res=command('Runtime.evaluate',{'expression':expr,'returnByValue':True})
 if 'exceptionDetails' in res:raise RuntimeError(str(res['exceptionDetails']))
 return res.get('result',{}).get('value')
def key(name,down):
 keys={'j':('KeyJ',74),'v':('KeyV',86),'w':('KeyW',87),'a':('KeyA',65),'d':('KeyD',68),'ArrowDown':('ArrowDown',40)}
 code,num=keys[name]
 args={'type':'keyDown' if down else 'keyUp','key':name,'code':code,'windowsVirtualKeyCode':num,'nativeVirtualKeyCode':num}
 if len(name)==1 and down:args['text']=name
 command('Input.dispatchKeyEvent',args)
def tap(name):
 key(name,True);time.sleep(.075);key(name,False)
def grab(name):
 blob=base64.b64decode(command('Page.captureScreenshot',{'format':'png','captureBeyondViewport':False})['data'])
 f=out/(name+'.png');f.write_bytes(blob);print('FRAME',name,len(blob),flush=True);return f
command('Page.enable');command('Runtime.enable');command('Log.enable');command('Network.enable');command('Network.setCacheDisabled',{'cacheDisabled':True});command('Page.navigate',{'url':'http://127.0.0.1:8766/?build=v081-football-slide-regression'});command('Page.bringToFront')
for n in range(230):
 loaded=js('({ready:document.readyState,loader:document.querySelector("#unity-loading-bar")?.style.display})')
 if loaded and loaded['ready']=='complete' and loaded['loader']=='none':break
 time.sleep(.15)
else:raise RuntimeError('WebGL loader did not disappear')
render=None
for n in range(230):
 blob=base64.b64decode(command('Page.captureScreenshot',{'format':'png','captureBeyondViewport':False})['data'])
 im=Image.open(io.BytesIO(blob)).convert('RGB');w,h=im.size
 st=sum(ImageStat.Stat(im.crop((int(w*.2),int(h*.56),int(w*.84),int(h*.91)))).stddev)/3
 if st>11:render={'frame':n,'scene_contrast':round(st,3),'size':(w,h)};break
 time.sleep(.23)
assert render, 'Still on Unity splash'
js('''(function(){document.querySelector("canvas").focus({preventScroll:true});
 window.__slideKeys=[];
 document.addEventListener('keydown',e=>{
 if(['j','v','w','a','d','ArrowDown'].includes(e.key))
 window.__slideKeys.push({key:e.key,prevented:e.defaultPrevented,focus:document.activeElement.id,scroll:scrollY});
 },false); return true})()''')
assert js('document.activeElement.id')=='unity-canvas'
tap('v');time.sleep(.75)
tap('j');time.sleep(.38)
base=grab('00-running-approach')
key('w',True)
tap('ArrowDown')
start=time.monotonic()
frames=[('01-tackle-launch',.035),('02-reclined-low-slide',.13),('03-feet-first-under-canopy',.25),('04-slide-end',.42),('05-protected-getup',.52),('06-rising',.62),('07-running-again',.89),('08-standing-idle',1.23)]
files={}
for name,offset in frames:
 remaining=start+offset-time.monotonic()
 if remaining>0:time.sleep(remaining)
 if name=='02-reclined-low-slide':key('a',True)
 if name=='03-feet-first-under-canopy':key('a',False);key('d',True)
 if name=='04-slide-end':key('d',False)
 if name=='07-running-again':key('w',False)
 files[name]=grab(name)
key('w',False)
final=js('({focus:document.activeElement.id,x:scrollX,y:scrollY,keys:window.__slideKeys,loader:document.querySelector("#unity-loading-bar")?.style.display})')
errors=[e['params']['entry'].get('text','') for e in events if e.get('method')=='Log.entryAdded' and e.get('params',{}).get('entry',{}).get('level')=='error']
assert not errors,errors
assert final['focus']=='unity-canvas' and final['x']==0 and final['y']==0
assert any(x['key']=='ArrowDown' and x['prevented'] for x in final['keys']),final
def diff(a,b):
 aa=Image.open(a).convert('RGB');bb=Image.open(b).convert('RGB')
 d=ImageChops.difference(aa,bb)
 return sum(max(x)>35 for x in d.getdata())
pixels={k:diff(base,v) for k,v in files.items()}
assert pixels['02-reclined-low-slide']>1500,pixels
assert pixels['05-protected-getup']>1500,pixels
report={'url':js('location.href'),'rendered_scene':render,'keys_and_focus':final,'console_errors':errors,'changed_pixels_gt35':pixels,
 'proof':'real Chrome WebGL with J staging, W + Down takeoff, A/D directional spam during slide, no special motor',
 'not_proven_by_screenshots':'Exact pose/collision safety is proven separately by PhysX and skinned-mesh tests'}
(out/'browser-tackle-verification.json').write_text(json.dumps(report,indent=2)+'\n')
print('WEBGL_FOOTBALL_TACKLE_PASS',json.dumps({'rendered_scene':render,'inputs':len(final['keys']),'pixel_checks':pixels,'browser_errors':0})[:2000],flush=True)
ws.close()
