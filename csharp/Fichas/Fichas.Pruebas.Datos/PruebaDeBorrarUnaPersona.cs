using Fichas.Contratos.Modelos;
using Fichas.Contratos.Puertos;
using Fichas.Datos.Falso;
using Fichas.Datos.Mantenimiento;
using Fichas.Datos.Repositorios;
using Microsoft.Data.Sqlite;

namespace Fichas.Pruebas.Datos;

/// <summary>
/// Borrar UNA persona de un documento, con lo que cuelga de ella, y dejar las demás.
/// </summary>
/// <remarks>
/// <para><b>De dónde sale.</b> Palabras del dueño: <i>«Si quiero eliminar un nombre puedo
/// hacerlo»</i> (2026-09-07) y <i>«dentro quiero que me permita eliminar personas o agregar
/// personas que quizás el escáner no contempló»</i> (2026-09-10). Medido antes de tocar nada:
/// <c>IPersonas</c> no tenía ningún método que borrase, y ningún otro puerto borraba una
/// persona suelta —solo el documento entero, por <c>IMantenimiento</c>—.</para>
///
/// <para><b>Lo que cuelga de una persona, medido en el esquema</b> (<c>grep REFERENCES</c> sobre
/// <c>Fichas.Datos/Esquema</c>): ninguna tabla apunta a <c>personas</c> con clave foránea. Lo
/// único que cuelga son los renglones de <c>procedencia_campo</c> con <c>tabla = 'personas'</c>
/// y <c>registro_id</c> igual a su id, que no llevan clave foránea y por eso <b>no caerían
/// solos</b>: sin borrarlos aquí quedarían huérfanos para siempre. Las seis respuestas y su
/// firma (<c>pasos_*</c>) son columnas de la propia fila y se van con ella.</para>
///
/// <para>⛔ <b>Sin copia previa no se borra</b>, igual que en <c>RepositorioDeMantenimiento</c>:
/// borrar es lo único del programa que no tiene vuelta atrás.</para>
/// </remarks>
[TestClass]
public sealed class PruebaDeBorrarUnaPersona
{
    /// <summary>La fecha fija de estas pruebas, para que las marcas sembradas no dependan del reloj.</summary>
    private const string DiaDeLasPruebas = "2026-09-14";

    /// <summary>
    /// Dado un documento de tres personas con su procedencia, cuando se borra una, entonces
    /// quedan dos en <c>personas</c>, cero renglones de procedencia de la borrada y los de las
    /// otras dos intactos.
    /// </summary>
    [TestMethod]
    public void BorrarUnaDejaLasOtrasDosConSuProcedencia()
    {
        using var baseDePrueba = BaseDePrueba.Nueva();
        var personas = new RepositorioDePersonas(baseDePrueba.Conexion);
        var procedencia = new RepositorioDeProcedencia(baseDePrueba.Conexion);
        var (caso, tres) = SembrarUnDocumentoDeTresPersonas(baseDePrueba.Conexion);

        var procedenciaAntes = baseDePrueba.ContarFilasDe("procedencia_campo");
        var deLaBorradaAntes = procedencia.DeRegistro(TablaDeProcedencia.Personas, tres[1]).Count;

        var resultado = personas.Borrar(tres[1]);

        var quedan = personas.DeCaso(caso);
        var deLaBorrada = procedencia.DeRegistro(TablaDeProcedencia.Personas, tres[1]).Count;
        var deLasOtras = tres.Where(id => id != tres[1])
            .Sum(id => procedencia.DeRegistro(TablaDeProcedencia.Personas, id).Count);

        Console.WriteLine(
            "== personas {0} → {1}; procedencia_campo {2} → {3}; de la borrada {4} → {5} ==",
            tres.Count, quedan.Count, procedenciaAntes, baseDePrueba.ContarFilasDe("procedencia_campo"),
            deLaBorradaAntes, deLaBorrada);

        Assert.IsTrue(resultado.SeBorro, "No se borró la persona.");
        Assert.HasCount(2, quedan, "El documento no se quedó con las otras dos.");
        Assert.IsFalse(quedan.Any(p => p.Id == tres[1]), "La persona borrada sigue en el documento.");
        Assert.AreEqual(2, deLaBorradaAntes, "La siembra no dejó dos renglones de procedencia a la persona.");
        Assert.AreEqual(0, deLaBorrada, "Quedaron renglones de procedencia huérfanos de la persona borrada.");
        Assert.AreEqual(4, deLasOtras, "Se tocó la procedencia de una persona que no se borró.");
        Assert.AreEqual(procedenciaAntes - 2, baseDePrueba.ContarFilasDe("procedencia_campo"));
        Assert.AreEqual(1, baseDePrueba.ContarFilasDe("casos"), "Borrar una persona no borra el documento.");
    }

