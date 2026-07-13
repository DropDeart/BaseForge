namespace BaseForge.CodeGen.Generation;

/// <summary>Scriban kod üretim şablonları (gömülü).</summary>
internal static class Templates
{
    public const string Entity =
        """
        using System.ComponentModel.DataAnnotations;
        using BaseForge.Core.Entities;

        namespace {{ Namespace }}.Entities;

        /// <summary>{{ Name }} entity'si (BaseForge.CodeGen tarafından üretildi).</summary>
        public sealed class {{ Name }} : BaseEntity
        {
        {{~ for p in Scalars ~}}
            /// <summary>{{ p.Name }}.</summary>
        {{~ if p.MaxLength ~}}
            [MaxLength({{ p.MaxLength }})]
        {{~ end ~}}
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
        using System.ComponentModel.DataAnnotations;
        using BaseForge.Core.CQRS;
        using BaseForge.Core.Exceptions;
        using BaseForge.Core.Interfaces;
        {{~ if HasAnyPublish ~}}
        using BaseForge.Core.Messaging;
        {{~ end ~}}
        using {{ Namespace }}.Entities;

        namespace {{ Namespace }}.Features.{{ Name }}s;

        /// <summary>Yeni bir {{ Name }} oluşturur; üretilen kimliği döndürür.</summary>
        public sealed class Create{{ Name }}Command : ICommand<Guid>
        {
        {{~ for f in Fields ~}}
            /// <summary>{{ f.Name }}.</summary>
        {{~ if f.MaxLength ~}}
            [MaxLength({{ f.MaxLength }})]
        {{~ end ~}}
            public {{ f.Type }} {{ f.Name }} { get; set; }{{ f.Init }}
        {{~ end ~}}
        }

        internal sealed class Create{{ Name }}Handler : ICommandHandler<Create{{ Name }}Command, Guid>
        {
            private readonly IRepository<{{ Name }}> _repository;
            private readonly IUnitOfWork _unitOfWork;
        {{~ if PublishCreated ~}}
            private readonly IEventBus _eventBus;

            public Create{{ Name }}Handler(IRepository<{{ Name }}> repository, IUnitOfWork unitOfWork, IEventBus eventBus)
            {
                _repository = repository;
                _unitOfWork = unitOfWork;
                _eventBus = eventBus;
            }
        {{~ else ~}}
            public Create{{ Name }}Handler(IRepository<{{ Name }}> repository, IUnitOfWork unitOfWork)
            {
                _repository = repository;
                _unitOfWork = unitOfWork;
            }
        {{~ end ~}}

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
        {{~ if PublishCreated ~}}
                await _eventBus.PublishAsync(new {{ Name }}CreatedEvent { Data = {{ Name }}Dto.From(entity) }, cancellationToken);
        {{~ end ~}}
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
        {{~ if f.MaxLength ~}}
            [MaxLength({{ f.MaxLength }})]
        {{~ end ~}}
            public {{ f.Type }} {{ f.Name }} { get; set; }{{ f.Init }}
        {{~ end ~}}
        }

        internal sealed class Update{{ Name }}Handler : ICommandHandler<Update{{ Name }}Command>
        {
            private readonly IRepository<{{ Name }}> _repository;
            private readonly IUnitOfWork _unitOfWork;
        {{~ if PublishUpdated ~}}
            private readonly IEventBus _eventBus;

            public Update{{ Name }}Handler(IRepository<{{ Name }}> repository, IUnitOfWork unitOfWork, IEventBus eventBus)
            {
                _repository = repository;
                _unitOfWork = unitOfWork;
                _eventBus = eventBus;
            }
        {{~ else ~}}
            public Update{{ Name }}Handler(IRepository<{{ Name }}> repository, IUnitOfWork unitOfWork)
            {
                _repository = repository;
                _unitOfWork = unitOfWork;
            }
        {{~ end ~}}

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
        {{~ if PublishUpdated ~}}
                await _eventBus.PublishAsync(new {{ Name }}UpdatedEvent { Data = {{ Name }}Dto.From(entity) }, cancellationToken);
        {{~ end ~}}
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
        {{~ if PublishDeleted ~}}
            private readonly IEventBus _eventBus;

            public Delete{{ Name }}Handler(IRepository<{{ Name }}> repository, IUnitOfWork unitOfWork, IEventBus eventBus)
            {
                _repository = repository;
                _unitOfWork = unitOfWork;
                _eventBus = eventBus;
            }
        {{~ else ~}}
            public Delete{{ Name }}Handler(IRepository<{{ Name }}> repository, IUnitOfWork unitOfWork)
            {
                _repository = repository;
                _unitOfWork = unitOfWork;
            }
        {{~ end ~}}

            public async Task Handle(Delete{{ Name }}Command request, CancellationToken cancellationToken)
            {
                ArgumentNullException.ThrowIfNull(request);
                var entity = await _repository.GetByIdAsync(request.Id, cancellationToken)
                    ?? throw new NotFoundException("{{ Name }}", request.Id);
                await _repository.DeleteAsync(entity, cancellationToken);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
        {{~ if PublishDeleted ~}}
                await _eventBus.PublishAsync(new {{ Name }}DeletedEvent { Data = {{ Name }}Dto.From(entity) }, cancellationToken);
        {{~ end ~}}
            }
        }
        {{~ for counter in Counters ~}}

        /// <summary>{{ Name }}'ın {{ counter }} sayacını bir artırır.</summary>
        public sealed class Increment{{ Name }}{{ counter }}Command : ICommand
        {
            /// <summary>Kaydın kimliği.</summary>
            public Guid Id { get; set; }
        }

        internal sealed class Increment{{ Name }}{{ counter }}Handler : ICommandHandler<Increment{{ Name }}{{ counter }}Command>
        {
            private readonly IRepository<{{ Name }}> _repository;
            private readonly IUnitOfWork _unitOfWork;
            public Increment{{ Name }}{{ counter }}Handler(IRepository<{{ Name }}> repository, IUnitOfWork unitOfWork)
            {
                _repository = repository;
                _unitOfWork = unitOfWork;
            }

            public async Task Handle(Increment{{ Name }}{{ counter }}Command request, CancellationToken cancellationToken)
            {
                ArgumentNullException.ThrowIfNull(request);
                var entity = await _repository.GetByIdAsync(request.Id, cancellationToken)
                    ?? throw new NotFoundException("{{ Name }}", request.Id);
                entity.{{ counter }}++;
                await _repository.UpdateAsync(entity, cancellationToken);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }
        }
        {{~ end ~}}

        """;

