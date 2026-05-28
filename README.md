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
- FFmpeg-based video processing
- manual ChatGPT import and export support
- local coaching data
- local APIs consumed by the desktop shell and UI

## Layout

- `src/ArenaGodEyes.Core`
- `src/ArenaGodEyes.Infrastructure`
- `src/ArenaGodEyes.ApiLocal`
- `tests/ArenaGodEyes.Tests`

## Current State

Implemented so far:

- clean backend structure
- safety-aware API root
- settings detection, validation, and addon installation flow
- combat log watcher and live match automation
- match import, JSON export, and SQLite persistence
- manual ChatGPT prompt export and response import
- OBS WebSocket status, start, and stop flow
- FFprobe metadata and FFmpeg thumbnail processing
- xUnit regression coverage for the current foundation

## Next Step

- FFmpeg clip generation
- richer video validation persistence tied to imported ChatGPT review data
