#!/bin/bash
# barrido-pendientes.sh — se ejecuta A MANO, no es un hook.
#
# QUE HACE: corre el comando de CADA fila de PENDIENTES.md y dice cual ya no
#           corre. Solo los de lectura: los que instalan o modifican se declaran
#           y NO se ejecutan.
#
# POR QUE EXISTE (planificador, 2026-08-25): «si la fila 12 llevaba un | sin
#   escapar y la 14 un comando que no corre, la pregunta que nadie hizo es
#   cuantas de las 15 filas tienen comandos que ya no corren».
#
# 📐 UNA FILA CUYO COMANDO NO CORRE ES UNA DEUDA QUE NADIE PUEDE VERIFICAR. Trae
#    su numero, parece medida, y no hay forma de comprobarla. Es peor que una fila
#    sin comando, porque una fila sin comando se ve incompleta a simple vista.

set -u
cd "${CLAUDE_PROJECT_DIR:-$(pwd)}" || exit 1
[[ -f PENDIENTES.md ]] || { echo "No hay PENDIENTES.md aqui."; exit 1; }

# fila|clase|comando
#   lectura       se ejecuta
#   sin-comando   NO existe comando que verifique la fila, y la fila lo dice con
#                 esas palabras. NO es lo mismo que «no-es-comando»: aquella era
#                 una nota disfrazada de comando; esta es una ausencia declarada.
#   MODIFICA      NO se ejecuta porque instala o cambia cosas, y se dice por que
#   respuesta-negativa  el comando CORRE bien y su respuesta correcta es «no».
#                 Un rc distinto de 0 NO significa que el comando este roto:
#                 significa que la cosa no esta. Anadida el 2026-08-25 por decision
#                 del dueno sobre la fila 17, con el argumento del planificador: la
#                 otra opcion —que la fila declare su dependencia y el barrido la
#                 salte— ESCONDE el caso; esta lo MIDE. Y el defecto vuelve en
#                 cualquier fila futura cuya respuesta correcta sea «no».
#                 📐 El dano que evita: un barrido que sale con rc=1 por una
#                 dependencia opcional ausente es una alarma que se aprende a
#                 ignorar, y entonces deja de avisar de las que si importan.
#   no-es-comando queda en el guion a proposito aunque hoy no lo use nadie: es la
#                 clase que hay que volver a ver si alguien mete otra nota en la
#                 columna «Comando».
CASOS=(
  "1|lectura|git ls-files app/ | wc -l"
  # ⚠️ Sin tuberia a proposito. `ls ... | head -1` devuelve el rc de HEAD, que es
  #    siempre 0, y con eso la rama del «no» de respuesta-negativa era INALCANZABLE
  #    (medido el 2026-08-25: con la ruta apuntando a algo inexistente seguia
  #    diciendo «RESPONDE (si)»). Se captura primero y se decide despues.
  "2|respuesta-negativa|f=\$(ls .venv/Scripts/lint-imports.exe .venv/bin/lint-imports 2>/dev/null | head -1); [ -n \"\$f\" ] && echo \"\$f\" || exit 1"
  "3|lectura|jq -r 'keys[]' .claude/agents/alcance-por-rol.json"
  "4|sin-comando|no lo hay: ver un hook dispararse exige una sesion viva y no deja rastro en el repo. Evidencia: ESTADO.md § La prueba en vivo"
  "5|lectura|jq -S '.hacker == (.\"hacker-seguridad\" | del(._alias_de))' .claude/agents/alcance-por-rol.json"
  "6|lectura|PIP=\$(ls .venv/Scripts/pip.exe .venv/bin/pip 2>/dev/null | head -1); PIP=\${PIP:-pip}; inst=\$(\"\$PIP\" list --format=freeze 2>/dev/null | cut -d= -f1 | tr '[:upper:]' '[:lower:]'); for t in \"1:schemathesis hypothesis import-linter\" \"2:cosmic-ray testcontainers\" \"3:vulture deptry radon xenon interrogate\" \"4:bandit semgrep atheris locust\"; do y=0; n=0; for p in \${t#*:}; do n=\$((n+1)); printf '%s\\n' \"\$inst\" | grep -qix \"\$p\" && y=\$((y+1)); done; printf 'tanda %s: %s/%s · ' \"\${t%%:*}\" \"\$y\" \"\$n\"; done; echo"
  "7|lectura|grep -c incidentes 'docs/Prarar el ambiente/kit-bitacora/kit-bitacora/EMPIEZA-AQUI.md'"
  "8|lectura|git ls-tree -r respaldo-codigo-24-08 --name-only -- tests/ | wc -l"
  "9|lectura|git ls-remote --tags origin"
  "10|lectura|R=../RESPALDO-Bitacora-fix-2026-08-18; if [ -d \"\$R\" ]; then comm -23 <(git ls-tree -r respaldo-codigo-24-08 --name-only -- app/ tests/ | sort) <(cd \"\$R\" && find app tests -type f -not -path '*__pycache__*' | sort) | wc -l; else echo 'el respaldo no esta en esta maquina'; fi"
  "11|lectura|for f in .claude/hooks/*.sh; do head -10 \"\$f\" | grep -qi 'no es un hook' && continue; grep -q \"\$(basename \$f)\" .claude/settings.json || echo \"\$f\"; done"
  "12|lectura|nombres=\$(grep -o '^ *[A-Za-z.|-]*) *permitido=1' .claude/hooks/no-crear-documentos.sh | sed 's/).*//' | tr -d ' ' | tr '|' '\\n' | grep '\\.md\$'); printf '%s permitidos: %s\\n' \"\$(printf '%s\\n' \"\$nombres\" | wc -l)\" \"\$(printf '%s ' \$nombres)\""
  # Comando cambiado el 2026-08-25 al cerrarse la fila: `ls` de los dos arboles
  # probaba que EXISTEN dos, que ya no es lo que la fila afirma. Lo que afirma
  # ahora es que NO eran duplicados, y eso se prueba comparando los titulos.
  "13|lectura|for f in A B C; do printf '%s -> fases: %s · PlAN MESTRO: %s\\n' \"\$f\" \"\$(head -1 docs/fases/FASE-\$f.md)\" \"\$(head -1 docs/'PlAN MESTRO'/FASE-\$f-*.md)\"; done"
  "14|lectura|jq -s '.[0] * .[1]' .claude/settings.json 'docs/Prarar el ambiente/V2/kit-bitacora/kit-bitacora/hooks/settings.json.NO-USAR' | jq -r '[.hooks|to_entries[]|.value[]|.hooks[]|.command]|unique|join(\", \")'"
  "15|lectura|bash .claude/hooks/prueba-carril.sh"
  "16|lectura|tot=\$(ls .claude/hooks/*.sh | wc -l); marc=\$(grep -lim1 'no es un hook' .claude/hooks/*.sh | wc -l); dec=\$(for f in .claude/hooks/*.sh; do grep -q \"\$(basename \$f)\" .claude/settings.json && echo x; done | wc -l); printf '%s .sh · %s con marcador · %s en settings.json · sin gobierno: %s\\n' \"\$tot\" \"\$marc\" \"\$dec\" \"\$((tot-marc-dec))\""
  "17|sin-comando|no lo hay sin recursion: la fila describe un defecto DE ESTE GUION, y el unico comando que lo ensena es correr el guion entero. Se comprueba a mano: bash .claude/hooks/barrido-pendientes.sh; echo rc=\$?"
  # --- filas 18 a 21, anadidas el 2026-08-25 ---------------------------------
  # ⚠️ `grep -c` devuelve rc=1 cuando cuenta CERO. La cuenta se captura en una
  #    variable y se imprime aparte: dejarla suelta marcaria la fila «YA NO
  #    CORRE» justo cuando dice la verdad — el defecto que pago la fila 17.
  # ⚠️ Y se ACOTA A LA FILA 2 con `sed -n`. Sobre el archivo entero el conteo da
  #    1, y ese 1 es la fila 18 citando el comando en su propio texto: una
  #    medicion que cuenta su propia cita no mide nada. Medido el 2026-08-25.
  "18|lectura|n=\$(sed -n '/^| 2 /p' PENDIENTES.md | grep -cF '.venv/bin/lint-imports'); printf '%s apariciones del comando del barrido dentro de la propia fila 2\\n' \"\${n:-0}\""
  # ⚠️ `grep` a secas (BRE), NO `grep -E`. En ERE el `|` es ALTERNACION, y el
  #    patron `^  \"[0-9]+|lectura|` casa con la cadena vacia: medido el
  #    2026-08-25, devolvia 95 —las lineas del archivo— para las cinco clases.
  #    En BRE el `|` es literal, que es lo que aqui hace falta.
  "19|lectura|for c in lectura respuesta-negativa sin-comando MODIFICA no-es-comando; do k=\$(grep -c \"^  \\\"[0-9]*|\$c|\" .claude/hooks/barrido-pendientes.sh); printf '%s:%s · ' \"\$c\" \"\${k:-0}\"; done; echo"
  "20|sin-comando|no lo hay: ejercer la rama del «no» exige MODIFICAR el entorno (borrar o mover el binario), que no es lectura, y ademas un tercero que lo verifique, que es de QA. Las dos mitades caen fuera de un comando de lectura de este rol"
  # ⚠️ Los dos patrones se LEEN del propio hook en vez de copiarse aqui. Copiarlos
  #    crearia exactamente la deriva que registra la fila 18.
  "21|lectura|pats=\$(grep -o \"grep -Eq '[^']*'\" .claude/hooks/avisos-de-escritura.sh | sed \"s/grep -Eq '//; s/'\$//\"); disp=\$(printf '%s\\n' \"\$pats\" | sed -n 1p); exim=\$(printf '%s\\n' \"\$pats\" | sed -n 2p); for t in 'c24-T1' 'c24·T1'; do if printf '%s' \"\$t\" | grep -Eq \"\$disp\" && ! printf '%s' \"\$t\" | grep -Eq \"\$exim\"; then r=AVISA; else r=exento; fi; printf '%s:%s · ' \"\$t\" \"\$r\"; done; echo"
)

