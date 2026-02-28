# Deploy Status

Date: 2026-02-28

## Current State

- Frontend is deployed on Amplify app `d35nn0pbd8bxxa`
- Backend is deployed on App Runner service `diario-intelligente-api`
- GitHub Actions workflow exists in `.github/workflows/deploy.yml`
- The workflow is configured to use AWS OIDC role assumption
- The workflow now deploys both backend and frontend on push to `main`
- App Runner runtime secrets now come from SSM Parameter Store
- Manual frontend deploy completed successfully on 2026-02-28
- Backend health endpoint is responding successfully after the latest deploy cycle
- Workflow `Deploy to AWS #1` for commit `d36609e` ran on GitHub Actions on 2026-02-28
- Backend job succeeded
- Frontend job failed in early version, then release path was fixed
- Amplify app is still not repository-connected at app level: `enableBranchAutoBuild=false` on app and no repository metadata on `get-app`
- This is no longer a blocker because frontend release now uses Amplify manual deployment APIs from GitHub Actions
- Frontend manual API path was validated end-to-end with Amplify job `4` status `SUCCEED` on 2026-02-28
- Workflow run `#12` (commit `89021c0`) failed on frontend at `CreateDeployment` because IAM policy did not include Amplify deployment resource path
- IAM fix applied: role `GitHubActionsDiarioDeployRole` now includes `.../branches/main/deployments/*` and `.../branches/main/jobs/*` with `CreateDeployment`, `StartDeployment`, `GetJob`, `ListJobs`
- Workflow run `#13` (commit `e256514`) completed with `success` on both jobs (`Deploy Backend`, `Deploy Frontend`)
- Amplify branch `main` received job `5` with status `SUCCEED`
- OpenSearch domain provisioning started: `diario-search-dev` (`eu-west-1`, `OpenSearch_2.19`)
- OpenSearch Serverless is still not reachable from this environment
- OpenSearch domain `diario-search-dev` is now active and has a reachable endpoint
- database reset (clean start) executed on PostgreSQL RDS on 2026-02-28 for all application tables

## What Happens On Push

Automatic deployment on `push` to `main` works only after the workflow file is committed and pushed to GitHub.

At the moment of this note:

- backend deploy is handled by GitHub Actions
- frontend build, artifact upload, and deploy to Amplify are handled by GitHub Actions
- AWS roles and trust configuration are already prepared
- current blocker for semantic retrieval rollout: domain provisioning must complete before wiring `EntityCard` projection

## Backend Deploy Path

- build Docker image
- push image to ECR `diario-intelligente`
- trigger App Runner deployment

## Frontend Deploy Path

- build Angular app in CI
- package `frontend/dist/frontend/browser` as zip artifact
- call `amplify create-deployment` on app `d35nn0pbd8bxxa` branch `main`
- upload zip to `zipUploadUrl`
- call `amplify start-deployment`
- wait for job status `SUCCEED`

## Security Notes

- static AWS access keys are no longer required by the workflow design
- secrets should remain in SSM or Secrets Manager, not in App Runner plain env vars
- the OpenAI key should still be rotated because it previously existed in clear text runtime configuration
