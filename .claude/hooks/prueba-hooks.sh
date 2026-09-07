#!/bin/bash
# prueba-hooks.sh — la batería de las DOS direcciones. Se ejecuta A MANO, no es un hook.
#
# QUE HACE: por cada puerta, comprueba que BLOQUEA lo que debe bloquear Y QUE DEJA
#           PASAR lo que debe dejar pasar, con la forma de entrada que Claude Code
#           produce de verdad: rutas absolutas con contrabarras, texto con tildes,
#           y los nombres de campo reales del JSON.
#
# POR QUE EXISTE (dueno, 2026-08-25): en una sola jornada CINCO puertas de
#   diecinueve fallaron por la misma firma —probadas solo en la direccion de
#   bloquear—, y la quinta hacia cumplir lo CONTRARIO de su propia regla:
#     carril.sh              denegaba a los 7 roles TODOS sus archivos
#     entregable-congelado   dejaba pasar la edicion de un entregable congelado
#     propiedad-por-rol      3 de 6 rutas ajenas pasaban
#     pase-completo          respondia "ask" a TODO, incluido un pase completo
#     cita-el-mandamiento    rechazaba «II · XII», que es la forma que el 0-bis EXIGE
#   Textual del dueno: «la cuarta vez con la misma firma ya no es coincidencia: es
#   como se escribio el kit entero».
#
# 📐 LAS DOS REGLAS QUE ESTA BATERIA HACE CUMPLIR, y salen de esos cinco casos:
#   1. Una puerta probada solo en la direccion de bloquear es MEDIA puerta.
#      Denegar de mas se ve exactamente igual que denegar bien si solo miras esa mitad.
#   2. Y se prueba con la entrada que el sistema REAL produce. Una puerta probada
#      con una entrada que el sistema nunca genera no esta probada: esta ensayada.
#      El caso 1 dio 27 de 27 con rutas POSIX inventadas mientras estaba abierta.
#
# ⛔ NO cubre: que la puerta este CABLEADA en settings.json. Esto prueba la logica
#    del guion, no que Claude Code lo llame. Eso solo se ve provocandolo en vivo.

set -u
raiz="${CLAUDE_PROJECT_DIR:-$(pwd)}"
# ⚠️ La raíz SIEMPRE en forma Windows (C:/...), venga de donde venga. Sin esto,
#    lanzada a pelo desde Git Bash la raíz sale POSIX (/c/Users/...) y WIN()
#    fabrica \c\Users\... — una ruta que NI Claude Code NI Windows producen
#    jamás. Medido el 2026-08-28: los 3 casos PASA de la sección 20 daban rojo
#    falso lanzados sin la variable, y verde con ella. Una batería cuyo
#    veredicto depende de CÓMO se lanza viola su propia regla n.º 2: se prueba
#    con la entrada que el sistema real produce.
command -v cygpath >/dev/null 2>&1 && raiz=$(cygpath -m "$raiz")
# Y se EXPORTA, porque los guiones que esta batería invoca la leen igual que en
# producción — donde Claude Code SIEMPRE la pone (medido con sondas el
# 2026-08-27). Sin exportarla, en un lanzamiento a pelo las puertas de ruta
# corrían con base vacía y la exención de «fuera del proyecto» de
# no-crear-documentos daba por ajena TODA ruta absoluta: 5 casos BLOQUEA
# fallaban ABIERTOS — la mitad mala de una puerta, medido el 2026-08-28.
export CLAUDE_PROJECT_DIR="$raiz"
cd "$raiz" || exit 1
H="$raiz/.claude/hooks"
command -v jq >/dev/null 2>&1 || { echo "Falta jq."; exit 1; }

# La forma REAL: ruta absoluta, con contrabarras, con espacios en el nombre.
WIN() { printf '%s' "$raiz/$1" | sed 's|/|\\|g'; }

fallos=0; pasan=0