corren=0; rotos=0; no_comando=0; no_ejecutados=0; sin_comando=0; negativas=0
printf '\n  %-5s %-14s %s\n' FILA VEREDICTO "PRIMERA LINEA DE SALIDA (o el motivo)"
printf '  %s\n' "--------------------------------------------------------------------------------"
for caso in "${CASOS[@]}"; do
  n="${caso%%|*}"; resto="${caso#*|}"; clase="${resto%%|*}"; cmd="${resto#*|}"
  case "$clase" in
    no-es-comando) printf '  %-5s %-14s %s\n' "$n" "NO-ES-COMANDO" "$cmd"; no_comando=$((no_comando+1)); continue ;;
    sin-comando)   printf '  %-5s %-14s %s\n' "$n" "SIN COMANDO" "$cmd"; sin_comando=$((sin_comando+1)); continue ;;
    MODIFICA)      printf '  %-5s %-14s %s\n' "$n" "NO EJECUTADO" "$cmd"; no_ejecutados=$((no_ejecutados+1)); continue ;;
    respuesta-negativa)
      # Se ejecuta igual que `lectura`. Lo que cambia es la LECTURA del rc: aqui
      # un rc!=0 es una respuesta, no un fallo, y no cuenta como roto.
      salida=$(eval "$cmd" 2>&1); rc=$?
      primera=$(printf '%s' "$salida" | head -1 | cut -c1-60)
      if [[ $rc -eq 0 ]]; then
        printf '  %-5s %-14s %s\n' "$n" "RESPONDE (si)" "${primera:-(sin salida)}"
      else
        printf '  %-5s %-14s %s\n' "$n" "RESPONDE (no)" "rc=$rc · la respuesta correcta es NO, y el comando esta sano"
      fi
      negativas=$((negativas+1)); continue ;;
  esac
  salida=$(eval "$cmd" 2>&1); rc=$?
  primera=$(printf '%s' "$salida" | head -1 | cut -c1-60)
  if [[ $rc -eq 0 ]]; then
    printf '  %-5s %-14s %s\n' "$n" "CORRE" "${primera:-(sin salida, rc=0)}"; corren=$((corren+1))
  else
    printf '  %-5s %-14s rc=%s · %s\n' "$n" "YA NO CORRE" "$rc" "${primera:-(sin salida)}"; rotos=$((rotos+1))
  fi
done

printf '\n  %d corren · %d YA NO CORREN · %d admiten respuesta negativa · %d no son comandos · %d sin comando declarado · %d no ejecutados a proposito\n\n' \
  "$corren" "$rotos" "$negativas" "$no_comando" "$sin_comando" "$no_ejecutados"
[[ "$rotos" -eq 0 ]] || exit 1
