#!/usr/bin/env python3
"""Real Chrome mouse/keyboard replay against the published-menu WebGL build.

Requires static WebGL server :8765 and Chrome CDP :9246.
All menu transitions use physical pointer input, never Unity SendMessage.
"""
import base64,io,json,time,urllib.request
from pathlib import Path
import websocket
from PIL import Image,ImageChops,ImageStat

root=Path(__file__).resolve().parents[2]
out=root/'docs/chase-v0.17.0/evidence/webgl'
out.mkdir(parents=True,exist_ok=True)
port=9246
pages=json.load(urllib.request.urlopen(f'http://127.0.0.1:{port}/json'))
page=next(p for p in pages if p.get('type')=='page' and ':8765/' in p.get('url',''))
ws=websocket.create_connection(page['webSocketDebuggerUrl'],timeout=35,suppress_origin=True)
serial=0
events=[]
def command(method,args=None):
    global serial
    serial+=1
    ws.send(json.dumps({'id':serial,'method':method,'params':args or {}}))
    while True:
        data=json.loads(ws.recv())
        if 'method' in data:events.append(data)
        if data.get('id')==serial:
            if 'error' in data:raise RuntimeError(data['error'])
            return data.get('result',{})
def js(expr):
    r=command('Runtime.evaluate',{'expression':expr,'returnByValue':True})
    if r.get('exceptionDetails'):raise RuntimeError(r['exceptionDetails'])
    return r.get('result',{}).get('value')
def shot(name):
    data=base64.b64decode(command('Page.captureScreenshot',{
        'format':'png','captureBeyondViewport':False})['data'])
    path=out/(name+'.png');path.write_bytes(data)
    print('SCREEN',name,len(data),flush=True)
    return path
def diff(a,b):
    x=Image.open(a).convert('RGB');y=Image.open(b).convert('RGB')
    if x.size!=y.size:raise RuntimeError(('mismatched capture size',x.size,y.size))
    return sum(max(rgb)>35 for rgb in ImageChops.difference(x,y).getdata())
def geometry():
    return js('''(()=>{let a=document.querySelector('#unity-canvas').getBoundingClientRect();
      return {left:a.left,top:a.top,width:a.width,height:a.height,
        backingWidth:document.querySelector('#unity-canvas').width,
        backingHeight:document.querySelector('#unity-canvas').height}})()''')
def pointer(u,v):
    r=geometry()
    # u,v are the Unity HUD's reference 1280x720 coordinates, with
    # vertical centering corrections for non-16:9 canvases.
    k=min(r['width']/1280,r['height']/720)
    w=r['width']/k;h=r['height']/k
    x=r['left']+u*k
    y=r['top']+v(h)*k if callable(v) else r['top']+v*k
    for kind in ('mouseMoved','mousePressed','mouseReleased'):
        command('Input.dispatchMouseEvent',{'type':kind,'x':x,'y':y,
            'button':'left' if kind!='mouseMoved' else 'none',
            'clickCount':1 if kind!='mouseMoved' else 0})
    time.sleep(.23)
    return [round(x),round(y)]
def key(k,down):
    codes={'w':('KeyW',87),'ArrowUp':('ArrowUp',38),
        'ArrowDown':('ArrowDown',40),' ':('Space',32),
        'Escape':('Escape',27),
        'Enter':('Enter',13),'r':('KeyR',82),'Tab':('Tab',9)}
    code,num=codes[k]
    d={'type':'keyDown' if down else 'keyUp','key':k,'code':code,
       'nativeVirtualKeyCode':num,'windowsVirtualKeyCode':num}
    if down and len(k)==1:d['text']=k
    command('Input.dispatchKeyEvent',d)
def tap(k):
    key(k,True);time.sleep(.13);key(k,False)
def ypanel(offset):
    return lambda h:(h-630)*.5+offset
def assert_transition(a,b,name):
    changed=diff(a,b)
    print('TRANSITION',name,changed,flush=True)
    assert changed>3500,(name,changed)
    return changed

command('Page.enable')
command('Runtime.enable')
command('Log.enable')
command('Network.enable')
command('Network.setCacheDisabled',{'cacheDisabled':True})
command('Page.navigate',{'url':'http://127.0.0.1:8765/?build=v017-esport-movement'})
command('Page.bringToFront')
for _ in range(250):
    d=js('''({ready:document.readyState,
      loader:document.querySelector('#unity-loading-bar')?.style.display})''')
    if d and d['ready']=='complete' and d['loader']=='none':break
    time.sleep(.15)
