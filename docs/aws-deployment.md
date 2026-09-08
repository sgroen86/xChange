# Deploying the xChange API to AWS

The frontend is a static export on Apache; the API is an ASP.NET Core container
on AWS App Runner.

Almost all of this is automated. A fresh account needs **three manual actions**;
after that every push that touches `src/**`, `Dockerfile` or `infra/aws/**`
deploys itself via `.github/workflows/deploy-api.yml`.

Region: `eu-central-1` (change `AWS_REGION` in the workflow to move it).

## Why Lambda, and what it costs us

The API runs as a Lambda behind a Function URL because, during testing, it is
**free**: 1M requests and 400,000 GB-seconds a month are a perpetual free tier,
a Function URL costs nothing and terminates TLS itself, and nothing runs between
uploads. CloudWatch log retention is capped at 14 days and old build artefacts
expire after 30 days, so the two things that would otherwise accrue cost do not.

This was not the first choice. The history is worth knowing:

- **App Runner** was the original target and would have been ideal. AWS closed
  it to new customers on **30 April 2026**, so this account can never subscribe.
  Every call returns `SubscriptionRequiredException`.
- **ECS Fargate behind an ALB** works and preserves 20 MB uploads, but bills
  roughly **$25-35/month whether or not anyone uses it** - an ALB and a task
  both run continuously.
- **API Gateway** caps a request payload at 10 MB, so it does not solve the size
  problem either.

**The cost of choosing free is upload size.** A Function URL rejects any request
over 6 MB, and a binary body is base64-encoded first, so roughly 4.4 MB of PDF
gets through. The API and the frontend both cap at 4 MB and say so plainly. That
is below the 20 MB originally specified, and is a deliberate trade for zero cost
while this is a testing prototype.

**To restore 20 MB later**, upload straight to S3 with a presigned URL and pass
the key to the endpoint. That removes the request-size limit entirely, stays on
the free tier, and is the architecture CLAUDE.md already describes (documents in
object storage, the database holding keys). It is the right move once there are
clients; it is more moving parts than testing needs today.

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

**Only pass that on the very first deploy.** Flipping it to `false` on a stack
that already owns the provider removes the resource from the stack, and every
role trusting it then fails with `The web identity token provided could not be
validated` — which reads nothing like "the provider is gone". The resource now
carries `DeletionPolicy: Retain` so this cannot happen again, but note also that
CloudFormation *remembers* parameter values between deploys: defaults apply only
at create time, so undoing it needs an explicit
`--parameter-overrides CreateOidcProvider=true`.

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

The trust accepts four subject claims, all scoped to this repository, because
GitHub varies the claim on two independent axes:

- **environment vs branch** — a job that declares `environment:` presents
  `repo:OWNER/REPO:environment:NAME` instead of `...:ref:refs/heads/BRANCH`
- **plain vs immutable identifiers** — GitHub may append numeric ids to the
  owner and repository. This account emits
  `repo:sgroen86@261750674/xChange@1360828142:environment:xchange-aws`, so a
  policy matching only the literal `sgroen86/xChange` never matches at all.

A mismatch on either axis fails identically, with
`Not authorized to perform sts:AssumeRoleWithWebIdentity` and nothing in the
GitHub log saying why. **CloudTrail is where the answer is**: look up
`AssumeRoleWithWebIdentity` in the deploy region and read `userIdentity.userName`
for the subject that was actually presented.

The `@*` wildcards are safe because a GitHub username cannot contain `@`, so
`sgroen86@*` cannot be matched by registering a similarly named account. Pull
requests, including from forks, still cannot assume the role.

If you change the environment name in the workflow, redeploy this stack with
`--parameter-overrides GitHubEnvironment=<new-name>`.

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
2. publishes the API and zips it (no Docker: the managed `dotnet8` runtime takes
   a zip, which builds faster and stores nothing in ECR)
3. creates the secret placeholder if missing
4. uploads the zip to the artefact bucket, keyed by commit sha
5. deploys `infra/aws/service.yml`, creating or updating the function
6. smoke-tests `/health`, retrying through the cold start
7. prints the Function URL in the job summary

Rollback is a redeploy pointing at an earlier commit's zip.

The key is read from Secrets Manager **at runtime by the function's own role**,
not injected as an environment variable. Anyone with `lambda:GetFunctionConfiguration`
can read environment variables; reading the secret needs the function's role.

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

## Troubleshooting

**`SubscriptionRequiredException` from App Runner** — App Runner was closed to
new customers on 30 April 2026 and this account can never subscribe. That is why
the API runs on Lambda. Nothing to fix.

**A 413, or an upload that fails at the edge** — the file is over the Function
URL's 6 MB request limit. See "Why Lambda" above; the fix for real use is
presigned S3 uploads.

**`Stack ... is in ROLLBACK_COMPLETE state and can not be updated`** — a stack
whose *first* create failed cannot be updated, only replaced. The workflow now
deletes such a stack before deploying, so this resolves itself on the next run.

## What is deliberately not here yet

No database, no S3 storage, no queue, no authentication, and no
`OrganizationId` enforcement.

**The API is unauthenticated.** Anyone who learns the URL can post a PDF and
spend Anthropic credit. Fix that before this is anything more than a prototype —
App Runner has no built-in auth, so it needs either a key check in the API or a
CloudFront/WAF layer in front.
