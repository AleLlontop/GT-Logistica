#!/usr/bin/env python3
"""Smoke local por HTTP real; credenciales externas, archivos sólo en /tmp."""
import os,json,pathlib,urllib.request,urllib.parse,zipfile,io,sys
repo=pathlib.Path(__file__).resolve().parents[3]
config={}
if (repo/'.env').exists():
    for line in (repo/'.env').read_text().splitlines():
        if '=' in line and not line.lstrip().startswith('#'):
            k,v=line.split('=',1);config[k.strip()]=v.strip().strip("\"'")
config.update(os.environ)
base=config.get('GT_DEMO_HTTP_URL','http://localhost:5173').rstrip('/')
if urllib.parse.urlparse(base).hostname not in ('localhost','127.0.0.1','::1'):sys.exit('Sólo HTTP local.')
password=config.get('GT_DEMO_PASSWORD') or config.get('GT_ADMIN_PASSWORD_INICIAL')
if not password:sys.exit('Falta password demo externo.')
headers={'Content-Type':'application/json'}
def request(path,data=None):
    req=urllib.request.Request(base+path,headers=headers,data=None if data is None else json.dumps(data).encode())
    return urllib.request.urlopen(req,timeout=90)
with request('/api/auth/login',{'username':'demo.DEMO015.v1.ready','password':password}) as response:
    #Cliente HTTP explícito sobre loopback; nunca persiste ni imprime la cookie Secure.
    headers['Cookie']='; '.join(v.split(';',1)[0] for v in response.headers.get_all('Set-Cookie',[]))
paths=['/api/usuarios/','/api/roles/','/api/tipos-documentacion/','/api/flota/tipos-vehiculo/','/api/personas/','/api/clientes/','/api/proveedores/','/api/transportistas/','/api/choferes/','/api/flota/vehiculos/','/api/viajes/','/api/facturas/','/api/proveedores/facturas','/api/liquidaciones/','/api/adelantos/','/api/caja/','/api/movimientos-caja','/api/pagos/ordenes','/api/pagos/cheques','/api/pagos/bancos','/api/vencimientos','/api/flota/vencimientos','/api/facturas/vencimientos']
result={'endpoints':{},'exportaciones':{}}
for path in paths:
    print("HTTP",path,flush=True)
    with request(path) as response:obj=json.load(response)
    count=len(obj) if isinstance(obj,list) else obj.get('total',len(obj.get('items',[])))
    assert count>0,(path,count)
    result['endpoints'][path]=count
period='desde=2026-10-01&hasta=2026-10-31'
with request('/api/reportes/dashboard?'+period) as response:result['dashboard']=json.load(response)
sections=result['dashboard']['secciones']
assert all(s['estado']=='disponible' for s in sections.values())
def positive(section,group,*fields):
    values=sections[section]['datos'][group]
    from decimal import Decimal
    assert all(Decimal(str(values[field]))>0 for field in fields),(section,group,values)
positive('viajes','resumen','cantidad','realizados','pendientesRendicion','pendientes')
positive('facturacion','documentos','facturado','pendienteVigenteDocumental')
positive('facturacion','cobros','importeCobrado')
positive('liquidaciones','documentos','importeLiquidado','pendienteVigenteDocumental')
positive('liquidaciones','pagos','importePagadoVigente')
positive('caja','resumen','ingresos','egresos')
from decimal import Decimal
assert Decimal(sections['caja']['datos']['resumen']['neto'])!=0
positive('flota','resumen','cantidadUnidades','totalViajesConVehiculo')
assert len(sections['flota']['datos']['ranking']['top'])>1
for section in ['vencimientosChoferes','vencimientosFlota','vencimientosFacturas']:
    positive(section,'resumen','cantidad')
folder=pathlib.Path('/tmp/gt-demo015-smoke');folder.mkdir(exist_ok=True)
for family in ['viajes','facturacion','facturacion/cobros','liquidaciones','liquidaciones/pagos','caja','flota']:
    for fmt in ['pdf','excel']:
        with request('/api/reportes/'+family+'/exportacion?'+period+'&formato='+fmt) as response:raw=response.read()
        if fmt=='pdf':assert raw.startswith(b'%PDF-')
        else:
            with zipfile.ZipFile(io.BytesIO(raw)) as book:
                assert book.testzip() is None
                assert 'xl/worksheets/sheet1.xml' in book.namelist()
        name=family.replace('/','-')+('.pdf' if fmt=='pdf' else '.xlsx');(folder/name).write_bytes(raw)
        result['exportaciones'][name]=len(raw)
(folder/'resultado.json').write_text(json.dumps(result,indent=2,ensure_ascii=False))
print(json.dumps({'endpoints_ok':len(paths),'exportaciones_ok':len(result['exportaciones']),'evidencia':str(folder/'resultado.json')}))
