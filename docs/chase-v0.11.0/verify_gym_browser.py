#!/usr/bin/env python3
"""Real Chrome/WebGL staging of both physical routes through one prefab.

The B and N keys position the shared Gym actor, never set a movement state.
The launch keys are real Chrome keydown/keyup events delivered to Unity.
"""
import base64,io,json,time,urllib.request,os
from pathlib import Path
from PIL import Image,ImageChops,ImageStat
import websocket

root=Path(__file__).resolve().parents[2]
output=root/'docs/chase-v0.11.0/evidence/gym'
output.mkdir(parents=True,exist_ok=True)
port=9227
pages=json.load(urllib.request.urlopen(f'http://127.0.0.1:{port}/json'))
page=next(x for x in pages if x.get('type')=='page' and ':8766/' in x.get('url',''))
ws=websocket.create_connection(page['webSocketDebuggerUrl'],timeout=25,suppress_origin=True)
seq=0
events=[]
def command(method,params=None):
    global seq
    seq+=1
    ws.send(json.dumps({'id':seq,'method':method,'params':params or {}}))
    while True:
        v=json.loads(ws.recv())
        if v.get('method'):events.append(v)
        if v.get('id')==seq:
            if 'error' in v:raise RuntimeError(v['error'])
            return v.get('result',{})
def js(expr):
    r=command('Runtime.evaluate',{'expression':expr,'returnByValue':True})
    if r.get('exceptionDetails'):raise RuntimeError(r['exceptionDetails'])
    return r.get('result',{}).get('value')
keys={
    'b':('KeyB',66),'n':('KeyN',78),'v':('KeyV',86),
    'w':('KeyW',87),'ArrowUp':('ArrowUp',38),'ArrowDown':('ArrowDown',40)}
def key(k,down):
    code,num=keys[k]
    opts={'type':'keyDown' if down else 'keyUp','key':k,'code':code,
          'windowsVirtualKeyCode':num,'nativeVirtualKeyCode':num}
    if len(k)==1 and down:opts['text']=k
    command('Input.dispatchKeyEvent',opts)
def tap(k):
    key(k,True);time.sleep(.09);key(k,False)
def grab(name):
    img=base64.b64decode(command('Page.captureScreenshot',{
        'format':'png','captureBeyondViewport':False})['data'])
    path=output/(name+'.png');path.write_bytes(img)
    print('CAPTURE',name,len(img),flush=True)
    return path
def diff(a,b):
    x=Image.open(a).convert('RGB');y=Image.open(b).convert('RGB')
    return sum(max(rgb)>35 for rgb in ImageChops.difference(x,y).getdata())
def yellow_rail_center_y(path):
    # Camera-relative pixel check: the fixed yellow rail must move from
    # ahead of the chibi to far behind it. Pixel changes alone once passed
    # even when the player ran into the solid bar and remained stationary.
    im=Image.open(path).convert('RGB');width,height=im.size
    ys=[]
    for y in range(int(.14*height),int(.94*height),2):
        for x in range(int(.20*width),int(.80*width),2):
            r,g,b=im.getpixel((x,y))
            if r>185 and g>140 and b<145 and r>g*1.04:
                ys.append(y)
    if len(ys)<80:raise RuntimeError('Could not locate the solid yellow crossing')
    return sum(ys)/len(ys)

command('Page.enable')
command('Runtime.enable')
command('Log.enable')
command('Network.enable')
command('Network.setCacheDisabled',{'cacheDisabled':True})
command('Page.navigate',{'url':'http://127.0.0.1:8766/?build=v011-mastered-course-gym'})
command('Page.bringToFront')
for i in range(280):
    loaded=js('({ready:document.readyState,loader:document.querySelector("#unity-loading-bar")?.style.display})')
    if loaded and loaded['ready']=='complete' and loaded['loader']=='none':break
    time.sleep(.20)