    public const string Queries =
        """
        using BaseForge.Core.CQRS;
        using BaseForge.Core.Interfaces;
        using {{ Namespace }}.Entities;
        {{~ if SearchPredicate ~}}
        using Microsoft.EntityFrameworkCore;
        {{~ end ~}}

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

        {{~ if Paginated ~}}
        /// <summary>{{ Name }} kayıtlarını sayfalı{{ if Sortable }}, sıralı{{ end }}{{ if SearchPredicate }} ve aranabilir{{ end }} biçimde listeler.</summary>
        public sealed class List{{ Name }}Query : PagedRequest, IQuery<PagedResult<{{ Name }}Dto>>;

        internal sealed class List{{ Name }}Handler : IQueryHandler<List{{ Name }}Query, PagedResult<{{ Name }}Dto>>
        {
            private readonly IRepository<{{ Name }}> _repository;

            public List{{ Name }}Handler(IRepository<{{ Name }}> repository) => _repository = repository;

            public async Task<PagedResult<{{ Name }}Dto>> Handle(List{{ Name }}Query request, CancellationToken cancellationToken)
            {
                ArgumentNullException.ThrowIfNull(request);
                var (items, totalCount) = await _repository.ListPagedAsync(
                    request.Skip,
                    request.PageSize,
        {{~ if Sortable ~}}
                    request.SortBy,
        {{~ else ~}}
                    null,
        {{~ end ~}}
        {{~ if SearchPredicate ~}}
                    query => string.IsNullOrWhiteSpace(request.Search) ? query : query.Where(x => {{ SearchPredicate }}),
        {{~ else ~}}
                    null,
        {{~ end ~}}
                    cancellationToken);

                return new PagedResult<{{ Name }}Dto>
                {
                    Items = items.Select({{ Name }}Dto.From).ToList(),
                    TotalCount = totalCount,
                    Page = request.Page,
                    PageSize = request.PageSize,
                };
            }
        }
        {{~ else ~}}
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
        {{~ end ~}}

        """;

