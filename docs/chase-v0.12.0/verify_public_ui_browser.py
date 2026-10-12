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
out=root/'docs/chase-v0.12.0/evidence/webgl'
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
        'Escape':('Escape',27)}
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
command('Page.navigate',{'url':'http://127.0.0.1:8765/?build=v012-public-menu-final'})
command('Page.bringToFront')
for _ in range(250):
    d=js('''({ready:document.readyState,
      loader:document.querySelector('#unity-loading-bar')?.style.display})''')
    if d and d['ready']=='complete' and d['loader']=='none':break
    time.sleep(.15)
else:raise RuntimeError('WebGL loading exceeded time limit')
# Unity may finish loading the WASM before its splash screen exits.
# The splash has no bright CHASE logotype in the upper-left menu region;
# require that distinct region in two consecutive screenshots.
consecutive=0
for attempt in range(210):
    frame=base64.b64decode(command('Page.captureScreenshot',{'format':'png'})['data'])
    image=Image.open(io.BytesIO(frame)).convert('RGB')
    w,h=image.size
    logo=image.crop((int(w*.07),int(h*.06),int(w*.35),int(h*.28)))
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
clicks=[]
# How to play, return; Settings toggle keys twice, return.
clicks.append(['howto',pointer(83+34+109,ypanel(402+27))])
how=shot('01-how-to-play')
assert_transition(menu,how,'how-to')
clicks.append(['back',pointer(83+33+51,ypanel(166+21))])
clicks.append(['settings',pointer(83+266+109,ypanel(402+27))])
settings=shot('02-settings')
assert_transition(menu,settings,'settings')
clicks.append(['toggle-hints',pointer(83+34+226,ypanel(310+32))])
settingsOff=shot('03-settings-hints-off')
assert_transition(settings,settingsOff,'hints toggle')
clicks.append(['toggle-hints-restore',pointer(83+34+226,ypanel(310+32))])
clicks.append(['back-to-menu',pointer(83+33+51,ypanel(166+21))])
clicks.append(['play',pointer(83+34+226,ypanel(329+31))])
difficulty=shot('04-difficulty')
assert_transition(menu,difficulty,'play to difficulty')
clicks.append(['rival',pointer(83+34+109,ypanel(397+45))])
arena=shot('05-arena-select')
assert_transition(difficulty,arena,'difficulty to arena')
clicks.append(['launch',pointer(83+34+226,ypanel(481+32))])
countdown=shot('06-countdown')
assert_transition(arena,countdown,'arena to countdown')
time.sleep(3.6)
active=shot('07-active-hud')
assert_transition(countdown,active,'countdown to active')
key('w',True);key('ArrowUp',True)
time.sleep(.25)
moving=shot('08-key-highlight-dive')
key('ArrowUp',False);key('w',False)
tap('ArrowDown');tap(' ')
tap('Escape')
paused=shot('09-pause')
assert_transition(active,paused,'pause overlay')
# Resume via pause overlay: x=(w-510)/2 +44+211, y=(h-352)/2 +221+23
clicks.append(['resume',pointer((1280-510)/2+44+211,lambda h:(h-352)/2+221+23)])
resumed=shot('10-resumed')
assert_transition(paused,resumed,'resume match')
tap('Escape')
clicks.append(['return-menu',pointer((1280-510)/2+264+101,lambda h:(h-352)/2+280+22)])
again=shot('11-menu-return')
assert_transition(paused,again,'return to main menu')
# End overlay can be tested through the normal countdown and full timed match.
clicks.append(['play-again',pointer(83+34+226,ypanel(329+31))])
clicks.append(['runner',pointer(83+266+109,ypanel(292+45))])
clicks.append(['launch-again',pointer(83+34+226,ypanel(481+32))])
time.sleep(25.4)
result=shot('12-round-result')
# Result remains a distinct UI surface from countdown and gameplay.
assert_transition(active,result,'round result overlay')
clicks.append(['next-round',pointer((1280-510)/2+44+211,lambda h:(h-352)/2+221+24)])
nextRound=shot('13-next-round')
assert_transition(result,nextRound,'next round button')

state=js('''({href:location.href,focus:document.activeElement.id,
scrollY:scrollY,scrollX:scrollX,inputs:window.__publicInputs,
loading:document.querySelector('#unity-loading-bar')?.style.display})''')
errors=[e['params']['entry'].get('text','') for e in events
    if e.get('method')=='Log.entryAdded' and e.get('params',{}).get('entry',{}).get('level')=='error']
exceptions=[e for e in events if e.get('method')=='Runtime.exceptionThrown']
assert not errors and not exceptions,(errors,exceptions[:2])
assert state['scrollY']==state['scrollX']==0,state
assert state['focus']=='unity-canvas',state
assert {'Escape','ArrowUp','ArrowDown',' ','w'}.issubset(
    {x['key'] for x in state['inputs']}),state['inputs']
assert all(x['prevented'] for x in state['inputs'] if x['key'] in ('ArrowUp','ArrowDown',' '))
report={
    'url':state['href'],'page_title':js('document.title'),
    'geometry':geometry(),'pointer_clicks':clicks,
    'real_inputs':state['inputs'],'console_errors':errors,'exceptions':len(exceptions),
    'status':state,'notes':'Real CDP mouse+keyboard interaction, zero Unity SendMessage shortcuts'
}
(out/'verification.json').write_text(json.dumps(report,indent=2)+'\n')
print('CHASE_PUBLIC_FRONTEND_WEBGL_PASS',len(clicks),'clicks',
      len(state['inputs']),'keys',flush=True)
ws.close()
