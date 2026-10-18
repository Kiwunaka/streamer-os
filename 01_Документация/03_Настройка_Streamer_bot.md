# Настройка Streamer.bot

Начни с [пошаговой установки](../START-HERE.md): там указаны точные имена действий для панели, DLL references, WebSocket и запуск. Каждый `CPHInline` компилируется отдельно.

## Подключение остальных действий

| Действие | Файл | Триггер / аргументы |
| --- | --- | --- |
| Ответ по команде | `AiChatBot.cs` | Команда `!ai`; `action=command`, текст приходит в `rawInput` |
| Автоответ | `AiChatBot.cs` | Twitch Chat Message; `action=auto`, сообщение в `message`; `ChatBotAutoEnabled=true` |
| Модератор | `AiModerator.cs` | Отдельные действия: `action=analyze` на сообщение; `action=log` на сообщение для журнала; `action=patrol` по таймеру для атмосферы чата. Страйки: предупреждение → таймаут → бан |
| Оракул | `FortuneOracle.cs` | Команда с вопросом в `rawInput`, `userName`, необязательно `tts=true` |
| Лут | `LootDrop.cs` | Команда / награда, `userName` |
| Инвентарь | `InventoryShow.cs` | Команда, `userName` |
| Дуэль | `DuelMiniGame.cs` | Команда, `userName` вызывающего и противник в `rawInput` |
| Смена игры | `GameChanged.cs` | Twitch Channel Game Changed, `gameName` |
| Режимы | `ViewerModesToggle.cs` | `mode=viewer_modes_enabled`, `chatbot_enabled`, `chatbot_auto_enabled` либо `all`; `enabled=true/false` или переключение без аргумента |
| Безопасный режим | `SafeModeToggle.cs` | Аргумент `enabled=true/false` либо переключение |
| Итоги | `AiStreamRecap.cs` | Конец стрима, передай реальные `gameName`, `sessionUptime`, `sessionFollowers`, `sessionSubs`, `sessionRaids`, `sessionClips` |
| Текущий трек | `RuntimeNowPlayingPush.cs` | Из события своего музыкального источника; аргументы перечислены в начале файла |

Не навешивай одновременно командный и автоматический ответ бота на одно событие. Выдели TTS отдельную последовательную очередь. Изменения инвентарей, дуэлей и истории донатов — в общей последовательной blocking-очереди.

Исходники действий находятся в [02_Стримербот_и_CSharp](../02_Стримербот_и_CSharp). `AiPersonaRotation.cs` можно привязать к таймеру: он переключает четыре персоны по кругу и отправляет вступление в runtime. Дополнительный вызов ручного переключателя не требуется.

`UpdateContext.cs` — прежняя заготовка обращения к Hivemind на порту 8000. Без отдельного сервиса её не подключай; для смены категории используй `GameChanged.cs`. Polls/combo/milestone из старых заметок здесь не реализованы.

Настройка коннекторов и передача их аргументов в AI — [в карте проекта](07_Карта_проекта.md#подключение-donationalerts-и-memealerts). Платные действия не запускаются при открытии панели или обычной проверке ключей.
