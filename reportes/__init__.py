"""Los reportes de la FASE 8: que se cuenta, y como sale a Excel y a PDF.

El reparto de este paquete, y por que esta partido asi:

    periodo.py     el trozo de calendario que se reporta, validado en un solo sitio
    consultas.py   lo que dice la base. Solo SQL, ni una cuenta
    preparacion.py cuantos viajaron sin la preparacion completa. Solo aritmetica
    metricas.py    las tres metricas del equipo. Solo aritmetica, ni una consulta
    avisos.py      de que NO se fia el numero que se acaba de contar
    documento.py   el reporte armado una sola vez: secciones, columnas y filas
    excel.py       ese documento escrito con openpyxl
    pdf.py         ese documento colocado sobre el papel: maqueta y paginas
    formato_pdf.py esa maqueta convertida en bytes de PDF, sin biblioteca ninguna
    generacion.py  el unico punto por el que se pide un reporte
    rutas.py       donde se guardan los archivos que salen

**Excel y PDF salen del MISMO documento.** No hay dos codigos que cuenten lo
mismo por su cuenta, porque dos codigos que cuentan lo mismo acaban dando dos
numeros distintos, y entonces el reporte deja de servir para lo unico que sirve.
`documento.py` cuenta una vez; `excel.py` y `pdf.py` solo dibujan lo que ya esta
contado.

**Por donde abre el informe, desde el 2026-09-03.** Por el numero que se mide:
«N de las M personas que ya viajaron lo hicieron SIN la preparacion completa»,
con cuatro cifras grandes debajo y **sin porcentajes**. Detras van las seis tablas
del informe del proyecto viejo —quienes viajaron sin la preparacion completa, los
viajes, a que van al templo, donde se traban las preparaciones, las unidades con
pendientes y el equipo— y detras de ellas las tres secciones de la FASE 8, que no
se han tocado.

⚠️ **«Verificada» no se dice de una persona en ninguna de esas seis, desde el
2026-09-03.** La palabra significaba dos cosas a la vez: la firma de Miguel sobre
un campo leido y los seis pasos que contesta un companero. Se dice **«con la
preparacion completa»**; la firma de Miguel se sigue llamando verificacion en las
tres metricas del final, que es donde de verdad es eso. **Sin dinero, a proposito:** el presupuesto lo lleva
otro departamento, y `pruebas/prueba_informe_para_los_jefes.py` lo comprueba
recorriendo el documento entero en vez de fiarse de este parrafo.

**Los archivados SI cuentan aqui.** `DECISIONES.md` (2026-09-02, «Historico»):
«los archivados salen de las listas de trabajo pero siguen contando en los
reportes». Por eso ninguna consulta de `consultas.py` filtra por `archivado`, al
reves que las cuatro de `datos/calendario.py` y `datos/pendientes.py`. Esa
ausencia es la decision, no un olvido.
"""