    public const string Events =
        """
        using BaseForge.Core.Messaging;

        namespace {{ Namespace }}.Features.{{ Name }}s;

        {{~ if PublishCreated ~}}
        /// <summary>Bir {{ Name }} oluşturulduğunda RabbitMQ'ya yayınlanan olay.</summary>
        public sealed class {{ Name }}CreatedEvent : IIntegrationEvent
        {
            /// <inheritdoc />
            public Guid EventId { get; init; } = Guid.NewGuid();

            /// <inheritdoc />
            public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;

            /// <inheritdoc />
            public string EventType => "{{ Service }}/{{ Name }}Created";

            /// <summary>Oluşturulan {{ Name }} kaydı.</summary>
            public required {{ Name }}Dto Data { get; init; }
        }

        {{~ end ~}}
        {{~ if PublishUpdated ~}}
        /// <summary>Bir {{ Name }} güncellendiğinde RabbitMQ'ya yayınlanan olay.</summary>
        public sealed class {{ Name }}UpdatedEvent : IIntegrationEvent
        {
            /// <inheritdoc />
            public Guid EventId { get; init; } = Guid.NewGuid();

            /// <inheritdoc />
            public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;

            /// <inheritdoc />
            public string EventType => "{{ Service }}/{{ Name }}Updated";

            /// <summary>Güncellenen {{ Name }} kaydı (güncel hâli).</summary>
            public required {{ Name }}Dto Data { get; init; }
        }

        {{~ end ~}}
        {{~ if PublishDeleted ~}}
        /// <summary>Bir {{ Name }} silindiğinde RabbitMQ'ya yayınlanan olay.</summary>
        public sealed class {{ Name }}DeletedEvent : IIntegrationEvent
        {
            /// <inheritdoc />
            public Guid EventId { get; init; } = Guid.NewGuid();

            /// <inheritdoc />
            public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;

            /// <inheritdoc />
            public string EventType => "{{ Service }}/{{ Name }}Deleted";

            /// <summary>Silinen {{ Name }} kaydı (silinmeden önceki hâli).</summary>
            public required {{ Name }}Dto Data { get; init; }
        }

        {{~ end ~}}
        """;

    public const string SubscriptionHandler =
        """
        using BaseForge.Core.Messaging;
        using MediatR;

        namespace {{ Namespace }}.Integration;

        /// <summary>
        /// <c>{{ EventType }}</c> olayının bu serviste tüketilen gölge şekli (BaseForge.CodeGen tarafından
        /// yayıncı servisin gerçek alanlarından üretildi — yayıncı taraftaki tipin kendisi değildir).
        /// </summary>
        public sealed class {{ SourceEntity }}{{ Kind }}Event : IIntegrationEvent
        {
            /// <inheritdoc />
            public Guid EventId { get; init; } = Guid.NewGuid();

            /// <inheritdoc />
            public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;

            /// <inheritdoc />
            public string EventType => "{{ EventType }}";

            /// <summary>{{ SourceEntity }} kaydının olay payload'ı.</summary>
            public required {{ SourceEntity }}{{ Kind }}EventData Data { get; init; }
        }

        /// <summary>{{ SourceEntity }}{{ Kind }} olayının taşıdığı alanlar.</summary>
        public sealed class {{ SourceEntity }}{{ Kind }}EventData
        {
            /// <summary>Kayıt kimliği.</summary>
            public Guid Id { get; set; }
        {{~ for f in Fields ~}}
            /// <summary>{{ f.Name }}.</summary>
            public {{ f.Type }} {{ f.Name }} { get; set; }{{ f.Init }}
        {{~ end ~}}
        }

        /// <summary>
        /// <c>{{ EventType }}</c> olduğunda çalışır (BaseForge.CodeGen tarafından iskeleti üretildi — gövdeyi doldurun).
        /// </summary>
        internal sealed class {{ Handler }} : INotificationHandler<{{ SourceEntity }}{{ Kind }}Event>
        {
            /// <inheritdoc />
            public Task Handle({{ SourceEntity }}{{ Kind }}Event notification, CancellationToken cancellationToken)
            {
                ArgumentNullException.ThrowIfNull(notification);
                // TODO: {{ Handler }} — {{ SourceEntity }} {{ Kind }} olduğunda iş mantığını burada uygulayın.
                Console.WriteLine($"[{{ Handler }}] {{ EventType }} — Id={notification.Data.Id}");
                return Task.CompletedTask;
            }
        }

        """;