# veredicto <hook> <json>  ->  BLOQUEA | PASA
veredicto() {
  local hook="$1" json="$2" salida rc guion="$H/$1.sh"
  # ⚠️ Si el guion no existe, esta prueba FALLA RUIDOSAMENTE. La primera version
  #    llamaba a "$H/$hook" sin el .sh: bash no encontraba nada, la salida venia
  #    vacia y el harness lo leia como PASA. Resultado: 49 casos, LOS 49 «PASA»,
  #    incluidas puertas que yo habia visto denegar diez minutos antes.
  #    Un banco de pruebas que no encuentra lo que prueba y dice «todo bien» es
  #    peor que no tenerlo: es la misma firma que este banco existe para cazar.
  [[ -f "$guion" ]] || { echo "NO-EXISTE:$guion"; return; }
  salida=$(printf '%s' "$json" | bash "$guion" 2>/dev/null); rc=$?
  if [[ $rc -eq 2 ]]; then echo "BLOQUEA"; return; fi
  [[ -z "$salida" ]] && { echo "PASA"; return; }
  local d
  d=$(printf '%s' "$salida" | jq -r '
        .hookSpecificOutput.permissionDecision // .decision // "aviso"' 2>/dev/null) || { echo "NO-JSON"; return; }
  case "$d" in
    deny|block|ask) echo "BLOQUEA" ;;
    allow)          echo "PASA" ;;
    *)              echo "AVISA" ;;   # PostToolUse informativo: no decide
  esac
}

caso() { # <hook> <esperado> <etiqueta> <json>
  local hook="$1" esp="$2" et="$3" json="$4" real
  real=$(veredicto "$hook" "$json")
  if [[ "$real" == "$esp" ]]; then
    pasan=$((pasan+1)); printf '    %-8s %-52s %s\n' "ok" "$et" "$real"
  else
    fallos=$((fallos+1)); printf '    %-8s %-52s %s (esperaba %s)\n' "<<<FALLA" "$et" "$real" "$esp"
  fi
}

J_BASH() { printf '{"tool_name":"Bash","tool_input":{"command":"%s"}}' "$1"; }
J_RUTA() { printf '{"tool_name":"%s","tool_input":{"file_path":"%s"},"agent_type":"%s","agent_id":"%s"}' \
             "${2:-Write}" "$(printf '%s' "$1" | sed 's|\\|\\\\|g')" "${3:-}" "${4:-}"; }
J_TXT()  { jq -nc --arg t "$1" --arg r "${2:-planificador}" \
             '{agent_type:$r, last_assistant_message:$t, stop_hook_active:false}'; }

RELLENO="Detalle de la medicion, con su comando y su salida literal. "
LARGO=""; for i in $(seq 1 14); do LARGO="$LARGO$RELLENO"; done

echo
echo "  ═══ 1 · cuenta-antes-de-borrar.sh — borrado recursivo ═══"
caso cuenta-antes-de-borrar BLOQUEA "rm -rf de una carpeta"          "$(J_BASH 'rm -rf build')"
caso cuenta-antes-de-borrar PASA    "rm de UN archivo (no recursivo)" "$(J_BASH 'rm /tmp/uno.txt')"
caso cuenta-antes-de-borrar PASA    "un comando que no borra nada"    "$(J_BASH 'ls -la')"

echo "  ═══ 2 · sin-add-all.sh — git add masivo ═══"
caso sin-add-all BLOQUEA "git add -A"                  "$(J_BASH 'git add -A')"
caso sin-add-all PASA    "git add POR RUTA explicita"  "$(J_BASH 'git add PENDIENTES.md')"
caso sin-add-all PASA    "git commit, que no es add"   "$(J_BASH 'git commit -m x')"

echo "  ═══ 3 · no-crear-documentos.sh — lista blanca ═══"
caso no-crear-documentos BLOQUEA "un .md que no es de los cuatro" "$(J_RUTA "$(WIN 'docs/INVENTADO.md')")"
caso no-crear-documentos PASA    "PENDIENTES.md"                  "$(J_RUTA "$(WIN 'PENDIENTES.md')")"
caso no-crear-documentos PASA    "ESTADO.md"                      "$(J_RUTA "$(WIN 'ESTADO.md')")"
caso no-crear-documentos PASA    "un .sh, que no es documento"    "$(J_RUTA "$(WIN '.claude/hooks/x.sh')")"

