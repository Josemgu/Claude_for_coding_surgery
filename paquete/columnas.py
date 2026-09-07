"""Las columnas de la hoja «Por verificar»: una sola definicion para ida y vuelta.

Este modulo existe para que el Excel que SALE y el que VUELVE no se puedan
separar. Si el exportador escribiera sus cabeceras y el lector buscara las suyas,
bastaria renombrar una columna en un sitio para que la vuelta dejara de encontrarla
—y una reconciliacion que no encuentra su columna no falla: descarta todo en
silencio—.

**De donde sale esta hoja.** Del proyecto viejo (`salida/asignacion.py`), que es el
que el dueno llama perfecto: «El Excel que crea el viejo para los agentes es
perfecto, ese es el que quiero para este» (`DECISIONES.md`, 2026-09-03). Una sola
hoja, «Por verificar», cinco filas de cabecera, la tabla desde la fila 6, y la
**clave a la vista** en la ultima columna. Su comentario, citado alli: «un dato
oculto es un dato que alguien borra sin saber lo que hace».

⚠️ **Dos columnas salen siempre vacias hoy, y es una decision, no un fallo.**
«Estaca o distrito» y «Fecha de solicitud» estan en la hoja del viejo y la base de
este proyecto **no las guarda**: el dueno dijo el mismo dia «solo debe leer los
campos que yo necesito» y enumero los suyos —nombre, unidad, cedula, fecha de
viaje, numero de caso, pais y templo— y esos dos no estan en la lista
(`DECISIONES.md`, 2026-09-03). Se dejan en la hoja porque la hoja es la que el
dueno aprobo, y se escriben con la palabra que este proyecto ya usa para lo que no
consta, en vez de en blanco: un blanco lo lee el companero como «la estaca esta
vacia en el papel», que es un dato falso.

Las clases de columna son las mismas tres de `espejo/hojas.py` y por el mismo
motivo. La que importa aqui es `TEXTO`: sin `number_format = '@'`, Excel lee
`055-1111-3853` como algo que puede normalizar y se come el cero de delante.
Y el MRN sin sus ceros deja de servir para reconciliar, que es todo su trabajo.
"""

from collections import namedtuple

from datos.pasos import PASOS, ROTULO_DE_LA_LLAMADA

TEXTO = "texto"
TEMPORAL = "temporal"
CRUDO = "crudo"

# `clave` dice si la columna es parte del par que reconcilia. `editable` dice si el
# companero la puede teclear: las que no lo son se escriben bloqueadas. `respuesta`
# dice si es una de las siete que el companero VIENE a rellenar.
#
# Son dos cosas distintas y hacen falta las dos. Solo las `respuesta` llevan fondo
# `PIEL` y menu desplegable, porque el fondo es lo que le dice al companero «esto lo
# rellenas tu»; `nombre` es `editable` pero **no** es una respuesta, y pintarla de
# amarillo le pediria que teclease un nombre que ya viene puesto. Se deja editable
# igual porque corregir una tilde es normal y no puede hacer dano: la
# reconciliacion nunca mira el nombre.
Columna = namedtuple(
    "Columna", ("nombre", "titulo", "clase", "clave", "editable", "respuesta")
)

# El nombre de la hoja. El lector la busca por este nombre y, si no esta, cae a la
# hoja activa: un companero puede guardar el archivo desde otro programa que
# renombre la pestana, y eso no puede costar la ronda entera.
NOMBRE_DE_LA_HOJA = "Por verificar"

# Donde empieza la tabla. Encima van cinco filas de cabecera —titulo, templo y
# salida, la fecha limite sola y en rojo, el agente, y la instruccion— y por eso la
# fila de titulos no es la 1. El lector **no** se fia de este numero: busca la fila
# que trae la columna «clave», que es la que no puede faltar. Esto es solo lo que
# escribe el exportador.
FILA_DE_LA_CABECERA = 6
PRIMERA_FILA_DE_DATOS = FILA_DE_LA_CABECERA + 1

