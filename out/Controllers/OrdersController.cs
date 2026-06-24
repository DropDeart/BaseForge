using BaseForge.API.Controllers;
using Microsoft.AspNetCore.Mvc;
using Orders.Features.Orders;

namespace Orders.Controllers;

/// <summary>Order CRUD uçları.</summary>
[Route("api/[controller]")]
public sealed class OrdersController : BaseController
{
    /// <summary>Kimliğe göre tek bir Order getirir.</summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<OrderDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(new GetOrderByIdQuery { Id = id }, cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }

    /// <summary>Tüm Order kayıtlarını listeler.</summary>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<OrderDto>>> List(CancellationToken cancellationToken)
        => Ok(await Mediator.Send(new ListOrderQuery(), cancellationToken));

    /// <summary>Yeni bir Order oluşturur.</summary>
    [HttpPost]
    public async Task<ActionResult<Guid>> Create(CreateOrderCommand command, CancellationToken cancellationToken)
    {
        var id = await Mediator.Send(command, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id }, id);
    }

    /// <summary>Var olan bir Order kaydını günceller.</summary>
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, UpdateOrderCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        command.Id = id;
        await Mediator.Send(command, cancellationToken);
        return NoContent();
    }

    /// <summary>Bir Order kaydını siler.</summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await Mediator.Send(new DeleteOrderCommand { Id = id }, cancellationToken);
        return NoContent();
    }
}
