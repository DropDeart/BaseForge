namespace BaseForge.CodeGen.Generation;

/// <summary>Scriban kod üretim şablonları (gömülü).</summary>
internal static class Templates
{
    public const string Entity =
        """
        using BaseForge.Core.Entities;

        namespace {{ Namespace }}.Entities;

        /// <summary>{{ Name }} entity'si (BaseForge.CodeGen tarafından üretildi).</summary>
        public sealed class {{ Name }} : BaseEntity
        {
        {{~ for p in Scalars ~}}
            /// <summary>{{ p.Name }}.</summary>
            public {{ p.Type }} {{ p.Name }} { get; set; }{{ p.Init }}
        {{~ end ~}}
        {{~ for n in Navigations ~}}
        {{~ if n.IsCollection ~}}
            /// <summary>{{ n.Name }} (servis içi ilişki).</summary>
            public ICollection<{{ n.Type }}> {{ n.Name }} { get; } = [];
        {{~ else ~}}
            /// <summary>{{ n.Name }} (servis içi ilişki).</summary>
            public {{ n.Type }}? {{ n.Name }} { get; set; }
        {{~ end ~}}
        {{~ end ~}}
        }

        """;

    public const string DbContext =
        """
        using BaseForge.Infrastructure.Data;
        using Microsoft.EntityFrameworkCore;
        using {{ Namespace }}.Entities;

        namespace {{ Namespace }}.Data;

        /// <summary>{{ ServiceName }} servisinin EF Core context'i.</summary>
        public sealed class {{ ContextName }} : BaseForgeDbContext
        {
            /// <summary>Yeni bir {{ ContextName }} oluşturur.</summary>
            public {{ ContextName }}(DbContextOptions<{{ ContextName }}> options)
                : base(options)
            {
            }

        {{~ for e in Entities ~}}
            /// <summary>{{ e.Name }} tablosu.</summary>
            public DbSet<{{ e.Name }}> {{ e.Plural }} => Set<{{ e.Name }}>();
        {{~ end ~}}
        }

        """;

    public const string Dto =
        """
        using {{ Namespace }}.Entities;

        namespace {{ Namespace }}.Features.{{ Name }}s;

        /// <summary>{{ Name }} veri transfer nesnesi.</summary>
        public sealed class {{ Name }}Dto
        {
            /// <summary>Kayıt kimliği.</summary>
            public Guid Id { get; set; }
        {{~ for f in Fields ~}}
            /// <summary>{{ f.Name }}.</summary>
            public {{ f.Type }} {{ f.Name }} { get; set; }{{ f.Init }}
        {{~ end ~}}

            /// <summary>Bir {{ Name }} entity'sinden DTO üretir.</summary>
            public static {{ Name }}Dto From({{ Name }} entity)
            {
                ArgumentNullException.ThrowIfNull(entity);
                return new {{ Name }}Dto
                {
                    Id = entity.Id,
        {{~ for f in Fields ~}}
                    {{ f.Name }} = entity.{{ f.Name }},
        {{~ end ~}}
                };
            }
        }

        """;

