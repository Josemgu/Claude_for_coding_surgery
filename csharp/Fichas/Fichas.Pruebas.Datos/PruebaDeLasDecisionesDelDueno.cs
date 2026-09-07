using Fichas.Contratos.Modelos;
using Fichas.Datos.Repositorios;

namespace Fichas.Pruebas.Datos;

/// <summary>
/// Las dos decisiones del dueno del 2026-09-04 que cambian lo que la base acepta.
/// </summary>
/// <remarks>
/// Las dos van juntas porque son la misma leccion medida dos veces: una regla de
/// formato escrita mirando papeles que no eran los del dueno rechazaba un dato
/// verdadero, y el campo quedaba vacio sin que nadie se enterase.
/// </remarks>
[TestClass]
public sealed class PruebaDeLasDecisionesDelDueno
{
    /// <summary>
    /// Numeros de caso que la regla vieja —4 letras y 4 digitos— rechazaba.
    /// </summary>
    /// <remarks>
    /// Requisito 9 del dueno: «avisar, nunca impedir». El CHECK de `numero_caso`
    /// NO se pone (DECISIONES.md, «El CHECK del numero de caso»): se guarda lo que
    /// venga y quien avise es la pantalla.
    /// </remarks>
    private static readonly string[] NumerosDeCasoRaros =
    [
        "BALC26",            // corto: el OCR se comio dos digitos
        "balc2609",          // en minuscula
        "BALC-2609",         // con guion
        "BALC26091",         // largo
        "12345678",          // todo digitos
        "BALC 2609",         // con espacio
        "Ñ",                 // un solo caracter que ni siquiera es ASCII
        "'); DROP TABLE casos; --",  // y el intento de inyeccion, que entra como dato inerte
    ];

    /// <summary>Cedulas que terminan en letra, que el papel trae y la regla vieja tiraba.</summary>
    /// <remarks>
    /// ⚠️ Los valores van SUSTITUIDOS desde el 2026-09-07; lo que sale del papel es la
    /// FORMA, y es la que hay que conservar: si alguien pone aqui una cedula que NO termine
    /// en letra, esta prueba se queda verde y deja de vigilar la regla de no regresion del
    /// 2026-09-04. Ver `EN-CURSO.md`, «Los datos de personas reales salen del repositorio».
    /// </remarks>
    private static readonly string[] CedulasConLetraFinal =
    [
        "066-2222-133A",   // la forma que el supervisor recorto del PDF real
        "055-1111-385Z",
        "123-4567-890a",   // en minuscula: se guarda tal cual, NO se sube a mayuscula
    ];

    [TestMethod]
    public void UnNumeroDeCasoRaroEntraEnLaBaseYSaleAvisado()
    {
        using var baseDePrueba = BaseDePrueba.Nueva();
        var casos = new RepositorioDeCasos(baseDePrueba.Conexion);

        foreach (var numero in NumerosDeCasoRaros)
        {
            var resultado = casos.Guardar(new Caso
            {
                NumeroCaso = numero,
                CreadoEn = "2026-09-04 10:00:00",
            });

            Assert.IsTrue(
                resultado.SeEscribio,
                $"El numero de caso '{numero}' no se guardo, y el requisito 9 dice " +
                "que se guarda lo que venga.");
            Assert.IsTrue(
                resultado.HayAvisos,
                $"El numero de caso '{numero}' se guardo sin avisar de nada. " +
                "Se guarda Y se avisa: las dos cosas.");

            var guardado = casos.Obtener(resultado.Id);
            Assert.IsNotNull(guardado, $"No se pudo releer el caso '{numero}'.");
            Assert.AreEqual(
                numero,
                guardado.NumeroCaso,
                "El numero de caso no se releyo LETRA POR LETRA como se guardo.");
        }
    }

    [TestMethod]
    public void UnNumeroDeCasoBienFormadoEntraSinAvisoNinguno()
    {
        // El control positivo: si todo avisa, el aviso no dice nada.
        using var baseDePrueba = BaseDePrueba.Nueva();
        var casos = new RepositorioDeCasos(baseDePrueba.Conexion);

        var resultado = casos.Guardar(new Caso
        {
            NumeroCaso = "BALC2609",
            CreadoEn = "2026-09-04 10:00:00",
        });

        Assert.IsTrue(resultado.SeEscribio, "Un numero de caso bien formado no se guardo.");
        Assert.IsFalse(
            resultado.HayAvisos,
            "Un numero de caso bien formado aviso de algo: " +
            string.Join(" | ", resultado.Avisos.Select(a => a.Linea)));
    }

