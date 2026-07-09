# Repo tree snapshot — 2026-07-08

Branch: `develop` @ `66f67f0`

## Counts

| Scope | Tracked files |
|---|---|
| `backend/` | 1253 |
| `frontend/` | 301 |
| `mobile/` | 151 |
| `docs/` | 10 |
| `plans/` | 9 |
| **total tracked** | **1733** |
| untracked/modified | 22 |

Backend services: identity-access-service mission-design-service scoring-monitoring-service session-operations-service 

## Active worktrees

```
/home/samu/Desktop/umbral                                                                              66f67f0 [develop]
/tmp/claude-1000/-home-samu-Desktop-umbral/b8c0125b-64c5-418d-b7fa-fea63f7260b9/scratchpad/wt-optionc  66f67f0 [optionc-proto]
/tmp/umbral-wt-identity-access                                                                         23f176c [refactor/app-layer-identity-access]
/tmp/umbral-wt-integration                                                                             5d0e4b9 [integration/app-layer-all]
/tmp/umbral-wt-mission-design                                                                          0369145 [refactor/app-layer-mission-design]
/tmp/umbral-wt-mission-design-green                                                                    0369145 [refactor/app-layer-mission-design-green]
/tmp/umbral-wt-session-operations                                                                      8d1567c [refactor/app-layer-session-operations]
/tmp/umbral-wt-session-ops-verify-cf74dea                                                              cf74dea (detached HEAD)
```

## Tree (depth 4, gitignore-aware)