echo "  ═══ 4 · propiedad-por-rol.sh — el supervisor no toca producto ajeno ═══"
# ⚠️ Desde el 2026-08-28 esta puerta respeta la LLAVE-DEL-DUENO (con ella puesta
#    habla el dueño y TODO pasa). Con llave, los casos BLOQUEA no se pueden
#    probar y se declaran — nunca en falso verde ni en falso rojo.
if [[ -f "$raiz/.claude/LLAVE-DEL-DUENO" ]]; then
  echo "    ⚠️  LLAVE PUESTA: los 4 casos BLOQUEA de esta seccion no se prueban. Borra la llave y reince la bateria."
else
  caso propiedad-por-rol BLOQUEA "app/main.py"                 "$(J_RUTA "$(WIN 'app/main.py')")"
  caso propiedad-por-rol BLOQUEA "app/config.yaml"             "$(J_RUTA "$(WIN 'app/config.yaml')")"
  caso propiedad-por-rol BLOQUEA "un mockup"                   "$(J_RUTA "$(WIN 'assets-portal/mockups/x.html')")"
  caso propiedad-por-rol BLOQUEA "docs/adr/ADR-0001.md"        "$(J_RUTA "$(WIN 'docs/adr/ADR-0001.md')")"
fi
caso propiedad-por-rol PASA    "ESTADO.md, que SI es suyo"   "$(J_RUTA "$(WIN 'ESTADO.md')")"
caso propiedad-por-rol PASA    "un hook, que es suyo"        "$(J_RUTA "$(WIN '.claude/hooks/carril.sh')")"
caso propiedad-por-rol PASA    "dentro de un subagente no aplica" "$(J_RUTA "$(WIN 'app/main.py')" Write '' 'ag-123')"

echo "  ═══ 5 · solo-supervisor-recuerda.sh — el cerebro es del supervisor ═══"
caso solo-supervisor-recuerda PASA    "el supervisor (sin agent_id)" \
  '{"tool_name":"mcp__gbrain__recall","agent_id":""}'
caso solo-supervisor-recuerda BLOQUEA "un subagente" \
  '{"tool_name":"mcp__gbrain__recall","agent_id":"ag-123"}'

echo "  ═══ 6 · pase-completo.sh — el pase lleva sus piezas ═══"
caso pase-completo BLOQUEA "un prompt vago de una linea" \
  "$(jq -nc '{tool_name:"Task",tool_input:{prompt:"arregla el frontend"}}')"
# ⚠️ El pase ENTERO, sin `head -c`. La primera version lo truncaba a 2500 bytes y
#    cortaba la seccion «Fronteras», que es una de las piezas que este hook exige:
#    daba «ask» sobre un pase que estaba completo. El pase se prueba entero o no
#    se prueba.
caso pase-completo PASA    "el pase REAL de EN-CURSO.md, ENTERO" \
  "$(jq -nc --rawfile p EN-CURSO.md '{tool_name:"Task",tool_input:{prompt:$p}}')"

echo "  ═══ 7 · informe-completo.sh y reencarrilar.sh — el informe cierra bien ═══"
for h in informe-completo reencarrilar; do
  caso "$h" PASA    "[$h] informe con tilde: «qué NO cubre»" \
    "$(J_TXT "La premisa se sostiene, lo comprobé con grep. $LARGO Qué NO cubre esto: no revisé las otras filas.")"
  caso "$h" PASA    "[$h] informe SIN tilde: «que NO cubre»" \
    "$(J_TXT "La premisa se sostiene, lo comprobe con grep. $LARGO Que NO cubre esto: no revise las otras filas.")"
  caso "$h" BLOQUEA "[$h] informe largo sin las secciones" "$(J_TXT "Cambie la fila y quedo lista. $LARGO")"
  caso "$h" BLOQUEA "[$h] corto: «ya esta listo, todo funciona»" "$(J_TXT 'Ya esta listo, todo funciona bien.')"
  caso "$h" PASA    "[$h] corto legitimo: «no me corresponde»" \
    "$(J_TXT 'No me corresponde. Yo hago especificaciones. Lo que pides lo hace programador.')"
  caso "$h" PASA    "[$h] corto legitimo: «no encontre evidencia»" \
    "$(J_TXT 'No encontre evidencia en la documentacion oficial. Devuelvo el pase.')"
done