    public const string Controller =
        """
        using BaseForge.API.Controllers;
        {{~ if Paginated ~}}
        using BaseForge.Core.CQRS;
        {{~ end ~}}
        {{~ if Protect ~}}
        using Microsoft.AspNetCore.Authorization;
        {{~ end ~}}
        using Microsoft.AspNetCore.Mvc;
        using {{ Namespace }}.Features.{{ Name }}s;

        namespace {{ Namespace }}.Controllers;

        /// <summary>{{ Name }} CRUD uçları.</summary>
        {{~ if Protect ~}}
        [Authorize]
        {{~ end ~}}
        [Route("api/[controller]")]
        public sealed class {{ Name }}sController : BaseController
        {
            /// <summary>Kimliğe göre tek bir {{ Name }} getirir.</summary>
            {{~ if AnonymousGetById ~}}
            [AllowAnonymous]
            {{~ end ~}}
            [HttpGet("{id:guid}")]
            public async Task<ActionResult<{{ Name }}Dto>> GetById(Guid id, CancellationToken cancellationToken)
            {
                var result = await Mediator.Send(new Get{{ Name }}ByIdQuery { Id = id }, cancellationToken);
                return result is null ? NotFound() : Ok(result);
            }

        {{~ if Paginated ~}}
            /// <summary>{{ Name }} kayıtlarını sayfalı listeler (query string: page, pageSize, sortBy, search).</summary>
            {{~ if AnonymousList ~}}
            [AllowAnonymous]
            {{~ end ~}}
            [HttpGet]
            public async Task<ActionResult<PagedResult<{{ Name }}Dto>>> List([FromQuery] List{{ Name }}Query query, CancellationToken cancellationToken)
                => Ok(await Mediator.Send(query, cancellationToken));
        {{~ else ~}}
            /// <summary>Tüm {{ Name }} kayıtlarını listeler.</summary>
            {{~ if AnonymousList ~}}
            [AllowAnonymous]
            {{~ end ~}}
            [HttpGet]
            public async Task<ActionResult<IReadOnlyList<{{ Name }}Dto>>> List(CancellationToken cancellationToken)
                => Ok(await Mediator.Send(new List{{ Name }}Query(), cancellationToken));
        {{~ end ~}}

            /// <summary>Yeni bir {{ Name }} oluşturur.</summary>
            {{~ if AnonymousCreate ~}}
            [AllowAnonymous]
            {{~ end ~}}
            [HttpPost]
            public async Task<ActionResult<Guid>> Create(Create{{ Name }}Command command, CancellationToken cancellationToken)
            {
                var id = await Mediator.Send(command, cancellationToken);
                return CreatedAtAction(nameof(GetById), new { id }, id);
            }

            /// <summary>Var olan bir {{ Name }} kaydını günceller.</summary>
            {{~ if AnonymousUpdate ~}}
            [AllowAnonymous]
            {{~ end ~}}
            [HttpPut("{id:guid}")]
            public async Task<IActionResult> Update(Guid id, Update{{ Name }}Command command, CancellationToken cancellationToken)
            {
                ArgumentNullException.ThrowIfNull(command);
                command.Id = id;
                await Mediator.Send(command, cancellationToken);
                return NoContent();
            }

            /// <summary>Bir {{ Name }} kaydını siler.</summary>
            {{~ if AnonymousDelete ~}}
            [AllowAnonymous]
            {{~ end ~}}
            [HttpDelete("{id:guid}")]
            public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
            {
                await Mediator.Send(new Delete{{ Name }}Command { Id = id }, cancellationToken);
                return NoContent();
            }
        {{~ for counter in Counters ~}}

            /// <summary>{{ counter }} sayacını bir artırır (herkese açık).</summary>
            {{~ if Protect ~}}
            [AllowAnonymous]
            {{~ end ~}}
            [HttpPost("{id:guid}/increment-{{ counter | string.downcase }}")]
            public async Task<IActionResult> Increment{{ counter }}(Guid id, CancellationToken cancellationToken)
            {
                await Mediator.Send(new Increment{{ Name }}{{ counter }}Command { Id = id }, cancellationToken);
                return NoContent();
            }
        {{~ end ~}}
        }

        """;

