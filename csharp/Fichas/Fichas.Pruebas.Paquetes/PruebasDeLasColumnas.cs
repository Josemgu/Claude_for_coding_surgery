using Fichas.Paquetes;

namespace Fichas.Pruebas.Paquetes;

/// <summary>
/// Las 18 columnas de la hoja «Por verificar», sus titulos y sus anchos.
/// </summary>
/// <remarks>
/// El criterio C6-1 de PENDIENTES.md habla de «16 titulos y anchos», que eran los 16 de
/// <c>paquete/columnas.py</c> del programa en Python. Ya no son 16 ni son los mismos:
/// <list type="bullet">
/// <item>El 2026-09-05 el dueno anadio dos —«¿Por qué no se completó?» y «Comentario»—
/// porque el programa viejo solo admitia trabajo hecho y el companero no tenia donde decir
/// por que NO pudo.</item>
/// <item>El 2026-09-06 el dueno quito dos —«Fecha de solicitud» y «Estaca o distrito»—:
/// «son informaciones que no me pide verificar».</item>
/// <item>El 2026-09-07 el dueno anadio una: «Número de unidad». Sus palabras: «el numero de
/// unidad en un lado y al otro el nombre de la unidad». Hasta ese dia el numero salia pegado
/// dentro de la celda del nombre.</item>
/// </list>
/// </remarks>
[TestClass]
public class PruebasDeLasColumnas
{
    /// <summary>Vigila el recuento: 16 del Python, menos dos, más dos, más una, más el templo (2026-09-16).</summary>
    [TestMethod]
    public void LaHojaTieneDieciochoColumnas() => Assert.HasCount(18, Columnas.Todas);

    /// <summary>Vigila los dieciocho títulos impresos, letra por letra y en su orden.</summary>
    [TestMethod]
    public void LosTitulosSonLosDeLaHojaQueElDuenoVerifica()
    {
        string[] esperados =
        [
            "Caso", "Fecha de viaje", "Templo", "Número de unidad", "Barrio o rama",
            "Hermano(a) que viaja", "Cédula de miembro", "A qué va",
            "1. Preparación", "2. Información", "3. Cita del templo",
            "4. Acciones requeridas", "5. Entrevistas", "6. Listo para el templo",
            "¿Llamó al líder?", "¿Por qué no se completó?", "Comentario", "clave",
        ];
        CollectionAssert.AreEqual(esperados, Columnas.Titulos().ToArray());
    }

    /// <summary>Vigila los dieciocho nombres de columna, en el mismo orden que los títulos.</summary>
    [TestMethod]
    public void LosNombresDeColumnaSonLosDeLaBase()
    {
        string[] esperados =
        [
            "numero_caso", "fecha_viaje", "templo", "unidad_numero", "unidad_nombre",
            "nombre", "mrn", "a_que_va",
            "paso_preparacion", "paso_informacion", "paso_cita_del_templo",
            "paso_acciones_requeridas", "paso_entrevistas", "paso_listo_para_el_templo",
            "llamo_al_lider", "motivo_del_companero", "nota_companero", "clave",
        ];
        CollectionAssert.AreEqual(esperados, Columnas.Todas.Select(c => c.Nombre).ToArray());
    }

    /// <summary>
    /// Lo que el dueno pidio el 2026-09-06, dicho por su nombre y por su rotulo.
    /// </summary>
    /// <remarks>
    /// Sus palabras: «En los paquetes para los agentes o gerentes debe eliminar la columna
    /// Fecha de solicitud, Estaca o Distrito a que va, porque son informaciones que no me pide
    /// verificar». Se comprueban las DOS caras porque son dos cosas distintas: el nombre es
    /// lo que busca el generador y el rotulo es lo que lee el companero en el papel, y una
    /// columna se podria quitar de la lista y seguir imprimiendose por otro sitio.
    /// </remarks>
    [TestMethod]
    public void NiLaFechaDeSolicitudNiLaEstacaSiguenEnLaHoja()
    {
        CollectionAssert.DoesNotContain(Columnas.Todas.Select(c => c.Nombre).ToArray(), "fecha_solicitud");
        CollectionAssert.DoesNotContain(Columnas.Todas.Select(c => c.Nombre).ToArray(), "estaca");
        CollectionAssert.DoesNotContain(Columnas.Titulos().ToArray(), "Fecha de solicitud");
        CollectionAssert.DoesNotContain(Columnas.Titulos().ToArray(), "Estaca o distrito");
        Assert.ThrowsExactly<KeyNotFoundException>(() => Columnas.Por("fecha_solicitud"));
        Assert.ThrowsExactly<KeyNotFoundException>(() => Columnas.Por("estaca"));
    }

