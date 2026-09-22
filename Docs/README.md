# LyricBuilder documentation

Background that the code cannot state for itself: why the structure is shaped this way, and
what happens if you work against it.

The [root README](../README.md) is the entry point — install, run, layout, conventions. Start
there. These pages go deeper.

API endpoints are **not** documented here. The OpenAPI document at `/openapi/v1.json`
(browsable at `/scalar/v1`) is generated from the code, so it cannot drift. Anything written
here by hand would.

## Architecture

| Page | What it covers |
|---|---|
| [Overview](architecture/overview.md) | The layers, which way dependencies point, and why |
| [Result pattern](architecture/result-pattern.md) | Why failures are return values instead of exceptions |
| [Models and DTOs](architecture/models-and-dtos.md) | The three model types per feature, and the attack they prevent |
| [Persistence](architecture/persistence.md) | Repository, unit of work, auditing, soft-delete, the UTC rule |
| [Dependency injection](architecture/dependency-injection.md) | Convention-based registration and its sharp edge |

## Guides

| Page | What it covers |
|---|---|
| [Local setup](guides/local-setup.md) | Getting a working database and a running app |
| [Adding a feature](guides/adding-a-feature.md) | The full walkthrough, one file at a time |

## Keeping these honest

Stale documentation is worse than none — it is confidently wrong. Two habits keep it alive:

- **Document the why, not the what.** A list of endpoints rots in a week. The reason
  `LyricMutation` has no `AuthorId` stays true as long as the decision does.
- **When a decision changes, update the page that explains it** in the same change that
  touches the code. A reader who finds one wrong page stops trusting all of them.
