#!/bin/bash
# contexto-arranque.sh — SessionStart
#
# QUÉ HACE: pone delante del agente el ESTADO MEDIDO del repositorio y la lista
#           corta de mandamientos, sin gastar espacio de CLAUDE.md.
# POR QUÉ EXISTE: mandamiento 0 y el fallo recurrente del dueño: "¿y dónde estamos?".
#           La documentación oficial dice que lo estático va en CLAUDE.md; esto es
#           lo que CAMBIA en cada sesión, que es justo lo que un archivo no puede dar.
# FORMA: texto plano por stdout. SessionStart es uno de los tres eventos donde el
#           stdout se añade como contexto que Claude ve.
# ⚠️ Se escribe como HECHOS, no como órdenes: un texto con forma de instrucción
#           fuera de banda dispara las defensas contra inyección y acaba mostrándose
#           al usuario en vez de usarse como contexto.

set -euo pipefail
cd "${CLAUDE_PROJECT_DIR:-.}" 2>/dev/null || exit 0

echo "## Estado medido del repositorio, $(date +%Y-%m-%d\ %H:%M)"
echo

if [[ ! -f CLAUDE.md ]]; then
  echo "AVISO: no hay CLAUDE.md en este directorio. Probablemente es la carpeta equivocada."
  exit 0
fi

echo "### Git"
git fetch --quiet 2>/dev/null || true
echo "Rama: $(git rev-parse --abbrev-ref HEAD 2>/dev/null || echo '?')"
sucio=$(git status --porcelain 2>/dev/null | wc -l | tr -d ' ')
echo "Archivos sin commitear: $sucio"
echo "Últimos commits:"
git log --oneline -5 2>/dev/null | sed 's/^/  /'
echo

