#!/usr/bin/env python3
"""CHASE v0.4 live browser verification.
Requires a running local WebGL server on port 8765 and Chrome launched with
--remote-debugging-port=9222; Python websocket-client installed. Replays
focused browser key events, checks no scrolling, records rendered screenshots.
"""
import json,urllib.request,websocket,time,base64,hashlib,os
from pathlib import Path
ROOT = Path(__file__).resolve().parents[2]
pages=json.load(urllib.request.urlopen('http://127.0.0.1:9222/json'))
p=next(x for x in pages if x.get('type')=='page' and '127.0.0.1:8765' in x.get('url',''))
w=websocket.create_connection(p['webSocketDebuggerUrl'],timeout=16,suppress_origin=True)
seq=0;events=[]
def cmd(method,params=None):
 global seq
 seq+=1;w.send(json.dumps({'id':seq,'method':method,'params':params or {}}))
 while True:
  v=json.loads(w.recv())
  if 'method' in v:
   events.append(v);continue
  if v.get('id')==seq:
   if 'error' in v:raise RuntimeError(str(v['error']))
   return v.get('result',{})
def js(expression):
 out=cmd('Runtime.evaluate',{'expression':expression,'returnByValue':True})
 if 'exceptionDetails' in out:raise RuntimeError(str(out['exceptionDetails']))
 return out.get('result',{}).get('value')
def shot(name):
 data=cmd('Page.captureScreenshot',{'format':'png','captureBeyondViewport':False})['data']
 path=Path(str(ROOT / 'docs/chase-v0.4/evidence'))/name
 payload=base64.b64decode(data);path.write_bytes(payload)
 print('SHOT',name,len(payload),hashlib.sha256(payload).hexdigest()[:14],flush=True)
key_codes={'w':('KeyW',87),'a':('KeyA',65),'d':('KeyD',68),'r':('KeyR',82),'Tab':('Tab',9),' ':('Space',32),'ArrowUp':('ArrowUp',38),'ArrowDown':('ArrowDown',40),'Shift':('ShiftLeft',16)}
def key(name,down):
 code,num=key_codes[name]
 params={'type':'keyDown' if down else 'keyUp','key':name,'code':code,
        'windowsVirtualKeyCode':num,'nativeVirtualKeyCode':num}
 if name in ('w','a','d','r',' '):params['text']=name if down else ''
 if name=='Shift':params['modifiers']=8 if down else 0
 cmd('Input.dispatchKeyEvent',params)
def tap(name,hold=.055):key(name,True);time.sleep(hold);key(name,False)
def restart():
 tap('r');time.sleep(3.18)

cmd('Runtime.enable');cmd('Log.enable');cmd('Page.bringToFront')
url='http://127.0.0.1:8765/?build=v04-verified-fullscreen-20261011'
cmd('Page.navigate',{'url':url})
for i in range(70):
 try:
  info=js('({ready:document.readyState,loader:document.querySelector("#unity-loading-bar")?.style.display,canvas:document.querySelector("canvas")?.getBoundingClientRect().toJSON(),focus:document.activeElement?.id,scrollY:scrollY})')
  if info and info.get('loader')=='none' and info.get('ready')=='complete':break
 except Exception:pass
 time.sleep(.25)
else:raise RuntimeError('Game never completed loading')
js('document.querySelector("#unity-canvas").focus({preventScroll:true});window.__chaseKeyEvidence=[];document.addEventListener("keydown",e=>{if([" ","ArrowUp","ArrowDown"].includes(e.key))window.__chaseKeyEvidence.push({key:e.key,prevented:e.defaultPrevented,focused:document.activeElement.id,scrollY:scrollY})},false);true')
info=js('({canvas:document.querySelector("canvas").getBoundingClientRect().toJSON(),inner:[innerWidth,innerHeight],focus:document.activeElement?.id,scrollY:scrollY})')
print('READY',info,flush=True)
assert info['focus']=='unity-canvas' and info['canvas']['width']>=1700 and info['canvas']['height']>=850
time.sleep(.40)
shot('webgl-v04-arena.png')
# Every action is an input routed through the actual freshly built Chrome player.
restart();key('w',True);time.sleep(.31);shot('webgl-v04-running.png')
tap(' ');time.sleep(.11);shot('webgl-v04-running-jump.png');key('w',False)
restart();key('w',True);time.sleep(.27);tap('ArrowUp');time.sleep(.13)
shot('webgl-v04-forward-leap.png');key('w',False);time.sleep(.32);shot('webgl-v04-leap-recovery.png')
restart();key('w',True);time.sleep(.35);tap('ArrowDown');time.sleep(.12)
shot('webgl-v04-power-slide.png');key('w',False)
restart();key('Shift',True);key('w',True);time.sleep(.31)
shot('webgl-v04-crouch.png');key('w',False);key('Shift',False)
# Simultaneous five-input regression chords with no browser key scrolling.
restart();key('w',True);key('a',True);tap('ArrowUp');key('a',False);key('w',False)
restart();key('w',True);key('d',True);tap('ArrowDown');key('d',False);key('w',False)
checks=js('window.__chaseKeyEvidence')
state=js('({scrollY:window.scrollY,scrollX:window.scrollX,focus:document.activeElement?.id,canvas:document.querySelector("canvas").getBoundingClientRect().toJSON(),loader:document.querySelector("#unity-loading-bar").style.display})')
errors=[{'level':x['params']['entry']['level'],'text':x['params']['entry'].get('text','')} for x in events if x.get('method')=='Log.entryAdded' and x.get('params',{}).get('entry',{}).get('level')=='error']
print('KEY_EVENTS',checks,flush=True)
print('FINAL_STATE',state,flush=True)
print('BROWSER_ERRORS',errors[:15],flush=True)
assert state['scrollY']==0 and state['scrollX']==0 and state['focus']=='unity-canvas'
assert len(checks)>=5 and all(x.get('prevented') and x.get('focused')=='unity-canvas' for x in checks)
result={'url':url,'ready':info,'checks':checks,'after':state,'browserErrorEntries':errors,'captureDate':'2026-10-11','notes':'CDP sends actual Chrome trusted keyboard events to focused Unity WebGL canvas; time intervals are wall seconds.'}
Path(str(ROOT / 'docs/chase-v0.4/browser-input-verification.json')).write_text(json.dumps(result,indent=2)+'\n')
print('CHROME_V04_KEYBOARD_FOCUS_AND_SCROLL_PASS',len(checks),flush=True)
w.close()