    [TestMethod]
    public void UnaCedulaTerminadaEnLetraEntraYSeReleeTalCual()
    {
        using var baseDePrueba = BaseDePrueba.Nueva();
        var casos = new RepositorioDeCasos(baseDePrueba.Conexion);
        var personas = new RepositorioDePersonas(baseDePrueba.Conexion);

        var caso = casos.Guardar(new Caso { NumeroCaso = "BALC2609", CreadoEn = "2026-09-04 10:00:00" });

        foreach (var cedula in CedulasConLetraFinal)
        {
            var resultado = personas.Guardar(new Persona
            {
                CasoId = caso.Id,
                Mrn = cedula,
                Nombre = "Fulano de Tal",
            });

            Assert.IsTrue(
                resultado.SeEscribio,
                $"La cedula '{cedula}' no se guardo, y DECISIONES.md 2026-09-04 dice " +
                "que el ultimo caracter puede ser letra.");
            Assert.IsFalse(
                resultado.HayAvisos,
                $"La cedula '{cedula}' es valida y aun asi aviso: " +
                string.Join(" | ", resultado.Avisos.Select(a => a.Linea)));

            var guardada = personas.Obtener(resultado.Id);
            Assert.IsNotNull(guardada, $"No se pudo releer la persona de cedula '{cedula}'.");
            Assert.AreEqual(
                cedula,
                guardada.Mrn,
                "La cedula no se releyo tal cual. La minuscula NO se sube a mayuscula: " +
                "corregir lo leido es lo que la regla permanente 1 prohibe.");
        }
    }

    [TestMethod]
    public void UnaCedulaSoloDeDigitosSigueEntrando()
    {
        // Que admitir la letra no haya roto la forma que ya funcionaba.
        using var baseDePrueba = BaseDePrueba.Nueva();
        var casos = new RepositorioDeCasos(baseDePrueba.Conexion);
        var personas = new RepositorioDePersonas(baseDePrueba.Conexion);

        var caso = casos.Guardar(new Caso { NumeroCaso = "BALC2609", CreadoEn = "2026-09-04 10:00:00" });
        var resultado = personas.Guardar(new Persona
        {
            CasoId = caso.Id,
            Mrn = "055-1111-3853",
            Nombre = "Fulano",
        });

        Assert.IsTrue(resultado.SeEscribio, "La cedula de solo digitos dejo de entrar.");
        Assert.IsFalse(resultado.HayAvisos, "La cedula de solo digitos aviso de algo.");
    }

    [TestMethod]
    public void UnaCedulaConLaFormaEquivocadaSeGuardaAvisadaYNoSePierde()
    {
        // Requisito 9 otra vez: lo raro entra y queda senalado. Lo que NO puede pasar
        // es que se pierda en silencio, que es justo lo que costo 2 de 7 cedulas.
        using var baseDePrueba = BaseDePrueba.Nueva();
        var casos = new RepositorioDeCasos(baseDePrueba.Conexion);
        var personas = new RepositorioDePersonas(baseDePrueba.Conexion);

        var caso = casos.Guardar(new Caso { NumeroCaso = "BALC2609", CreadoEn = "2026-09-04 10:00:00" });
        var resultado = personas.Guardar(new Persona
        {
            CasoId = caso.Id,
            Mrn = "1111-3853",
            Nombre = "Fulano",
        });

        Assert.IsTrue(
            resultado.HayAvisos,
            "Una cedula con la forma equivocada no aviso de nada.");
        Assert.IsTrue(
            resultado.Avisos.Any(a => a.Campo == "mrn"),
            "El aviso no dice que el campo es 'mrn', y sin eso la pantalla no sabe " +
            "que casilla poner en rojo.");
    }

    [TestMethod]
    public void GuardarNoLanzaNuncaPorUnValorRaro()
    {
        // Requisito 9 llevado al limite: ni el valor mas absurdo levanta excepcion.
        using var baseDePrueba = BaseDePrueba.Nueva();
        var casos = new RepositorioDeCasos(baseDePrueba.Conexion);

        var resultado = casos.Guardar(new Caso
        {
            NumeroCaso = new string('X', 5000),
            UnidadNumero = "no soy un numero",
            FechaViaje = "2026-02-31",       // forma correcta, fecha que no existe
            EstadoRecomendacion = "lo que sea",
            CreadoEn = "2026-09-04 10:00:00",
        });

        Assert.IsTrue(
            resultado.HayAvisos,
            "Un caso lleno de valores imposibles no aviso de nada.");
    }
}
