# ArenaGodEyes.Backend

This project group contains the local backend for ArenaGodEyes.

## Role

The backend is the local core behind the desktop product.

It is responsible for:

- settings
- addon installation support
- combat log reading
- parsing pipeline
- match detection
- SQLite persistence
- OBS WebSocket integration
- future FFmpeg utilities
- manual ChatGPT import and export support
- local coaching data
- local APIs consumed by the desktop shell and UI

## Layout

- `src/ArenaGodEyes.Core`
- `src/ArenaGodEyes.Infrastructure`
- `src/ArenaGodEyes.ApiLocal`
- `tests/ArenaGodEyes.Tests`

## Current Repo Reality

Implemented so far:

- clean backend structure
- safety-aware API root
- settings detection, validation, and addon installation flow
- combat log watcher and import foundation
- focused xUnit regression coverage

## Canonical Next Major Step

The updated architecture plan now puts the proper Electron desktop shell before more backend expansion.
