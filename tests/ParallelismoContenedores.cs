using Xunit.Sdk;
using Xunit.v3;

// Limita fixtures simultáneas por ensamblado; cada caso conserva sus solicitudes concurrentes.
[assembly: Parallelization(MaxThreads = 4, Algorithm = ParallelAlgorithm.Conservative)]
