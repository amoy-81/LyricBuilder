# Authentication

Username and password in, a signed JWT out. The token is then sent as
`Authorization: Bearer <token>` on every request that needs a caller.

```
POST /api/auth/register  { name, username, password }  → the new user's id
POST /api/auth/login     { username, password }        → { accessToken, expiresAt, … }
```

Registration does not log you in. Call login afterwards for a token.

## Account and User are two entities

```
Account ──► User ◄──── Lyric.Author
(username,   (name)
 password,
 role)
```

- **`User`** is the profile, the person lyrics are authored under. It has a display name and
  nothing secret.
- **`Account`** is how that person signs in: username, password hash, role. One per user.

They are split so that credentials never travel with the profile. Anything that loads a user
to show an author's name has no password hash in hand, and cannot serialize one by accident.

The ids are different, and **the one that matters everywhere else is the user id**.
`Lyric.AuthorId` points at `User`, and the token's subject is the user id, so
`RequestContext.UserId` can be compared with `AuthorId` directly.

## Roles

| Role | Who | Gets it by |
|---|---|---|
| `User` | Every writer | Registering. The only role registration gives |
| `Admin` | Curates styles, tags and reference lyrics | Being promoted in the database |

`RegisterMutation` has no role field, so a client cannot sign itself up as an admin. The same
over-posting rule as `LyricMutation`'s missing `AuthorId` (see
[models and DTOs](models-and-dtos.md)).

There is no endpoint to promote an account. Make the first admin by hand:

```sql
UPDATE "Accounts" SET "Role" = 1 WHERE "Username" = 'someone';
```

The role is baked into the token, so the account has to **log in again** before the change
takes effect.

To restrict an endpoint to admins:

```csharp
[Authorize(Roles = nameof(AccountRole.Admin))]
```

Inside a service, `requestContext.IsAdmin` answers the same question.

## Usernames

Stored normalized: trimmed and lower-cased by `Account.NormalizeUsername`. `Amir` and `amir`
are the same account, and login accepts either spelling. **Normalize before every lookup**, or
a mixed-case query will not match the stored row.

The unique index on `Username` is filtered on `IsDeleted`, like the slug indexes, so a
soft-deleted account frees its name.

## Passwords

Hashed with ASP.NET Core's `PasswordHasher<Account>`: salted PBKDF2, with the algorithm version
stored in the hash itself so it can change later without breaking old hashes. It ships in the
shared framework, so it needs no ASP.NET Core Identity tables or extra package.

Login returns the same `401 invalid username or password` whether the username is unknown or
the password is wrong, so the endpoint cannot be used to find out which usernames exist.

## The token

Signed with HMAC-SHA256 using `Jwt:SigningKey`. Its claims, named in `TokenClaims`:

| Claim | Holds | Read into |
|---|---|---|
| `sub` | User id | `RequestContext.UserId` |
| `preferred_username` | Username | `RequestContext.UserName`, `User.Identity.Name` |
| `name` | Display name | — |
| `role` | `User` or `Admin` | `RequestContext.Role`, `[Authorize(Roles = …)]` |

**Inbound claim mapping is off** (`MapInboundClaims = false`). By default ASP.NET Core renames
`sub` to a long `ClaimTypes.NameIdentifier` URI on the way in, so the name a token is written
with would not be the name it is read by. With mapping off, issuing, validation and
`RequestContextMiddleware` all use the same `TokenClaims` constants.

Tokens cannot be revoked. A token stays valid until it expires, even after a password change
or a role change. Keep `AccessTokenLifetimeMinutes` short. Refresh tokens would fix this, and
are not built yet.

## Configuration

| Key | Purpose |
|---|---|
| `Jwt:Issuer`, `Jwt:Audience` | Written into the token and checked on the way back in |
| `Jwt:SigningKey` | Shared secret. At least 32 bytes |
| `Jwt:AccessTokenLifetimeMinutes` | Defaults to 60 |

Startup throws if the issuer or audience is missing or the key is too short, for the same
reason it throws on a missing connection string: a misconfigured app should fail on start, not
on the first login.

`appsettings.Development.json` holds a development key. It is committed, so it is not a secret.
Anywhere else, supply the key through user-secrets or the `Jwt__SigningKey` environment
variable.

## Lyrics are private

Each writer sees only their own lyrics. `LyricsController` is a `SecureEndpoint`, and
`LyricService` filters every read by `AuthorId == requestContext.UserId`. The filter lives in
the service, not the controller, so no other caller of the service can skip it.

Reading someone else's lyric by id returns `404`, not `403`: a `403` would confirm that the
id exists.

## Trying it in Scalar

`/scalar/v1` knows about the Bearer scheme. Log in, copy `accessToken` into the auth section,
and every endpoint marked `[Authorize]` sends it. Those are the only operations that show the
lock, so the docs also show which endpoints need a token.
