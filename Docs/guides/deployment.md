# Deployment

The repository ships a `Dockerfile` at the root. Any container host can build it; this page
walks through [SnapDeploy](https://snapdeploy.dev).

## What the image does

| | |
|---|---|
| Port | Listens on `$PORT`, or `8080` without one |
| Environment | `Production` |
| Schema | Applies pending migrations on startup (`Database__MigrateOnStartup=true`) |
| User | The base image's non-root user |

Migrating on startup is safe with one instance. With several starting at once they race for
the same migration, so scale out only after moving migrations into a separate step.

## Configuration

Set these on the container, never in the image:

| Variable | Required | Meaning |
|---|---|---|
| `DATABASE_URL` | yes, or the next one | `postgres://user:password@host:port/database`, as managed databases inject it |
| `ConnectionStrings__LyricBuilderDatabase` | yes, or the previous one | Npgsql key-value form. Wins over `DATABASE_URL` when both are set |
| `Jwt__SigningKey` | yes | 32+ random characters. Startup fails without it outside Development |
| `OpenAI__ApiKey` | no | Without it, only AI section writing fails |

Changing `Jwt__SigningKey` signs everyone out — existing tokens stop validating.

## SnapDeploy

1. **New container → GitHub**, pick this repository and branch. SnapDeploy finds the
   `Dockerfile` at the root.
2. **Port:** `8080`.
3. **Database:** add the PostgreSQL add-on. It injects `DATABASE_URL` into the container.
   For a database hosted elsewhere, set `DATABASE_URL` or `ConnectionStrings__LyricBuilderDatabase`
   yourself.
4. **Environment variables:** `Jwt__SigningKey`, and `OpenAI__ApiKey` if you want AI writing.
5. **Health check path:** `/live`. The default, `/`, has no route and answers 404, so the
   deploy is marked unhealthy.
6. Deploy. The first start creates the schema; then `/scalar/v1` on the container URL shows
   the API.

On the free tier the container sleeps after about 15 minutes idle, so the first request after
that is slow.

## Running the image locally

```bash
docker build -t lyricbuilder .
docker run --rm -p 8080:8080 \
  -e DATABASE_URL="postgres://postgres:<password>@host.docker.internal:5432/lyricbuilder" \
  -e Jwt__SigningKey="<32+ random characters>" \
  lyricbuilder
```

`host.docker.internal` reaches a PostgreSQL running on your machine from inside the container.
