namespace MarwanQuTech.Application.Common.Interfaces;

/// <summary>The authenticated admin, read from the JWT by the WebApi.</summary>
public interface ICurrentUser
{
    int? AdminId { get; }
}
