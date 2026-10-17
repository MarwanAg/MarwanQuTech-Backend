using FluentValidation;
using MarwanQuTech.Application.Common.Behaviors;
using MarwanQuTech.Application.Common.Exceptions;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace MarwanQuTech.Application.UnitTests.Common;

/// <summary>Runs the real MediatR pipeline so the test doesn't depend on delegate signatures.</summary>
public class ValidationBehaviorTests
{
    public sealed record Ping(string? Name) : IRequest<string>;

    public sealed class PingHandler : IRequestHandler<Ping, string>
    {
        public static int Calls;

        public Task<string> Handle(Ping request, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref Calls);
            return Task.FromResult("pong");
        }
    }

    public sealed class PingValidator : AbstractValidator<Ping>
    {
        public PingValidator() => RuleFor(x => x.Name).NotEmpty();
    }

    private static IMediator BuildMediator(bool withValidator)
    {
        var services = new ServiceCollection();
        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssemblyContaining<ValidationBehaviorTests>();
            cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));
        });

        if (withValidator)
            services.AddSingleton<IValidator<Ping>, PingValidator>();

        return services.BuildServiceProvider().GetRequiredService<IMediator>();
    }

    [Fact]
    public async Task Throws_with_field_errors_and_skips_handler()
    {
        var mediator = BuildMediator(withValidator: true);
        var before = PingHandler.Calls;

        var ex = await Should.ThrowAsync<RequestValidationException>(() => mediator.Send(new Ping("")));

        ex.Errors.ShouldContainKey("Name");
        PingHandler.Calls.ShouldBe(before);
    }

    [Fact]
    public async Task Calls_handler_when_valid()
        => (await BuildMediator(withValidator: true).Send(new Ping("ok"))).ShouldBe("pong");

    [Fact]
    public async Task Calls_handler_when_no_validators()
        => (await BuildMediator(withValidator: false).Send(new Ping(null))).ShouldBe("pong");
}
