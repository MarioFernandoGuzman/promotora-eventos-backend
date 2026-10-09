using System.Reflection;
using System.Xml.Linq;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using NetArchTest.Rules;
using Promotora.Application.Shared.Ports;
using Promotora.Domain.Shared;
using Promotora.Infrastructure.Shared.Persistence;
using Promotora.WebApi.Controllers;

namespace Promotora.Architecture.Tests;

public sealed class DependencyTests
{
    private static readonly Assembly Domain = typeof(Result<>).Assembly;
    private static readonly Assembly Application = typeof(IClock).Assembly;
    private static readonly Assembly Infrastructure = typeof(PromotoraDbContext).Assembly;
    private static readonly Assembly WebApi = typeof(ApiControllerBase).Assembly;

    [Fact]
    public void DomainNoDependeDeOtrasCapasNiBibliotecasExternas()
    {
        var result = Types.InAssembly(Domain).ShouldNot().HaveDependencyOnAny(
            "Promotora.Application", "Promotora.Infrastructure", "Promotora.WebApi", "Promotora.Worker",
            "Microsoft.EntityFrameworkCore", "Npgsql", "Pgvector", "FluentValidation", "Microsoft.AspNetCore", "Serilog", "RabbitMQ", "OpenAI").GetResult();
        Assert.True(result.IsSuccessful, Describe(result));
        var project = XDocument.Load(ProjectPath("Domain"));
        Assert.Empty(project.Descendants("PackageReference"));
        Assert.Empty(project.Descendants("FrameworkReference"));
    }

    [Fact]
    public void ApplicationSoloConoceDomainYSusValidadores()
    {
        var result = Types.InAssembly(Application).ShouldNot().HaveDependencyOnAny(
            "Promotora.Infrastructure", "Promotora.WebApi", "Promotora.Worker", "Microsoft.EntityFrameworkCore",
            "Npgsql", "Pgvector", "Microsoft.AspNetCore", "Serilog", "RabbitMQ", "OpenAI").GetResult();
        Assert.True(result.IsSuccessful, Describe(result));
    }

    [Fact]
    public void InfrastructureNoDependeDeLosHosts()
    {
        var result = Types.InAssembly(Infrastructure).ShouldNot().HaveDependencyOnAny("Promotora.WebApi", "Promotora.Worker").GetResult();
        Assert.True(result.IsSuccessful, Describe(result));
    }

    [Fact]
    public void ControllersNoUsanInfraestructuraDbContextMensajeriaNiLlm()
    {
        var result = Types.InAssembly(WebApi).That().Inherit(typeof(ControllerBase)).ShouldNot().HaveDependencyOnAny(
            "Promotora.Infrastructure", "Microsoft.EntityFrameworkCore", "Npgsql", "Pgvector", "RabbitMQ", "OpenAI").GetResult();
        Assert.True(result.IsSuccessful, Describe(result));
    }

    [Fact]
    public void SoloElCompositionRootDeLaApiConoceInfrastructure()
    {
        // Program es top-level (namespace global); los componentes del API tienen namespace propio.
        var result = Types.InAssembly(WebApi).That().ResideInNamespaceStartingWith("Promotora.WebApi").ShouldNot()
            .HaveDependencyOn("Promotora.Infrastructure").GetResult();
        Assert.True(result.IsSuccessful, Describe(result));
    }

    [Theory]
    [InlineData("Domain", "")]
    [InlineData("Application", "Domain")]
    [InlineData("Infrastructure", "Application,Domain")]
    [InlineData("WebApi", "Application,Infrastructure")]
    [InlineData("Worker", "Application,Infrastructure")]
    public void ReferenciasDeProyectoRespetanElGrafo(string project, string dependencies)
    {
        var xml = XDocument.Load(ProjectPath(project));
        var actual = xml.Descendants("ProjectReference")
            .Select(reference => Path.GetFileNameWithoutExtension(reference.Attribute("Include")!.Value.Replace('\\', '/')))
            .Order().ToArray();
        var expected = dependencies.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(name => "Promotora." + name).Order().ToArray();
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void ControllersHeredenLaBaseVersionada()
    {
        var route = Assert.Single(typeof(ApiControllerBase).GetCustomAttributes<RouteAttribute>(true));
        Assert.Equal("api/v1/[controller]", route.Template);
        Assert.NotNull(typeof(ApiControllerBase).GetCustomAttribute<ApiControllerAttribute>());
        var controllers = WebApi.GetTypes().Where(type => !type.IsAbstract && type.IsAssignableTo(typeof(ControllerBase)));
        Assert.All(controllers, controller => Assert.True(controller.IsAssignableTo(typeof(ApiControllerBase))));
    }

    private static string Describe(NetArchTest.Rules.TestResult result) =>
        "Dependencias prohibidas en: " + string.Join(", ", result.FailingTypeNames ?? []);

    [Fact]
    public void CadaOperacionExplicitaAutenticacionOPublicaAnonima()
    {
        var controllers = WebApi.GetTypes().Where(type => !type.IsAbstract && type.IsAssignableTo(typeof(ControllerBase)));
        foreach (var controller in controllers)
        {
            foreach (var method in controller.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                         .Where(method => method.GetCustomAttributes<HttpMethodAttribute>().Any()))
            {
                Assert.True(method.GetCustomAttributes<AuthorizeAttribute>().Any() || controller.GetCustomAttributes<AuthorizeAttribute>().Any()
                    || method.GetCustomAttributes<AllowAnonymousAttribute>().Any(), $"{controller.Name}.{method.Name} requiere permiso explícito.");
            }
        }
    }

    private static string ProjectPath(string project)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Promotora.sln")))
        {
            directory = directory.Parent;
        }

        if (directory is null)
        {
            throw new DirectoryNotFoundException("No se encontró la raíz de la solución.");
        }

        return Path.Combine(directory.FullName, "src", "Promotora." + project, "Promotora." + project + ".csproj");
    }
}