    public const string Commands =
        """
        using BaseForge.Core.CQRS;
        using BaseForge.Core.Exceptions;
        using BaseForge.Core.Interfaces;
        using {{ Namespace }}.Entities;

        namespace {{ Namespace }}.Features.{{ Name }}s;

        /// <summary>Yeni bir {{ Name }} oluşturur; üretilen kimliği döndürür.</summary>
        public sealed class Create{{ Name }}Command : ICommand<Guid>
        {
        {{~ for f in Fields ~}}
            /// <summary>{{ f.Name }}.</summary>
            public {{ f.Type }} {{ f.Name }} { get; set; }{{ f.Init }}
        {{~ end ~}}
        }

        internal sealed class Create{{ Name }}Handler : ICommandHandler<Create{{ Name }}Command, Guid>
        {
            private readonly IRepository<{{ Name }}> _repository;
            private readonly IUnitOfWork _unitOfWork;

            public Create{{ Name }}Handler(IRepository<{{ Name }}> repository, IUnitOfWork unitOfWork)
            {
                _repository = repository;
                _unitOfWork = unitOfWork;
            }

            public async Task<Guid> Handle(Create{{ Name }}Command request, CancellationToken cancellationToken)
            {
                ArgumentNullException.ThrowIfNull(request);
                var entity = new {{ Name }}
                {
        {{~ for f in Fields ~}}
                    {{ f.Name }} = request.{{ f.Name }},
        {{~ end ~}}
                };
                await _repository.AddAsync(entity, cancellationToken);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
                return entity.Id;
            }
        }

        /// <summary>Var olan bir {{ Name }} kaydını günceller.</summary>
        public sealed class Update{{ Name }}Command : ICommand
        {
            /// <summary>Güncellenecek kaydın kimliği.</summary>
            public Guid Id { get; set; }
        {{~ for f in Fields ~}}
            /// <summary>{{ f.Name }}.</summary>
            public {{ f.Type }} {{ f.Name }} { get; set; }{{ f.Init }}
        {{~ end ~}}
        }

        internal sealed class Update{{ Name }}Handler : ICommandHandler<Update{{ Name }}Command>
        {
            private readonly IRepository<{{ Name }}> _repository;
            private readonly IUnitOfWork _unitOfWork;

            public Update{{ Name }}Handler(IRepository<{{ Name }}> repository, IUnitOfWork unitOfWork)
            {
                _repository = repository;
                _unitOfWork = unitOfWork;
            }

            public async Task Handle(Update{{ Name }}Command request, CancellationToken cancellationToken)
            {
                ArgumentNullException.ThrowIfNull(request);
                var entity = await _repository.GetByIdAsync(request.Id, cancellationToken)
                    ?? throw new NotFoundException("{{ Name }}", request.Id);
        {{~ for f in Fields ~}}
                entity.{{ f.Name }} = request.{{ f.Name }};
        {{~ end ~}}
                await _repository.UpdateAsync(entity, cancellationToken);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }
        }

        /// <summary>Bir {{ Name }} kaydını siler (soft delete).</summary>
        public sealed class Delete{{ Name }}Command : ICommand
        {
            /// <summary>Silinecek kaydın kimliği.</summary>
            public Guid Id { get; set; }
        }

        internal sealed class Delete{{ Name }}Handler : ICommandHandler<Delete{{ Name }}Command>
        {
            private readonly IRepository<{{ Name }}> _repository;
            private readonly IUnitOfWork _unitOfWork;

            public Delete{{ Name }}Handler(IRepository<{{ Name }}> repository, IUnitOfWork unitOfWork)
            {
                _repository = repository;
                _unitOfWork = unitOfWork;
            }

            public async Task Handle(Delete{{ Name }}Command request, CancellationToken cancellationToken)
            {
                ArgumentNullException.ThrowIfNull(request);
                var entity = await _repository.GetByIdAsync(request.Id, cancellationToken)
                    ?? throw new NotFoundException("{{ Name }}", request.Id);
                await _repository.DeleteAsync(entity, cancellationToken);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }
        }

        """;

    public const string Queries =
        """
        using BaseForge.Core.CQRS;
        using BaseForge.Core.Interfaces;
        using {{ Namespace }}.Entities;

        namespace {{ Namespace }}.Features.{{ Name }}s;

        /// <summary>Kimliğe göre tek bir {{ Name }} getirir.</summary>
        public sealed class Get{{ Name }}ByIdQuery : IQuery<{{ Name }}Dto?>
        {
            /// <summary>Aranan kaydın kimliği.</summary>
            public Guid Id { get; set; }
        }

        internal sealed class Get{{ Name }}ByIdHandler : IQueryHandler<Get{{ Name }}ByIdQuery, {{ Name }}Dto?>
        {
            private readonly IRepository<{{ Name }}> _repository;

            public Get{{ Name }}ByIdHandler(IRepository<{{ Name }}> repository) => _repository = repository;

            public async Task<{{ Name }}Dto?> Handle(Get{{ Name }}ByIdQuery request, CancellationToken cancellationToken)
            {
                ArgumentNullException.ThrowIfNull(request);
                var entity = await _repository.GetByIdAsync(request.Id, cancellationToken);
                return entity is null ? null : {{ Name }}Dto.From(entity);
            }
        }

        /// <summary>Tüm {{ Name }} kayıtlarını getirir.</summary>
        public sealed class List{{ Name }}Query : IQuery<IReadOnlyList<{{ Name }}Dto>>;

        internal sealed class List{{ Name }}Handler : IQueryHandler<List{{ Name }}Query, IReadOnlyList<{{ Name }}Dto>>
        {
            private readonly IRepository<{{ Name }}> _repository;

            public List{{ Name }}Handler(IRepository<{{ Name }}> repository) => _repository = repository;

            public async Task<IReadOnlyList<{{ Name }}Dto>> Handle(List{{ Name }}Query request, CancellationToken cancellationToken)
            {
                var items = await _repository.ListAllAsync(cancellationToken);
                return items.Select({{ Name }}Dto.From).ToList();
            }
        }

        """;