    /// <summary>
    /// Quitar dos columnas de EN MEDIO no puede mover a las demas de su orden relativo.
    /// </summary>
    /// <remarks>
    /// Es lo que vigilaba la lista literal de titulos, dicho de otra forma y sin depender de
    /// ella: el compañero lee la hoja de izquierda a derecha, y el caso, la fecha de viaje, el
    /// barrio, el nombre y la cedula tienen que seguir en ese orden.
    /// </remarks>
    [TestMethod]
    public void LasQueQuedanConservanSuOrdenRelativo()
    {
        var nombres = Columnas.Todas.Select(c => c.Nombre).ToList();
        string[] enOrden = ["numero_caso", "fecha_viaje", "unidad_nombre", "nombre", "mrn", "a_que_va"];
        var posiciones = enOrden.Select(nombre => nombres.IndexOf(nombre)).ToArray();
        CollectionAssert.AreEqual(posiciones.OrderBy(p => p).ToArray(), posiciones,
            "el orden de izquierda a derecha de las que quedan es el que era");
    }

    /// <summary>
    /// ⚠️ Ninguna columna va bloqueada desde el 2026-09-07: «no bloquees las celdas por favor,
    /// de los paquetes».
    /// </summary>
    /// <remarks>
    /// Aqui se afirmaba lo contrario —que <c>numero_caso</c>, <c>mrn</c> y <c>clave</c> iban
    /// bloqueadas— con el motivo de proteger el par que reconciliaba. Ese motivo caduco el
    /// 2026-09-03, cuando la hoja empezo a llevar la columna <c>clave</c> y la vuelta paso a
    /// casar por ella. Lo que protegia el bloqueo se mide ahora en
    /// <see cref="PruebasDeLaClaveEstropeada"/>.
    /// </remarks>
    [TestMethod]
    public void NingunaColumnaSeDeclaraNoEditable()
        => Assert.IsEmpty(
            Columnas.Todas.Where(columna => !columna.EsEditable).Select(columna => columna.Nombre).ToList(),
            "el dueño pidió que no se bloquee ninguna celda de los paquetes");

    /// <summary>
    /// Nueve columnas rellena el companero, y solo siete se leen como «si o no».
    /// </summary>
    /// <remarks>
    /// La distincion no es cosmetica: una respuesta de si o no que no se entiende DESCARTA
    /// la fila entera, y si el motivo o el comentario entraran en ese saco, una frase
    /// escrita a mano tiraria el trabajo bueno de esa persona.
    /// </remarks>
    [TestMethod]
    public void LasNueveQueRellenaElCompaneroYLasSieteQueSeLeenComoSiONo()
    {
        var respuestas = Columnas.Todas.Where(c => c.EsRespuesta).Select(c => c.Nombre).ToArray();
        Assert.HasCount(9, respuestas);
        CollectionAssert.Contains(respuestas, "llamo_al_lider");
        CollectionAssert.Contains(respuestas, MotivosDeLaHoja.ColumnaDelMotivo);
        CollectionAssert.Contains(respuestas, MotivosDeLaHoja.ColumnaDelComentario);
        CollectionAssert.DoesNotContain(respuestas, "nombre", "el nombre es editable pero NO es una respuesta: pintarlo de amarillo pediria teclear un nombre que ya viene puesto");

        var siONo = Columnas.Todas.Where(c => c.EsSiONo).Select(c => c.Nombre).ToArray();
        Assert.HasCount(7, siONo);
        CollectionAssert.DoesNotContain(siONo, MotivosDeLaHoja.ColumnaDelMotivo, "un motivo no es un sí ni un no");
        CollectionAssert.DoesNotContain(siONo, MotivosDeLaHoja.ColumnaDelComentario);
    }

