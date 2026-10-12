#!/usr/bin/env python3
"""Real Chrome replay of dive -> prone -> smooth automatic get-up.
No Unity scripting or synthetic animator state. Captures actual browser video frames.
"""
import base64,io,json,time,urllib.request
from pathlib import Path
from PIL import Image,ImageStat
import websocket
out=Path(__file__).resolve().parent/'evidence/gym'
out.mkdir(parents=True,exist_ok=True)
d=json.load(urllib.request.urlopen('http://127.0.0.1:9227/json'))
p=next(t for t in d if t.get('type')=='page')
ws=websocket.create_connection(p['webSocketDebuggerUrl'],timeout=30,suppress_origin=True)
cid=0;eventlist=[]
def call(method,params=None):
 global cid
 cid+=1
 ws.send(json.dumps({'id':cid,'method':method,'params':params or {}}))
 while True:
  msg=json.loads(ws.recv())
  if msg.get('method'):eventlist.append(msg)
  if msg.get('id')==cid:
   if 'error' in msg:raise RuntimeError(msg['error'])
   return msg.get('result',{})
def js(expr):
 d=call('Runtime.evaluate',{'expression':expr,'returnByValue':True})
 if d.get('exceptionDetails'):raise RuntimeError(str(d['exceptionDetails']))
 return d.get('result',{}).get('value')
def key(name,down):
 code,num={'v':('KeyV',86),'r':('KeyR',82),'k':('KeyK',75),'ArrowUp':('ArrowUp',38)}[name]
 params={'type':'keyDown' if down else 'keyUp','key':name,'code':code,
         'windowsVirtualKeyCode':num,'nativeVirtualKeyCode':num}
 if down and len(name)==1:params['text']=name
 call('Input.dispatchKeyEvent',params)
def tap(name):
 key(name,True);time.sleep(.08);key(name,False)
def shot(label):
 data=base64.b64decode(call('Page.captureScreenshot',{'format':'png','captureBeyondViewport':False})['data'])
 f=out/(label+'.png');f.write_bytes(data);print('FRAME',label,len(data),flush=True)
 return f
call('Page.enable');call('Runtime.enable');call('Log.enable')
call('Network.enable');call('Network.setCacheDisabled',{'cacheDisabled':True})
call('Page.navigate',{'url':'http://127.0.0.1:8766/?build=v081-smooth-dive-getup'})
for _ in range(200):
 d=js('({ready:document.readyState,loader:document.querySelector("#unity-loading-bar")?.style.display})')
 if d and d['ready']=='complete' and d['loader']=='none':break
 time.sleep(.15)
else:raise RuntimeError('WebGL loader was not dismissed')
for _ in range(150):
 im=Image.open(io.BytesIO(base64.b64decode(call('Page.captureScreenshot',{'format':'png','captureBeyondViewport':False})['data']))).convert('RGB')
 w,h=im.size
 if sum(ImageStat.Stat(im.crop((int(w*.2),int(h*.56),int(w*.84),int(h*.91)))).stddev)/3>11:break
 time.sleep(.16)
else:raise RuntimeError('Unity game not actually rendered')
call('Page.bringToFront')
js('document.querySelector("canvas").focus({preventScroll:true});true')
tap('v');time.sleep(.35)
tap('r');time.sleep(.4)
shot('00-open-idle')
key('ArrowUp',True);time.sleep(.08);key('ArrowUp',False)
start=time.monotonic()
# Capture dense real-frame intervals across the chest landing and recovery,
# rather than a shot after it has already finished.
for i in range(30):
 want=start+(.11+i*.054)
 if want>time.monotonic():time.sleep(want-time.monotonic())
 shot('smooth-%02d'%i)
status=js('({url:location.href,focus:document.activeElement.id,scrollX:scrollX,scrollY:scrollY})')
errors=[e['params']['entry'].get('text','') for e in eventlist
        if e.get('method')=='Log.entryAdded' and e.get('params',{}).get('entry',{}).get('level')=='error']
assert status['focus']=='unity-canvas' and status['scrollX']==0 and status['scrollY']==0
assert not errors,errors
(out/'browser-smooth-getup-verification.json').write_text(json.dumps({
 'browser':status,'consoleErrors':errors,'frames':30,
 'capture':'real Chrome sequential frames through Superman takeoff, chest landing and smooth get-up',
 'noSyntheticMotorState':True},indent=2)+'\n')
print('REAL_CHROME_SMOOTH_GETUP_CAPTURE_PASS',len(errors),'console errors',flush=True)
ws.close()
