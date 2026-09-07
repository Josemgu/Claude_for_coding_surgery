// Cada prueba construye su propio almacen y lo tira: no comparten nada.
// MSTest exige decirlo a la cara (MSTEST0001) en vez de dejarlo al azar del entorno.
[assembly: Parallelize(Workers = 0, Scope = ExecutionScope.MethodLevel)]
