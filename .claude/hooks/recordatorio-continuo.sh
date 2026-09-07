#!/bin/bash
# recordatorio-continuo.sh — UserPromptSubmit y PostCompact
#
# QUÉ HACE: pone delante del agente, en CADA turno, la regla que gobierna lo que
#           se acaba de pedir. No los diecinueve mandamientos: el que aplica.
# POR QUÉ EXISTE: "que siempre vea los mandamientos y que no vuele solo".
#           El mandamiento 0 ya manda releerlos y se dejó de ejecutar. El fallo no
#           es de ignorancia, es de OLVIDO — y el olvido no se arregla con otra
#           regla escrita encima: se arregla poniéndola delante otra vez.
#
# ⚠️ POR QUÉ NO INYECTA LOS DIECINUEVE EN CADA TURNO: la capacidad del modelo de
#           recordar con precisión BAJA a medida que la ventana se llena. Repetir
#           un texto largo cada turno hace exactamente el daño que intenta evitar.
#           Se manda lo mínimo que cambia la conducta.
#
# ⚠️ PostCompact ES EL MÁS IMPORTANTE DE TODOS: al comprimir, la conversación se
#           resume y las reglas se van con el resumen. Ese es el momento exacto en
#           que un agente empieza a "volar solo", y nadie lo ve ocurrir.
#
# FORMA: additionalContext, escrito como HECHOS. Un texto con forma de orden fuera
#           de banda dispara las defensas contra inyección y acaba mostrándose al
#           usuario en vez de usarse como contexto.

set -euo pipefail
command -v jq >/dev/null 2>&1 || exit 0

input=$(cat)
evento=$(jq -r '.hook_event_name // empty' <<<"$input")
prompt=$(jq -r '.prompt // empty' <<<"$input")

# --- El núcleo. Va SIEMPRE, y es corto a propósito. ---
nucleo="En este proyecto una afirmación se escribe de una de dos formas, nunca de otra: «lo medí yo, con este comando» o «lo dice X y NO lo he comprobado». Un hecho a secas no cuenta. Un documento del repositorio no es una medición: es lo que alguien midió o supuso en su momento, y se cita diciendo que es una cita. Cuando un documento y el código no coinciden, gana el código. Los cuatro documentos del proyecto son ESTADO.md, DECISIONES.md, PENDIENTES.md y EN-CURSO.md; no se crea ninguno más. Antes de cerrar el turno se nombra el mandamiento que gobierna lo que se hizo — solo el número. Los diecinueve están en MANDAMIENTOS-DEL-SUPERVISOR.md."

extra=""

# --- Tras comprimir: el contexto se perdió. Se repone entero, no en resumen. ---
if [[ "$evento" == "PostCompact" ]]; then
  extra="Se acaba de comprimir el contexto, así que las reglas del proyecto ya no están en la conversación. Antes de seguir conviene releer ESTADO.md y EN-CURSO.md, porque el resumen conserva el qué y pierde el con-qué-comando. Todo número heredado del resumen y no remedido es una cita, no una medición."
else
  # --- La regla que gobierna ESTE encargo, elegida por lo que pidió el dueño. ---
  case "${prompt,,}" in
    *borra*|*elimina*|*limpia*|*"hoja en blanco"*)
      extra="Antes de borrar una carpeta se cuenta qué hay dentro y se pregunta qué depende de ella. Un nombre de carpeta no dice qué contiene: static/ parece código y tiene 13.608 iconos, la marca y las cuatro tipografías con su aviso de licencia. Un corte a hoja en blanco ya se llevó 13.626 archivos, y se recuperaron solo porque se hizo en una rama aparte." ;;
    *commit*|*push*|*git*)
      extra="Con agentes trabajando no se usa git add -A: se añade por ruta explícita, solo lo del tema que se cierra. Un git add -A captura el trabajo a medias de otro agente en mitad de una edición, y el commit siguiente no lo arregla porque la historia ya dice que ese cambio entró con otro tema." ;;
    *lanza*|*agente*|*subagente*|*pase*)
      extra="Antes de lanzar un agente: buscar si ya está hecho, y ABRIR lo que la búsqueda devuelva. Una lista de rutas no es una respuesta, es una lista de sitios donde mirar. El pase lleva el objeto y la pregunta, nunca el método ni lo que se espera encontrar." ;;
    *arregla*|*corrige*|*cambia*|*actualiza*|*edita*)
      extra="Antes de editar un archivo se comprueba de qué rol es su producto. El supervisor escribe el pase y lo lanza; no lo arregla. La señal de que esto se está saltando es pensar «son ocho números», «es una línea» o «es aritmética»." ;;
    *qa*|*audit*|*verifica*|*prueba*)
      extra="QA audita a ciegas: recibe el artefacto congelado y su criterio de aceptación, nada más. Ningún número, ningún hallazgo previo, ningún contexto de producto. Un número leído antes de medir cambia dónde se mira. Y el entregable no se toca mientras la auditoría corre." ;;
    *cierra*|*aprueba*|*listo*|*terminado*|*apta*)
      extra="El supervisor no cierra. Mide y recomienda; cierra QA y después el dueño. Y una entrega sin la sección de qué NO cubre y qué no se pudo verificar está incompleta, aunque todo lo demás esté bien." ;;
    *decid*|*eleg*|*opcion*|*recomienda*)
      extra="Una decisión del dueño se le devuelve en tres partes: lo que SÍ funciona (medido), una recomendación con su motivo, y la parte en contra — el mejor argumento contra la propia recomendación. Si no aparece uno, no se entendió; se busca otra vez. Y las decisiones ya cerradas se citan verificando su motivo, porque una decisión puede ser correcta y su motivo falso." ;;
  esac
fi

mensaje="$nucleo"
[[ -n "$extra" ]] && mensaje="$mensaje

$extra"

jq -n --arg e "$evento" --arg m "$mensaje" '{
  hookSpecificOutput: { hookEventName: $e, additionalContext: $m }
}'
