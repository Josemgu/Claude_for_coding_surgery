using Fichas.Paquetes;

namespace Fichas.Pruebas.Paquetes;

/// <summary>
/// Las 16 columnas de la hoja «Por verificar», sus titulos y sus anchos.
/// </summary>
/// <remarks>
/// El criterio C6-1 de PENDIENTES.md habla de «16 titulos y anchos», que eran los 16 de
/// <c>paquete/columnas.py</c> del programa en Python. Siguen siendo 16, pero YA NO son los
/// mismos 16, y la cuenta que coincide es una casualidad que conviene no confundir:
/// <list type="bullet">
/// <item>El 2026-09-05 el dueno anadio dos —«¿Por qué no se completó?» y «Comentario»—
/// porque el programa viejo solo admitia trabajo hecho y el companero no tenia donde decir
/// por que NO pudo.</item>
/// <item>El 2026-09-06 el dueno quito dos —«Fecha de solicitud» y «Estaca o distrito»—:
/// «son informaciones que no me pide verificar».</item>
/// </list>
/// </remarks>
[TestClass]
public class PruebasDeLasColumnas
{
    [TestMethod]
    public void LaHojaTieneDieciseisColumnas() => Assert.HasCount(16, Columnas.Todas);

    [TestMethod]
    public void LosTitulosSonLosDeLaHojaQueElDuenoVerifica()
    {
        string[] esperados =
        [
            "Caso", "Fecha de viaje", "Barrio o rama",
            "Hermano(a) que viaja", "Cédula de miembro", "A qué va",
            "1. Preparación", "2. Información", "3. Cita del templo",
            "4. Acciones requeridas", "5. Entrevistas", "6. Listo para el templo",
            "¿Llamó al líder?", "¿Por qué no se completó?", "Comentario", "clave",
        ];
        CollectionAssert.AreEqual(esperados, Columnas.Titulos().ToArray());
    }

    [TestMethod]
    public void LosNombresDeColumnaSonLosDeLaBase()
    {
        string[] esperados =
        [
            "numero_caso", "fecha_viaje", "unidad_nombre",
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
    /// Las dos mitades del par que reconcilia van bloqueadas: si el companero «corrige» un
    /// MRN, su fila deja de casar y su trabajo entero se pierde.
    /// </summary>
    [TestMethod]
    public void ElCasoElMrnYLaClaveVanBloqueadosYElNombreNo()
    {
        Assert.IsFalse(Columnas.Por("numero_caso").EsEditable);
        Assert.IsFalse(Columnas.Por("mrn").EsEditable);
        Assert.IsFalse(Columnas.Por("clave").EsEditable);
        Assert.IsTrue(Columnas.Por("nombre").EsEditable, "corregir una tilde no puede hacer dano: nunca se casa por nombre");
    }

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

    [TestMethod]
    public void ElParQueReconciliaEsElNumeroDeCasoYElMrn()
        => CollectionAssert.AreEqual(new[] { "numero_caso", "mrn" }, Columnas.Todas.Where(c => c.EsClave).Select(c => c.Nombre).ToArray());

    [TestMethod]
    public void LasTresColumnasDeTextoSonLasQueExcelPuedeEstropear()
        => CollectionAssert.AreEqual(
            new[] { "numero_caso", "mrn", "clave" },
            Columnas.Todas.Where(c => c.Clase == ClaseDeColumna.Texto).Select(c => c.Nombre).ToArray());

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

    [TestMethod]
    public void LaClaveEsLaUltimaColumnaYVaALaVista()
        => Assert.AreEqual(Columnas.Todas.Count, Columnas.IndiceDe("clave"));

    [TestMethod]
    public void PedirUnaColumnaQueNoExisteDiceCualesHay()
    {
        var fallo = Assert.ThrowsExactly<KeyNotFoundException>(() => Columnas.Por("no_existe"));
        StringAssert.Contains(fallo.Message, "numero_caso");
    }
}
