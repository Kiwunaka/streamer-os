# Streamer OS Agent Rules

## Scope

- Main stack: `Streamer.bot + OBS plugin`
- `Dashboard` is deprecated
- `TwitchHivemind` is future work and out of scope unless explicitly requested

## Main Work Areas

- `02_Стримербот_и_CSharp/01_Персоны_и_донаты`
- `02_Стримербот_и_CSharp/02_Экшены_и_ядро`
- `03_OBS_и_аудио/01_Плагин_OBS/obs-plugin`
- `01_Документация`

## Guardrails

- `Streamer.bot` owns triggers, commands, moderation, donations, persona logic, and viewer mini-games
- `OBS plugin` owns runtime visuals, managed sources, and the visual queue
- New visuals should go through `VisualRuntimeEndpoint`
- New persistent state should prefer `StreamState`, `PersonaState`, `AutomationState`, `ViewerGameState`

## UX Policy

- Keep docs in Russian
- Keep setup beginner-friendly and step-by-step