    public const string MediaController =
        """
        using BaseForge.API.Controllers;
        {{~ if Protect ~}}
        using Microsoft.AspNetCore.Authorization;
        {{~ end ~}}
        using Microsoft.AspNetCore.Mvc;

        namespace {{ Namespace }}.Controllers;

        /// <summary>Genel görsel yükleme ucu — dosyayı wwwroot/uploads altına fiziksel olarak kaydeder (URL/base64 değil).</summary>
        {{~ if Protect ~}}
        [Authorize]
        {{~ end ~}}
        [Route("api/media")]
        public sealed class MediaController : BaseController
        {
            private static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
            {
                "image/jpeg", "image/png", "image/webp", "image/gif",
            };

            private const long MaxFileBytes = 5 * 1024 * 1024; // 5 MB

            private readonly IWebHostEnvironment _env;

            public MediaController(IWebHostEnvironment env) => _env = env;

            /// <summary>Bir görseli yükler ve genel erişilebilir URL'ini döndürür.</summary>
            [HttpPost]
            [RequestSizeLimit(MaxFileBytes + 4096)]
            public async Task<ActionResult<MediaUploadResponse>> Upload(IFormFile? file, [FromForm] string? category, CancellationToken cancellationToken)
            {
                if (file is null || file.Length == 0)
                {
                    return BadRequest(new { error = "Dosya gerekli." });
                }

                if (file.Length > MaxFileBytes)
                {
                    return BadRequest(new { error = "Dosya en fazla 5 MB olabilir." });
                }

                if (!AllowedContentTypes.Contains(file.ContentType))
                {
                    return BadRequest(new { error = "Sadece JPEG, PNG, WEBP veya GIF yükleyebilirsiniz." });
                }

                var extension = file.ContentType switch
                {
                    "image/jpeg" => ".jpg",
                    "image/png" => ".png",
                    "image/webp" => ".webp",
                    "image/gif" => ".gif",
                    _ => ".bin",
                };

                var safeCategory = string.IsNullOrWhiteSpace(category) || category.Any(c => !char.IsLetterOrDigit(c) && c != '-')
                    ? "misc"
                    : category;

                var uploadsDir = Path.Combine(_env.WebRootPath, "uploads", safeCategory);
                Directory.CreateDirectory(uploadsDir);

                var fileName = $"{Guid.NewGuid():N}{extension}";
                var filePath = Path.Combine(uploadsDir, fileName);

                await using (var stream = System.IO.File.Create(filePath))
                {
                    await file.CopyToAsync(stream, cancellationToken);
                }

                return Ok(new MediaUploadResponse($"/uploads/{safeCategory}/{fileName}"));
            }
        }

        /// <summary>Yüklenen görselin genel erişilebilir yolu.</summary>
        public sealed record MediaUploadResponse(string Url);

        """;

