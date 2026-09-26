namespace BaseForge.CodeGen.Generation;

/// <summary>Scriban kod üretim şablonları (gömülü).</summary>
internal static class Templates
{
    public const string Entity =
        """
        using System.ComponentModel.DataAnnotations;
        using System.ComponentModel.DataAnnotations.Schema;
        using BaseForge.Core.Entities;

        namespace {{ Namespace }}.Entities;

        /// <summary>{{ Name }} entity'si (BaseForge.CodeGen tarafından üretildi).</summary>
        public sealed class {{ Name }} : BaseEntity{{ if IsMultiTenant }}, ITenantEntity{{ end }}
        {
        {{~ for p in Scalars ~}}
            /// <summary>{{ p.Name }}.</summary>
        {{~ if p.MaxLength ~}}
            [MaxLength({{ p.MaxLength }})]
        {{~ end ~}}
        {{~ if p.IsJson ~}}
            [Column(TypeName = "jsonb")]
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
        using BaseForge.Core.Interfaces;
        using BaseForge.Infrastructure.Data;
        using Microsoft.EntityFrameworkCore;
        using {{ Namespace }}.Entities;

        namespace {{ Namespace }}.Data;

        /// <summary>{{ ServiceName }} servisinin EF Core context'i.</summary>
        public sealed class {{ ContextName }} : BaseForgeDbContext
        {
            /// <summary>Yeni bir {{ ContextName }} oluşturur.</summary>
            public {{ ContextName }}(DbContextOptions<{{ ContextName }}> options, ICurrentUser? currentUser = null, ICurrentTenant? currentTenant = null)
                : base(options, currentUser, currentTenant)
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
        {{~ if PublishCreated ~}}
                await _eventBus.PublishAsync(new {{ Name }}CreatedEvent { Data = {{ Name }}Dto.From(entity) }, cancellationToken);
        {{~ end ~}}
                await _unitOfWork.SaveChangesAsync(cancellationToken);
                return entity.Id;
            }
        }

        {{~ if IncludeUpdate ~}}
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
        {{~ if OwnerField ~}}

            /// <summary>
            /// Doluysa yalnızca {{ OwnerField }}'ı bu kullanıcı olan kayıt güncellenebilir (aksi halde 403). İstemciden
            /// bağlanmaz; controller rol/sahiplik kuralına göre doldurur (bkz. docs/ARCH.md §6.1).
            /// </summary>
            [System.Text.Json.Serialization.JsonIgnore]
            [Microsoft.AspNetCore.Mvc.ModelBinding.BindNever]
            public Guid? RestrictToOwnerId { get; set; }
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
        {{~ if OwnerField ~}}
                if (request.RestrictToOwnerId is { } ownerId && entity.{{ OwnerField }} != ownerId)
                {
                    throw new ForbiddenException("{{ Name }}", request.Id);
                }

        {{~ end ~}}
        {{~ for f in Fields ~}}
        {{~ if f.Name != OwnerField ~}}
                entity.{{ f.Name }} = request.{{ f.Name }};
        {{~ end ~}}
        {{~ end ~}}
                await _repository.UpdateAsync(entity, cancellationToken);
        {{~ if PublishUpdated ~}}
                await _eventBus.PublishAsync(new {{ Name }}UpdatedEvent { Data = {{ Name }}Dto.From(entity) }, cancellationToken);
        {{~ end ~}}
                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }
        }
        {{~ end ~}}

        {{~ if IncludeDelete ~}}
        /// <summary>Bir {{ Name }} kaydını siler (soft delete).</summary>
        public sealed class Delete{{ Name }}Command : ICommand
        {
            /// <summary>Silinecek kaydın kimliği.</summary>
            public Guid Id { get; set; }
        {{~ if OwnerField ~}}

            /// <summary>Doluysa yalnızca {{ OwnerField }}'ı bu kullanıcı olan kayıt silinebilir (aksi halde 403). Controller doldurur.</summary>
            [System.Text.Json.Serialization.JsonIgnore]
            [Microsoft.AspNetCore.Mvc.ModelBinding.BindNever]
            public Guid? RestrictToOwnerId { get; set; }
        {{~ end ~}}
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
        {{~ if OwnerField ~}}
                if (request.RestrictToOwnerId is { } ownerId && entity.{{ OwnerField }} != ownerId)
                {
                    throw new ForbiddenException("{{ Name }}", request.Id);
                }
        {{~ end ~}}
                await _repository.DeleteAsync(entity, cancellationToken);
        {{~ if PublishDeleted ~}}
                await _eventBus.PublishAsync(new {{ Name }}DeletedEvent { Data = {{ Name }}Dto.From(entity) }, cancellationToken);
        {{~ end ~}}
                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }
        }
        {{~ end ~}}
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
        {{~ if OwnerField ~}}

            /// <summary>
            /// Doluysa yalnızca {{ OwnerField }}'ı bu kullanıcı olan kayıt döner (başkasınınki bulunamadı sayılır).
            /// Controller doldurur; gRPC gibi servisler arası çağrılarda boş kalır (bkz. docs/ARCH.md §6.1).
            /// </summary>
            public Guid? RestrictToOwnerId { get; set; }
        {{~ end ~}}
        }

        internal sealed class Get{{ Name }}ByIdHandler : IQueryHandler<Get{{ Name }}ByIdQuery, {{ Name }}Dto?>
        {
            private readonly IRepository<{{ Name }}> _repository;

            public Get{{ Name }}ByIdHandler(IRepository<{{ Name }}> repository) => _repository = repository;

            public async Task<{{ Name }}Dto?> Handle(Get{{ Name }}ByIdQuery request, CancellationToken cancellationToken)
            {
                ArgumentNullException.ThrowIfNull(request);
                var entity = await _repository.GetByIdAsync(request.Id, cancellationToken);
        {{~ if OwnerField ~}}
                if (entity is null || (request.RestrictToOwnerId is { } ownerId && entity.{{ OwnerField }} != ownerId))
                {
                    return null;
                }

                return {{ Name }}Dto.From(entity);
        {{~ else ~}}
                return entity is null ? null : {{ Name }}Dto.From(entity);
        {{~ end ~}}
            }
        }

        {{~ if Paginated ~}}
        /// <summary>{{ Name }} kayıtlarını sayfalı{{ if Sortable }}, sıralı{{ end }}{{ if SearchPredicate }} ve aranabilir{{ end }} biçimde listeler.</summary>
        {{~ if OwnerField ~}}
        public sealed class List{{ Name }}Query : PagedRequest, IQuery<PagedResult<{{ Name }}Dto>>
        {
            /// <summary>Doluysa yalnızca {{ OwnerField }}'ı bu kullanıcı olan kayıtlar döner. İstemciden bağlanmaz; controller doldurur.</summary>
            [Microsoft.AspNetCore.Mvc.ModelBinding.BindNever]
            public Guid? RestrictToOwnerId { get; set; }
        }
        {{~ else ~}}
        public sealed class List{{ Name }}Query : PagedRequest, IQuery<PagedResult<{{ Name }}Dto>>;
        {{~ end ~}}

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
        {{~ if OwnerField ~}}
                    query =>
                    {
                        if (request.RestrictToOwnerId is { } ownerId)
                        {
                            query = query.Where(x => x.{{ OwnerField }} == ownerId);
                        }
        {{~ if SearchPredicate ~}}

                        return string.IsNullOrWhiteSpace(request.Search) ? query : query.Where(x => {{ SearchPredicate }});
        {{~ else ~}}

                        return query;
        {{~ end ~}}
                    },
        {{~ else if SearchPredicate ~}}
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
        {{~ if OwnerField ~}}
        public sealed class List{{ Name }}Query : IQuery<IReadOnlyList<{{ Name }}Dto>>
        {
            /// <summary>Doluysa yalnızca {{ OwnerField }}'ı bu kullanıcı olan kayıtlar döner. Controller doldurur.</summary>
            public Guid? RestrictToOwnerId { get; set; }
        }
        {{~ else ~}}
        public sealed class List{{ Name }}Query : IQuery<IReadOnlyList<{{ Name }}Dto>>;
        {{~ end ~}}

        internal sealed class List{{ Name }}Handler : IQueryHandler<List{{ Name }}Query, IReadOnlyList<{{ Name }}Dto>>
        {
            private readonly IRepository<{{ Name }}> _repository;

            public List{{ Name }}Handler(IRepository<{{ Name }}> repository) => _repository = repository;

            public async Task<IReadOnlyList<{{ Name }}Dto>> Handle(List{{ Name }}Query request, CancellationToken cancellationToken)
            {
        {{~ if OwnerField ~}}
                ArgumentNullException.ThrowIfNull(request);
        {{~ end ~}}
                var items = await _repository.ListAllAsync(cancellationToken);
        {{~ if OwnerField ~}}
                // Sayfalamasız liste küçük veri içindir; sahiplik filtresi bellekte uygulanır.
                return items
                    .Where(x => request.RestrictToOwnerId is not { } ownerId || x.{{ OwnerField }} == ownerId)
                    .Select({{ Name }}Dto.From)
                    .ToList();
        {{~ else ~}}
                return items.Select({{ Name }}Dto.From).ToList();
        {{~ end ~}}
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
        {{~ if OwnerField ~}}
        using BaseForge.Core.Exceptions;
        {{~ end ~}}
        {{~ if UsesAuthorization ~}}
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
            /// <summary>Kimliğe göre tek bir {{ Name }} getirir.{{ if GetByIdAccess.OwnerCheck }} Sahibi olmayanlara başkasının kaydı 404 döner.{{ end }}</summary>
            {{~ if GetByIdAccess.Attribute ~}}
            {{ GetByIdAccess.Attribute }}
            {{~ end ~}}
            [HttpGet("{id:guid}")]
            public async Task<ActionResult<{{ Name }}Dto>> GetById(Guid id, CancellationToken cancellationToken)
            {
            {{~ if GetByIdAccess.OwnerCheck ~}}
                var query = new Get{{ Name }}ByIdQuery { Id = id, RestrictToOwnerId = OwnerRestriction({{ GetByIdAccess.BypassRoles }}) };
                var result = await Mediator.Send(query, cancellationToken);
            {{~ else ~}}
                var result = await Mediator.Send(new Get{{ Name }}ByIdQuery { Id = id }, cancellationToken);
            {{~ end ~}}
                return result is null ? NotFound() : Ok(result);
            }

        {{~ if Paginated ~}}
            /// <summary>{{ Name }} kayıtlarını sayfalı listeler (query string: page, pageSize, sortBy, search).{{ if ListAccess.OwnerCheck }} Sahibi olmayanlar yalnızca kendi kayıtlarını görür.{{ end }}</summary>
            {{~ if ListAccess.Attribute ~}}
            {{ ListAccess.Attribute }}
            {{~ end ~}}
            [HttpGet]
            {{~ if ListAccess.OwnerCheck ~}}
            public async Task<ActionResult<PagedResult<{{ Name }}Dto>>> List([FromQuery] List{{ Name }}Query query, CancellationToken cancellationToken)
            {
                ArgumentNullException.ThrowIfNull(query);
                query.RestrictToOwnerId = OwnerRestriction({{ ListAccess.BypassRoles }});
                return Ok(await Mediator.Send(query, cancellationToken));
            }
            {{~ else ~}}
            public async Task<ActionResult<PagedResult<{{ Name }}Dto>>> List([FromQuery] List{{ Name }}Query query, CancellationToken cancellationToken)
                => Ok(await Mediator.Send(query, cancellationToken));
            {{~ end ~}}
        {{~ else ~}}
            /// <summary>Tüm {{ Name }} kayıtlarını listeler.{{ if ListAccess.OwnerCheck }} Sahibi olmayanlar yalnızca kendi kayıtlarını görür.{{ end }}</summary>
            {{~ if ListAccess.Attribute ~}}
            {{ ListAccess.Attribute }}
            {{~ end ~}}
            [HttpGet]
            public async Task<ActionResult<IReadOnlyList<{{ Name }}Dto>>> List(CancellationToken cancellationToken)
            {{~ if ListAccess.OwnerCheck ~}}
                => Ok(await Mediator.Send(new List{{ Name }}Query { RestrictToOwnerId = OwnerRestriction({{ ListAccess.BypassRoles }}) }, cancellationToken));
            {{~ else ~}}
                => Ok(await Mediator.Send(new List{{ Name }}Query(), cancellationToken));
            {{~ end ~}}
        {{~ end ~}}

            /// <summary>Yeni bir {{ Name }} oluşturur.{{ if OwnerField }} {{ OwnerField }} istekten okunmaz, çağıranın kimliğiyle doldurulur.{{ end }}</summary>
            {{~ if CreateAccess.Attribute ~}}
            {{ CreateAccess.Attribute }}
            {{~ end ~}}
            [HttpPost]
            public async Task<ActionResult<Guid>> Create(Create{{ Name }}Command command, CancellationToken cancellationToken)
            {
            {{~ if OwnerField ~}}
                ArgumentNullException.ThrowIfNull(command);
                command.{{ OwnerField }} = CurrentUserId ?? throw new ForbiddenException("Kullanıcı kimliği (sub) token'da bulunamadı.");
            {{~ end ~}}
                var id = await Mediator.Send(command, cancellationToken);
                return CreatedAtAction(nameof(GetById), new { id }, id);
            }

        {{~ if IncludeUpdate ~}}
            /// <summary>Var olan bir {{ Name }} kaydını günceller.{{ if UpdateAccess.OwnerCheck }} Sahibi olmayanlara 403 döner.{{ end }}</summary>
            {{~ if UpdateAccess.Attribute ~}}
            {{ UpdateAccess.Attribute }}
            {{~ end ~}}
            [HttpPut("{id:guid}")]
            public async Task<IActionResult> Update(Guid id, Update{{ Name }}Command command, CancellationToken cancellationToken)
            {
                ArgumentNullException.ThrowIfNull(command);
                command.Id = id;
            {{~ if UpdateAccess.OwnerCheck ~}}
                command.RestrictToOwnerId = OwnerRestriction({{ UpdateAccess.BypassRoles }});
            {{~ end ~}}
                await Mediator.Send(command, cancellationToken);
                return NoContent();
            }
        {{~ end ~}}

        {{~ if IncludeDelete ~}}
            /// <summary>Bir {{ Name }} kaydını siler.{{ if DeleteAccess.OwnerCheck }} Sahibi olmayanlara 403 döner.{{ end }}</summary>
            {{~ if DeleteAccess.Attribute ~}}
            {{ DeleteAccess.Attribute }}
            {{~ end ~}}
            [HttpDelete("{id:guid}")]
            public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
            {
            {{~ if DeleteAccess.OwnerCheck ~}}
                await Mediator.Send(new Delete{{ Name }}Command { Id = id, RestrictToOwnerId = OwnerRestriction({{ DeleteAccess.BypassRoles }}) }, cancellationToken);
            {{~ else ~}}
                await Mediator.Send(new Delete{{ Name }}Command { Id = id }, cancellationToken);
            {{~ end ~}}
                return NoContent();
            }
        {{~ end ~}}
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
        {{~ if CreateAccess.Attribute ~}}
        {{ CreateAccess.Attribute }}
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

                // WebRootPath yalnızca wwwroot klasörü başlangıçta varsa dolu gelir (üretici wwwroot/uploads'ı oluşturur);
                // yine de silinmişse içerik köküne düş — aksi halde Path.Combine null ile 500 fırlatır.
                var webRoot = _env.WebRootPath ?? Path.Combine(_env.ContentRootPath, "wwwroot");
                var uploadsDir = Path.Combine(webRoot, "uploads", safeCategory);
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

        // Merkezi loglama: Serilog + (yapılandırılmışsa) Grafana Loki. Serilog:LokiUrl boşsa
        // sadece konsola loglar — Loki'nin ayakta olması bir ön koşul değildir.
        builder.AddBaseForgeLogging("{{ ServiceKey }}");

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
        builder.Services.AddGrpc(grpc => grpc.Interceptors.Add<BaseForge.API.Grpc.CorrelationIdServerInterceptor>());
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
        {{~ if HasMultiTenancy ~}}
            options.EnableMultiTenancy();
        {{~ end ~}}
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
        {{~ if OutboxMaxRetries ~}}
                mq.OutboxMaxRetries = {{ OutboxMaxRetries }};
        {{~ end ~}}
        {{~ if OutboxRetentionDays ~}}
                mq.OutboxRetention = TimeSpan.FromDays({{ OutboxRetentionDays }});
        {{~ end ~}}
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
                ?? throw new InvalidOperationException("Grpc:{{ c.ConfigKey }} tanımlı değil.")))
            .AddInterceptor<BaseForge.API.Grpc.CorrelationIdClientInterceptor>();
        builder.Services.AddScoped<{{ Namespace }}.Integration.I{{ c.Entity }}Client, {{ Namespace }}.Integration.{{ c.Entity }}Client>();
        {{~ end ~}}

        {{~ if GatewayTargets.size > 0 ~}}
        // Gateway: kardeş servislerin TÜM /api/* yüzeyini /api/gateway/{servis}/... altında şeffafça
        // ileten YARP reverse-proxy — route/cluster tanımları appsettings.json "ReverseProxy" bölümünden
        // gelir (bkz. docs/ARCH.md §5.8). [Authorize] bu uçlara UYGULANMAZ (MapReverseProxy MVC pipeline
        // dışında çalışır) — gerçek yetkilendirme sınırı hâlâ Authorization header'ının şeffafça iletildiği
        // hedef serviste.
        builder.Services.AddReverseProxy().LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));
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
        {{~ if GatewayTargets.size > 0 ~}}
        app.MapReverseProxy();
        {{~ end ~}}
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
        {{~ if GatewayTargets.size > 0 ~}}
          "ReverseProxy": {
            "Routes": {
        {{~ for t in GatewayTargets ~}}
              "{{ t.ServiceName }}-route": {
                "ClusterId": "{{ t.ServiceName }}-cluster",
                "Match": { "Path": "/api/gateway/{{ t.ServiceName }}/{**catch-all}" },
                "Transforms": [ { "PathPattern": "/api/{catch-all}" } ]
              }{{ if !for.last }},{{ end }}
        {{~ end ~}}
            },
            "Clusters": {
        {{~ for t in GatewayTargets ~}}
              "{{ t.ServiceName }}-cluster": {
                "Destinations": {
                  "destination1": { "Address": "http://host.docker.internal:{{ t.RestPort }}/" }
                }
              }{{ if !for.last }},{{ end }}
        {{~ end ~}}
            }
          },
        {{~ end ~}}
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
            "{{ c.ConfigKey }}": "http://{{ c.ProviderHost }}:{{ c.ProviderGrpcPort }}"{{ if !for.last }},{{ end }}
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
          "Serilog": {
            "LokiUrl": "http://host.docker.internal:3100"
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
        RUN apt-get update && apt-get install -y --no-install-recommends curl && rm -rf /var/lib/apt/lists/*
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
        # Merkezi log toplama (Grafana Loki) da kökteki docker-compose.yml'da paylaşılan bir
        # container'dır ('docker compose up -d loki grafana') — appsettings.json'daki Serilog:LokiUrl
        # boşsa/erişilemezse servis sadece konsola loglamaya devam eder, bu bir ön koşul değildir.
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
            healthcheck:
              test: ["CMD-SHELL", "curl -f http://localhost:8080/health || exit 1"]
              interval: 10s
              timeout: 3s
              retries: 5
              start_period: 15s
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
        # Merkezi log toplama (Grafana Loki) da kökteki docker-compose.yml'da paylaşılan bir container'dır.
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
        {{~ if HasGateway ~}}
            <!-- Gateway: kardeş servislerin REST yüzeyini /api/gateway/{servis}/... altında ileten reverse-proxy -->
            <PackageReference Include="Yarp.ReverseProxy" Version="2.3.0" />
        {{~ end ~}}
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
