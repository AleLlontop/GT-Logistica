#!/usr/bin/env bash
set -euo pipefail
# Valores explícitos. La contraseña se transmite por SQLCMDPASSWORD, nunca por argv.
: "${SQLCMD_SERVER:?Definí SQLCMD_SERVER para el servidor destino.}"
: "${SQLCMD_DATABASE:?Definí SQLCMD_DATABASE para la base de aplicación destino.}"
: "${SQLCMD_USER:?Definí SQLCMD_USER para la cuenta administrativa.}"
: "${SQLCMDPASSWORD:?Definí SQLCMDPASSWORD fuera del repositorio.}"
if [[ ! "$SQLCMD_DATABASE" =~ ^[A-Za-z_][A-Za-z0-9_]{0,127}$ ]]; then
    echo 'El nombre de la base debe tener letras ASCII, números o guion bajo y comenzar con letra o guion bajo.' >&2
    exit 2
fi
script_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
# Timeout explícito: un error no autoriza a expulsar conexiones ni reducir el aislamiento.
exec sqlcmd -S "$SQLCMD_SERVER" -d master -U "$SQLCMD_USER" -b -t 60 \
    -v "Base015=$SQLCMD_DATABASE" -i "$script_dir/habilitar-snapshot-reportes.sql"