    public const string Program =
        """
        using BaseForge.API.Extensions;
        using Microsoft.AspNetCore.HttpOverrides;
        using Microsoft.EntityFrameworkCore;
        {{~ if HasAuth ~}}
        using Microsoft.OpenApi;
        {{~ end ~}}
        using Scalar.AspNetCore;
        using {{ Namespace }}.Data;

        // h2c (TLS'siz HTTP/2) desteği — container/yerel ağda düz HTTP üzerinden gRPC istemci çağrıları için.
        AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);

        var builder = WebApplication.CreateBuilder(args);

        // SPA'lardan (farklı origin) çağrılabilmesi için — izinli origin'ler appsettings/env'den
        // gelir, kod değişikliği/regen gerekmez (bkz. appsettings.json "Cors:AllowedOrigins").
        var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
        builder.Services.AddCors(cors =>
        {
            cors.AddPolicy("ConfiguredOrigins", policy =>
            {
                if (allowedOrigins.Length > 0)
                {
                    policy.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod();
                }
            });
        });

        builder.Services.AddControllers();
        builder.Services.AddGrpc();
        builder.Services.AddOpenApi(openApi =>
        {
            // Scalar'daki "Introduction" bölümü bu bilgilerden gelir (Markdown desteklenir).
            openApi.AddDocumentTransformer((document, _, _) =>
            {
                document.Info.Title = "{{ Title }}";
                document.Info.Version = "v1";
                document.Info.Description = {{ DescriptionLiteral }};
                return Task.CompletedTask;
            });
        {{~ if HasAuth ~}}

            // JWT korumalı servis: OpenAPI dokümanına Bearer security scheme'i ekle
            // (Scalar'daki "Authentication" panelinin kaynağı budur).
            openApi.AddDocumentTransformer((document, _, _) =>
            {
                document.Components ??= new OpenApiComponents();
                document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
                document.Components.SecuritySchemes["Bearer"] = new OpenApiSecurityScheme
                {
                    Type = SecuritySchemeType.Http,
                    Scheme = "bearer",
                    BearerFormat = "JWT",
                    Description = "Identity'nin `/connect/token` ucundan aldığınız access_token'ı buraya yapıştırın.",
                };
                document.Security ??= [];
                document.Security.Add(new OpenApiSecurityRequirement
                {
                    [new OpenApiSecuritySchemeReference("Bearer", document)] = [],
                });
                return Task.CompletedTask;
            });
        {{~ end ~}}
        });
        builder.Services.AddBaseForge(options =>
        {
            options.UsePostgreSQL<{{ ContextName }}>(
                builder.Configuration.GetConnectionString("Default")
                    ?? throw new InvalidOperationException("ConnectionStrings:Default tanımlı değil."));
            options.EnableCQRS(typeof(Program).Assembly);
            options.EnableAuditLog();
        {{~ if HasAuth ~}}
            options.EnableJwt(jwt =>
            {
                jwt.Authority = "{{ Authority }}";          // merkez Identity (discovery/JWKS)
                jwt.Audience = "{{ Audience }}";
                jwt.RequireHttpsMetadata = {{ if RequireHttpsMetadata }}true{{ else }}false{{ end }};
            });
        {{~ end ~}}
        {{~ if HasRabbitMq ~}}
            options.EnableRabbitMq(mq =>
            {
                mq.Host = builder.Configuration["RabbitMq:Host"] ?? "localhost";
                mq.Port = int.Parse(builder.Configuration["RabbitMq:Port"] ?? "5672");
                mq.Username = builder.Configuration["RabbitMq:Username"] ?? "guest";
                mq.Password = builder.Configuration["RabbitMq:Password"] ?? "guest";
        {{~ for s in Subscriptions ~}}
                mq.Subscribe<{{ Namespace }}.Integration.{{ s.SourceEntity }}{{ s.Kind }}Event>("{{ s.EventType }}", "{{ Namespace | string.downcase }}.{{ s.Handler }}");
        {{~ end ~}}
            });
        {{~ end ~}}
        });

        {{~ for c in GrpcClients ~}}
        // {{ c.Target }} servisine gRPC istemcisi (BaseForge.CodeGen tarafından üretildi).
        builder.Services.AddGrpcClient<{{ c.ProviderNamespace }}.Grpc.{{ c.Entity }}Service.{{ c.Entity }}ServiceClient>(o =>
            o.Address = new Uri(builder.Configuration["Grpc:{{ c.ConfigKey }}"]
                ?? throw new InvalidOperationException("Grpc:{{ c.ConfigKey }} tanımlı değil.")));
        builder.Services.AddScoped<{{ Namespace }}.Integration.I{{ c.Entity }}Client, {{ Namespace }}.Integration.{{ c.Entity }}Client>();
        {{~ end ~}}

        var app = builder.Build();

        // Reverse proxy (nginx vb.) arkasında çalışırken gerçek şema/host'u (https, gerçek domain)
        // Kestrel'e bildirir — aksi halde OpenAPI/discovery gibi üretilen mutlak URL'ler yanlış (http,
        // proxy'nin iç adresi) görünür. Docker port publish NAT'i yüzünden istek, proxy'nin gerçek IP'si
        // yerine değişken bir docker gateway IP'sinden gelir; bu yüzden KnownNetworks/KnownProxies temizlenir
        // (herkesten gelen X-Forwarded-* güvenilir sayılır). Güvenlik, container portunun yalnızca
        // 127.0.0.1'e (host loopback) publish edilmesinden gelir — dışarıdan bu porta doğrudan erişilemez,
        // yalnızca aynı host'taki nginx erişebilir.
        var forwardedHeadersOptions = new ForwardedHeadersOptions
        {
            ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto,
        };
        forwardedHeadersOptions.KnownNetworks.Clear();
        forwardedHeadersOptions.KnownProxies.Clear();
        app.UseForwardedHeaders(forwardedHeadersOptions);

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
                // Dark mode toggle varsayılan açık. Gizlemek için: options.HideDarkModeToggle();

                // "Ask AI" (Agent Scalar): localhost'ta key'siz ücretsiz/limitli çalışır.
                // Production'da Scalar Agent key'i ver: options.WithAgentKey("SCALAR_AGENT_KEY");
                // Veya veri gizliliği için tamamen kapat:        options.DisableAgent();
            {{~ if HasAuth ~}}

                // Token'ı Identity'nin /connect/token ucundan alıp buraya bir kere yapıştırın;
                // "Persist" sayesinde sayfa yenilense de kaybolmaz (localStorage).
                options.AddHttpAuthentication("Bearer", auth => auth.WithToken(string.Empty));
                options.AddPreferredSecuritySchemes("Bearer");
                options.EnablePersistentAuthentication();
            {{~ end ~}}
            });
        }

        app.UseCors("ConfiguredOrigins");
        app.UseStaticFiles(); // wwwroot/uploads — MediaController'ın fiziksel olarak kaydettiği dosyalar için
        app.UseBaseForge();
        app.MapControllers();
        {{~ for e in GrpcServerEntities ~}}
        app.MapGrpcService<{{ Namespace }}.Grpc.{{ e }}GrpcService>();
        {{~ end ~}}
        app.Run();

        """;