    /// <summary>El resultado dice qué cayó tabla por tabla, para que el acuse lo pueda decir con cifras.</summary>
    [TestMethod]
    public void ElResultadoCuentaLoQueCayoTablaPorTabla()
    {
        using var baseDePrueba = BaseDePrueba.Nueva();
        var personas = new RepositorioDePersonas(baseDePrueba.Conexion);
        var (_, tres) = SembrarUnDocumentoDeTresPersonas(baseDePrueba.Conexion);

        var resultado = personas.Borrar(tres[0]);

        Console.WriteLine("== {0} ==", resultado.Linea);

        Assert.AreEqual(1, resultado.Borradas.Single(c => c.Tabla == "personas").Filas);
        Assert.AreEqual(2, resultado.Borradas.Single(c => c.Tabla == "procedencia_campo").Filas);
        StringAssert.Contains(resultado.Linea, "1 persona");
    }

    /// <summary>
    /// Dada la regla de que nada se borra sin copia, cuando se borra una persona, entonces
    /// hay una copia de la base al lado con la marca «antes-de-borrar» y el resultado la nombra.
    /// </summary>
    [TestMethod]
    public void AntesDeBorrarHayCopiaDeLaBaseYElResultadoLaNombra()
    {
        using var baseDePrueba = BaseDePrueba.Nueva();
        var personas = new RepositorioDePersonas(baseDePrueba.Conexion);
        var (caso, tres) = SembrarUnDocumentoDeTresPersonas(baseDePrueba.Conexion);

        var resultado = personas.Borrar(tres[2]);

        Console.WriteLine("== copia en «{0}» ==", resultado.RutaDeLaCopia);

        Assert.IsNotNull(resultado.RutaDeLaCopia, "El resultado no dice dónde quedó la copia.");
        Assert.IsTrue(File.Exists(resultado.RutaDeLaCopia), "La copia no está en el disco.");
        StringAssert.Contains(resultado.RutaDeLaCopia, RespaldoAntesDeBorrar.MarcaDeLaCopia);
        Assert.AreEqual(
            Path.GetDirectoryName(baseDePrueba.Ruta), Path.GetDirectoryName(resultado.RutaDeLaCopia),
            "La copia no quedó al lado de la base.");

        // Y la copia es de ANTES: dentro siguen las tres personas.
        using var copia = Fichas.Datos.Conexion.FabricaDeConexiones.Abrir(resultado.RutaDeLaCopia);
        Assert.HasCount(3, new RepositorioDePersonas(copia).DeCaso(caso), "La copia no es de antes de borrar.");
        Assert.HasCount(2, personas.DeCaso(caso), "La base viva no se quedó con dos.");
    }

    /// <summary>Dado un id que no está, cuando se intenta borrar, entonces no se borra nada, no se copia nada y se dice.</summary>
    [TestMethod]
    public void UnIdQueNoEstaNoBorraNadaYLoDice()
    {
        using var baseDePrueba = BaseDePrueba.Nueva();
        var personas = new RepositorioDePersonas(baseDePrueba.Conexion);
        var (_, tres) = SembrarUnDocumentoDeTresPersonas(baseDePrueba.Conexion);
        var copiasAntes = Directory.GetFiles(Path.GetDirectoryName(baseDePrueba.Ruta)!, "*.db").Length;

        var resultado = personas.Borrar(9_999);

        Assert.IsFalse(resultado.SeBorro);
        Assert.IsNotEmpty(resultado.Avisos, "Un borrado que no hace nada y calla es un botón roto.");
        Assert.AreEqual(3, baseDePrueba.ContarFilasDe("personas"));
        Assert.HasCount(
            copiasAntes, Directory.GetFiles(Path.GetDirectoryName(baseDePrueba.Ruta)!, "*.db"),
            "Se hizo una copia de la base para un borrado que no tenía nada que borrar.");
        Assert.HasCount(3, tres);
    }

    /// <summary>
    /// Dada una persona con las seis contestadas y firmadas, cuando se borra, entonces su
    /// firma ya no sale entre las del caso y las de las demás siguen.
    /// </summary>
    [TestMethod]
    public void LaFirmaDeLasSeisSeVaConLaPersona()
    {
        using var baseDePrueba = BaseDePrueba.Nueva();
        var personas = new RepositorioDePersonas(baseDePrueba.Conexion);
        var (caso, tres) = SembrarUnDocumentoDeTresPersonas(baseDePrueba.Conexion);
        var miguel = new RepositorioDeCompaneros(baseDePrueba.Conexion)
            .Guardar(new Companero { Nombre = "Miguel", CreadoEn = DiaDeLasPruebas + " 09:00:00" }).Id;
        var lasSeisEnSi = new RespuestaALosPasos(true, true, true, true, true, true);
        personas.ResponderLosPasos(tres[0], lasSeisEnSi, miguel, "a mano en la pantalla");
        personas.ResponderLosPasos(tres[1], lasSeisEnSi, miguel, "a mano en la pantalla");

        personas.Borrar(tres[0]);

        var firmas = personas.FirmasDeLosPasosDelCaso(caso);
        Assert.IsFalse(firmas.ContainsKey(tres[0]), "La firma de la persona borrada sigue en el caso.");
        Assert.IsTrue(firmas[tres[1]].YaContesto, "La firma de otra persona se perdió.");
        Assert.HasCount(2, firmas);
    }