# Pases sin entregable: ese agente se perdió y se relanza fresco, no se reanuda.
echo "### Pases lanzados sin entregable"
huerfanos=0
while IFS= read -r pase; do
  base=$(basename "$pase" .md); base=${base#PASE-}
  if ! ls ./**/FIXES-*"${base}"* ./**/QA-*"${base}"* 2>/dev/null | grep -q .; then
    echo "  $pase"; huerfanos=$((huerfanos+1))
  fi
# El `tr -d '\r'` aqui es preventivo: `find` no mete \r, pero este es el tercer
# `while read` del proyecto y los otros dos SI estaban rotos por eso (2026-08-25).
# Se unifica para que el patron no vuelva a colarse por el hueco de siempre.
done < <(find . -name 'PASE-*.md' -newermt '-7 days' 2>/dev/null | head -10 | tr -d '\r')
[[ $huerfanos -eq 0 ]] && echo "  ninguno en los últimos 7 días"
echo

echo "### Auditorías con entregable congelado"
if [[ -s .claude/congelados.txt ]]; then
  sed 's/^/  /' .claude/congelados.txt
else
  echo "  ninguna"
fi
echo

# El cerebro es de UN inquilino (PGLite, un escritor): la primera sesión que
# arranca se lo queda y las demás fallan EN SILENCIO. El dueño (2026-08-28)
# exige que cada sesión arranque SABIENDO cómo quedó — libre, tomado o roto —
# en vez de descubrirlo tres horas después por una llamada que nunca respondió.
#
# ⚠️ CORREGIDO EL MISMO DÍA, y la lección duele: la primera versión preguntaba
#    si el PID del candado VIVE y afirmaba «es de OTRA sesión: hay que cerrarla»
#    — la otredad nunca se midió. Daba la alarma justo cuando gbrain conectaba
#    BIEN, y el dueño cerró una sesión sana fiándose. XV-VII de manual. Ahora se
#    compara el ÁRBOL de procesos: si el candado desciende del mismo claude.exe
#    que este hook, es NUESTRO. Si la comparación falla, se dice que no se sabe
#    — jamás se ordena cerrar nada sin haber medido de quién es.

# ancestros_de <pid> -> lista de PIDs remontando padres (máx 12 saltos)
ancestros_de() {
  local p="$1" i=0
  while [[ -n "$p" && "$p" != "0" && $i -lt 12 ]]; do
    echo "$p"
    p=$(powershell -NoProfile -Command "(Get-CimInstance Win32_Process -Filter 'ProcessId=$p' -ErrorAction SilentlyContinue).ParentProcessId" 2>/dev/null | tr -d '\r ' || true)
    i=$((i+1))
  done
}

echo "### El cerebro (gbrain), medido ahora"
candado_gbrain="$HOME/.gbrain/brain.pglite/.gbrain-lock/lock"
if [[ -f "$candado_gbrain" ]]; then
  pid_gbrain=$(jq -r '.pid // empty' "$candado_gbrain" 2>/dev/null || true)
  if [[ -n "${pid_gbrain:-}" ]] && tasklist //FI "PID eq $pid_gbrain" 2>/dev/null | grep -q "$pid_gbrain"; then
    # ¿De quién es? El WINPID propio sale de ps (MSYS); si algo de esto falla,
    # se degrada a «no sé de quién», nunca a una acusación.
    mi_winpid=$(ps -p $$ 2>/dev/null | awk 'NR==2{print $4}' || true)
    duenio="desconocido"
    if [[ -n "${mi_winpid:-}" ]]; then
      mios=$(ancestros_de "$mi_winpid")
      suyos=$(ancestros_de "$pid_gbrain")
      if [[ -n "$mios" && -n "$suyos" ]]; then
        if comm -12 <(sort -u <<<"$mios") <(sort -u <<<"$suyos") | grep -q '[0-9]'; then
          duenio="propio"
        else
          duenio="ajeno"
        fi
      fi
    fi
    case "$duenio" in
      propio)
        echo "  TOMADO POR ESTA MISMA SESIÓN (PID $pid_gbrain, mismo árbol de procesos)."
        echo "  Es el estado BUENO: el cerebro es tuyo y mcp__gbrain__recall debe responder." ;;
      ajeno)
        echo "  TOMADO por OTRA sesión (PID $pid_gbrain, árbol de procesos distinto — medido,"
        echo "  no supuesto). Esta sesión trabaja SIN memoria: sus verbos mcp__gbrain__*"
        echo "  fallarán. No es avería, es el inquilino único. Si esta sesión NECESITA el"
        echo "  cerebro, el dueño decide qué sesión cerrar." ;;
      *)
        echo "  TOMADO por un proceso vivo (PID $pid_gbrain) y NO SE PUDO MEDIR de quién es"
        echo "  (falló la comparación de árboles). La prueba discriminante: si"
        echo "  mcp__gbrain__recall responde en esta sesión, el candado es NUESTRO y todo va"
        echo "  bien. No cerrar nada sin esa prueba." ;;
    esac
  else
    echo "  CANDADO HUÉRFANO: el archivo existe pero su proceso (PID ${pid_gbrain:-desconocido}) ya no vive."
    echo "  gbrain no lo limpia solo; toca al dueño decidir (gbrain doctor lo diagnostica)."
  fi
else
  echo "  LIBRE al arrancar: esta sesión debería haberlo conectado. La prueba: que"
  echo "  mcp__gbrain__recall responda. Si falla igualmente, mirar mcp-logs-gbrain."
fi
echo

echo "### Las reglas de este proyecto, en corto"
cat <<'REGLAS'
  Cada afirmación va con su comando y su salida, o con «lo dice X y NO lo he comprobado».
  Nunca un hecho a secas: la frase queda incompleta si falta la fuente.
  Un documento del proyecto no es una medición: es lo que alguien midió o supuso en su momento.
  Cuando un documento y el código no coinciden, gana el código y el documento se corrige.
  El supervisor administra, confronta, recomienda y verifica. No construye.
  Nada se cierra sin QA, y el dueño da el visto bueno.
  Nada se borra de un documento de estado: se tacha en el sitio con su motivo y su fecha.
  Los mandamientos completos están en MANDAMIENTOS-DEL-SUPERVISOR.md.
REGLAS
