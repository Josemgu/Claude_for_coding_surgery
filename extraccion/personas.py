"""Las filas de personas del formulario: quien viaja y con que MRN.

El formulario trae seis filas y casi nunca vienen todas llenas. Una fila sin
nombre y sin MRN se descarta: no se guarda una persona en blanco.

Por que las filas se buscan por el NOMBRE y no por el MRN, que seria lo comodo:
esta medido que el OCR falla el MRN mas veces que el nombre. En los formularios
de referencia hay una persona cuyo MRN salio ilegible del todo. Si las filas se
buscaran por MRN, esa persona desapareceria del caso sin que nadie se entere.
Buscada por nombre, aparece con el MRN vacio y marcado para revision, que es un
problema visible y por lo tanto arreglable.

⚠️ **Este parrafo citaba un segundo caso que resulto no serlo.** Decia que el OCR
habia leido mal un MRN «con una letra dentro», `066-2222-133A`. El 2026-09-04 se
recorto del PDF esa banda y se miro la imagen: el OCR habia leido bien y el papel
dice `133A`. No era un fallo de lectura sino una regla nuestra equivocada
(`DECISIONES.md`, «La cedula PUEDE terminar en letra»). El argumento de buscar por
nombre no depende de ese caso, y se queda con el que si se midio.
"""

from collections import namedtuple

from extraccion.campos import ORIGEN_OCR, ORIGEN_VACIO, CampoExtraido, campo_vacio, resolver_campo
from extraccion.geometria import Rectangulo, fraccion_de_traslape_vertical
from extraccion.normalizacion import normalizar_mrn

# Las etiquetas de las dos columnas —«Full Name(s)» / «Nombre(s) de pila» y
# «Membership Record Number» / «Número de cédula de miembro»— ya no viven aqui:
# estan en `extraccion/etiquetas.py` con las de los demas campos, porque el
# formulario llega en dos idiomas y el sitio donde se dice como se llama cada
# rotulo tiene que ser uno solo. Este modulo nunca las uso: recibe las cabeceras
# ya localizadas, como RECTANGULOS, y por eso no le hace falta importarlas.

# El formulario impreso trae seis filas de personas. Casi nunca vienen todas
# llenas; las que quedan en blanco no producen ni una linea de OCR, asi que no
# hay nada que descartar en ellas. El numero esta aqui para poder informar
# «2 de 6» y que se vea cuantas quedaron vacias.
FILAS_DEL_FORMULARIO = 6

# `banda` es el rectangulo de la fila de esta persona en la imagen rasterizada, en
# pixeles. Antes se calculaba y se quedaba en una variable local de
# `extraer_personas`, y con el se perdia lo unico que permite ensenar el trozo del
# escaneo al lado del campo: sin eso, corregir un nombre obliga a abrir el PDF
# entero y buscar la fila a ojo.
#
# Es UNA banda por persona y no dos —una para el nombre y otra para el MRN—
# porque en el papel las dos cosas comparten renglon, y porque asi la tira ensena
# la fila entera: es viendo el nombre al lado del MRN como se cae en la cuenta de
# que el MRN es del de arriba.
PersonaExtraida = namedtuple(
    "PersonaExtraida", ("fila_formulario", "nombre", "mrn", "banda")
)


def limite_de_la_columna_de_nombres(ancla_nombres, ancla_mrn):
    """El punto medio entre las dos cabeceras: a su izquierda estan los nombres.

    No es una coordenada fija: sale de donde el OCR encontro las dos etiquetas en
    ESTA pagina, asi que se mueve con el escaneo.

    Hace falta porque el formulario lleva notas de revision —«Verified for
    endowment and sealing»— escritas a la izquierda de la columna de MRN. Sin
    este limite, cada nota se contaria como una persona mas.
    """
    return (ancla_nombres.x0 + ancla_mrn.x0) / 2.0