    /// <summary>
    /// Dada la misma operación sobre el repositorio falso y el de verdad, cuando se borra
    /// la misma persona, entonces los dos dejan lo mismo: dos personas, cero procedencia de
    /// la borrada, y el mismo veredicto. Lo único que no se compara es la copia: el falso no
    /// tiene archivo que copiar y lo dice devolviendo nulo.
    /// </summary>
    [TestMethod]
    public void ElFalsoBorraLoMismoQueElDeVerdad()
    {
        using var baseDePrueba = BaseDePrueba.Nueva();
        var almacen = new AlmacenFalso(new RelojFijo(DiaDeLasPruebas), semilla: 1);
        var deVerdad = new RepositorioDePersonas(baseDePrueba.Conexion);
        var procedenciaDeVerdad = new RepositorioDeProcedencia(baseDePrueba.Conexion);
        var falso = new RepositorioDePersonasFalso(almacen);
        var procedenciaFalsa = new RepositorioDeProcedenciaFalso(almacen);

        var (casoDeVerdad, tresDeVerdad) = SembrarUnDocumentoDeTresPersonas(baseDePrueba.Conexion);
        var casoFalso = new RepositorioDeCasosFalso(almacen)
            .Guardar(new Caso { NumeroCaso = "SURB2609", CreadoEn = DiaDeLasPruebas + " 10:00:00" }).Id;
        var tresFalsas = SembrarTresPersonasEn(falso, procedenciaFalsa, casoFalso);

        var resultadoDeVerdad = deVerdad.Borrar(tresDeVerdad[1]);
        var resultadoFalso = falso.Borrar(tresFalsas[1]);

        Console.WriteLine(
            "== de verdad: {0} · falso: {1} ==", resultadoDeVerdad.LineaDelRegistro, resultadoFalso.LineaDelRegistro);

        Assert.AreEqual(resultadoDeVerdad.SeBorro, resultadoFalso.SeBorro);
        Assert.HasCount(deVerdad.DeCaso(casoDeVerdad).Count, falso.DeCaso(casoFalso));
        Assert.HasCount(
            procedenciaDeVerdad.DeRegistro(TablaDeProcedencia.Personas, tresDeVerdad[1]).Count,
            procedenciaFalsa.DeRegistro(TablaDeProcedencia.Personas, tresFalsas[1]));
        CollectionAssert.AreEqual(
            resultadoDeVerdad.Borradas.Select(c => (c.Tabla, c.Filas)).ToList(),
            resultadoFalso.Borradas.Select(c => (c.Tabla, c.Filas)).ToList(),
            "Los dos no cuentan lo mismo tabla por tabla.");
        Assert.IsNull(resultadoFalso.RutaDeLaCopia, "El falso no tiene archivo que copiar y no debe inventar una ruta.");

        var noEsta = falso.Borrar(9_999);
        Assert.IsFalse(noEsta.SeBorro);
        Assert.IsNotEmpty(noEsta.Avisos);
    }

    /// <summary>Un documento con tres personas y DOS renglones de procedencia por persona (nombre y cédula), como deja la lectura.</summary>
    /// <param name="conexion">La base de prueba abierta.</param>
    /// <returns>El id del caso y los tres ids de las personas, en el orden del papel.</returns>
    private static (long Caso, List<long> Personas) SembrarUnDocumentoDeTresPersonas(SqliteConnection conexion)
    {
        var caso = new RepositorioDeCasos(conexion)
            .Guardar(new Caso { NumeroCaso = "SURB2609", CreadoEn = DiaDeLasPruebas + " 10:00:00" }).Id;
        return (caso, SembrarTresPersonasEn(
            new RepositorioDePersonas(conexion), new RepositorioDeProcedencia(conexion), caso));
    }

    /// <summary>Tres personas con su procedencia en el repositorio que se le pase, falso o de verdad.</summary>
    /// <param name="personas">Donde se guardan las personas.</param>
    /// <param name="procedencia">Donde se anota de dónde salió cada campo.</param>
    /// <param name="caso">El documento al que pertenecen.</param>
    /// <returns>Los tres ids, en el orden del papel.</returns>
    private static List<long> SembrarTresPersonasEn(IPersonas personas, IProcedencia procedencia, long caso)
    {
        var nombres = new[] { "Elena", "Julia", "Marlon" };
        var ids = new List<long>();
        for (var i = 0; i < nombres.Length; i++)
        {
            var id = personas.Guardar(new Persona
            {
                CasoId = caso,
                Mrn = $"055-1111-38{(50 + i).ToString(System.Globalization.CultureInfo.InvariantCulture)}",
                Nombre = nombres[i],
                FilaFormulario = i + 1,
            }).Id;
            ids.Add(id);

            foreach (var campo in new[] { "nombre", "mrn" })
            {
                procedencia.Anotar(new ProcedenciaDeCampo
                {
                    Tabla = TablaDeProcedencia.Personas,
                    RegistroId = id,
                    Campo = campo,
                    Origen = OrigenDeCampo.Ocr,
                    Confianza = 0.95,
                    ValorOcr = campo == "nombre" ? nombres[i] : "leído",
                });
            }
        }

        return ids;
    }
}
