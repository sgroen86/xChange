# Deploying the xChange API to AWS

The frontend is a static export on Apache; the API is an ASP.NET Core container
on AWS. This document covers the one-time AWS setup. Once it exists, every push
that touches `src/**` or `Dockerfile` deploys automatically via
`.github/workflows/deploy-api.yml`.

Region used throughout: `eu-central-1`. Change `AWS_REGION` in the workflow if
you want another.

## Why App Runner

The endpoint accepts uploads up to 20 MB, and a single request can run for
minutes while the model reads the PDF.

- **API Gateway** caps a request payload at **10 MB**.
- **Lambda function URLs** cap it at **6 MB**.

Either would reject valid invoices, so the serverless-behind-a-gateway shapes are
out. App Runner takes the container directly, terminates TLS, gives the service a
public HTTPS hostname, and scales down between uploads.

If App Runner's own request timeout turns out to be shorter than a slow
extraction needs, the fallback is ECS Fargate behind an ALB (an ALB's idle
timeout is configurable up to 4000s). Watch the first few real extractions before
assuming this is settled.

## Step 1 — the deploy role (GitHub OIDC)

The workflow authenticates with GitHub's OIDC provider and assumes a role. There
is no long-lived AWS access key in the repository, in GitHub, or on anyone's
laptop; nothing has to be pasted into a chat window or an email.

If your account has never trusted GitHub before, add the provider once:

```bash
aws iam create-open-id-connect-provider \
  --url https://token.actions.githubusercontent.com \
  --client-id-list sts.amazonaws.com \
  --thumbprint-list 6938fd4d98bab03faadb97b34396831e3780aea1
```

Create `trust-policy.json`, restricting the trust to this repository's `main`
branch so no other repo, and no pull request from a fork, can assume it:

```json
{
  "Version": "2012-10-17",
  "Statement": [
    {
      "Effect": "Allow",
      "Principal": {
        "Federated": "arn:aws:iam::<ACCOUNT_ID>:oidc-provider/token.actions.githubusercontent.com"
      },
      "Action": "sts:AssumeRoleWithWebIdentity",
      "Condition": {
        "StringEquals": {
          "token.actions.githubusercontent.com:aud": "sts.amazonaws.com",
          "token.actions.githubusercontent.com:sub": "repo:sgroen86/xChange:ref:refs/heads/main"
        }
      }
    }
  ]
}
```

```bash
aws iam create-role \
  --role-name xchange-github-deploy \
  --assume-role-policy-document file://trust-policy.json

# Scoped to pushing images and rolling out this one service.
aws iam put-role-policy \
  --role-name xchange-github-deploy \
  --policy-name xchange-deploy \
  --policy-document '{
    "Version": "2012-10-17",
    "Statement": [
      { "Effect": "Allow",
        "Action": ["ecr:GetAuthorizationToken"],
        "Resource": "*" },
      { "Effect": "Allow",
        "Action": [
          "ecr:BatchCheckLayerAvailability", "ecr:CompleteLayerUpload",
          "ecr:InitiateLayerUpload", "ecr:PutImage", "ecr:UploadLayerPart",
          "ecr:DescribeRepositories", "ecr:CreateRepository"
        ],
        "Resource": "*" },
      { "Effect": "Allow",
        "Action": ["apprunner:ListServices", "apprunner:DescribeService", "apprunner:UpdateService"],
        "Resource": "*" }
    ]
  }'
```

Then in GitHub: **Settings → Environments → New environment** named
`xchange-aws`, and add one secret to it:

| Secret | Value |
|---|---|
| `AWS_DEPLOY_ROLE_ARN` | `arn:aws:iam::<ACCOUNT_ID>:role/xchange-github-deploy` |

## Step 2 — the API key, stored in AWS

The Anthropic key must never reach CI logs, the container image, or the browser.
Put it in Secrets Manager and have App Runner read it from there:

```bash
aws secretsmanager create-secret \
  --name xchange/anthropic-api-key \
  --secret-string '<your-anthropic-key>'
```

## Step 3 — create the App Runner service (once)

The workflow updates this service but deliberately does not create it: creation
carries the API key configuration, which should not pass through CI.

First push an image so there is something to run — either run the workflow (it
will push the image, then stop with a message saying the service is missing), or
build and push locally.

Then create the service in the console, or:

```bash
aws apprunner create-service \
  --service-name xchange-api \
  --source-configuration '{
    "ImageRepository": {
      "ImageIdentifier": "<ACCOUNT_ID>.dkr.ecr.eu-central-1.amazonaws.com/xchange-api:latest",
      "ImageRepositoryType": "ECR",
      "ImageConfiguration": {
        "Port": "8080",
        "RuntimeEnvironmentSecrets": {
          "Anthropic__ApiKey": "arn:aws:secretsmanager:eu-central-1:<ACCOUNT_ID>:secret:xchange/anthropic-api-key"
        },
        "RuntimeEnvironmentVariables": {
          "ASPNETCORE_ENVIRONMENT": "Production",
          "Cors__AllowedOrigins__0": "https://greenitsolutions.net",
          "Cors__AllowedOrigins__1": "https://www.greenitsolutions.net"
        }
      }
    },
    "AutoDeploymentsEnabled": false,
    "AuthenticationConfiguration": { "AccessRoleArn": "arn:aws:iam::<ACCOUNT_ID>:role/service-role/AppRunnerECRAccessRole" }
  }' \
  --health-check-configuration '{"Protocol":"HTTP","Path":"/health","Interval":10,"Timeout":5}' \
  --instance-configuration '{"Cpu":"1 vCPU","Memory":"2 GB"}'
```

`AppRunnerECRAccessRole` is the standard service role App Runner uses to pull
from ECR; the console offers to create it for you on first use.

Note the double underscore in `Anthropic__ApiKey` and `Cors__AllowedOrigins__0` —
that is how .NET configuration maps environment variables onto nested keys.

## Step 4 — point the frontend at it

Take the service URL from the workflow output (or
`aws apprunner describe-service --service-arn <arn> --query 'Service.ServiceUrl'`)
and set it as a **repository variable**, not a secret — it is a public URL and it
gets baked into the JavaScript bundle:

**Settings → Secrets and variables → Actions → Variables → New repository variable**

| Variable | Value |
|---|---|
| `XCHANGE_API_URL` | `https://<id>.eu-central-1.awsapprunner.com` |

Then re-run **Deploy xChange UI**. Until this variable is set, the frontend
stays on mock data — a build that has not been pointed at an API still works
rather than failing in the browser.

## Verifying

```bash
curl https://<id>.eu-central-1.awsapprunner.com/health          # -> Healthy
curl -F "file=@invoice.pdf" \
  https://<id>.eu-central-1.awsapprunner.com/api/v1/invoices/extract
```

The deploy workflow already checks `/health` itself and fails if it does not
return 200.

## What is deliberately not here yet

No database, no S3 storage, no queue, no authentication, and no `OrganizationId`
enforcement. The API is currently unauthenticated: anyone who learns the URL can
post a PDF and spend Anthropic credit. **Do not treat this as production-ready
until authentication is added** — see the open decisions in CLAUDE.md.