echo "  ═══ 8 · cita-el-mandamiento.sh — mandamiento 0-bis ═══"
caso cita-el-mandamiento PASA    "cita DESNUDA con · : «II · III · XII»" "$(J_TXT "$LARGO
II · III · XII")"
caso cita-el-mandamiento PASA    "cita desnuda de uno: «XII»"            "$(J_TXT "$LARGO
XII")"
caso cita-el-mandamiento PASA    "cita desnuda con -bis: «0-bis · I-bis»" "$(J_TXT "$LARGO
0-bis · I-bis")"
caso cita-el-mandamiento PASA    "con la palabra: «mandamiento XII»"      "$(J_TXT "$LARGO
mandamiento XII")"
caso cita-el-mandamiento PASA    "con NEGRITA en el numero: «Mandamiento **XVI**.»" "$(J_TXT "$LARGO
Mandamiento **XVI**.")"
caso cita-el-mandamiento PASA    "cita desnuda en negrita: «**XVI**»"      "$(J_TXT "$LARGO
**XVI**")"
caso cita-el-mandamiento BLOQUEA "sin ninguna cita"                       "$(J_TXT "$LARGO")"
caso cita-el-mandamiento BLOQUEA "una «I» suelta en la prosa no es cita"  "$(J_TXT "$LARGO
La opcion I del menu quedo descartada por coste.")"

echo "  ═══ 9 · ficha-del-rol.sh — SubagentStart inyecta el puesto ═══"
caso ficha-del-rol AVISA "nace un planificador: le llega su ficha" '{"agent_type":"planificador"}'
caso ficha-del-rol PASA  "un rol que no existe: no inventa ficha"  '{"agent_type":"inexistente"}'

echo "  ═══ 10 · avisos-de-escritura.sh — mandamiento XIX ═══"
tmpx="$(mktemp).md"; printf 'Tareas: P1 arreglar, D2 revisar.\n' > "$tmpx"
caso avisos-de-escritura AVISA "un archivo con etiquetas P1/D2" "$(J_RUTA "$tmpx")"
# ⚠️ Con el · del mandamiento XIX, no con guion. La exencion del hook busca
#    literalmente `c24·T1`. Escrito `c24-T1` avisa igual — es un falso positivo
#    pequeno, registrado en PENDIENTES, no arreglado aqui: cambiar la exencion
#    sin que el dueno lo decida seria ampliar la regla por mi cuenta.
printf 'Tareas: c24·T1 arreglar, c24·T2 revisar.\n' > "$tmpx"
caso avisos-de-escritura PASA  "el mismo con la numeracion c24·T1 correcta" "$(J_RUTA "$tmpx")"
rm -f "$tmpx"

echo "  ═══ 11 · entregable-congelado.sh — mandamiento XVII ═══"
lista="$raiz/.claude/congelados.txt"; copia="$(mktemp)"
cp "$lista" "$copia" 2>/dev/null || : > "$copia"
printf '.claude/hooks/prueba-hooks.sh\n' >> "$lista"
caso entregable-congelado BLOQUEA "editar el archivo CONGELADO" \
  "$(J_RUTA "$(WIN '.claude/hooks/prueba-hooks.sh')" Edit)"
caso entregable-congelado PASA    "editar otro que NO esta congelado" \
  "$(J_RUTA "$(WIN 'ESTADO.md')" Edit)"
printf '%s\r\n' '.claude/hooks/prueba-hooks.sh' > "$lista.crlf"; cat "$copia" "$lista.crlf" > "$lista"; rm -f "$lista.crlf"
caso entregable-congelado BLOQUEA "congelado con final de linea CRLF" \
  "$(J_RUTA "$(WIN '.claude/hooks/prueba-hooks.sh')" Edit)"
cp "$copia" "$lista"; rm -f "$copia"

echo "  ═══ 12 · abogado-solo-sus-archivos.sh — ⚠️ HOOK HUERFANO (PENDIENTES #11) ═══"
echo "    (no esta en settings.json: se prueba su logica, pero HOY NO LO LLAMA NADIE)"
caso abogado-solo-sus-archivos BLOQUEA "el abogado sobre app/" \
  "$(J_RUTA "$(WIN 'app/main.py')" Write 'abogado')"
caso abogado-solo-sus-archivos PASA    "el abogado sobre lo suyo" \
  "$(J_RUTA "$(WIN 'docs/QUE-HACEMOS-Y-QUE-NO.md')" Write 'abogado')"