# La columna que devuelve cada renglon a su persona. Lleva dentro el numero de
# caso, el MRN y el id del caso, separados por dos puntos: `CASO:MRN:ID`.
#
# ⚠️ El id del caso entro el 2026-09-03. Desde la version 12 del esquema el numero
# de caso NO es unico —identifica una unidad y un mes, no una familia— y un
# documento duplicado entra en vez de rechazarse, asi que dos casos pueden llevar
# el mismo numero y las mismas personas. Sin el id, la fila que vuelve no sabe a
# cual de los dos pertenece.
COLUMNA_DE_LA_CLAVE = "clave"
SEPARADOR_DE_LA_CLAVE = ":"

# Lo que se escribe donde la base no tiene el dato. Es la misma palabra que usa
# `reportes/documento.py`, y a proposito: dos maneras de decir «no lo se» en dos
# documentos del mismo programa se leen como dos cosas distintas.
SIN_DATO = "no consta"

# ⚠️ `numero_caso` y `mrn` van BLOQUEADAS (`editable=False`), y era el requisito
# explicito del pase de la FASE 6 que sigue vigente. Son las dos mitades del par que
# reconcilia (`docs/ARQUITECTURA.md` §4): si el companero corrige un MRN «que estaba
# mal», su fila deja de casar y su trabajo entero se va a la lista de descartados.
#
# Desde que existe la columna `clave`, ademas, la identidad se lee de ELLA y no de
# estas dos: la clave es una sola celda y se ve de un vistazo si se borro, mientras
# que un digito cambiado en el MRN no se nota. Las dos columnas siguen a la vista
# porque el companero busca a la persona por su cedula, que es su trabajo.
#
# `nombre` SI se deja editable, y no es un descuido: la reconciliacion **nunca**
# mira el nombre (`DECISIONES.md`, 2026-09-02), asi que un acento cambiado no puede
# hacer dano. Bloquearlo sugeriria que el nombre importa para casar, que es
# justamente lo contrario de lo que este sistema hace.
COLUMNAS = (
    Columna("numero_caso", "Caso", TEXTO, True, False, False),
    Columna("fecha_solicitud", "Fecha de solicitud", CRUDO, False, False, False),
    Columna("fecha_viaje", "Fecha de viaje", TEMPORAL, False, False, False),
    Columna("unidad_nombre", "Barrio o rama", CRUDO, False, False, False),
    Columna("estaca", "Estaca o distrito", CRUDO, False, False, False),
    Columna("nombre", "Hermano(a) que viaja", CRUDO, False, True, False),
    Columna("mrn", "Cédula de miembro", TEXTO, True, False, False),
    Columna("a_que_va", "A qué va", CRUDO, False, False, False),
) + tuple(
    Columna(nombre, rotulo, CRUDO, False, True, True) for nombre, rotulo in PASOS
) + (
    Columna("llamo_al_lider", ROTULO_DE_LA_LLAMADA, CRUDO, False, True, True),
    Columna(COLUMNA_DE_LA_CLAVE, "clave", TEXTO, False, False, False),
)

COLUMNAS_POR_NOMBRE = {columna.nombre: columna for columna in COLUMNAS}

# Cuanto se ensancha cada columna, en caracteres. No es decoracion: con el ancho
# por defecto un MRN de once digitos sale como `#####` y el companero no puede
# comprobar contra que trabaja. Los numeros son los del proyecto viejo.
ANCHOS = {
    "numero_caso": 11,
    "fecha_solicitud": 15,
    "fecha_viaje": 14,
    "unidad_nombre": 24,
    "estaca": 22,
    "nombre": 26,
    "mrn": 19,
    "a_que_va": 26,
    "llamo_al_lider": 16,
    # 26 y no los 16 del proyecto viejo: la clave lleva ahora tres partes
    # —`BALC2609:055-1111-3853:12` son 25 caracteres— y a 16 sale cortada. Se ve a
    # proposito, y una clave que no se ve entera no se puede comprobar.
    COLUMNA_DE_LA_CLAVE: 26,
}
ANCHO_DE_UN_PASO = 13