    public const string AppSettings =
        """
        {
          "ConnectionStrings": {
            "Default": "Host=localhost;Port={{ PostgresPort }};Database={{ Database }};Username=baseforge;Password=change_me"
          },
          "Cors": {
            "AllowedOrigins": [{{ for o in CorsOrigins }}"{{ o }}"{{ if !for.last }}, {{ end }}{{ end }}]
          },
          "Kestrel": {
            "Endpoints": {
              "Http": {
                "Url": "http://+:8080",
                "Protocols": "Http1"
              },
              "Grpc": {
                "Url": "http://+:8081",
                "Protocols": "Http2"
              }
            }
          },
        {{~ if GrpcClients.size > 0 ~}}
          "Grpc": {
        {{~ for c in GrpcClients ~}}
            "{{ c.ConfigKey }}": "http://{{ c.ProviderHost }}:8081"{{ if !for.last }},{{ end }}
        {{~ end ~}}
          },
        {{~ end ~}}
        {{~ if HasRabbitMq ~}}
          "RabbitMq": {
            "Host": "host.docker.internal",
            "Port": 5672,
            "Username": "baseforge",
            "Password": "change_me"
          },
        {{~ end ~}}
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

    public const string DockerCompose =
        """
        # {{ Service }} — izole test ortamı: servis + kendi PostgreSQL'i (mevcut DB'ye dokunmaz).
        # Ayağa kaldır:  docker compose up --build -d
        # API arayüzü:   http://localhost:{{ RestPort }}/scalar/v1
        # Durdur+temizle: docker compose down -v
        {{~ if HasRabbitMq ~}}
        # RabbitMq'ya bağlanır: kökteki docker-compose.yml'daki paylaşılan broker
        # (bir kere 'docker compose up -d rabbitmq' — bu servis kendi RabbitMq container'ını açmaz).
        {{~ end ~}}
        services:
          postgres:
            image: postgres:17-alpine
            environment:
              POSTGRES_USER: baseforge
              POSTGRES_PASSWORD: change_me
              POSTGRES_DB: {{ Database }}
            healthcheck:
              test: ["CMD-SHELL", "pg_isready -U baseforge -d {{ Database }}"]
              interval: 10s
              timeout: 5s
              retries: 5
            ports:
              - "{{ PostgresPort }}:5432"   # yerelde 'dotnet run' + sadece bu postgres'i container'da çalıştırmak için (appsettings.json ile eşleşir)
            volumes:
              - {{ Service }}-pgdata:/var/lib/postgresql/data

          {{ ServiceKey }}:
            build: .
            environment:
              ASPNETCORE_ENVIRONMENT: Development
              ConnectionStrings__Default: "Host=postgres;Port=5432;Database={{ Database }};Username=baseforge;Password=change_me"
            ports:
              - "{{ RestPort }}:8080"   # REST (HTTP/1.1) — appsettings.json Kestrel:Endpoints:Http
              - "{{ GrpcPort }}:8081"   # gRPC (h2c, TLS'siz HTTP/2)  — Kestrel:Endpoints:Grpc
            volumes:
              - {{ Service }}-uploads:/app/wwwroot/uploads   # MediaController'ın kaydettiği dosyalar — container recreate'te kaybolmasın
            depends_on:
              postgres:
                condition: service_healthy

        volumes:
          {{ Service }}-pgdata:
          {{ Service }}-uploads:

        """;

    public const string DockerIgnore =
        """
        bin/
        obj/
        **/bin/
        **/obj/
        .vs/
        .git/
        docs/
        *.user

        """;

    public const string ComposeSnippet =
        """
        # Bu bloğu kök docker-compose.yml'a ekleyin (postgres servisinin yanına).
        {{~ if HasRabbitMq ~}}
        # RabbitMq'ya bağlanır: kökteki docker-compose.yml'daki paylaşılan broker.
        {{~ end ~}}
        services:
          {{ ServiceKey }}-service:
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

    public const string ProtoServer =
        """
        syntax = "proto3";

        option csharp_namespace = "{{ Namespace }}.Grpc";

        package {{ Package }};

        // BaseForge.CodeGen tarafından üretildi — {{ Entity }} entity'sine diğer servislerin
        // salt-okunur gRPC erişimi. Servis adı ({{ Entity }}Service), C# implementasyon sınıfıyla
        // ({{ Entity }}GrpcService) çakışmasın diye ayrı tutulur.
        service {{ Entity }}Service {
          rpc GetById ({{ Entity }}ByIdRequest) returns ({{ Entity }}Message);
        }

        message {{ Entity }}ByIdRequest {
          string id = 1;
        }

        message {{ Entity }}Message {
          string id = 1;
        {{~ for f in Fields ~}}
          {{ f.ProtoType }} {{ f.ProtoName }} = {{ f.Number }};
        {{~ end ~}}
        }

        """;

