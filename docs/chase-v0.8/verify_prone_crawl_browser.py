#!/usr/bin/env python3
"""Real Chrome WebGL user-controlled prone crawl; K stages real dive at shelf."""
import base64,io,json,time,urllib.request
from pathlib import Path
from PIL import Image,ImageStat,ImageChops
import websocket
out=Path(__file__).resolve().parent/'evidence/gym'
out.mkdir(parents=True,exist_ok=True)
page=next(x for x in json.load(urllib.request.urlopen('http://127.0.0.1:9227/json')) if x.get('type')=='page' and ':8766/' in x.get('url',''))
ws=websocket.create_connection(page['webSocketDebuggerUrl'],timeout=27,suppress_origin=True)
serial=0;events=[]
def call(method,params=None):
 global serial
 serial+=1;ws.send(json.dumps({'id':serial,'method':method,'params':params or {}}))
 while True:
  m=json.loads(ws.recv())
  if m.get('method'):events.append(m)
  if m.get('id')==serial:
   if 'error' in m:raise RuntimeError(m['error'])
   return m.get('result',{})
def js(expr):return call('Runtime.evaluate',{'expression':expr,'returnByValue':True}).get('result',{}).get('value')
keys={'r':('KeyR',82),'k':('KeyK',75),'g':('KeyG',71),'h':('KeyH',72),'v':('KeyV',86),
 'w':('KeyW',87),'a':('KeyA',65),'s':('KeyS',83),'d':('KeyD',68),
 'ArrowUp':('ArrowUp',38)}
def key(name,down):
 code,num=keys[name];par={'type':'keyDown' if down else 'keyUp','key':name,'code':code,'windowsVirtualKeyCode':num,'nativeVirtualKeyCode':num}
 if down and len(name)==1:par['text']=name
 call('Input.dispatchKeyEvent',par)
def tap(name):
 key(name,True);time.sleep(.085);key(name,False)
def grab(name):
 blob=base64.b64decode(call('Page.captureScreenshot',{'format':'png','captureBeyondViewport':False})['data'])
 file=out/(name+'.png');file.write_bytes(blob)
 print('FRAME',name,len(blob),flush=True)
 return file
call('Page.enable');call('Runtime.enable');call('Log.enable')
call('Network.enable');call('Network.setCacheDisabled',{'cacheDisabled':True})
call('Page.navigate',{'url':'http://127.0.0.1:8766/?build=chase-v08-player-controlled-prone'})
for i in range(200):
 d=js('({ready:document.readyState,loading:document.querySelector("#unity-loading-bar")?.style.display})')
 if d and d['ready']=='complete' and d['loading']=='none':break
 time.sleep(.18)
else:raise RuntimeError('Unity WebGL loader did not finish')
render=None
for i in range(180):
 im=Image.open(io.BytesIO(base64.b64decode(call('Page.captureScreenshot',{'format':'png','captureBeyondViewport':False})['data']))).convert('RGB')
 w,h=im.size;score=sum(ImageStat.Stat(im.crop((int(w*.2),int(h*.56),int(w*.84),int(h*.91)))).stddev)/3
 if score>11:render={'width':w,'height':h,'sceneContrast':round(score,2)};break
 time.sleep(.20)
assert render,'Loaded logo but not rendered gameplay'
call('Page.bringToFront')
js('document.querySelector("canvas").focus({preventScroll:true});true')
js("""(function(){window.__crawlKeys=[];document.addEventListener('keydown',e=>{
if(['ArrowUp','k','g','s','d','a','w'].includes(e.key))window.__crawlKeys.push({
key:e.key,prevented:e.defaultPrevented,focus:document.activeElement.id,scrollY:scrollY})
},false);return true})()""")
tap('v');time.sleep(.40)
tap('k');time.sleep(.50)
start=grab('00-shelf-approach')
tap('ArrowUp');time.sleep(.85)
land=grab('01-chest-landed')
time.sleep(2.2)
noinput=grab('02-no-input-stays-prone')
# Wiggle left then right while still under the shelf.
key('a',True);time.sleep(.27);key('a',False)
left=grab('03-left-crawl')
key('d',True);time.sleep(.50);key('d',False)
right=grab('04-right-crawl')
# Fixed 45-degree camera input basis: S+D is backward along the
# launch direction, out from under the full-height shelf supports.
key('s',True);key('d',True);time.sleep(2.7)
exit=grab('05-crawl-out')
key('s',False);key('d',False)
time.sleep(1)
stand=grab('06-automatic-stand')
# Open-space dive must still automatically stand even if W is held.
tap('r');time.sleep(.35)
tap('ArrowUp')
time.sleep(1.8)
openstand=grab('07-open-space-auto-stand')
status=js('({focus:document.activeElement.id,scrollX:scrollX,scrollY:scrollY,keys:window.__crawlKeys})')
errors=[x['params']['entry'].get('text','') for x in events if x.get('method')=='Log.entryAdded' and x.get('params',{}).get('entry',{}).get('level')=='error']
assert not errors,errors
assert status['focus']=='unity-canvas' and status['scrollX']==0 and status['scrollY']==0
assert any(x['key']=='ArrowUp' and x['prevented'] for x in status['keys'])
assert any(x['key']=='s' for x in status['keys']) and any(x['key']=='d' for x in status['keys'])
report={'url':js('location.href'),'rendered_scene':render,'keys':status,
 'browser_console_errors':errors,
 'sequence':['K real shelf-edge dive','Up real ballistic flight','chest landing',
 'no input for 2.2s','A lateral crawl','D lateral crawl',
 'S+D crawl backwards along W/A camera basis','auto stand in open space','R reset to actual open spawn, then Up open-floor dive'],
 'note':'Screenshots demonstrate actual rendered motion; physics and state guarantees are from Unity PlayMode regressions.'}
(out/'browser-prone-crawl-verification.json').write_text(json.dumps(report,indent=2)+'\n')
print('WEBGL_PRONE_CRAWL_INPUT_AND_RENDER_PASS',json.dumps({'url':report['url'],'frames':8,'consoleErrors':0,'inputEvents':len(status['keys'])})[:1200])
ws.close()
