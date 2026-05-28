# CQRS with MediatR ASP.NET Core Examples

These examples are templates, not a framework. Adapt names, namespaces, persistence style, and error conventions to the target codebase.

## Basic Registration

```csharp
using MediatR;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

builder.Services.AddMediatR(cfg =>
{
    cfg.RegisterServicesFromAssemblyContaining<CreateOrderCommand>();
    cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));
    cfg.AddOpenBehavior(typeof(RequestLoggingBehavior<,>));
    cfg.AddOpenBehavior(typeof(CommandTransactionBehavior<,>));
});

builder.Services.AddScoped<IOrderRepository, OrderRepository>();
builder.Services.AddScoped<IUnitOfWork, EfUnitOfWork>();
builder.Services.AddScoped<IValidator<CreateOrderCommand>, CreateOrderCommandValidator>();

var app = builder.Build();

app.MapControllers();

await app.RunAsync();
```

## Command and Handler

```csharp
using MediatR;

public sealed record CreateOrderCommand(
    Guid CustomerId,
    IReadOnlyList<CreateOrderLineItem> Items) : IRequest<CreateOrderResult>;

public sealed record CreateOrderLineItem(Guid ProductId, int Quantity);

public sealed record CreateOrderResult(Guid OrderId);

public sealed class CreateOrderHandler : IRequestHandler<CreateOrderCommand, CreateOrderResult>
{
    private readonly IOrderRepository _orders;
    private readonly IUnitOfWork _unitOfWork;

    public CreateOrderHandler(IOrderRepository orders, IUnitOfWork unitOfWork)
    {
        _orders = orders;
        _unitOfWork = unitOfWork;
    }

    public async Task<CreateOrderResult> Handle(CreateOrderCommand request, CancellationToken cancellationToken)
    {
        var order = Order.Create(request.CustomerId);

        foreach (var item in request.Items)
        {
            order.AddItem(item.ProductId, item.Quantity);
        }

        _orders.Add(order);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new CreateOrderResult(order.Id);
    }
}
```

## Query and Handler

```csharp
using MediatR;
using Microsoft.EntityFrameworkCore;

public sealed record GetOrderByIdQuery(Guid OrderId) : IRequest<OrderDetailsDto?>;

public sealed record OrderDetailsDto(
    Guid OrderId,
    string Number,
    string Status,
    decimal Total);

public sealed class GetOrderByIdHandler : IRequestHandler<GetOrderByIdQuery, OrderDetailsDto?>
{
    private readonly OrdersDbContext _dbContext;

    public GetOrderByIdHandler(OrdersDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<OrderDetailsDto?> Handle(GetOrderByIdQuery request, CancellationToken cancellationToken)
    {
        return _dbContext.Orders
            .AsNoTracking()
            .Where(x => x.Id == request.OrderId)
            .Select(x => new OrderDetailsDto(
                x.Id,
                x.Number,
                x.Status.Name,
                x.Total.Amount))
            .SingleOrDefaultAsync(cancellationToken);
    }
}
```

## Validation Behavior

```csharp
using FluentValidation;
using MediatR;

public sealed class ValidationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly IEnumerable<IValidator<TRequest>> _validators;

    public ValidationBehavior(IEnumerable<IValidator<TRequest>> validators)
    {
        _validators = validators;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        if (!_validators.Any())
        {
            return await next();
        }

        var context = new ValidationContext<TRequest>(request);
        var results = await Task.WhenAll(_validators.Select(v => v.ValidateAsync(context, cancellationToken)));
        var failures = results
            .SelectMany(x => x.Errors)
            .Where(x => x is not null)
            .ToList();

        if (failures.Count != 0)
        {
            throw new ValidationException(failures);
        }

        return await next();
    }
}
```

## Request Logging Behavior

```csharp
using MediatR;
using Microsoft.Extensions.Logging;

public sealed class RequestLoggingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly ILogger<RequestLoggingBehavior<TRequest, TResponse>> _logger;

    public RequestLoggingBehavior(ILogger<RequestLoggingBehavior<TRequest, TResponse>> logger)
    {
        _logger = logger;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var requestName = typeof(TRequest).Name;
        var startedAt = DateTimeOffset.UtcNow;

        _logger.LogInformation("Handling {RequestName} at {StartedAt}", requestName, startedAt);

        try
        {
            var response = await next();
            _logger.LogInformation("Handled {RequestName}", requestName);
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed {RequestName}", requestName);
            throw;
        }
    }
}
```

## Command-Only Transaction Behavior

```csharp
using MediatR;
using Microsoft.EntityFrameworkCore;

public interface ICommand<out TResponse> : IRequest<TResponse>;

public sealed class CommandTransactionBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull, ICommand<TResponse>
{
    private readonly OrdersDbContext _dbContext;

    public CommandTransactionBehavior(OrdersDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

        var response = await next();

        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return response;
    }
}
```

## Minimal API Endpoint

```csharp
using MediatR;
using Microsoft.AspNetCore.Http.HttpResults;

app.MapPost("/orders", async Task<Results<Created<CreateOrderResult>, ValidationProblem>> (
    CreateOrderRequest request,
    ISender sender,
    CancellationToken cancellationToken) =>
{
    var command = new CreateOrderCommand(
        request.CustomerId,
        request.Items.Select(x => new CreateOrderLineItem(x.ProductId, x.Quantity)).ToList());

    var result = await sender.Send(command, cancellationToken);

    return TypedResults.Created($"/orders/{result.OrderId}", result);
});

public sealed record CreateOrderRequest(Guid CustomerId, IReadOnlyList<CreateOrderItemRequest> Items);
public sealed record CreateOrderItemRequest(Guid ProductId, int Quantity);
```

## Controller Endpoint

```csharp
using MediatR;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("orders")]
public sealed class OrdersController : ControllerBase
{
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<OrderDetailsDto>> GetById(
        Guid id,
        [FromServices] ISender sender,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetOrderByIdQuery(id), cancellationToken);

        return result is null ? NotFound() : Ok(result);
    }
}
```

## Notification for In-Process Reactions

```csharp
using MediatR;

public sealed record OrderCreated(Guid OrderId) : INotification;

public sealed class SendOrderCreatedEmailHandler : INotificationHandler<OrderCreated>
{
    private readonly IEmailSender _emailSender;

    public SendOrderCreatedEmailHandler(IEmailSender emailSender)
    {
        _emailSender = emailSender;
    }

    public Task Handle(OrderCreated notification, CancellationToken cancellationToken)
    {
        return _emailSender.SendAsync(
            "ops@example.com",
            "Order created",
            $"Order {notification.OrderId} was created.",
            cancellationToken);
    }
}
```

## Lightweight Command and Query Marker Interfaces

```csharp
using MediatR;

public interface ICommand<out TResponse> : IRequest<TResponse>;

public interface IQuery<out TResponse> : IRequest<TResponse>;
```

Use marker interfaces only when they unlock a real behavior boundary such as transactions for commands or caching for queries.