    public const string GrpcServerService =
        """
        using System.Globalization;
        using Grpc.Core;
        using MediatR;
        using {{ Namespace }}.Features.{{ Entity }}s;

        namespace {{ Namespace }}.Grpc;

        /// <summary>
        /// {{ Entity }} entity'sine diğer servislerin salt-okunur gRPC erişimi
        /// (BaseForge.CodeGen tarafından üretildi; mevcut CQRS sorgusu üzerinden veri okur).
        /// </summary>
        public sealed class {{ Entity }}GrpcService(ISender sender) : {{ Entity }}Service.{{ Entity }}ServiceBase
        {
            public override async Task<{{ Entity }}Message> GetById({{ Entity }}ByIdRequest request, ServerCallContext context)
            {
                ArgumentNullException.ThrowIfNull(request);
                if (!Guid.TryParse(request.Id, out var id))
                {
                    throw new RpcException(new Status(StatusCode.InvalidArgument, "Geçersiz id."));
                }

                var value = await sender.Send(new Get{{ Entity }}ByIdQuery { Id = id }, context.CancellationToken)
                    ?? throw new RpcException(new Status(StatusCode.NotFound, $"{{ Entity }} bulunamadı: {id}"));

                return new {{ Entity }}Message
                {
                    Id = value.Id.ToString(),
        {{~ for f in Fields ~}}
                    {{ f.Name }} = {{ f.ToProtoExpr }},
        {{~ end ~}}
                };
            }
        }

        """;

    public const string GrpcClientRich =
        """
        using System.Globalization;
        using Grpc.Core;
        using {{ ProviderNamespace }}.Grpc;

        namespace {{ Namespace }}.Integration;

        /// <summary>{{ Target }} servisine senkron (gRPC) erişim sözleşmesi. BaseForge.CodeGen tarafından üretildi.</summary>
        public interface I{{ Entity }}Client
        {
            /// <summary>Uzak servisten {{ Entity }} kaydını getirir; bulunamazsa <see langword="null"/>.</summary>
            Task<{{ Entity }}Reference?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
        }

        /// <summary>Uzak {{ Entity }} kaydının yerel (zengin) görünümü.</summary>
        public sealed class {{ Entity }}Reference
        {
            /// <summary>Uzak kaydın kimliği.</summary>
            public Guid Id { get; set; }
        {{~ for f in Fields ~}}
            /// <summary>{{ f.Name }}.</summary>
            public {{ f.CSharpType }} {{ f.Name }} { get; set; }{{ f.Init }}
        {{~ end ~}}
        }

        /// <summary><see cref="I{{ Entity }}Client"/>'in {{ ProviderNamespace }} servisine gRPC ile bağlanan gerçek implementasyonu.</summary>
        public sealed class {{ Entity }}Client({{ Entity }}Service.{{ Entity }}ServiceClient client) : I{{ Entity }}Client
        {
            public async Task<{{ Entity }}Reference?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
            {
                try
                {
                    var response = await client.GetByIdAsync(
                        new {{ Entity }}ByIdRequest { Id = id.ToString() },
                        cancellationToken: cancellationToken);

                    return new {{ Entity }}Reference
                    {
                        Id = Guid.Parse(response.Id),
        {{~ for f in Fields ~}}
                        {{ f.Name }} = {{ f.FromProtoExpr }},
        {{~ end ~}}
                    };
                }
                catch (RpcException ex) when (ex.StatusCode == StatusCode.NotFound)
                {
                    return null;
                }
            }
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
            <!-- Servisler arası senkron iletişim (gRPC) — sunucu + istemci taraflarını birlikte getirir -->
            <PackageReference Include="Grpc.AspNetCore" Version="2.71.0" />
            <PackageReference Include="Grpc.Net.ClientFactory" Version="2.71.0" />
          </ItemGroup>
        {{~ if ServerProtoFiles.size > 0 || ClientProtoFiles.size > 0 ~}}

          <ItemGroup>
        {{~ for p in ServerProtoFiles ~}}
            <Protobuf Include="Protos/{{ p }}.proto" GrpcServices="Server" />
        {{~ end ~}}
        {{~ for p in ClientProtoFiles ~}}
            <Protobuf Include="Protos/{{ p }}.proto" GrpcServices="Client" />
        {{~ end ~}}
          </ItemGroup>
        {{~ end ~}}

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
              "applicationUrl": "http://localhost:{{ RestPort }}",
              "environmentVariables": {
                "ASPNETCORE_ENVIRONMENT": "Development"
              }
            }
          }
        }

        """;
}