def _bloque_de_personas(ancla_nombres, ancla_mrn, cierres):
    """Desde debajo de las cabeceras hasta la siguiente seccion del formulario.

    Devuelve `(arriba, abajo)`, y `abajo` es None cuando NINGUNA de las etiquetas
    que cierran el bloque aparecio. Ese None no es un detalle: sin cierre el
    bloque llegaria hasta el pie de la pagina y cada linea del formulario —los
    costes, las firmas, la letra pequena— se contaria como una persona.

    Medido el 2026-09-02: en las dos paginas peor escaneadas de los cuatro
    documentos, cerrar el bloque con el borde de la imagen producia 35 y 31
    personas donde hay una. Con este None salen 0 y la pagina se marca para
    captura manual, que es la respuesta correcta a un escaneo que no se lee.
    """
    arriba = max(ancla_nombres.y1, ancla_mrn.y1)
    posibles = [cierre.y0 for cierre in cierres if cierre is not None and cierre.y0 > arriba]
    return arriba, (min(posibles) if posibles else None)


def _lineas_de_nombre(lineas, arriba, abajo, limite_derecho):
    """Las lineas que pueden ser el nombre de una persona, de arriba abajo."""
    candidatas = [
        linea
        for linea in lineas
        if arriba <= linea.rectangulo.y0 < abajo and linea.rectangulo.x0 < limite_derecho
    ]
    return sorted(candidatas, key=lambda linea: linea.rectangulo.y0)


def _solo_las_filas_seguidas(candidatas):
    """Corta la lista en el primer hueco mayor que una fila.

    Las filas de personas del formulario van pegadas una debajo de otra: medido
    sobre los formularios de referencia con varias personas, la separacion entre
    una fila y la siguiente va de 0 a 7 pixeles sobre filas de 46 a 53 de alto.

    Debajo de la ultima persona ya no hay personas, pero sigue habiendo texto: el
    nombre del templo, la moneda, la tabla de costes. En una pagina mal escaneada
    la etiqueta que deberia cerrar el bloque no se lee, y todo eso entraba como
    personas. Medido: producia 4 y 2 personas en dos paginas que tienen una cada
    una. Con el corte por hueco salen 1 y 1.

    Se corta, no se filtra por contenido: decidir si «BEM BRASIL» es el nombre de
    alguien seria interpretar, y eso no se hace aqui.
    """
    if not candidatas:
        return []
    seguidas = [candidatas[0]]
    for linea in candidatas[1:]:
        anterior = seguidas[-1].rectangulo
        alto_de_la_fila_anterior = anterior.y1 - anterior.y0
        if linea.rectangulo.y0 - anterior.y1 > alto_de_la_fila_anterior:
            break
        seguidas.append(linea)
    return seguidas


def _mrn_de_la_fila(lineas, banda_de_la_fila):
    """El MRN que comparte fila con el nombre, o un campo vacio si no hay.

    Se miran TODAS las lineas de la fila, tambien la del nombre, y no solo las de
    la columna de MRN. Motivo medido: en uno de los formularios reales el OCR
    junto el nombre y el MRN en una sola linea, y buscando solo a la derecha ese
    MRN se perdia entero.

    El formato que se acepta esta en `normalizar_mrn`: 3 digitos, 4 digitos y 4
    caracteres, y el ultimo de esos cuatro puede ser un digito o una LETRA
    (`DECISIONES.md`, 2026-09-04). Lo que no encaje se guarda como `valor_ocr`, el
    campo va a revision y la pantalla de correccion ENSENA lo que se leyo
    (`interfaz/campo.py`, `linea_de_lo_que_se_leyo`): antes solo quedaba en la
    tabla de procedencia, donde el dueno no lo ve.

    No se corrige una letra por un digito, ni al reves.
    """
    de_la_fila = [
        linea
        for linea in lineas
        if fraccion_de_traslape_vertical(linea.rectangulo, banda_de_la_fila) > 0.5
    ]
    return resolver_campo(de_la_fila, [], hay_tachon=False, normalizar=normalizar_mrn)


