#!/usr/bin/env bash
set -euo pipefail
repo="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
# .env se interpreta como datos: nunca se ejecuta con source/eval.
exec python3 - "$repo" "$@" <<'PY'
import os,sys,pathlib
repo=pathlib.Path(sys.argv[1]);env=os.environ.copy()
p=repo/'.env'
if p.exists():
    for line in p.read_text().splitlines():
        line=line.strip()
        if not line or line.startswith('#') or '=' not in line:continue
        key,value=line.split('=',1);key=key.strip();value=value.strip()
        if len(value)>1 and value[0]==value[-1] and value[0] in "\"'":value=value[1:-1]
        env.setdefault(key,value)
if not env.get('ConnectionStrings__Gt'):
    if not env.get('GT_SQL_PASSWORD'):sys.exit('Falta configuración SQL externa.')
    # Connection string value quoted, supports semicolons and quote escaping in passwords.
    secret=env['GT_SQL_PASSWORD'].replace('"','""')
    env['ConnectionStrings__Gt']='Server=localhost,1433;Database=GtLogistica;User Id=sa;Password="'+secret+'";TrustServerCertificate=True;Encrypt=True'
env.setdefault('GT_ARCHIVOS_RUTA',str(repo/'archivos'))
env.setdefault('GT_DEMO_PASSWORD',env.get('GT_ADMIN_PASSWORD_INICIAL',''))
if env['GT_ARCHIVOS_RUTA']=='/var/gt/archivos':
    # Con configuración Docker se comparte el volumen de la API; nunca escribir en /var del host.
    os.chdir(repo)
    cmd=['docker','compose','run','--rm','seed-demo',*sys.argv[2:]]
else:
    for name in ['ASPNETCORE_ENVIRONMENT','GT_SEED_DEMO']:
        if name not in env:sys.exit('Falta '+name+' explícita para ejecución nativa.')
    cmd=[env.get('GT_DOTNET','dotnet'),'run','--project',str(repo/'backend/tools/GT.SeedDemo/GT.SeedDemo.csproj'),'--no-restore','--',*sys.argv[2:]]
os.execvpe(cmd[0],cmd,env)
PY
