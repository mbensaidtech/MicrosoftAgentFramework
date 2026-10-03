---
title: Local Dashboard Reporting to the Admin Dashboard
status: implemented
priority: high
author: PM Agent
created: 2026-10-02
updated: 2026-10-02
jira_project: none
jira_epic: none
---

# Local Dashboard Reporting to the Admin Dashboard

## Summary

This repository holds the **local** side of the admin-dashboard feature: the Lab Bench (`Dashboard/LabDashboard`, .NET) can report a developer's identity, lab runs, progress, heartbeats and help requests to a hosted **Admin Dashboard**. The Admin Dashboard is a separate Next.js + MongoDB application kept in **its own repository** (`AdminDashboard`), and the **full specification and API contract live there**: `specs/admin-dashboard-progress-tracking/spec.md` of that repository (v1.3). This file is the pointer kept here so that the local side stays traceable; it is not a second source of truth.

## What lives in this repository

| Task of the full spec | Delivered here |
|---|---|
| T12 — identity setup and registration | `Dashboard/LabDashboard/Reporting/IdentityStore.cs`, welcome dialog, settings section *Trainer dashboard*, `GET /api/reporting/status`, `POST /api/identity`, `PUT /api/reporting/settings` |
| T13 — reporting client | `Reporting/ReportingClient.cs`, `ReportingOutbox.cs`, `ProgressSnapshot.cs`; hooks in `LabRunner` and `Program.cs` (`run.started`, `run.finished`, `progress.snapshot`, `catalog.synced`, `lab.opened`, `solution.viewed`, `settings.changed`, `heartbeat`) |
| T14 — Help / Je suis bloqué | `GET/POST /api/help`, `POST /api/help/cancel`, `POST /api/help/dismiss`; top-bar button, dialog, status banner (open → acknowledged → resolved / cancelled) |
| T15 (local half) — tests and docs | `LabDashboard.Tests/ReportingStoreTests.cs`, `ReportingClientTests.cs`, `FakeAdminServer.cs`; section *Reporting to the admin dashboard* in `Dashboard/README.md` |

Configuration: `Dashboard:Reporting` (`ServerUrl`, `WorkshopKey`, `HeartbeatSeconds`, `FlushIntervalSeconds`, `RequestTimeoutSeconds`). At the first launch a welcome dialog lets the developer **join the workshop** (username and workshop key; the admin URL is fixed in `appsettings.json`, shown but never editable) or **work on their own** (choice remembered in `Dashboard/.data/standalone`; no pill, no Help button, no network call; joining later is possible from the settings). Nothing has to be typed on the command line; `ServerUrl` is a project constant (empty = only *work on my own* is offered), `WorkshopKey` in the configuration only pre-fills the dialog. Local files: `Dashboard/.data/identity.json` (mode 600) and `Dashboard/.data/outbox.json`, both git-ignored.

## Contract this side depends on (summary)

Base URL = `ServerUrl`, JSON bodies, every body carries `userId` and `username`. Errors: `{ error, message, details? }`.

| Call | Auth | Purpose |
|---|---|---|
| `POST /api/v1/devs/register` `{ userId, username, dashboardVersion?, platform? }` | `X-Workshop-Key` (+ bearer when re-registering) | 201 `{ devToken }` for a new id, 200 for a known id with a valid token, 403 for a known id without it |
| `POST /api/v1/events` `{ userId, username, events: [{ eventId, type, occurredAt, labId?, payload }] }` | `Authorization: Bearer <devToken>` | 1–100 events, 256 KB, idempotent by `eventId` → 202 `{ accepted, ignored }`; 400 with `details.index`; 429 with `Retry-After` |
| `POST /api/v1/help-requests` `{ userId, username, labId?, message? }` | bearer | 201 open; 409 with the existing active request |
| `POST /api/v1/help-requests/{id}/cancel` `{ userId, username }` | bearer | 200 cancelled; 409 if terminal |
| `GET /api/v1/devs/me` | bearer | `{ activeHelpRequest, lastHelpRequest, archived, serverTime }` (polled every 10 s while a request is active) |
| `GET /api/v1/health` | none | reachability test |

Event payloads, derived states and business rules: see the full spec in the AdminDashboard repository. Any change to the contract is made there first, then mirrored here.

## Test Plan

Covered by the Test Plan of the full spec (API flows verified end to end on 2026-10-02; browser pass still manual). Automated here: `cd Dashboard && dotnet test` (139 tests, including the reporting client against a fake admin server and the hanging-server run).

## Changelog

- **v1.2** (2026-10-03) — The admin dashboard URL is fixed in the project configuration (`Dashboard:Reporting:ServerUrl`), removed from the welcome dialog, the settings and `identity.json`; the developer only enters a username and the workshop key. To mirror in the full spec (Q7, T12).
- **v1.1** (2026-10-03) — First-launch flow reworked: the welcome dialog always appears once, with *Join the workshop* (username + URL + key entered in the UI) and *Work on my own* (remembered, no reporting). Replaces the previous rule "no dialog unless `ServerUrl` is configured". To mirror in the full spec of the AdminDashboard repository (Edge case "reporting not configured", UI/UX local additions, T12).
- **v1.0** (2026-10-02) — Pointer created when the AdminDashboard app and the full spec were isolated in their own repository.
