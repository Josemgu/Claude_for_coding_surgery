#!/bin/bash
# prueba-carril.sh — la prueba que faltaba. Se ejecuta A MANO, no es un hook.
#
# QUÉ HACE: comprueba carril.sh en LAS DOS DIRECCIONES para cada rol: que
#           DENIEGA lo ajeno y que PERMITE lo propio.
#
# POR QUÉ EXISTE, y es el hallazgo que la motivó (dueño, 2026-08-25):
#   «Un guardián probado solo en la dirección de denegar es medio guardián — y
#    es exactamente cómo se coló este fallo.»
#   El 2026-08-25 carril.sh denegaba a los 7 roles TODOS sus archivos, incluidos
#   los suyos. Nadie lo vio porque solo se había probado que denegaba lo ajeno,
#   y denegar de más se ve igual que denegar bien si solo miras esa mitad.
#
# 📐 Las expectativas van ESCRITAS A MANO, no derivadas de alcance-por-rol.json.
#    Una prueba que lee la misma tabla que el código bajo prueba solo demuestra
#    que los dos leen igual — confirmaría el fallo en vez de cazarlo.

set -u
raiz="${CLAUDE_PROJECT_DIR:-$(pwd)}"
hook="$raiz/.claude/hooks/carril.sh"
[[ -f "$hook" ]] || { echo "No existe $hook"; exit 1; }

for dep in jq bash; do
  command -v "$dep" >/dev/null 2>&1 || { echo "Falta $dep en el PATH."; exit 1; }
done

# rol · ruta · esperado (PERMITE | DENIEGA)
CASOS=(
  "programador|app/main.py|PERMITE"
  "programador|tests/test_x.py|PERMITE"
  "programador|assets-portal/mockups/x.html|DENIEGA"
  "programador|PENDIENTES.md|DENIEGA"
  "disenador|assets-portal/mockups/x.html|PERMITE"
  "disenador|app/main.py|DENIEGA"
  "disenador|docs/ARQUITECTURA.md|DENIEGA"
  "planificador|PENDIENTES.md|PERMITE"
  # 2026-08-28: pasa de PERMITE a DENIEGA por decision del dueno. docs/fases/ es el
  # arbol MUERTO (decision 9: no gobierna, no se corrige) y el planificador lo tenia
  # en su "escribe" mientras el plan VIVO no estaba en el de nadie. No se cambia la
  # prueba para que pase: cambio la regla, y la prueba la sigue.
  "planificador|docs/fases/FASE-C.md|DENIEGA"
  "planificador|docs/PlAN MESTRO/FASE-C-PAGINA-PUBLICA.md|PERMITE"
  "planificador|app/main.py|DENIEGA"
  "planificador|ESTADO.md|DENIEGA"
  "qa|EN-CURSO.md|PERMITE"
  "qa|app/main.py|DENIEGA"
  "qa|assets-portal/mockups/x.html|DENIEGA"
  "supervisor|ESTADO.md|PERMITE"
  "supervisor|DECISIONES.md|PERMITE"
  "supervisor|app/main.py|DENIEGA"
  "supervisor|PENDIENTES.md|DENIEGA"
  "abogado|docs/QUE-HACEMOS-Y-QUE-NO.md|PERMITE"
  "abogado|docs/CREDITOS.md|PERMITE"
  "abogado|app/main.py|DENIEGA"
  "hacker|EN-CURSO.md|PERMITE"
  "hacker|app/main.py|DENIEGA"
  "hacker-seguridad|EN-CURSO.md|PERMITE"
  "hacker-seguridad|app/main.py|DENIEGA"
  # Sin rol = hilo principal. Lo gobierna propiedad-por-rol.sh, no este hook.
  "|app/main.py|PERMITE"
  # Rol sin entrada en la tabla: sale con 0 y no lo gobierna nadie.
  # ⚠️ Esto NO es un acierto, es la deuda de PENDIENTES fila 3. Se prueba para
  #    que quede escrito que hoy pasa, no porque esté bien.
  "lector|app/main.py|PERMITE"
)

# ⚠️ Cada caso se corre en LAS DOS FORMAS de ruta, y las dos tienen que dar lo
#    mismo. Es la correccion del fallo del 2026-08-25: la primera version de esta
#    prueba usaba solo rutas POSIX relativas, daba 27/27, y el hook estaba ABIERTO
#    de par en par — porque Claude Code en Windows manda la ruta ABSOLUTA y CON
#    CONTRABARRAS, que es una forma que esta prueba nunca le habia enseñado.
#    Una puerta probada con una entrada que el sistema real no produce no esta
#    probada: esta ensayada.
a_windows() {
  local r="$1" base="${CLAUDE_PROJECT_DIR:-$(pwd)}"
  printf '%s' "$(printf '%s/%s' "${base%/}" "$r" | sed 's|/|\\\\|g')"
}

veredicto() {
  local rol="$1" ruta="$2" salida decision
  salida=$(printf '{"tool_name":"Write","tool_input":{"file_path":"%s"},"agent_type":"%s"}' "$ruta" "$rol" \
           | bash "$hook" 2>/dev/null)
  [[ -z "$salida" ]] && { echo "PERMITE"; return; }
  decision=$(printf '%s' "$salida" | jq -r '.hookSpecificOutput.permissionDecision // "SIN-DECISION"' 2>/dev/null) \
    || { echo "SALIDA-NO-JSON"; return; }
  case "$decision" in
    deny) echo "DENIEGA" ;;
    allow|ask) echo "PERMITE" ;;
    *) echo "$decision" ;;
  esac
}

fallos=0; pasan=0
printf '\n  %-18s %-34s %-9s %-9s %-9s\n' ROL RUTA ESPERADO "POSIX" "WINDOWS"
printf '  %s\n' "------------------------------------------------------------------------------------"
for caso in "${CASOS[@]}"; do
  IFS='|' read -r rol ruta esperado <<< "$caso"
  obt_posix=$(veredicto "$rol" "$ruta")
  obt_win=$(veredicto "$rol" "$(a_windows "$ruta")")
  marca="ok"
  [[ "$obt_posix" == "$esperado" ]] || marca="<<< FALLA POSIX"
  [[ "$obt_win"   == "$esperado" ]] || marca="<<< FALLA WINDOWS"
  [[ "$obt_posix" != "$esperado" && "$obt_win" != "$esperado" ]] && marca="<<< FALLAN LAS DOS"
  if [[ "$marca" == "ok" ]]; then pasan=$((pasan + 1)); else fallos=$((fallos + 1)); fi
  printf '  %-18s %-34s %-9s %-9s %-9s %s\n' "${rol:-(sin rol)}" "$ruta" "$esperado" "$obt_posix" "$obt_win" "$marca"
done

printf '\n  %d pasan · %d FALLAN  (de %d casos, cada uno en las DOS formas de ruta)\n\n' "$pasan" "$fallos" "${#CASOS[@]}"
[[ "$fallos" -eq 0 ]] || exit 1