echo "  ═══ 13 · carril.sh — delegado en prueba-carril.sh ═══"
if bash "$H/prueba-carril.sh" >/dev/null 2>&1; then
  pasan=$((pasan+1)); printf '    %-8s %-52s %s\n' "ok" "prueba-carril.sh (27 casos x 2 formas de ruta)" "PASA"
else
  fallos=$((fallos+1)); printf '    %-8s %-52s %s\n' "<<<FALLA" "prueba-carril.sh" "FALLA"
fi

echo "  ═══ 14 · pase-sin-sesgo.sh — la solución no viaja dentro del pase ═══"
caso pase-sin-sesgo BLOQUEA "pase QA con el diagnostico dentro" \
  "$(jq -nc '{tool_name:"Task",tool_input:{subagent_type:"qa",prompt:"Audita app-roadmap.html. La causa es el IntersectionObserver."}}')"
caso pase-sin-sesgo BLOQUEA "pase que dicta el arreglo linea a linea" \
  "$(jq -nc '{tool_name:"Task",tool_input:{subagent_type:"disenador",prompt:"En la linea 214 cambia opacity:0, la solucion es quitar el observer"}}')"
caso pase-sin-sesgo BLOQUEA "pase con la expectativa: «deberia dar»" \
  "$(jq -nc '{tool_name:"Task",tool_input:{subagent_type:"qa",prompt:"Mide el contraste del boton; deberia dar 4.5:1 o mas"}}')"
caso pase-sin-sesgo PASA    "pase limpio: objeto + criterio con numeros" \
  "$(jq -nc '{tool_name:"Task",tool_input:{subagent_type:"qa",prompt:"Audita app-roadmap.html en frio. Criterio de cierre: contraste 4.5:1 o mas en los 12 textos, sin fogonazo, medido con Playwright. Informa con numeros."}}')"
caso pase-sin-sesgo PASA    "pase de programador sin diagnostico" \
  "$(jq -nc '{tool_name:"Task",tool_input:{subagent_type:"programador",prompt:"Escribe la prueba y el codigo de login_guard segun la spec de EN-CURSO.md seccion 2. Cierra cuando pytest la recoja y pase."}}')"

echo "  ═══ 15 · candado-del-dueno.sh — las barandillas son del dueño ═══"
if [[ -f "$raiz/.claude/LLAVE-DEL-DUENO" ]]; then
  echo "    ⚠️  LLAVE-DEL-DUENO PUESTA: el candado esta abierto y sus 8 casos NO SE PRUEBAN."
  echo "        Borra la llave y vuelve a correr la bateria — abierta, esta puerta no protege nada."
else
  caso candado-del-dueno BLOQUEA "Edit a un hook"            "$(J_RUTA "$(WIN '.claude/hooks/carril.sh')" Edit)"
  caso candado-del-dueno BLOQUEA "Edit a settings.json"      "$(J_RUTA "$(WIN '.claude/settings.json')" Edit)"
  caso candado-del-dueno BLOQUEA "Edit a los MANDAMIENTOS"   "$(J_RUTA "$(WIN 'docs/MANDAMIENTOS-DEL-SUPERVISOR.md')" Edit)"
  caso candado-del-dueno BLOQUEA "Write a PROCESOS.md"       "$(J_RUTA "$(WIN 'PROCESOS.md')")"
  caso candado-del-dueno PASA    "Edit a un mockup (libre)"  "$(J_RUTA "$(WIN 'assets-portal/mockups/x.html')" Edit)"
  caso candado-del-dueno BLOQUEA "Bash que sobreescribe un hook" "$(J_BASH 'echo x > .claude/hooks/carril.sh')"
  caso candado-del-dueno PASA    "Bash que LEE un hook con 2>/dev/null" "$(J_BASH 'cat .claude/hooks/carril.sh 2>/dev/null')"
  caso candado-del-dueno PASA    "Bash git normal"           "$(J_BASH 'git status')"
  # Los cuerpos de heredoc son DATO (2026-08-28, defecto cazado por la sesion
  # del supervisor): prosa con «> » de cita markdown y PROCESOS.md como texto
  # ya no dispara; la linea de APERTURA del heredoc se conserva, asi que un
  # heredoc cuyo destino SI es una barandilla sigue denegado. Y las comillas
  # NO se quitan: la ruta citada sigue contando.
  caso candado-del-dueno PASA    "heredoc de PROSA que cita barandillas" \
    "$(jq -nc '{tool_name:"Bash",tool_input:{command:"cat > /tmp/x.txt <<'\''EOF'\''\n> cita markdown, no redireccion\nmenciona PROCESOS.md como texto\nEOF"}}')"
  caso candado-del-dueno BLOQUEA "heredoc cuyo DESTINO es un hook" \
    "$(jq -nc '{tool_name:"Bash",tool_input:{command:"cat > .claude/hooks/carril.sh <<'\''EOF'\''\nexit 0\nEOF"}}')"
  caso candado-del-dueno BLOQUEA "rm con la ruta ENTRE COMILLAS" \
    "$(jq -nc '{tool_name:"Bash",tool_input:{command:"rm '\''.claude/hooks/x.sh'\''"}}')"