def ancho_de(nombre):
    """Los caracteres de ancho de esa columna. Los seis pasos miden todos igual."""
    return ANCHOS.get(nombre, ANCHO_DE_UN_PASO)


def indice_de(nombre):
    """La posicion de una columna en la hoja, contando desde 1 como Excel."""
    for numero, columna in enumerate(COLUMNAS, start=1):
        if columna.nombre == nombre:
            return numero
    raise KeyError(
        f"La columna {nombre!r} no existe en la hoja «{NOMBRE_DE_LA_HOJA}». Las que "
        f"hay son {[columna.nombre for columna in COLUMNAS]}."
    )


def titulos():
    """La fila de titulos de la hoja, tal como se escribe y tal como se busca."""
    return [columna.titulo for columna in COLUMNAS]


def nombres_de_las_claves():
    """Las columnas que forman el par que reconcilia: `numero_caso` y `mrn`."""
    return tuple(columna.nombre for columna in COLUMNAS if columna.clave)


def armar_la_clave(numero_caso, mrn, caso_id=None):
    """El texto de la columna «clave» de una persona: `CASO:MRN:ID`.

    Una persona sin MRN sale con la clave a medias —`CASO::ID`— y no con una clave
    inventada. Al volver, esa fila cae en descartados con su motivo, que es donde
    Miguel la ve: en SQLite dos NULL no son iguales entre si, asi que un caso puede
    tener varias personas sin MRN y no hay forma de saber a cual se referia el
    companero (`datos/propuestas.py`, `resolver_persona_por_par`).

    ⚠️ **La tercera parte —el id del caso— entro el 2026-09-03 y es obligatoria
    para que la vuelta no cruce dos familias.** Desde la version 12 del esquema dos
    casos distintos pueden llevar el MISMO numero: son cuatro letras mas el ano y el
    mes, o sea una unidad y un mes, no una familia. Y desde que un documento
    duplicado entra en vez de rechazarse, dos casos pueden llevar el mismo numero
    **y las mismas personas**. Con `CASO:MRN` a secas, la fila que vuelve casaria
    con dos personas y no habria forma de saber con cual: el trabajo del companero
    acabaria escrito sobre la familia equivocada. El id del caso es lo unico que no
    se repite.

    `caso_id` admite None para no romper a quien todavia arme una clave sin el; esa
    clave se resuelve por el camino viejo, que ahora puede quedar ambiguo y
    descartar la fila con su motivo escrito. El exportador SIEMPRE lo pasa.
    """
    cola = "" if caso_id is None else f"{SEPARADOR_DE_LA_CLAVE}{caso_id}"
    return f"{numero_caso or ''}{SEPARADOR_DE_LA_CLAVE}{mrn or ''}{cola}"


def partir_la_clave(clave):
    """Lo que lleva dentro una clave: `(numero_caso, mrn, caso_id)`, o None.

    `caso_id` sale como entero, o como None cuando la clave viene del formato
    viejo de dos partes —un paquete generado antes del 2026-09-03— o cuando la
    tercera parte no es un numero. Las dos primeras partes se leen igual que
    siempre, asi que un Excel antiguo sigue volviendo.

    Devuelve None —y no levanta— cuando la clave no tiene la forma esperada: es un
    resultado normal del trabajo (a alguien se le borro la celda) y quien llama lo
    convierte en una fila descartada con su motivo.
    """
    if not clave or SEPARADOR_DE_LA_CLAVE not in str(clave):
        return None
    partes = str(clave).split(SEPARADOR_DE_LA_CLAVE)
    numero_caso = partes[0].strip() or None
    mrn = partes[1].strip() or None
    return numero_caso, mrn, _entero_o_nada(partes[2] if len(partes) > 2 else "")


def _entero_o_nada(texto):
    """El id del caso que trae la clave, o None si no lo trae o no es un numero.

    None y no un error: una clave sin tercera parte es la de un paquete anterior a
    este cambio, y esas filas tienen que seguir volviendo.
    """
    texto = str(texto).strip()
    return int(texto) if texto.isdigit() else None