else:raise RuntimeError('WebGL loading exceeded time limit')
# Unity may finish loading the WASM before its splash screen exits.
# The splash has no bright CHASE logotype in the centered menu region;
# require that distinct region in two consecutive screenshots.
consecutive=0
for attempt in range(210):
    frame=base64.b64decode(command('Page.captureScreenshot',{'format':'png'})['data'])
    image=Image.open(io.BytesIO(frame)).convert('RGB')
    w,h=image.size
    logo=image.crop((int(w*.32),int(h*.06),int(w*.67),int(h*.28)))
    bright=sum(1 for r,g,b in logo.getdata() if r>195 and g>190 and b>155)
    consecutive=consecutive+1 if bright>950 else 0
    if consecutive>=2:
        print('REAL_CHASE_MENU_READY',attempt,bright,flush=True)
        break
    time.sleep(.25)
else:raise RuntimeError('Unity splash did not transition to the CHASE menu')

js('''(()=>{
 window.__publicInputs=[];
 document.addEventListener('keydown',e=>{
  window.__publicInputs.push({key:e.key,focus:document.activeElement.id,
     prevented:e.defaultPrevented,scroll:window.scrollY});
 },false);
 return document.querySelector('#unity-canvas').focus({preventScroll:true})})()''')
assert js('document.activeElement.id')=='unity-canvas'
assert js('document.title')=='CHASE | Mastered Routes'
menu=shot('00-main-menu')
time.sleep(.85)
menuMoving=shot('00a-showcase-moving')
def outside_difference(a,b):
    one=Image.open(a).convert('RGB');two=Image.open(b).convert('RGB')
    width,height=one.size
    d=ImageChops.difference(one,two)
    left=d.crop((0,int(height*.10),int(width*.28),int(height*.90)))
    right=d.crop((int(width*.72),int(height*.10),width,int(height*.90)))
    return sum(max(p)>35 for p in left.getdata())+sum(max(p)>35 for p in right.getdata())
movement=outside_difference(menu,menuMoving)
print('MENU_RECORDING_REAL_MOTION',movement,flush=True)
assert movement>250,('Recorded backdrop did not move',movement)



clicks=[]
clicks.append(['how-to',pointer(363+34+109,ypanel(402+27))])
how=shot('01-how-to')
assert_transition(menu,how,'how-to')
clicks.append(['enter-practice',pointer(363+34+226,ypanel(526+29))])
time.sleep(.7)
practice=shot('02-practice-free')
assert_transition(how,practice,'practice entry')
key('w',True);time.sleep(.55);key('w',False)
run=shot('03-practice-sprint')
assert_transition(practice,run,'athlete sprint responds')
key(' ',True);time.sleep(.14);key(' ',False)
jump=shot('04-practice-jump')
assert_transition(run,jump,'athlete jumps')
time.sleep(.8)
tap('ArrowUp')
time.sleep(.14)
dive=shot('05-practice-dive')
assert_transition(jump,dive,'competitive dive movement')
time.sleep(1.3)
tap('ArrowDown')
slide=shot('06-practice-slide')
assert_transition(dive,slide,'feet-first slide movement')
clicks.append(['practice-direct-exit',pointer(1280-207+91,43)])
time.sleep(.35)
returned=shot('07-returned-menu')
assert_transition(slide,returned,'practice to main menu')
clicks.append(['start-match',pointer(363+34+226,ypanel(329+31))])
difficulty=shot('08-difficulty')
clicks.append(['select-runner',pointer(363+266+109,ypanel(292+45))])
arena=shot('09-arena')
clicks.append(['launch',pointer(74+34+226,ypanel(481+32))])
countdown=shot('10-countdown')
assert_transition(arena,countdown,'competition round')
time.sleep(3.7)
active=shot('11-match-active')
assert_transition(countdown,active,'chase active')
key('w',True);time.sleep(.35);key('w',False)
moved=shot('12-match-sprint')
assert_transition(active,moved,'real match movement')
tap('Escape')
paused=shot('13-paused')
assert_transition(moved,paused,'pause no restart')
errors=[e['params']['entry'].get('text','') for e in events
        if e.get('method')=='Log.entryAdded' and e.get('params',{}).get('entry',{}).get('level')=='error']
exceptions=[e for e in events if e.get('method')=='Runtime.exceptionThrown']
assert not errors and not exceptions,(errors,exceptions[:3])
report={'pointer_clicks':clicks,'real_keyboard_movement':True,
        'practice_jump_dive_slide':True,'normal_competitive_match_started':True,
        'runtime_console_errors':errors,'javascript_exceptions':len(exceptions)}
(out/'verification.json').write_text(json.dumps(report,indent=2)+'\n')
print('CHASE_V017_FAST_MOVEMENT_BROWSER_PASS',len(clicks),flush=True)
ws.close()
