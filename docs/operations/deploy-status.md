# Deploy Status

Date: 2026-02-28

## Current State

- Frontend is deployed on Amplify app `d35nn0pbd8bxxa`
- Backend is deployed on App Runner service `diario-intelligente-api`
- GitHub Actions workflow exists in `.github/workflows/deploy.yml`
- The workflow is configured to use AWS OIDC role assumption
- App Runner runtime secrets now come from SSM Parameter Store
- Manual frontend deploy completed successfully on 2026-02-28
- Backend health endpoint is responding successfully after the latest deploy cycle
- Workflow `Deploy to AWS #1` for commit `d36609e` ran on GitHub Actions on 2026-02-28
- Backend job succeeded
- Frontend job failed
- The release path will be simplified: GitHub Actions for backend, Amplify native CI/CD for frontend
- Amplify app is still not repository-connected at app level: `enableBranchAutoBuild=false` on app and no repository metadata on `get-app`
- as of 2026-02-28, no classic OpenSearch domains are present in the AWS account, and OpenSearch Serverless is not currently reachable from this environment

## What Happens On Push

Automatic deployment on `push` to `main` works only after the workflow file is committed and pushed to GitHub.

At the moment of this note:

- backend deploy is handled by GitHub Actions
- frontend deploy is expected to be handled by Amplify branch auto build
- AWS roles and trust configuration are already prepared
- current blocker: frontend auto-build is not truly active until repo/webhook connection is fixed in Amplify
- current blocker for semantic retrieval rollout: there is no reachable OpenSearch resource to wire the new `EntityCard` projection against

## Backend Deploy Path

- build Docker image
- push image to ECR `diario-intelligente`
- trigger App Runner deployment

## Frontend Deploy Path

- push on the repository branch connected to Amplify
- Amplify builds the frontend
- Amplify deploys branch `main`

## Security Notes

- static AWS access keys are no longer required by the workflow design
- secrets should remain in SSM or Secrets Manager, not in App Runner plain env vars
- the OpenAI key should still be rotated because it previously existed in clear text runtime configuration
