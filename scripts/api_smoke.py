"""Optional live HTTP checks. Requires Python 3 and an already-running Smart-X gateway.
Runs against demo data and intentionally creates a uniquely named test device/attachment.
Usage: python scripts/api_smoke.py http://localhost:5080
"""
import json, sys, time, urllib.request, urllib.error
base = (sys.argv[1] if len(sys.argv)>1 else 'http://localhost:5080').rstrip('/')
passed = 0
opener=urllib.request.build_opener(urllib.request.ProxyHandler({}))
def request(path, method='GET', body=None, content_type='application/json'):
    data = body if isinstance(body,bytes) else json.dumps(body).encode() if body is not None else None
    req=urllib.request.Request(base+path,data=data,method=method,headers={'Content-Type':content_type})
    try:
        with opener.open(req,timeout=10) as response:return response.status,response.read()
    except urllib.error.HTTPError as error:return error.code,error.read()
def api(path, method='GET', body=None, status=200):
    code,raw=request('/api'+path,method,body)
    assert code==status,(path,code,raw)
    return json.loads(raw)
def check(name, condition):
    global passed
    assert condition,name
    passed+=1;print('PASS ',name)
def wait_for(predicate):
    for _ in range(30):
        if predicate(): return
        time.sleep(.2)
    raise AssertionError('Timed out waiting for worker')
check('health endpoint',api('/health')['status']=='healthy')
check('HTML delivered',b'Sensor telemetry' in request('/')[1])
check('CSS delivered',request('/app.css')[0]==200)
check('JavaScript delivered',request('/app.js')[0]==200)
check('unknown API is 404',request('/api/not-real')[0]==404)
check('invalid moisture rejected',request('/api/telemetry/ingest','POST',{'sensorId':'MS-01','payload':101})[0]==400)
api('/devices/MS-01/online','POST',{'online':True})
api('/devices/PM-01/online','POST',{'online':True})
api('/processing','POST',{'paused':True})
a=api('/telemetry/ingest','POST',{'sensorId':'TP-01','payload':25},202)
b=api('/telemetry/ingest','POST',{'sensorId':'MS-01','payload':13},202)
s=api('/state');check('paused HTTP ingestion queued',s['queue']['standard']>=1 and s['queue']['critical']>=1)
api('/processing','POST',{'paused':False})
wait_for(lambda:api('/state')['queue']['critical']==0)
s=api('/state');check('worker consumes real HTTP packets',s['queue']['standard']==0)
processed=[entry['message'] for entry in reversed(s['audit']) if entry['kind'].endswith('processed')]
check('critical packet processed before earlier standard',next(i for i,x in enumerate(processed) if f"#{b['sequence']}:" in x)<next(i for i,x in enumerate(processed) if f"#{a['sequence']}:" in x))
api('/commands','POST',{'sensorId':'PM-01','enabled':True})
api('/commands','POST',{'sensorId':'PM-01','enabled':False})
api('/commands/undo','POST')
check('HTTP undo restores actuator',next(d for d in api('/devices') if d['id']=='PM-01')['enabled'])
api('/commands','POST',{'sensorId':'PM-01','enabled':False})
for _ in range(2):api('/search','POST',{'query':'Pump A'})
check('HTTP searches generate suggestion',any(i['targetId']=='PM-01' for i in api('/state')['insights']))
api('/devices/TP-01/online','POST',{'online':True})
api('/devices/PW-01/online','POST',{'online':True})
check('initial minimum route',api('/topology')['routeCost']==3)
api('/devices/TP-01/online','POST',{'online':False})
check('failure reroutes through alternate node',api('/topology')['routeCost']==9)
api('/devices/TP-01/online','POST',{'online':True})
h=api('/history/MS-01');check('history endpoint uses populated tree',h['treeCount']>=31 and h['treeHeight']>0)
check('reversed dates rejected',request('/api/history/MS-01?from=2026-11-09T12:00:00Z&to=2026-11-08T12:00:00Z')[0]==400)
key=f'TEST-{int(time.time())}'; mac=f'AA:CC:{int(time.time())&0xffffffff:08X}'; mac=mac[:6]+':'.join(mac[6:][i:i+2] for i in range(0,8,2))
api('/devices','POST',{'id':key,'mac':mac,'name':'HTTP test node','category':'Moisture','location':'Lab'},201)
check('registration through API',any(d['id']==key for d in api('/devices')))
boundary='smartx-test-boundary'; payload=(f'--{boundary}\r\nContent-Disposition: form-data; name="file"; filename="http-test.txt"\r\nContent-Type: text/plain\r\n\r\nHello Smart-X\r\n--{boundary}--\r\n').encode()
code,raw=request('/api/attachments/MS-01','POST',payload,f'multipart/form-data; boundary={boundary}')
check('multipart upload',code==201)
attachment=json.loads(raw);check('download retains original bytes',request('/api/attachments/'+attachment['id'])[1]==b'Hello Smart-X')
print(f'\n{passed} live HTTP checks passed.')