    /// <summary>El motivo y el comentario son de quien rellena la hoja, y por eso se teclean.</summary>
    [TestMethod]
    public void ElMotivoYElComentarioSonEditablesYCadaUnoDeSuClase()
    {
        Assert.IsTrue(Columnas.Por(MotivosDeLaHoja.ColumnaDelMotivo).EsEditable);
        Assert.IsTrue(Columnas.Por(MotivosDeLaHoja.ColumnaDelComentario).EsEditable);
        Assert.AreEqual(ClaseDeRespuesta.Motivo, Columnas.Por(MotivosDeLaHoja.ColumnaDelMotivo).Respuesta);
        Assert.AreEqual(ClaseDeRespuesta.TextoLibre, Columnas.Por(MotivosDeLaHoja.ColumnaDelComentario).Respuesta);
    }

    /// <summary>Vigila que solo el número de caso y el MRN se declaren clave: son el par de respaldo cuando no hay columna «clave».</summary>
    [TestMethod]
    public void ElParQueReconciliaEsElNumeroDeCasoYElMrn()
        => CollectionAssert.AreEqual(new[] { "numero_caso", "mrn" }, Columnas.Todas.Where(c => c.EsClave).Select(c => c.Nombre).ToArray());

    /// <summary>Vigila que las cuatro con ceros o guiones que Excel normalizaría vayan con formato de texto, y ninguna más.</summary>
    [TestMethod]
    public void LasCuatroColumnasDeTextoSonLasQueExcelPuedeEstropear()
        => CollectionAssert.AreEqual(
            new[] { "numero_caso", "unidad_numero", "mrn", "clave" },
            Columnas.Todas.Where(c => c.Clase == ClaseDeColumna.Texto).Select(c => c.Nombre).ToArray());

    /// <summary>Vigila los anchos medidos del programa viejo, y los dos que se cambiaron con su motivo.</summary>
    [TestMethod]
    public void LosAnchosSonLosDelProgramaEnPython()
    {
        Assert.AreEqual(11, Columnas.AnchoDe("numero_caso"));
        Assert.AreEqual(19, Columnas.AnchoDe("mrn"));
        Assert.AreEqual(26, Columnas.AnchoDe("clave"), "26 y no 16: «BALC2609:055-1111-3853:12» son 25 caracteres y a 16 sale cortada");
        Assert.AreEqual(13, Columnas.AnchoDe("paso_preparacion"), "los seis pasos miden todos igual");
        Assert.AreEqual(34, Columnas.AnchoDe(MotivosDeLaHoja.ColumnaDelMotivo), "la opción más larga del menú son 32 caracteres");
        Assert.AreEqual(40, Columnas.AnchoDe(MotivosDeLaHoja.ColumnaDelComentario));
    }

    /// <summary>Vigila que la clave sea la última columna: a la vista, donde se nota si falta.</summary>
    [TestMethod]
    public void LaClaveEsLaUltimaColumnaYVaALaVista()
        => Assert.AreEqual(Columnas.Todas.Count, Columnas.IndiceDe("clave"));

    /// <summary>Vigila que pedir una columna inexistente lance nombrando las que sí hay.</summary>
    [TestMethod]
    public void PedirUnaColumnaQueNoExisteDiceCualesHay()
    {
        var fallo = Assert.ThrowsExactly<KeyNotFoundException>(() => Columnas.Por("no_existe"));
        StringAssert.Contains(fallo.Message, "numero_caso");
    }
}
