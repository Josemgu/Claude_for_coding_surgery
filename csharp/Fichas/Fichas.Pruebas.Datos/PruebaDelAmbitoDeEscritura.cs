using Fichas.Contratos.Modelos;
using Fichas.Datos.Conexion;
using Fichas.Datos.Repositorios;
using Microsoft.Data.Sqlite;

namespace Fichas.Pruebas.Datos;

/// <summary>
/// El ámbito de escritura (R-4): varias filas de varios repositorios en UNA transacción de
/// SQLite, y lo que el motor de Microsoft.Data.Sqlite 10 exige para que eso funcione.
/// </summary>
/// <remarks>
/// <para>Las dos primeras pruebas son las que el plan del 2026-09-15 pedía comprobar antes de
/// programar (PENDIENTES.md, R-4): si una orden SIN <c>Transaction</c> asignada puede
/// ejecutarse mientras la conexión tiene una transacción abierta. Medido con
/// Microsoft.Data.Sqlite 10.0.11: <b>no puede</b> (levanta <see cref="InvalidOperationException"/>),
/// pero <c>SqliteConnection.CreateCommand()</c> <b>asigna a la orden la transacción abierta en ese
/// momento</b>. Como los repositorios crean su orden en cada llamada, se unen solos al ámbito
/// sin recibir nada; lo que NO vale es guardar una orden y reutilizarla de un ámbito a otro.</para>
/// </remarks>
[TestClass]
public sealed class PruebaDelAmbitoDeEscritura
{
    /// <summary>
    /// Una orden creada ANTES de abrir la transacción, y por tanto sin ella asignada, NO se
    /// ejecuta mientras la conexión la tiene abierta: el motor la rechaza.
    /// </summary>
    /// <remarks>
    /// Es la mitad del plan que sí se cumple, y la que prohíbe guardar una orden preparada y
    /// reutilizarla de un ámbito a otro: se quedaría con la transacción de cuando nació.
    /// </remarks>
    [TestMethod]
    public void UnaOrdenCreadaAntesDeLaTransaccionNoSeEjecutaDentro()
    {
        using var base_ = BaseDePrueba.Nueva();
        using var orden = base_.Conexion.CreateCommand();
        orden.CommandText = "SELECT COUNT(*) FROM casos";
        Assert.IsNull(orden.Transaction, "nació sin transacción porque no había ninguna.");

        using var trato = base_.Conexion.BeginTransaction();

        var fallo = Assert.ThrowsExactly<InvalidOperationException>(() => orden.ExecuteScalar());
        Assert.Contains("transaction", fallo.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Una orden creada DESPUÉS de abrir la transacción nace con ella asignada, sin que nadie
    /// se la pase, y lo que escribe cae dentro: se va con el rollback.
    /// </summary>
    /// <remarks>
    /// Es la mitad que el plan no sabía y la que decide el diseño: los repositorios crean su
    /// orden en cada llamada, así que basta abrir el ámbito sobre la conexión. Si una versión
    /// futura del paquete dejara de asignarla en <c>CreateCommand</c>, esta prueba se pondría
    /// en rojo y avisaría de que los repositorios tendrían que recibirla.
    /// </remarks>
    [TestMethod]
    public void UnaOrdenCreadaDentroDeLaTransaccionNaceUnidaAEllaYCaeConElRollback()
    {
        using var base_ = BaseDePrueba.Nueva();

        using (var trato = base_.Conexion.BeginTransaction())
        {
            using var orden = base_.Conexion.CreateCommand();
            Assert.AreSame(trato, orden.Transaction, "CreateCommand le asignó la transacción abierta.");
            orden.CommandText = "INSERT INTO casos (numero_caso, creado_en) VALUES ('CASP2609', '2026-09-15T10:00:00')";
            Assert.AreEqual(1, orden.ExecuteNonQuery());
            trato.Rollback();
        }

        Assert.AreEqual(0, base_.ContarFilasDe("casos"), "el rollback se llevó lo escrito: estaba dentro.");
    }

    /// <summary>
    /// Un repositorio escribe y lee DENTRO de un ámbito abierto sobre su conexión, sin que
    /// nadie le pase la transacción.
    /// </summary>
    [TestMethod]
    public void LosRepositoriosSeUnenSolosAlAmbitoAbiertoSobreSuConexion()
    {
        using var base_ = BaseDePrueba.Nueva();
        var casos = new RepositorioDeCasos(base_.Conexion);
        var personas = new RepositorioDePersonas(base_.Conexion);

        using (var ambito = AmbitoDeEscritura.Abrir(base_.Conexion))
        {
            var caso = casos.Guardar(new Caso { NumeroCaso = "CASP2609" });
            Assert.IsTrue(caso.SeEscribio, string.Join(" ", caso.Avisos.Select(uno => uno.Linea)));
            var persona = personas.Guardar(new Persona { CasoId = caso.Id, Nombre = "Ana", Mrn = "055-1111-3853" });
            Assert.IsTrue(persona.SeEscribio, string.Join(" ", persona.Avisos.Select(uno => uno.Linea)));
            Assert.IsNotNull(casos.Obtener(caso.Id), "dentro del ámbito, la lectura también se une a la transacción.");
            ambito.Confirmar();
        }

        Assert.AreEqual(1, base_.ContarFilasDe("casos"));
        Assert.AreEqual(1, base_.ContarFilasDe("personas"));
    }

    /// <summary>
    /// Un ámbito que se cierra SIN confirmar deshace todo lo escrito dentro: no queda ni el
    /// caso ni las personas anteriores al fallo.
    /// </summary>
    /// <remarks>
    /// Es el defecto 3 de la deuda de Datos del 2026-09-11 aplicado a la importación: hoy,
    /// si algo revienta en la tercera persona, el caso y las dos primeras quedan a medias.
    /// </remarks>
    [TestMethod]
    public void UnAmbitoQueNoSeConfirmaNoDejaNada()
    {
        using var base_ = BaseDePrueba.Nueva();
        var casos = new RepositorioDeCasos(base_.Conexion);
        var personas = new RepositorioDePersonas(base_.Conexion);

        using (AmbitoDeEscritura.Abrir(base_.Conexion))
        {
            var caso = casos.Guardar(new Caso { NumeroCaso = "CASP2609" });
            personas.Guardar(new Persona { CasoId = caso.Id, Nombre = "Ana", Mrn = "055-1111-3853" });
            personas.Guardar(new Persona { CasoId = caso.Id, Nombre = "Luis", Mrn = "055-2222-3853" });
            // La tercera «falla»: quien guarda sale sin confirmar.
        }

        Assert.AreEqual(0, base_.ContarFilasDe("casos"));
        Assert.AreEqual(0, base_.ContarFilasDe("personas"));
    }

    /// <summary>
    /// Una fila que el motor rechaza DENTRO del ámbito no tumba las demás: el ámbito se
    /// confirma con lo que sí entró y el rechazo sigue siendo un aviso (requisito 9).
    /// </summary>
    /// <remarks>
    /// Es lo que mantiene «ninguna hoja se rechaza»: la cédula repetida de una persona deja
    /// un aviso y las otras personas y el caso entran. SQLite deshace solo la orden que falló
    /// (<c>ON CONFLICT ABORT</c>, el valor por defecto), no la transacción.
    /// </remarks>
    [TestMethod]
    public void UnaFilaRechazadaDentroDelAmbitoNoTumbaLasDemas()
    {
        using var base_ = BaseDePrueba.Nueva();
        var casos = new RepositorioDeCasos(base_.Conexion);
        var personas = new RepositorioDePersonas(base_.Conexion);

        using (var ambito = AmbitoDeEscritura.Abrir(base_.Conexion))
        {
            var caso = casos.Guardar(new Caso { NumeroCaso = "CASP2609" });
            personas.Guardar(new Persona { CasoId = caso.Id, Nombre = "Ana", Mrn = "055-1111-3853" });
            var repetida = personas.Guardar(new Persona { CasoId = caso.Id, Nombre = "Ana otra vez", Mrn = "055-1111-3853" });
            Assert.IsFalse(repetida.SeEscribio, "la cédula repetida en el mismo caso la rechaza el UNIQUE.");
            personas.Guardar(new Persona { CasoId = caso.Id, Nombre = "Luis", Mrn = "055-2222-3853" });
            ambito.Confirmar();
        }

        Assert.AreEqual(1, base_.ContarFilasDe("casos"));
        Assert.AreEqual(2, base_.ContarFilasDe("personas"));
    }

    /// <summary>
    /// Confirmar dos veces, o confirmar después de cerrar, no hace nada raro: el segundo
    /// intento se rechaza con una excepción clara y la base queda como la dejó el primero.
    /// </summary>
    [TestMethod]
    public void ConfirmarDosVecesSeRechaza()
    {
        using var base_ = BaseDePrueba.Nueva();
        var ambito = AmbitoDeEscritura.Abrir(base_.Conexion);
        ambito.Confirmar();

        Assert.ThrowsExactly<InvalidOperationException>(ambito.Confirmar);
        ambito.Dispose();
        AssertQueLaConexionQuedaLibre(base_.Conexion);
    }

    /// <summary>
    /// Abrir un ámbito cuando la conexión ya tiene una transacción se rechaza: el motor no
    /// anida transacciones y el programa no debe fingir que sí. Vale para las dos formas de
    /// tenerla abierta: otro ámbito, o una abierta a mano como hacen borrar y unificar.
    /// </summary>
    [TestMethod]
    public void AbrirUnAmbitoConOtraTransaccionAbiertaSeRechaza()
    {
        using var base_ = BaseDePrueba.Nueva();

        using (AmbitoDeEscritura.Abrir(base_.Conexion))
        {
            Assert.ThrowsExactly<InvalidOperationException>(() => AmbitoDeEscritura.Abrir(base_.Conexion));
        }

        using var trato = base_.Conexion.BeginTransaction();
        Assert.ThrowsExactly<InvalidOperationException>(() => AmbitoDeEscritura.Abrir(base_.Conexion));
    }

    /// <summary>
    /// Cerrado un ámbito, la conexión queda libre: los repositorios vuelven a escribir sueltos
    /// y se puede abrir el siguiente. Es lo que pasa hoja tras hoja en una tanda.
    /// </summary>
    [TestMethod]
    public void TrasCerrarUnAmbitoLosRepositoriosSiguenEscribiendoYSePuedeAbrirOtro()
    {
        using var base_ = BaseDePrueba.Nueva();
        var casos = new RepositorioDeCasos(base_.Conexion);

        using (AmbitoDeEscritura.Abrir(base_.Conexion))
        {
            casos.Guardar(new Caso { NumeroCaso = "CASP2609" });
        }

        AssertQueLaConexionQuedaLibre(base_.Conexion);
        Assert.IsTrue(casos.Guardar(new Caso { NumeroCaso = "CASP2610" }).SeEscribio, "suelto, fuera de todo ámbito.");

        using (var segundo = AmbitoDeEscritura.Abrir(base_.Conexion))
        {
            casos.Guardar(new Caso { NumeroCaso = "CASP2611" });
            segundo.Confirmar();
        }

        Assert.AreEqual(2, base_.ContarFilasDe("casos"), "el deshecho no está; el suelto y el confirmado sí.");
    }

    /// <summary>Una orden SIN transacción se ejecuta: la conexión no tiene ninguna abierta.</summary>
    /// <param name="conexion">La conexión que se comprueba.</param>
    private static void AssertQueLaConexionQuedaLibre(SqliteConnection conexion)
    {
        using var orden = conexion.CreateCommand();
        orden.CommandText = "SELECT 1";
        Assert.AreEqual(1L, orden.ExecuteScalar(), "una orden sin transacción solo se ejecuta si la conexión no tiene ninguna abierta.");
    }

    /// <summary>
    /// El contador de confirmaciones de SQLite: N escrituras dentro de un ámbito son UNA
    /// confirmación; las mismas N sin ámbito son N.
    /// </summary>
    /// <remarks>
    /// Se cuenta con el «file change counter» de la cabecera del archivo de SQLite
    /// (<see href="https://www.sqlite.org/fileformat.html#file_change_counter"/>, bytes 24 a
    /// 27, entero sin signo en orden de red), que en el modo de diario por defecto —el de
    /// este programa, sin WAL— sube UNA vez por cada transacción confirmada que cambió la base.
    /// </remarks>
    [TestMethod]
    public void TresEscriturasEnUnAmbitoSonUnaSolaConfirmacion()
    {
        using var base_ = BaseDePrueba.Nueva();
        var casos = new RepositorioDeCasos(base_.Conexion);
        var personas = new RepositorioDePersonas(base_.Conexion);

        var antes = ContadorDeCambiosDelArchivo(base_.Ruta);
        using (var ambito = AmbitoDeEscritura.Abrir(base_.Conexion))
        {
            var caso = casos.Guardar(new Caso { NumeroCaso = "CASP2609" });
            personas.Guardar(new Persona { CasoId = caso.Id, Nombre = "Ana", Mrn = "055-1111-3853" });
            personas.Guardar(new Persona { CasoId = caso.Id, Nombre = "Luis", Mrn = "055-2222-3853" });
            ambito.Confirmar();
        }

        Assert.AreEqual(1u, ContadorDeCambiosDelArchivo(base_.Ruta) - antes, "tres escrituras en un ámbito son una confirmación.");

        antes = ContadorDeCambiosDelArchivo(base_.Ruta);
        var otro = casos.Guardar(new Caso { NumeroCaso = "CASP2610" });
        personas.Guardar(new Persona { CasoId = otro.Id, Nombre = "Ana", Mrn = "055-1111-3853" });
        personas.Guardar(new Persona { CasoId = otro.Id, Nombre = "Luis", Mrn = "055-2222-3853" });

        Assert.AreEqual(3u, ContadorDeCambiosDelArchivo(base_.Ruta) - antes, "control positivo: sin ámbito, tres escrituras son tres confirmaciones.");
    }

    /// <summary>El contador de cambios de la cabecera del archivo: cuántas transacciones lo han cambiado.</summary>
    /// <param name="ruta">El archivo <c>.db</c>, que puede seguir abierto por la conexión de la prueba.</param>
    public static uint ContadorDeCambiosDelArchivo(string ruta)
    {
        using var flujo = new FileStream(ruta, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        var cabecera = new byte[28];
        flujo.ReadExactly(cabecera);
        return System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(cabecera.AsSpan(24, 4));
    }
}