fi

echo "  ═══ 16 · no-crear-documentos.sh — la puerta del .txt (2026-08-27) ═══"
caso no-crear-documentos BLOQUEA "un PASE-*.txt nuevo"       "$(J_RUTA "$(WIN 'assets-portal/mockups/PASE-nuevo.txt')")"
caso no-crear-documentos PASA    "requirements.txt (lista blanca)" "$(J_RUTA "$(WIN 'requirements.txt')")"
caso no-crear-documentos PASA    "un .md FUERA del proyecto (scratchpad)" \
  '{"tool_name":"Write","tool_input":{"file_path":"C:\\Users\\josem\\AppData\\Local\\Temp\\claude\\x\\notas.md"}}'

echo "  ═══ 17 · afirmacion-con-fuente.sh — el supervisor no afirma sin fuente ═══"
caso afirmacion-con-fuente BLOQUEA "«verificado y funciona» sin ninguna fuente" \
  "$(J_TXT 'Verificado: el hook funciona y todo quedo listo.')"
caso afirmacion-con-fuente PASA    "la misma afirmacion CON su comando" \
  "$(J_TXT 'Verificado: `git status --porcelain` -> vacio, el hook funciona.')"
caso afirmacion-con-fuente PASA    "afirmacion honesta: «no lo he comprobado»" \
  "$(J_TXT 'El informe dice que funciona; lo dice QA y NO lo he comprobado.')"
caso afirmacion-con-fuente PASA    "turno conversacional sin afirmaciones" \
  "$(J_TXT 'De acuerdo, empiezo por la primera fila y te aviso.')"
caso afirmacion-con-fuente PASA    "si ya bloqueo una vez, no entra en bucle" \
  "$(jq -nc '{last_assistant_message:"Verificado: todo funciona.", stop_hook_active:true}')"

echo "  ═══ 18 · docs-al-dia.sh — los documentos viajan con su código ═══"
caso docs-al-dia PASA "un comando que no es push"  "$(J_BASH 'git status')"
caso docs-al-dia PASA "un commit, que no es push"  "$(J_BASH 'git commit -m x')"
# La dirección BLOQUEA depende del estado real del repositorio (commits sin subir
# y sin documento en el lote), así que se prueba señalando un upstream FICTICIO
# muy atrás solo si existe; si no, se declara no probada — nunca en falso verde.
if git -C "$raiz" rev-parse 'HEAD~3' >/dev/null 2>&1; then
  lote=$(git -C "$raiz" log HEAD~3..HEAD --name-only --pretty=format: | grep -v '^$' | sort -u)
  if printf '%s\n' "$lote" | grep -Eq '^(ESTADO|EN-CURSO|DECISIONES|PENDIENTES)\.md$'; then
    echo "    (el rango HEAD~3..HEAD lleva documento: la direccion BLOQUEA no se puede provocar aqui — declarado, no en verde)"
  else
    echo "    (la direccion BLOQUEA se prueba EN VIVO: requiere un push real sin documentos en el lote)"
  fi
fi

echo "  ═══ 19 · pase-en-su-puesto.sh — cada encargo a su puesto ═══"
caso pase-en-su-puesto BLOQUEA "planificador + escanear el proyecto" \
  "$(jq -nc '{tool_name:"Task",tool_input:{subagent_type:"planificador",prompt:"Escanea el proyecto para saber el estado actual de los mockups"}}')"
