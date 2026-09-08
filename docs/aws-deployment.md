# Deploying the xChange API to AWS

The frontend is a static export on Apache; the API is an ASP.NET Core container
on AWS App Runner.

Almost all of this is automated. A fresh account needs **three manual actions**;
after that every push that touches `src/**`, `Dockerfile` or `infra/aws/**`
deploys itself via `.github/workflows/deploy-api.yml`.

Region: `eu-central-1` (change `AWS_REGION` in the workflow to move it).

## Why App Runner

The endpoint accepts uploads up to 20 MB, and one request can run for minutes
while the model reads the PDF.

- **API Gateway** caps a request payload at **10 MB**.
- **Lambda function URLs** cap it at **6 MB**.

Both would reject valid invoices, so the serverless-behind-a-gateway shapes are
out. App Runner takes the container directly, terminates TLS, gives it a public
HTTPS hostname and scales down between uploads.

If App Runner's request timeout turns out to be shorter than a slow extraction
needs, the fallback is ECS Fargate behind an ALB, whose idle timeout is
configurable up to 4000s. Watch the first real extractions before assuming this
is settled.

---

## Step 1 — bootstrap the account (once)

This is the only stack CI cannot create, because it is what grants CI its
permissions. It creates the GitHub OIDC trust, the deploy role, the role App
Runner uses to pull images, and the runtime role that reads the API key.

```bash
aws cloudformation deploy \
  --template-file infra/aws/bootstrap.yml \
  --stack-name xchange-bootstrap \
  --capabilities CAPABILITY_NAMED_IAM \
  --region eu-central-1
```

If the account already trusts `token.actions.githubusercontent.com` (an account
may only have one provider per URL), add
`--parameter-overrides CreateOidcProvider=false`.

Then read the role ARN:

```bash
aws cloudformation describe-stacks --stack-name xchange-bootstrap \
  --query "Stacks[0].Outputs[?OutputKey=='GitHubDeployRoleArn'].OutputValue" \
  --output text
```

The deploy role is deliberately narrow: it can push images and manage
`xchange-*` stacks, but it has **no IAM write permissions**, so CI cannot grant
itself anything. It may create the Anthropic secret and read its metadata, but
not read or overwrite its value.

The trust is pinned to `repo:sgroen86/xChange:ref:refs/heads/main`. Pull
requests, including from forks, cannot assume it.

## Step 2 — tell GitHub about the role (once)

**Settings → Environments → New environment**, named `xchange-aws`, with one
secret:

| Secret | Value |
|---|---|
| `AWS_DEPLOY_ROLE_ARN` | the ARN from step 1 |

That is the only AWS credential GitHub holds, and it is not a credential — it is
the name of a role that only this repository's `main` branch may assume.

## Step 3 — set the Anthropic key (once)

The first workflow run creates the secret with a placeholder so the stack has
something to reference. Put the real key in it:

```bash
aws secretsmanager put-secret-value \
  --secret-id xchange/anthropic-api-key \
  --secret-string '<your-anthropic-key>' \
  --region eu-central-1
```

Extraction returns 502 until this is set. The key never passes through CI, the
image, or the browser: App Runner injects it into the container at runtime and
only the instance role can read it.

---

## What the workflow does on its own

Everything else, on every push:

1. runs `dotnet test` — a red build cannot deploy
2. creates the ECR repository if missing
3. creates the secret placeholder if missing
4. builds the image and pushes it tagged with the commit sha
5. deploys `infra/aws/service.yml`, creating or updating the App Runner service
6. waits for health, retrying while App Runner swaps instances
7. prints the service URL in the job summary

Rollback is a redeploy of an earlier image tag.

## Step 4 — point the frontend at the API

Take the URL from the job summary and set it as a **repository variable** — not
a secret; it is a public URL and gets baked into the JavaScript bundle:

**Settings → Secrets and variables → Actions → Variables → New repository variable**

| Variable | Value |
|---|---|
| `XCHANGE_API_URL` | `https://<id>.eu-central-1.awsapprunner.com` |

Then re-run **Deploy xChange UI**. Until this is set the frontend stays on mock
data, so a build that has never been pointed at an API still works rather than
failing in the browser.

## Verifying

```bash
curl https://<id>.eu-central-1.awsapprunner.com/health          # -> Healthy
curl -F "file=@invoice.pdf" \
  https://<id>.eu-central-1.awsapprunner.com/api/v1/invoices/extract
```

## Tearing it down

```bash
aws cloudformation delete-stack --stack-name xchange-service
aws cloudformation delete-stack --stack-name xchange-bootstrap
```

The secret and the ECR repository are created by the workflow rather than the
stacks, so they survive deletion and must be removed separately if you want them
gone.

## What is deliberately not here yet

No database, no S3 storage, no queue, no authentication, and no
`OrganizationId` enforcement.

**The API is unauthenticated.** Anyone who learns the URL can post a PDF and
spend Anthropic credit. Fix that before this is anything more than a prototype —
App Runner has no built-in auth, so it needs either a key check in the API or a
CloudFront/WAF layer in front.
