// Las pruebas de contratos no comparten estado: cada una construye lo suyo y lo tira.
// MSTest exige decir esto a la cara (MSTEST0001) en vez de dejarlo al azar del entorno.
[assembly: Parallelize(Workers = 0, Scope = ExecutionScope.MethodLevel)]
