using BaseForge.API.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Orders.Features.OrderItems;

namespace Orders.Controllers;

/// <summary>OrderItem CRUD uçları.</summary>
[Authorize]
[Route("api/[controller]")]
public sealed class OrderItemsController : BaseController
{
    /// <summary>Kimliğe göre tek bir OrderItem getirir.</summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<OrderItemDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(new GetOrderItemByIdQuery { Id = id }, cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }

    /// <summary>Tüm OrderItem kayıtlarını listeler.</summary>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<OrderItemDto>>> List(CancellationToken cancellationToken)
        => Ok(await Mediator.Send(new ListOrderItemQuery(), cancellationToken));

    /// <summary>Yeni bir OrderItem oluşturur.</summary>
    [HttpPost]
    public async Task<ActionResult<Guid>> Create(CreateOrderItemCommand command, CancellationToken cancellationToken)
    {
        var id = await Mediator.Send(command, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id }, id);
    }

    /// <summary>Var olan bir OrderItem kaydını günceller.</summary>
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, UpdateOrderItemCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        command.Id = id;
        await Mediator.Send(command, cancellationToken);
        return NoContent();
    }

    /// <summary>Bir OrderItem kaydını siler.</summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await Mediator.Send(new DeleteOrderItemCommand { Id = id }, cancellationToken);
        return NoContent();
    }
}
