#!/usr/bin/env python3
"""Smoke local por HTTP real; credenciales externas, archivos sólo en /tmp."""
import os,json,pathlib,urllib.request,urllib.parse,zipfile,io,sys,re,subprocess,xml.etree.ElementTree as ET
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
with request('/api/auth/login',{'username':'administrador','password':password}) as response:
    #Cliente HTTP explícito sobre loopback; nunca persiste ni imprime la cookie Secure.
    headers['Cookie']='; '.join(v.split(';',1)[0] for v in response.headers.get_all('Set-Cookie',[]))
paths=['/api/empleados/','/api/usuarios/','/api/roles/','/api/tipos-documentacion/','/api/flota/tipos-vehiculo/','/api/personas/','/api/clientes/','/api/proveedores/','/api/transportistas/','/api/choferes/','/api/flota/vehiculos/','/api/viajes/','/api/facturas/','/api/proveedores/facturas','/api/liquidaciones/','/api/adelantos/','/api/caja/','/api/movimientos-caja','/api/pagos/ordenes','/api/pagos/cheques','/api/pagos/bancos','/api/vencimientos','/api/flota/vencimientos','/api/facturas/vencimientos']
forbidden=re.compile('DEMO|TEST|PRUEBA|FAKE|SAMPLE',re.I)
def clean(value,context):
    if isinstance(value,str):assert not forbidden.search(value),'Texto no permitido en '+context
    elif isinstance(value,list):
        for item in value:clean(item,context)
    elif isinstance(value,dict):
        for field,item in value.items():clean(item,context+'/'+field)
result={'endpoints':{},'exportaciones':{}}
samples={}

for path in paths:
    print("HTTP",path,flush=True)
    with request(path) as response:obj=json.load(response)
    count=len(obj) if isinstance(obj,list) else obj.get('total',len(obj.get('items',[])))
    assert count>0,(path,count)
    clean(obj,path)
    items=obj if isinstance(obj,list) else obj.get('items',[])
    collected=list(items)
    page=2
    while not isinstance(obj,list) and len(collected)<count:
        with request(path+('?pagina='+str(page))) as response:more=json.load(response)
        clean(more,path)
        chunk=more.get('items',[])
        assert chunk and [r.get('id') for r in chunk]!=[r.get('id') for r in items], 'Paginación no avanza: '+path
        collected.extend(chunk);page+=1
    samples[path]=collected
    result['endpoints'][path]=count
period='desde=2026-10-01&hasta=2026-10-31'
with request('/api/reportes/dashboard?'+period) as response:result['dashboard']=json.load(response)
clean(result['dashboard'],'dashboard')
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
        if fmt=='pdf':
            assert raw.startswith(b'%PDF-')
            extracted=subprocess.run(['pdftotext','-','-'],input=raw,capture_output=True,check=True).stdout.decode()
            assert extracted.strip()
            clean(extracted,family+'/pdf')
        else:
            with zipfile.ZipFile(io.BytesIO(raw)) as book:
                assert book.testzip() is None
                assert 'xl/worksheets/sheet1.xml' in book.namelist()
                for member in book.namelist():
                    if member.startswith('xl/') and member.endswith('.xml'):
                        tree=ET.fromstring(book.read(member))
                        for node in tree.iter():
                            if node.tag.rsplit('}',1)[-1]=='t' and node.text:clean(node.text,family+'/xlsx')
        name=family.replace('/','-')+('.pdf' if fmt=='pdf' else '.xlsx');(folder/name).write_bytes(raw)
        result['exportaciones'][name]=len(raw)
# Inspecciona comprobantes persistidos y un adjunto real por clase además de los reportes.
attachments=[]
for row in samples['/api/facturas/'][:1]:attachments.append(('/api/facturas/'+str(row['id'])+'/documento','comprobante-cliente.pdf'))
for path,prefix in [('/api/proveedores/facturas','proveedor'),('/api/choferes/','chofer'),('/api/flota/vehiculos/','vehiculo')]:
    for row in samples[path]:
        with request(path.rstrip('/')+'/'+str(row['id'])) as response:detail=json.load(response)
        clean(detail,path+'/detalle')
        if prefix=='proveedor':
            if detail.get('archivo'):
                attachments.append(('/api/proveedores/facturas/'+str(row['id'])+'/archivo','adjunto-proveedor.pdf'));break
        else:
            docs=detail.get('documentacion',detail.get('documentos',[]))
            doc=next((d for d in docs if d.get('tieneArchivo')),None)
            if doc:
                route='/api/documentacion/' if prefix=='chofer' else '/api/flota/documentacion/'
                attachments.append((route+str(doc['id'])+'/archivo','adjunto-'+prefix+'.pdf'));break
for path,name in attachments:
    with request(path) as response:raw=response.read()
    assert raw.startswith(b'%PDF-')
    clean(subprocess.run(['pdftotext','-','-'],input=raw,capture_output=True,check=True).stdout.decode(),name)
    (folder/name).write_bytes(raw)
result['adjuntos_verificados']=[name for _,name in attachments]
assert len(attachments)==4, 'Faltan comprobantes/adjuntos de alguna clase'
(folder/'resultado.json').write_text(json.dumps(result,indent=2,ensure_ascii=False))
print(json.dumps({'endpoints_ok':len(paths),'exportaciones_ok':len(result['exportaciones']),'evidencia':str(folder/'resultado.json')}))