caso pase-en-su-puesto PASA    "planificador + investigar arquitectura (suyo)" \
  "$(jq -nc '{tool_name:"Task",tool_input:{subagent_type:"planificador",prompt:"Investiga en la documentacion oficial de FastAPI y en dos proyectos grandes cual es la arquitectura mas segura para auth, compara y recomienda con criterios de aceptacion"}}')"
caso pase-en-su-puesto BLOQUEA "qa + arreglar lo que encuentre" \
  "$(jq -nc '{tool_name:"Task",tool_input:{subagent_type:"qa",prompt:"Audita y arregla los contrastes que encuentres mal"}}')"
caso pase-en-su-puesto BLOQUEA "hacker + parchear" \
  "$(jq -nc '{tool_name:"Task",tool_input:{subagent_type:"hacker-seguridad",prompt:"Si encuentras la inyeccion, parchea el endpoint"}}')"
caso pase-en-su-puesto BLOQUEA "programador + elegir la arquitectura" \
  "$(jq -nc '{tool_name:"Task",tool_input:{subagent_type:"programador",prompt:"Elige la arquitectura del modulo de mailbox y construyelo"}}')"
caso pase-en-su-puesto PASA    "programador + construir sobre spec (suyo)" \
  "$(jq -nc '{tool_name:"Task",tool_input:{subagent_type:"programador",prompt:"Construye login_guard segun la spec de EN-CURSO.md seccion 2, primero la prueba"}}')"
caso pase-en-su-puesto PASA    "rol sin encargos_vedados (lector)" \
  "$(jq -nc '{tool_name:"Task",tool_input:{subagent_type:"lector",prompt:"Busca si ya esta escrito lo del fogonazo"}}')"

echo "  ═══ 20 · los documentos de conocimiento (2026-08-28) — lista blanca y propiedad ═══"
caso no-crear-documentos PASA    "crear docs/ARQUITECTURA.md (autorizado)" "$(J_RUTA "$(WIN 'docs/ARQUITECTURA.md')")"
caso no-crear-documentos PASA    "crear docs/adr/ADR-0001-x.md (patron)"   "$(J_RUTA "$(WIN 'docs/adr/ADR-0001-monolito.md')")"
caso no-crear-documentos PASA    "crear docs/INFORMES-QA.md"               "$(J_RUTA "$(WIN 'docs/INFORMES-QA.md')")"
caso no-crear-documentos BLOQUEA "ARQUITECTURA.md FUERA de docs/"          "$(J_RUTA "$(WIN 'assets-portal/ARQUITECTURA.md')")"
caso no-crear-documentos BLOQUEA "un ADR fuera de docs/adr/"               "$(J_RUTA "$(WIN 'docs/ADR-0009-suelto.md')")"
caso no-crear-documentos BLOQUEA "un QA-hallazgos.md suelto en mockups"    "$(J_RUTA "$(WIN 'assets-portal/mockups/QA-hallazgos.md')")"
if [[ -f "$raiz/.claude/LLAVE-DEL-DUENO" ]]; then
  echo "    ⚠️  LLAVE PUESTA: los 3 casos de propiedad de esta seccion no se prueban (la llave abre esa puerta a proposito)."
else
  caso propiedad-por-rol BLOQUEA "supervisor sobre ARQUITECTURA.md (planificador)" "$(J_RUTA "$(WIN 'docs/ARQUITECTURA.md')" Edit)"
  caso propiedad-por-rol BLOQUEA "supervisor sobre el libro (programador)"         "$(J_RUTA "$(WIN 'docs/GUIA-TECNOLOGIAS-BITACORA.md')" Edit)"
  caso propiedad-por-rol BLOQUEA "supervisor sobre INFORMES-QA.md (QA)"            "$(J_RUTA "$(WIN 'docs/INFORMES-QA.md')" Edit)"
fi

