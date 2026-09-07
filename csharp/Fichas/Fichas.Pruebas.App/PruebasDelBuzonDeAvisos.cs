using Fichas.App.Cascara;
using Fichas.Contratos.Modelos;

namespace Fichas.Pruebas.App;

/// <summary>
/// El buzon donde las pantallas dejan sus avisos y de donde los recoge la franja.
/// </summary>
/// <remarks>
/// Criterio: requisito 4 del dueno («ni un parrafo en pantalla»: avisos de una linea,
/// cerrables, con «ver») y requisito 9 («avisar, nunca impedir»). Se prueba sin ventana,
/// que es donde tiene que poder probarse (ADR-0003 §8.1).
/// </remarks>
[TestClass]
public sealed class PruebasDelBuzonDeAvisos
{
    /// <summary>Un buzon recien hecho no tiene nada que ensenar.</summary>
    [TestMethod]
    public void UnBuzonNuevoEstaVacio()
    {
        var buzon = new BuzonDeAvisos();

        Assert.IsEmpty(buzon.Pendientes);
    }

    /// <summary>El aviso mas nuevo es el que se ensena primero.</summary>
    [TestMethod]
    public void ElAvisoMasNuevoSeEnsenaPrimero()
    {
        var buzon = new BuzonDeAvisos();

        buzon.Dejar(Aviso.Informa("El primero."));
        buzon.Dejar(Aviso.Advierte("El segundo."));

        Assert.AreEqual("El segundo.", buzon.Pendientes[0].Linea);
        Assert.HasCount(2, buzon.Pendientes);
    }

    /// <summary>Cerrar el que se ensena deja pasar al siguiente.</summary>
    [TestMethod]
    public void CerrarElPrimeroDejaPasarAlSiguiente()
    {
        var buzon = new BuzonDeAvisos();
        buzon.Dejar(Aviso.Informa("El primero."));
        buzon.Dejar(Aviso.Advierte("El segundo."));

        buzon.CerrarElPrimero();

        Assert.HasCount(1, buzon.Pendientes);
        Assert.AreEqual("El primero.", buzon.Pendientes[0].Linea);
    }

    /// <summary>Cerrar un buzon vacio no lanza.</summary>
    [TestMethod]
    public void CerrarUnBuzonVacioNoLanza()
    {
        var buzon = new BuzonDeAvisos();

        buzon.CerrarElPrimero();
        buzon.CerrarTodos();

        Assert.IsEmpty(buzon.Pendientes);
    }

    /// <summary>Dejar varios avisos de golpe conserva el orden en que venian.</summary>
    [TestMethod]
    public void DejarVariosDeGolpeConservaSuOrden()
    {
        var buzon = new BuzonDeAvisos();

        buzon.Dejar(new[] { Aviso.Advierte("Uno."), Aviso.Advierte("Dos."), Aviso.Advierte("Tres.") });

        Assert.HasCount(3, buzon.Pendientes);
        Assert.AreEqual("Uno.", buzon.Pendientes[0].Linea);
        Assert.AreEqual("Tres.", buzon.Pendientes[2].Linea);
    }

    /// <summary>Una lista de avisos vacia no molesta ni dispara el repintado.</summary>
    [TestMethod]
    public void UnaListaVaciaDeAvisosNoMolesta()
    {
        var buzon = new BuzonDeAvisos();
        var repintados = 0;
        buzon.Cambio += (quien, cuando) => repintados++;

        buzon.Dejar(Array.Empty<Aviso>());

        Assert.AreEqual(0, repintados);
        Assert.IsEmpty(buzon.Pendientes);
    }

    /// <summary>La franja se entera de cada cambio para poder repintarse sola.</summary>
    [TestMethod]
    public void LaFranjaSeEnteraDeCadaCambio()
    {
        var buzon = new BuzonDeAvisos();
        var repintados = 0;
        buzon.Cambio += (quien, cuando) => repintados++;

        buzon.Dejar(Aviso.Informa("Guardado."));
        buzon.CerrarElPrimero();

        Assert.AreEqual(2, repintados);
    }
}