    public const string Controller =
        """
        using BaseForge.API.Controllers;
        using Microsoft.AspNetCore.Mvc;
        using {{ Namespace }}.Features.{{ Name }}s;

        namespace {{ Namespace }}.Controllers;

        /// <summary>{{ Name }} CRUD uçları.</summary>
        [Route("api/[controller]")]
        public sealed class {{ Name }}sController : BaseController
        {
            /// <summary>Kimliğe göre tek bir {{ Name }} getirir.</summary>
            [HttpGet("{id:guid}")]
            public async Task<ActionResult<{{ Name }}Dto>> GetById(Guid id, CancellationToken cancellationToken)
            {
                var result = await Mediator.Send(new Get{{ Name }}ByIdQuery { Id = id }, cancellationToken);
                return result is null ? NotFound() : Ok(result);
            }

            /// <summary>Tüm {{ Name }} kayıtlarını listeler.</summary>
            [HttpGet]
            public async Task<ActionResult<IReadOnlyList<{{ Name }}Dto>>> List(CancellationToken cancellationToken)
                => Ok(await Mediator.Send(new List{{ Name }}Query(), cancellationToken));

            /// <summary>Yeni bir {{ Name }} oluşturur.</summary>
            [HttpPost]
            public async Task<ActionResult<Guid>> Create(Create{{ Name }}Command command, CancellationToken cancellationToken)
            {
                var id = await Mediator.Send(command, cancellationToken);
                return CreatedAtAction(nameof(GetById), new { id }, id);
            }

            /// <summary>Var olan bir {{ Name }} kaydını günceller.</summary>
            [HttpPut("{id:guid}")]
            public async Task<IActionResult> Update(Guid id, Update{{ Name }}Command command, CancellationToken cancellationToken)
            {
                ArgumentNullException.ThrowIfNull(command);
                command.Id = id;
                await Mediator.Send(command, cancellationToken);
                return NoContent();
            }

            /// <summary>Bir {{ Name }} kaydını siler.</summary>
            [HttpDelete("{id:guid}")]
            public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
            {
                await Mediator.Send(new Delete{{ Name }}Command { Id = id }, cancellationToken);
                return NoContent();
            }
        }

        """;

    public const string Program =
        """
        using BaseForge.API.Extensions;
        using Microsoft.EntityFrameworkCore;
        using Scalar.AspNetCore;
        using {{ Namespace }}.Data;

        var builder = WebApplication.CreateBuilder(args);

        builder.Services.AddControllers();
        builder.Services.AddOpenApi();
        builder.Services.AddBaseForge(options =>
        {
            options.UsePostgreSQL<{{ ContextName }}>(
                builder.Configuration.GetConnectionString("Default")
                    ?? throw new InvalidOperationException("ConnectionStrings:Default tanımlı değil."));
            options.EnableCQRS(typeof(Program).Assembly);
            options.EnableAuditLog();
        });

        var app = builder.Build();

        if (app.Environment.IsDevelopment())
        {
            // Hızlı başlangıç: migration yerine şemayı oluştur (yalnızca geliştirme).
            // Postgres erişilemezse uygulama yine de açılır; API arayüzü görülebilir.
            try
            {
                using var scope = app.Services.CreateScope();
                scope.ServiceProvider.GetRequiredService<{{ ContextName }}>().Database.EnsureCreated();
            }
            catch (Exception ex)
            {
                app.Logger.LogWarning(ex, "Veritabanı şeması oluşturulamadı (Postgres çalışıyor mu?). API arayüzü yine de açık.");
            }

            // API arayüzü: /scalar/v1 (OpenAPI: /openapi/v1.json)
            app.MapOpenApi();
            app.MapScalarApiReference(options =>
            {
                options.WithTitle("{{ Namespace }} API");
                options.WithTheme(ScalarTheme.Default);
                options.WithDarkModeToggle(true);

                // "Ask AI" (Agent Scalar): localhost'ta key'siz ücretsiz/limitli çalışır.
                // Production'da Scalar Agent key'i ver: options.WithAgentKey("SCALAR_AGENT_KEY");
                // Veya veri gizliliği için tamamen kapat:        options.DisableAgent();
            });
        }

        app.UseBaseForge();
        app.MapControllers();
        app.Run();

        """;