echo "  ═══ 21 · leer-antes-de-lanzar.sh — mandamiento XVI con puerta (2026-08-28) ═══"
# Raíz de ensayo propia: doc con contenido real y transcripts fabricados, para
# que las dos direcciones se prueben aunque la LLAVE real esté puesta.
E21=$(mktemp -d); mkdir -p "$E21/docs"
printf '# INFORMES QA\n## AUDITORIA 1\napp-x.html APTA, contraste 4.6:1\n' > "$E21/docs/INFORMES-QA.md"
printf '{"otro":"turno sin lecturas"}\n' > "$E21/t-sin.jsonl"
printf '{"tool_use":{"name":"Read","input":{"file_path":"C:\\\\x\\\\docs\\\\INFORMES-QA.md"}}}\n' > "$E21/t-con.jsonl"
J21() { jq -nc --arg t "$1" --arg r "$2" '{tool_input:{subagent_type:$r,prompt:"audita x"},transcript_path:$t}'; }
_v21() { local salida; salida=$(printf '%s' "$1" | CLAUDE_PROJECT_DIR="$E21" bash "$H/leer-antes-de-lanzar.sh" 2>/dev/null); \
  printf '%s' "$salida" | jq -r '.hookSpecificOutput.permissionDecision // "PASA"' 2>/dev/null || echo PASA; }
c21() { local r; r=$(_v21 "$3"); [[ -z "$r" ]] && r=PASA
  if [[ "$r" == "$2" ]]; then pasan=$((pasan+1)); printf '    %-8s %-52s %s\n' "ok" "$1" "$r";
  else fallos=$((fallos+1)); printf '    %-8s %-52s %s (esperaba %s)\n' "<<<FALLA" "$1" "$r" "$2"; fi; }
c21 "lanzar QA sin haber leido INFORMES-QA" deny "$(J21 "$E21/t-sin.jsonl" qa)"
c21 "lanzar QA CON la lectura en el transcript" PASA "$(J21 "$E21/t-con.jsonl" qa)"
c21 "lanzar planificador (sin requisito de lectura)" PASA "$(J21 "$E21/t-sin.jsonl" planificador)"
printf '# marco\n*(Marco a la espera de su primera seccion.)*\n' > "$E21/docs/INFORMES-QA.md"
c21 "doc todavia en marco vacio: no exige lectura" PASA "$(J21 "$E21/t-sin.jsonl" qa)"
rm -rf "$E21"

echo "  ═══ 22 · PATRONES MUERTOS — las barandillas apuntan a carpetas que existen ═══"
# POR QUÉ EXISTE (2026-08-30): static-nuevo/ pasó a llamarse static/ y NUEVE
# patrones de alcance-por-rol.json más una línea de propiedad-por-rol.sh se
# quedaron apuntando al nombre muerto — fallo ABIERTO en el frontend entero,
# cazado por el supervisor días de calendario cero pero commits después. Un
# renombrado de carpeta no avisa a .claude/: este detector hace que la batería
# cante el agujero en rojo la próxima vez.
muertos=0
while IFS= read -r patron; do
  base="${patron%%/\*\**}"
  # solo patrones de carpeta con raíz literal (app/**, static/**, docs/adr/**…);
  # los de archivo (*.py, ESTADO.md) y los que empiezan por comodín no se juzgan aquí
  [[ "$patron" == */\*\* && "$base" != *"*"* && -n "$base" ]] || continue
  # PREMATUROS declarados: destinos del árbol de CLAUDE.md §3 que la fase 0 aún
  # no construyó. No son muertos — el nombre es el canónico y la regla espera a
  # su carpeta. Cuando existan, esta lista sobra y no estorba. Detectado el
  # 2026-08-30: alembic/** saltó como muerto siendo prematuro.
  case "$base" in alembic) continue ;; esac
  if [[ ! -e "$raiz/$base" ]] && ! git -C "$raiz" ls-files "$base" 2>/dev/null | grep -q .; then
    fallos=$((fallos+1)); muertos=$((muertos+1))
    printf '    %-8s %-52s %s\n' "<<<FALLA" "patron muerto en alcance-por-rol.json" "$patron"
  fi
done < <(jq -r 'to_entries[] | select(.value|type=="object") | (.value.escribe // [])[], (.value.no_escribe // [])[]' "$raiz/.claude/agents/alcance-por-rol.json" 2>/dev/null | sort -u | tr -d '\r')
if [[ $muertos -eq 0 ]]; then
  pasan=$((pasan+1)); printf '    %-8s %-52s %s\n' "ok" "todos los patrones de carpeta apuntan a algo vivo" "PASA"
fi

printf '\n  %d pasan · %d FALLAN\n\n' "$pasan" "$fallos"
[[ "$fallos" -eq 0 ]] || exit 1