else:raise RuntimeError('Gym WebGL did not finish loading')
for i in range(220):
    blob=base64.b64decode(command('Page.captureScreenshot',{'format':'png'})['data'])
    im=Image.open(io.BytesIO(blob)).convert('RGB')
    w,h=im.size
    metric=sum(ImageStat.Stat(im.crop((int(w*.23),int(h*.26),int(w*.77),int(h*.8)))).stddev)/3
    if metric>12:break
    time.sleep(.2)
else:raise RuntimeError('Rendered gym did not become visible')
js('''(()=>{document.querySelector('canvas').focus({preventScroll:true});
 window.__routeKeys=[];
 document.addEventListener('keydown',e=>{
 if(['b','n','w','ArrowUp','ArrowDown'].includes(e.key))
 window.__routeKeys.push({key:e.key,prevented:e.defaultPrevented,focus:document.activeElement.id});
 },false);return document.activeElement.id})()''')
assert js('document.activeElement.id')=='unity-canvas'
tap('v');time.sleep(.6)
tap('b');time.sleep(.45)
before=grab('00-rail-dive-approach')
key('w',True);key('ArrowUp',True)
t0=time.monotonic()
dive={}
for n,secs in [('01-dive-takeoff',.10),('02-dive-over-rail',.29),
               ('03-dive-descent',.55),('04-dive-recovery',1.05)]:
    delay=t0+secs-time.monotonic()
    if delay>0:time.sleep(delay)
    dive[n]=grab(n)
key('ArrowUp',False);key('w',False)
tap('n');time.sleep(.12)
slidebase=grab('05-rail-slide-approach')
key('w',True)
time.sleep(float(os.environ.get('CHASE_RAIL_RUNUP', '.37')))
key('ArrowDown',True)
t0=time.monotonic()
slide={}
for n,secs in [('06-slide-start',.10),('07-slide-under-rail',.36),
               ('08-slide-low-recovery',.70),('09-slide-crawl-out',1.45),
               ('10-slide-standing',2.65)]:
    delay=t0+secs-time.monotonic()
    if delay>0:time.sleep(delay)
    slide[n]=grab(n)
key('ArrowDown',False);key('w',False)
final=js('({href:location.href,focus:document.activeElement.id,x:scrollX,y:scrollY,keys:window.__routeKeys})')
errors=[e['params']['entry'].get('text','') for e in events
        if e.get('method')=='Log.entryAdded' and
        e.get('params',{}).get('entry',{}).get('level')=='error']
exceptions=[e for e in events if e.get('method')=='Runtime.exceptionThrown']
assert not errors and not exceptions,(errors,exceptions[:1])
assert final['focus']=='unity-canvas' and final['x']==final['y']==0
assert {k for k in ['b','n','w','ArrowUp','ArrowDown']}.issubset(
    {x['key'] for x in final['keys']})
changed={**{k:diff(before,p) for k,p in dive.items()},
         **{k:diff(slidebase,p) for k,p in slide.items()}}
assert min(changed.values())>850,changed
rail_before=yellow_rail_center_y(slidebase)
rail_behind=yellow_rail_center_y(slide['09-slide-crawl-out'])
assert rail_behind-rail_before>200,(
    'Football slide must actually pass to the far side of the rail',
    rail_before,rail_behind)
report={
    'url':final['href'],'rendered_gym_contrast':round(metric,2),
    'real_keys':final['keys'],'console_errors':errors,'javascript_exceptions':len(exceptions),
    'changed_pixels_over_35':changed,
    'under_route_rail_screen_y_before_after': [round(rail_before,1),round(rail_behind,1)],
    'evidence':'actual rebuilt Unity WebGL, shared motor, B/W+Up over and N-run-up-W+Down under',
    'runup_seconds':float(os.environ.get('CHASE_RAIL_RUNUP', '.37')),
    'limitations':'screenshots establish rendered input response, not precise collision clearance (proven by PhysX tests)'
}
(output/'verification.json').write_text(json.dumps(report,indent=2)+'\n')
print('CHASE_WEBGL_MODULAR_ROUTES_PASS',json.dumps({
    'inputs':len(final['keys']),'changed':changed,'errors':len(errors)},sort_keys=True),flush=True)
ws.close()