    public const string AppSettings =
        """
        {
          "ConnectionStrings": {
            "Default": "Host=localhost;Port=5432;Database={{ Database }};Username=baseforge;Password=change_me"
          },
          "Logging": {
            "LogLevel": {
              "Default": "Information",
              "Microsoft.AspNetCore": "Warning"
            }
          }
        }

        """;

    public const string Dockerfile =
        """
        FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
        WORKDIR /src
        COPY . .
        RUN dotnet publish {{ Namespace }}.csproj -c Release -o /app

        FROM mcr.microsoft.com/dotnet/aspnet:10.0
        WORKDIR /app
        COPY --from=build /app .
        ENTRYPOINT ["dotnet", "{{ Namespace }}.dll"]

        """;

    public const string ComposeSnippet =
        """
        # Bu bloğu kök docker-compose.yml'a ekleyin (postgres servisinin yanına).
        services:
          {{ Service }}-service:
            build: .
            environment:
              ConnectionStrings__Default: "Host=postgres;Port=5432;Database={{ Database }};Username=baseforge;Password=change_me"
            depends_on:
              postgres:
                condition: service_healthy

        """;

    public const string GrpcStub =
        """
        namespace {{ Namespace }}.Integration;

        /// <summary>
        /// {{ Target }} servisine senkron (gRPC) erişim sözleşmesi (stub).
        /// Gerçek gRPC istemcisi ve .proto dosyası ayrıca eklenmelidir; bu servis
        /// uzak kaydın yalnızca kimliğini tutar (cross-DB FK yoktur).
        /// </summary>
        public interface I{{ Entity }}Client
        {
            /// <summary>Uzak servisten {{ Entity }} referansını getirir.</summary>
            Task<{{ Entity }}Reference?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
        }

        /// <summary>Uzak {{ Entity }} kaydının yerel referans görünümü.</summary>
        public sealed class {{ Entity }}Reference
        {
            /// <summary>Uzak kaydın kimliği.</summary>
            public Guid Id { get; set; }
        }

        """;

    public const string Project =
        """
        <Project Sdk="Microsoft.NET.Sdk.Web">

          <PropertyGroup>
            <TargetFramework>net10.0</TargetFramework>
            <Nullable>enable</Nullable>
            <ImplicitUsings>enable</ImplicitUsings>
            <RootNamespace>{{ Namespace }}</RootNamespace>
            <!-- Üretilen iskele kodu; üst klasörden miras kalabilecek katı ayarları nötrle -->
            <TreatWarningsAsErrors>false</TreatWarningsAsErrors>
            <GenerateDocumentationFile>false</GenerateDocumentationFile>
          </PropertyGroup>

          <ItemGroup>
            <PackageReference Include="BaseForge.API" Version="{{ BaseForgeVersion }}" />
            <PackageReference Include="Microsoft.AspNetCore.OpenApi" Version="10.0.9" />
            <PackageReference Include="Scalar.AspNetCore" Version="2.16.5" />
          </ItemGroup>

        </Project>

        """;

    public const string LaunchSettings =
        """
        {
          "$schema": "https://json.schemastore.org/launchsettings.json",
          "profiles": {
            "{{ Namespace }}": {
              "commandName": "Project",
              "launchBrowser": false,
              "applicationUrl": "http://localhost:5080",
              "environmentVariables": {
                "ASPNETCORE_ENVIRONMENT": "Development"
              }
            }
          }
        }

        """;
}