def _nombre_de_la_fila(linea_de_nombre):
    """El nombre tal como lo leyo el OCR. No se limpia ni se corrige la ortografia."""
    texto = (linea_de_nombre.texto or "").strip()
    if not texto:
        return campo_vacio()
    return CampoExtraido(
        valor=texto,
        origen=ORIGEN_OCR,
        confianza=linea_de_nombre.confianza,
        valor_ocr=texto,
        anulado_por_tachon=False,
        necesita_revision=False,
    )


def _banda_de_busqueda_de_la_fila(linea_de_nombre, limite_de_la_columna):
    """La franja donde se busca el MRN de esta fila. NO es lo que se dibuja.

    Es deliberadamente exagerada de ancha —diez veces el limite de la columna de
    nombres— porque solo sirve para preguntar «¿esta linea comparte renglon con
    esta persona?», y la respuesta la decide el traslape VERTICAL. Ensanarla no
    cambia ninguna respuesta y garantiza que no se escape un MRN escrito muy a la
    derecha.
    """
    return Rectangulo(
        x0=0.0,
        y0=linea_de_nombre.rectangulo.y0,
        x1=float(limite_de_la_columna) * 10.0,
        y1=linea_de_nombre.rectangulo.y1,
    )


def _banda_visible_de_la_fila(linea_de_nombre, ancla_mrn):
    """El trozo del escaneo que se ensena de esta fila. Va del nombre al MRN.

    Va aparte de la banda de busqueda por una razon concreta: aquella mide diez
    veces el ancho de la pagina a proposito, y recortar la imagen con ella daria
    una tira casi toda en blanco con el dato perdido en la esquina.

    Empieza en el borde izquierdo de la columna de nombres y termina donde acaba
    la de MRN, mas un respiro: es la fila del papel tal como se lee, con el nombre
    y el numero juntos. El respiro sale del alto de la propia fila, que es lo que
    escala con el tamano del escaneo; un numero fijo de pixeles valdria para una
    resolucion y no para otra.
    """
    fila = linea_de_nombre.rectangulo
    alto_de_la_fila = fila.y1 - fila.y0
    return Rectangulo(
        x0=min(fila.x0, ancla_mrn.x0) - alto_de_la_fila,
        y0=fila.y0,
        x1=max(fila.x1, ancla_mrn.x1) + alto_de_la_fila,
        y1=fila.y1,
    )


def extraer_personas(lineas, ancla_nombres, ancla_mrn, cierres_del_bloque):
    """Las personas de la pagina, numeradas por su fila, sin las filas vacias.

    Devuelve `(personas, filas_descartadas)`. El segundo numero se informa: una
    fila que se descarta en silencio es una persona que puede haberse perdido.

    Sin las dos cabeceras, o sin ninguna etiqueta que cierre el bloque por abajo,
    devuelve cero personas. Es a proposito: de esa pagina no se sabe donde
    empieza ni donde acaba la lista, y adivinarlo produce personas que no existen.
    """
    if ancla_nombres is None or ancla_mrn is None:
        return [], 0

    limite = limite_de_la_columna_de_nombres(ancla_nombres, ancla_mrn)
    arriba, abajo = _bloque_de_personas(ancla_nombres, ancla_mrn, cierres_del_bloque)
    if abajo is None:
        return [], 0
    candidatas = _solo_las_filas_seguidas(_lineas_de_nombre(lineas, arriba, abajo, limite))

    personas = []
    descartadas = 0
    for numero_de_fila, linea_de_nombre in enumerate(candidatas, start=1):
        banda = _banda_de_busqueda_de_la_fila(linea_de_nombre, limite)
        nombre = _nombre_de_la_fila(linea_de_nombre)
        mrn = _mrn_de_la_fila(lineas, banda)
        if nombre.origen == ORIGEN_VACIO and mrn.origen == ORIGEN_VACIO:
            descartadas += 1
            continue
        personas.append(
            PersonaExtraida(
                fila_formulario=numero_de_fila,
                nombre=nombre,
                mrn=mrn,
                banda=_banda_visible_de_la_fila(linea_de_nombre, ancla_mrn),
            )
        )
    return personas, descartadas
