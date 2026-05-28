# ArenaGodEyes.ApiLocal

ApiLocal is the local HTTP surface for the desktop-facing backend.

## Current Role

- expose health and identity endpoints
- expose settings endpoints
- expose match import and review endpoints
- expose OBS and video endpoints
- host the dependency injection composition root
- run watcher-related hosted services

## Important Boundary

This API is local-first and desktop-facing.

It is not a public cloud API.
