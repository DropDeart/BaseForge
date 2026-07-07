using BaseForge.API.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Warehouse.Features.StockItems;

namespace Warehouse.Controllers;

/// <summary>StockItem CRUD uçları.</summary>
[Authorize]
[Route("api/[controller]")]
public sealed class StockItemsController : BaseController
{
    /// <summary>Kimliğe göre tek bir StockItem getirir.</summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<StockItemDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(new GetStockItemByIdQuery { Id = id }, cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }

    /// <summary>Tüm StockItem kayıtlarını listeler.</summary>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<StockItemDto>>> List(CancellationToken cancellationToken)
        => Ok(await Mediator.Send(new ListStockItemQuery(), cancellationToken));

    /// <summary>Yeni bir StockItem oluşturur.</summary>
    [HttpPost]
    public async Task<ActionResult<Guid>> Create(CreateStockItemCommand command, CancellationToken cancellationToken)
    {
        var id = await Mediator.Send(command, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id }, id);
    }

    /// <summary>Var olan bir StockItem kaydını günceller.</summary>
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, UpdateStockItemCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        command.Id = id;
        await Mediator.Send(command, cancellationToken);
        return NoContent();
    }

    /// <summary>Bir StockItem kaydını siler.</summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await Mediator.Send(new DeleteStockItemCommand { Id = id }, cancellationToken);
        return NoContent();
    }
}