```
.
├── .agents
├── backend
│   ├── adr
│   │   ├── 0001-team-reference-data-in-identity.md
│   │   ├── 0002-trivia-question-score-and-timer-ranges.md
│   │   ├── 0003-archive-time-enforcement-for-quizzes-referenced-by-active-missions.md
│   │   ├── 0004-exception-to-problemdetails-mapping.md
│   │   └── 0005-substage-advancement-pointer-and-timer-driven-orchestration.md
│   ├── .agents
│   │   ├── skills
│   │   │   ├── aspnet-backend-testing
│   │   │   ├── conventional-commits
│   │   │   ├── cqrs-mediatr-aspnetcore
│   │   │   ├── ef-core-postgresql
│   │   │   ├── generate-service-readme
│   │   │   ├── git-graph-merge
│   │   │   ├── grill-with-docs
│   │   │   ├── handoff
│   │   │   ├── prd-to-plan
│   │   │   ├── prototype
│   │   │   ├── rabbitmq-events-dotnet
│   │   │   ├── safe-pr-creator
│   │   │   ├── signalr-websockets-aspnetcore
│   │   │   ├── to-issues
│   │   │   ├── to-prd
│   │   │   ├── write-a-skill
│   │   │   └── zoom-out
│   │   ├── architect-agent.md
│   │   ├── backend-agent.md
│   │   ├── driver-agent.md
│   │   └── generator-agent.md
│   ├── api-gateway
│   │   ├── src
│   │   │   ├── bin
│   │   │   ├── obj
│   │   │   ├── Transforms
│   │   │   ├── ApiGateway.csproj
│   │   │   ├── appsettings.Development.json
│   │   │   ├── appsettings.json
│   │   │   ├── DependencyInjection.cs
│   │   │   ├── Directory.Build.props
│   │   │   ├── Directory.Packages.props
│   │   │   ├── GlobalUsings.cs
│   │   │   ├── Program.cs
│   │   │   └── RewriteLocalhostBackchannelHandler.cs
│   │   ├── tests
│   │   │   ├── AuthPath.EndToEndTests
│   │   │   ├── AuthProbe
│   │   │   └── docker-compose.auth-tests.yml
│   │   └── Dockerfile
│   ├── .claude
│   │   ├── .cc-writes
│   │   ├── skills
│   │   │   ├── aspnet-backend-testing
│   │   │   ├── cqrs-mediatr-aspnetcore
│   │   │   ├── ef-core-postgresql
│   │   │   ├── grill-with-docs
│   │   │   ├── prd-to-plan
│   │   │   ├── prototype
│   │   │   ├── rabbitmq-events-dotnet
│   │   │   ├── signalr-websockets-aspnetcore
│   │   │   ├── to-issues
│   │   │   ├── to-prd
│   │   │   ├── write-a-skill
│   │   │   └── zoom-out
│   │   ├── architect-agent.md
│   │   ├── backend-agent.md
│   │   ├── driver-agent.md
│   │   ├── generator-agent.md
│   │   └── settings.local.json
│   ├── deploy
│   │   ├── keycloak
│   │   │   └── import
│   │   └── postgres
│   │       └── init-dbs.sql
│   ├── docs
│   │   ├── adr
│   │   │   ├── 0001-gateway-central-jwt-validation.md
│   │   │   ├── 0002-websocket-token-extraction-at-gateway.md
│   │   │   ├── 0003-api-gateway-keycloak-design-summary.md
│   │   │   ├── 0004-required-domain-patterns.md
│   │   │   ├── 0005-coverlet-msbuild-for-aggregate-coverage.md
│   │   │   ├── 0006-hu02-user-management-architecture.md
│   │   │   ├── 0007-join-token-identity-ownership-and-non-consuming-validation.md
│   │   │   ├── 0008-shared-postgres-testcontainer-for-integration-tests.md
│   │   │   ├── 0009-resolve-operator-ownership-via-identity-actor-profile.md
│   │   │   ├── 0010-evidence-qr-only-first-delivery.md
│   │   │   ├── 0011-application-layer-vertical-slice-organization.md
│   │   │   └── 0012-design-pattern-placement-convention.md
│   │   ├── archive
│   │   │   ├── old-des
│   │   │   ├── fix-hu-05-testcontainers-vpn.md
│   │   │   └── fix-vpn-docker-test-containers.md
│   │   ├── .claude
│   │   │   ├── .cc-writes
│   │   │   └── settings.local.json
│   │   ├── decisions
│   │   │   └── identity-access-service.md
│   │   ├── faq
│   │   │   ├── identity-access-application-layer.md
│   │   │   ├── strategy-vs-session-progression.md
│   │   │   └── workflow-and-sprint-planning.md
│   │   ├── findings
│   │   │   └── violations-overengineering.md
│   │   ├── grill
│   │   │   └── summary.md
│   │   ├── prd
│   │   │   ├── DES-62-mission-design-service-baseline.md
│   │   │   ├── DES-66-local-backend-platform-baseline.md
│   │   │   ├── DES-67-primera-implementacion-de-identity-access-service-hu-01-a-hu-08.md
│   │   │   ├── DES-68-prd-hu-06-inicio-de-sesion-de-participantes.md
│   │   │   ├── DES-69-participant-membership-validation.md
│   │   │   └── DES-70-primera-implementacion-de-session-operations-service-hu-15-a-hu-36.md
│   │   ├── qa
│   │   │   └── mission-design-service-basics.md
│   │   ├── refactors
│   │   │   ├── application-layer-cqrs-mission-design-phase-1.diff
│   │   │   └── application-layer-overengineering-checklist.md
│   │   ├── academic-requirements-canon.md
│   │   ├── adr-0012-pattern-realizations-by-layer.md
│   │   ├── agent-skills-reference.md
│   │   ├── application-slice-shape-decision-handoff-2026-07-08.md
│   │   ├── bd_umbral_entity_spec.md
│   │   ├── canon-realignment-after-mission-runtime-rewrite.md
│   │   ├── canon-realignment-workflow.md
│   │   ├── codex-sandbox-setup.md
│   │   ├── cover-script.md
│   │   ├── current_workflow.md
│   │   ├── ddd_solution_model.md
│   │   ├── decisions.md
│   │   ├── delegation_hus_order_sprint1.md
│   │   ├── grilling-session-mission-restructure.md
│   │   ├── hu02-context.md
│   │   ├── hu03-context.md
│   │   ├── hu04-context.md
│   │   ├── hu05-context.md
│   │   ├── hu07a-context.md
│   │   ├── hu07b-context.md
│   │   ├── hu07b-reconnect-disconnect-lifecycle-handoff.md
│   │   ├── hu09-context.md
│   │   ├── hu10a-brief.md
│   │   ├── hu10a-context.md
│   │   ├── hu11-context.md
│   │   ├── hu12-context.md
│   │   ├── hu13-context.md
│   │   ├── hu14a-context.md
│   │   ├── hu15-brief.md
│   │   ├── hu15-context.md
│   │   ├── hu15-how-to-test.md
│   │   ├── hu16-brief.md
│   │   ├── hu16-context.md
│   │   ├── hu17-brief.md
│   │   ├── hu17-context.md
│   │   ├── hu18-context.md
│   │   ├── hu19-context.md
│   │   ├── hu21a-brief.md
│   │   ├── hu21a-context.md
│   │   ├── hu22-brief.md
│   │   ├── hu22-context.md
│   │   ├── hu33a-brief.md
│   │   ├── hu33a-context.md
│   │   ├── hu33b-brief.md
│   │   ├── hu33b-context.md
│   │   ├── hu33b-generation-handoff-2026-07-08.md
│   │   ├── hu33b-rabbitmq-contract.md
│   │   ├── identity-access-service-alignment-findings-2026-07-06.md
│   │   ├── participant-lobby-join-handoff-2026-07-08.md
│   │   ├── prompt_example_feature_hu01.md
│   │   ├── prompt_example_feature_hu02.md
│   │   ├── prompt_example_feature_hu03.md
│   │   ├── prompt_example_feature_hu04.md
│   │   ├── prompt_example_feature_hu05.md
│   │   ├── prompt_example_feature_hu07a.md
│   │   ├── prompt_example_feature_hu07b.md
│   │   ├── prompt_example_feature_hu09.md
│   │   ├── prompt_example_feature_hu10a.md
│   │   ├── prompt_example_feature_hu11.md
│   │   ├── prompt_example_feature_hu12.md
│   │   ├── prompt_example_feature_hu13.md
│   │   ├── prompt_example_feature_hu14a.md
│   │   ├── prompt_example_feature_hu15.md
│   │   ├── prompt_example_feature_hu16.md
│   │   ├── prompt_example_feature_hu17.md
│   │   ├── prompt_example_feature_hu18.md
│   │   ├── prompt_example_feature_hu19.md
│   │   ├── prompt_example_feature_hu21a.md
│   │   ├── prompt_example_feature_hu22.md
│   │   ├── prompt_example_feature_hu33a.md
│   │   ├── prompt_example_feature_hu33b.md
│   │   ├── Proyecto_Integrador_UMBRAL_UCAB.pdf
│   │   ├── required_patterns_matrix.md
│   │   ├── requisitos-academicos-umbral-ucab.md
│   │   ├── sprint1_delegation_roadmap.md
│   │   ├── sprint_1_final_tickets.md
│   │   ├── ticket-playbook.md
│   │   ├── tickets_trivia.md
│   │   ├── tickets_trivia_v2.md
│   │   ├── trivia_sprint_required_patterns_matrix.md
│   │   ├── umbral-users-realignment-handoff-2026-07-06.md
│   │   ├── umbral_user_stories.md
│   │   ├── Untitled
│   │   ├── users-realignment-decisions-2026-07-06.md
│   │   ├── workflow_for_prompts.md
│   │   └── workflow_refactor.md
│   ├── plans
│   │   ├── api-gateway-scaffold.md
│   │   ├── application-layer-cqrs-refactor.md
│   │   ├── des-66-local-backend-platform-baseline.md
│   │   ├── exception-mapper-alignment.md
│   │   ├── immediate-fixes.md
│   │   ├── migration-to-target-structure.md
│   │   ├── multi-phase-service-implementation.md
│   │   ├── participant-sign-up-backend-then-mobile.md
│   │   ├── pattern-placement-remediation.md
│   │   └── seed-all-mission-only-fix.md
│   ├── scripts
│   │   ├── cover-gate.sh
│   │   ├── cover.sh
│   │   ├── dev-up.sh
│   │   ├── README.md
│   │   ├── seed-all.sh
│   │   └── structure-guard.sh
│   ├── services
│   │   ├── .claude
│   │   │   └── .cc-writes
│   │   ├── identity-access-service
│   │   │   ├── .claude
│   │   │   ├── src
│   │   │   ├── tests
│   │   │   ├── CONTEXT.md
│   │   │   ├── Dockerfile
│   │   │   ├── README.md
│   │   │   └── structure.md
│   │   ├── mission-design-service
│   │   │   ├── src
│   │   │   ├── tests
│   │   │   ├── CONTEXT.md
│   │   │   ├── Dockerfile
│   │   │   ├── README.md
│   │   │   └── structure.md
│   │   ├── scoring-monitoring-service
│   │   │   ├── .claude
│   │   │   ├── src
│   │   │   ├── tests
│   │   │   ├── CONTEXT.md
│   │   │   ├── README.md
│   │   │   └── structure.md
│   │   └── session-operations-service
│   │       ├── .claude
│   │       ├── src
│   │       ├── tests
│   │       ├── CONTEXT.md
│   │       ├── Dockerfile
│   │       ├── README.md
│   │       └── structure.md
│   ├── AGENTS.md
│   ├── CLAUDE.md
│   ├── CONTEXT-MAP.md
│   ├── docker-compose.override.yml
│   ├── docker-compose.yml
│   ├── .gitignore
│   ├── Makefile
│   ├── README.md
│   └── structure.md
├── .claude
│   ├── .cc-writes
│   ├── worktrees
│   ├── agents
│   ├── commands
│   ├── hooks
│   ├── launch.json
│   ├── routines
│   ├── scheduled_tasks.json
│   ├── scheduled_tasks.lock
│   ├── settings.json
│   ├── settings.local.json
│   ├── skills
│   └── workflows
├── .codex
├── docs
│   ├── archive
│   │   └── hu04-hu05-orient.md
│   ├── decisions
│   │   └── identity-access-service.md
│   ├── decisions.md
│   ├── hu-01-validation.md
│   ├── hu04-hu05-best_parallel_prompt.md
│   ├── hu-06-mobile-test-workflow.md
│   ├── hu-07b-cross-service-data-alignment-fix.md
│   ├── hu-07b-plan-fixes.md
│   ├── tree-snapshot-2026-07-08.md
│   ├── workflow_multiple_agents.md
│   └── zoom-out.md
├── frontend
│   ├── .agents
│   │   └── skills
│   │       ├── impeccable
│   │       ├── prototype
│   │       ├── to-issues
│   │       ├── to-prd
│   │       ├── vercel-composition-patterns
│   │       ├── vercel-react-best-practices
│   │       ├── web-design-guidelines
│   │       └── zoom-out
│   ├── app
│   │   ├── actions
│   │   │   ├── auth.ts
│   │   │   ├── mission-structure.ts
│   │   │   ├── missions.ts
│   │   │   ├── sessions.ts
│   │   │   ├── session.ts
│   │   │   ├── teams.ts
│   │   │   ├── trivias.ts
│   │   │   └── users.ts
│   │   ├── api
│   │   │   ├── auth
│   │   │   └── realtime
│   │   ├── dashboard
│   │   │   ├── mission
│   │   │   ├── DashboardClient.tsx
│   │   │   ├── dashboard.module.css
│   │   │   ├── MissionsPanel.tsx
│   │   │   ├── operatorSessionTimerPanel.module.css
│   │   │   ├── OperatorSessionTimerPanel.tsx
│   │   │   ├── page.tsx
│   │   │   ├── SessionOperatorPanel.tsx
│   │   │   ├── SessionsPanel.tsx
│   │   │   ├── TeamsPanel.tsx
│   │   │   ├── triviaRoundPanel.module.css
│   │   │   ├── TriviaRoundPanel.tsx
│   │   │   └── TriviasPanel.tsx
│   │   ├── lib
│   │   │   ├── realtime
│   │   │   ├── dal.ts
│   │   │   ├── definitions.ts
│   │   │   ├── identity.ts
│   │   │   ├── keycloak-tokens.ts
│   │   │   ├── keycloak.ts
│   │   │   ├── mission-structure.ts
│   │   │   ├── missions.ts
│   │   │   ├── session-lifecycle.ts
│   │   │   ├── sessions.ts
│   │   │   ├── session.ts
│   │   │   ├── team-catalog.ts
│   │   │   ├── teams.ts
│   │   │   ├── trivias.ts
│   │   │   └── users.ts
│   │   ├── login
│   │   │   ├── LoginCard.tsx
│   │   │   ├── login.module.css
│   │   │   └── page.tsx
│   │   ├── favicon.ico
│   │   ├── globals.css
│   │   ├── layout.tsx
│   │   ├── page.module.css
│   │   └── page.tsx
│   ├── .claude
│   │   └── .cc-writes
│   ├── docs
│   │   ├── adr
│   │   │   ├── 0001-no-auth-library-manual-oidc.md
│   │   │   ├── 0002-stateless-session-cookie.md
│   │   │   ├── 0003-proxy-optimistic-only.md
│   │   │   ├── 0004-one-shared-app-role-aware-rendering.md
│   │   │   ├── 0005-access-token-not-persisted.md
│   │   │   ├── 0006-gateway-identity-integration-and-jwt-backchannel.md
│   │   │   ├── 0007-role-authority-app-database.md
│   │   │   ├── 0008-session-ownership-and-operator-assignment.md
│   │   │   └── 001-keycloak-public-hostname.md
│   │   ├── backlog
│   │   │   ├── issues_linear.md
│   │   │   └── umbral-equipo-12 › All issues.csv
│   │   ├── admin-vs-operator-dashboard.md
│   │   ├── condensed_roadmap_umbral.md
│   │   ├── hu-15-frontend-test-workflow.md
│   │   ├── requirements_traceability.md
│   │   └── umbral_user_stories.md
│   ├── .github
│   │   └── workflows
│   │       └── playwright.yml
│   ├── plans
│   │   ├── e2e-operator-sessions-500-handoff.md
│   │   ├── hu-01-frontend-general-user-login.md
│   │   ├── hu-02-frontend-user-access-management.md
│   │   ├── hu-03-frontend-role-permission-assignment.md
│   │   ├── hu-04-frontend-team-registration-maintenance.md
│   │   ├── hu-05-frontend-participant-team-assignment.md
│   │   ├── hu-09-frontend-mission-management.md
│   │   ├── hu-10a-frontend-mission-hierarchy-authoring.md
│   │   ├── hu-10a-frontend-mission-hierarchy-ux-remediation.md
│   │   ├── hu-11-frontend-trivia-quiz-management.md
│   │   ├── hu-12-frontend-trivia-quiz-publication-and-archive.md
│   │   ├── hu-13-frontend-trivia-quiz-duplication-and-retirement.md
│   │   ├── hu-14a-frontend-trivia-question-management.md
│   │   ├── hu-15-e2e-session-path-failures.md
│   │   ├── hu-15-frontend-session-creation-from-mission.md
│   │   ├── hu-16-frontend-trivia-session-creation.md
│   │   ├── hu-16-frontend-trivia-substage-runtime-snapshot-realignment.md
│   │   ├── hu-19-frontend-session-operator-assignment.md
│   │   ├── hu-21a-frontend-session-state-machine-controls.md
│   │   ├── hu-22-frontend-authoritative-session-timer.md
│   │   ├── hu-22-frontend-operator-live-session-timer.md
│   │   └── hu-33a-frontend-trivia-substage-orchestration.md
│   ├── public
│   │   ├── img
│   │   │   └── hero-background.png
│   │   ├── file.svg
│   │   ├── globe.svg
│   │   ├── next.svg
│   │   ├── probe-a-dark.png
│   │   ├── probe-a-light.png
│   │   ├── vercel.svg
│   │   └── window.svg
│   ├── tests
│   │   ├── e2e
│   │   │   ├── auth.spec.ts
│   │   │   ├── mission-hierarchy.spec.ts
│   │   │   ├── missions.spec.ts
│   │   │   ├── participants.spec.ts
│   │   │   ├── roles.spec.ts
│   │   │   ├── session-operator.spec.ts
│   │   │   ├── session-operator-timer.spec.ts
│   │   │   ├── sessions.spec.ts
│   │   │   ├── teams.spec.ts
│   │   │   ├── trivias.spec.ts
│   │   │   └── users.spec.ts
│   │   ├── fixtures
│   │   │   └── auth.ts
│   │   ├── lib
│   │   │   ├── keycloak-session-helper.ts
│   │   │   └── session-helper.ts
│   │   ├── setup
│   │   │   ├── global-setup.ts
│   │   │   └── vitest.setup.ts
│   │   └── unit
│   │       └── app
│   ├── AGENTS.md
│   ├── CLAUDE.md
│   ├── DESIGN.md
│   ├── .env.local.example
│   ├── eslint.config.mjs
│   ├── .gitignore
│   ├── next.config.ts
│   ├── package.json
│   ├── playwright.config.ts
│   ├── pnpm-lock.yaml
│   ├── postcss.config.mjs
│   ├── PRODUCT.md
│   ├── proxy.ts
│   ├── README.md
│   ├── skills-lock.json
│   ├── tsconfig.json
│   └── vitest.config.ts
├── mobile
│   ├── .agents
│   │   └── skills
│   │       ├── building-native-ui
│   │       ├── expo-api-routes
│   │       ├── expo-dev-client
│   │       └── native-data-fetching
│   ├── assets
│   │   ├── expo.icon
│   │   │   ├── Assets
│   │   │   └── icon.json
│   │   └── images
│   │       ├── tabIcons
│   │       ├── android-icon-background.png
│   │       ├── android-icon-foreground.png
│   │       ├── android-icon-monochrome.png
│   │       ├── expo-badge.png
│   │       ├── expo-badge-white.png
│   │       ├── expo-logo.png
│   │       ├── favicon.png
│   │       ├── icon.png
│   │       ├── logo-glow.png
│   │       ├── react-logo@2x.png
│   │       ├── react-logo@3x.png
│   │       ├── react-logo.png
│   │       ├── splash-icon.png
│   │       └── tutorial-web.png
│   ├── .claude
│   │   ├── .cc-writes
│   │   ├── skills
│   │   │   ├── building-native-ui -> ../../.agents/skills/building-native-ui
│   │   │   ├── expo-api-routes -> ../../.agents/skills/expo-api-routes
│   │   │   ├── expo-dev-client -> ../../.agents/skills/expo-dev-client
│   │   │   └── native-data-fetching -> ../../.agents/skills/native-data-fetching
│   │   └── settings.json
│   ├── docs
│   │   ├── adr
│   │   │   └── 0001-dynamic-host-via-expo-hostUri.md
│   │   ├── future-participant-session-team-lobby.md
│   │   ├── hu-07a-mobile-test-workflow.md
│   │   ├── hu-07b-mobile-test-workflow.md
│   │   └── plan-participant-session-team-lobby.md
│   ├── plans
│   │   ├── hu-06-mobile-login.md
│   │   ├── hu-07b-participant-reconnection.md
│   │   ├── hu-22-participant-live-session-timer.md
│   │   └── post-hu-34a-mobile-trivia-breakdown.md
│   ├── scripts
│   │   ├── reset-project.js
│   │   ├── smoke-reconnect-hub.mjs
│   │   └── start-expo.mjs
│   ├── src
│   │   ├── app
│   │   │   ├── (app)
│   │   │   ├── (auth)
│   │   │   ├── index.tsx
│   │   │   └── _layout.tsx
│   │   ├── components
│   │   │   ├── ui
│   │   │   ├── animated-icon.module.css
│   │   │   ├── animated-icon.tsx
│   │   │   ├── animated-icon.web.tsx
│   │   │   ├── app-tabs.tsx
│   │   │   ├── app-tabs.web.tsx
│   │   │   ├── external-link.tsx
│   │   │   ├── hint-row.tsx
│   │   │   ├── session-timer-bar.tsx
│   │   │   ├── themed-text.tsx
│   │   │   ├── themed-view.tsx
│   │   │   └── web-badge.tsx
│   │   ├── constants
│   │   │   └── theme.ts
│   │   ├── hooks
│   │   │   ├── use-color-scheme.ts
│   │   │   ├── use-color-scheme.web.ts
│   │   │   └── use-theme.ts
│   │   ├── lib
│   │   │   ├── api
│   │   │   ├── auth
│   │   │   ├── membership
│   │   │   ├── realtime
│   │   │   └── host.ts
│   │   ├── __tests__
│   │   │   ├── access-policy.test.ts
│   │   │   ├── keycloak-errors.test.ts
│   │   │   ├── membership-policy.test.ts
│   │   │   ├── reconnect-context-resolution.test.ts
│   │   │   ├── reconnect-context.test.ts
│   │   │   ├── reconnect-hook.test.tsx
│   │   │   ├── reconnect-policy.test.ts
│   │   │   ├── sessions-api.test.ts
│   │   │   ├── sessions-hub-timer.test.ts
│   │   │   ├── session-timer-bar.test.tsx
│   │   │   ├── session-timer-hook.test.ts
│   │   │   ├── team-join-hook.test.ts
│   │   │   ├── team-lobby-hook.test.ts
│   │   │   └── team-lobby-state.test.ts
│   │   ├── types
│   │   │   └── env.d.ts
│   │   └── global.css
│   ├── types
│   │   ├── css-modules.d.ts
│   │   └── env.d.ts
│   ├── .vscode
│   │   ├── extensions.json
│   │   └── settings.json
│   ├── AGENTS.md
│   ├── app.json
│   ├── CLAUDE.md
│   ├── DESIGN.md
│   ├── eas.json
│   ├── .env
│   ├── .env.example
│   ├── eslint.config.js
│   ├── .gitignore
│   ├── LICENSE
│   ├── package.json
│   ├── package-lock.json
│   ├── README.md
│   ├── skills-lock.json
│   └── tsconfig.json
├── plans
│   ├── docker-compose-local-backend.md
│   ├── fix-published-quiz-readiness-gap.md
│   ├── hu-19-session-list-endpoint-and-panel.md
│   ├── hu-22-option-a-operator-timer-endpoint-fix.md
│   ├── hu-33a-operator-team-association-code-routing-and-lobby-sync.md
│   ├── hu-34a-34b-answer-submission.md
│   ├── signalr-auth-through-gateway.md
│   ├── trivia-7-requirements-coverage.md
│   └── trivia-build-order-and-priority.md
├── AGENTS.md
├── .bash_profile
├── .bashrc
├── CLAUDE.md
├── CONTEXT-MAP.md
├── .gitconfig
├── .gitignore
├── .gitmodules
├── .idea
├── .mcp.json
├── opencode.json
├── .profile
├── README.md
├── .ripgreprc
├── .vscode
├── .zprofile
└── .zshrc

169 directories, 433 files
```
