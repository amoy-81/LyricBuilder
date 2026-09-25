# Dependency injection

One entry point, and a convention that registers most services for you.

## AddCore

The Host calls exactly one method:

```csharp
builder.Services.AddCore(builder.Configuration);
```

`AddCore` composes the application layer:

```csharp
services
    .AddLyricBuilderDatabase(configuration)
    .AddScoped<RequestContext>()
    .RegisterByNamingConvention(typeof(RequestContext).Assembly, "Service");
```

Everything a layer needs is registered by that layer's own extension method.
`AddLyricBuilderDatabase` lives in Infrastructure and owns the DbContext, the repository, and
the unit of work. The Host never sees any of it.

## Convention-based registration

`RegisterByNamingConvention` (in `Abstractions/Extensions/`) scans an assembly for concrete
classes whose name ends in a suffix — `"Service"` — and registers each against the interfaces
it implements.

`LyricService : ILyricService` is picked up with **no DI line**. Adding `AlbumService`
requires no change to `AddCore`.

### How it registers

The concrete type is registered once, then each interface is pointed at that same
registration:

```csharp
services.Add(new ServiceDescriptor(implementation, implementation, lifetime));
foreach (var contract in contracts)
    services.Add(new ServiceDescriptor(contract, sp => sp.GetRequiredService(implementation), lifetime));
```

The indirection matters: a service implementing two interfaces resolves to **one instance per
scope**, not one per interface. Registering each interface independently would silently give
you two objects and two change-tracking states.

Default lifetime is `Scoped` — one instance per HTTP request, matching the DbContext.

### The sharp edge

**A class not ending in `Service` is not registered.** Nothing warns you; the failure arrives
at runtime as:

> Unable to resolve service for type 'ILyricPublishingGuard'…

This is a real trap, not a hypothetical: a class named `…Guard`, `…Notifier`, `…Manager`, or
`…Factory` silently falls outside the convention and has to be registered by hand.

So: **name it `…Service` and get it free, or register it explicitly.** If you add an explicit
registration, put a comment saying why, so nobody deletes it as redundant.

Only interfaces **declared in the same assembly** are wired up. A service implementing
`IHostedService` is not registered against it.

### What it costs

Reflection-based registration trades explicitness for brevity:

- You cannot find the registration by searching for the type name.
- Errors surface at runtime, not compile time.
- It scans all types at startup — negligible for one assembly, but not free.

The trade is worth it at the point where `AddCore` would otherwise be fifty near-identical
lines. Below a handful of services it would not be.

## RequestContext

`RequestContext` is registered explicitly — it is not a `…Service` — and is scoped, so one
instance exists per request.

`RequestContextMiddleware` fills it from the authenticated principal, and services take it as
a constructor parameter. That is how `LyricService` knows who is calling without touching
`HttpContext`:

```csharp
AuthorId = requestContext.UserId.Value
```

**Ordering matters.** `UseRequestContext()` must come after `UseAuthentication()`, or the
principal will not be populated yet and `UserId` will always be null.

`UserId` is the token's `sub` claim — the caller's `User` id, which is what `AuthorId` holds.
See [authentication](authentication.md).

## Pipeline order

From `Program.cs`, in order:

```csharp
app.UseErrorMiddleware();      // outermost: catches everything below
app.UseHttpsRedirection();
app.UseAuthentication();       // populates HttpContext.User
app.UseAuthorization();
app.UseRequestContext();       // reads that principal into RequestContext
app.MapControllers();
```

`UseErrorMiddleware` is first so it wraps every later component. Middleware registered before
it is outside its try/catch and its exceptions will not be translated.

## Adding registrations

| Situation | What to do |
|---|---|
| A normal service | Name it `…Service`. Nothing else. |
| A class named otherwise | Register explicitly in `AddCore`, with a comment. |
| Options bound from config | `services.AddOptions<T>().Bind(configuration.GetSection(...))` in `AddCore` |
| Infrastructure concerns | A new extension method in that layer, called from `AddCore` |

Keep `AddCore` as the only thing the Host calls. Once `Program.cs` starts registering
application services directly, the boundary stops meaning anything.
