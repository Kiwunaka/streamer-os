# Настройка Streamer.bot

Каждый файл с классом `CPHInline` вставляется в отдельное поддействие **Execute C# Code**. Для скриптов с многострочными литералами `"""` нужен компилятор C# 11+. В **Name** поддействия укажи имя, чтобы затем вызывать его через **Execute C# Method**. Общий `SharedCore` собирается отдельной библиотекой; вставлять его файлы как отдельные действия не нужно.

Действия для донатов DonationAlerts/MemeAlerts и соответствие аргументов описаны в [карте проекта](07_Карта_проекта.md#подключение-donationalerts-и-memealerts).

## Где лежит код

- [02_Стримербот_и_CSharp](../02_Стримербот_и_CSharp)

Главные папки:

- [01_Персоны_и_донаты](../02_Стримербот_и_CSharp/01_Персоны_и_донаты)
- [02_Экшены_и_ядро](../02_Стримербот_и_CSharp/02_Экшены_и_ядро)

## Первый action

Сначала заведи action на базе:

- [InitGlobals.cs](../02_Стримербот_и_CSharp/02_Экшены_и_ядро/InitGlobals.cs)

Запусти его один раз с аргументом:

```text
force=false
```

После этого сразу открой:
- [04_Ключи_и_маршрутизация.md](04_Ключи_и_маршрутизация.md)

Там уже зафиксирована текущая база проекта:
- `Inworld` как основной TTS
- `Kontext Max` как основной генератор картинок
- текстовые модели по задачам через отдельные глобалки

## Что создать в первую очередь

- [AiChatBot.cs](../02_Стримербот_и_CSharp/02_Экшены_и_ядро/AiChatBot.cs)
- [AiModerator.cs](../02_Стримербот_и_CSharp/02_Экшены_и_ядро/AiModerator.cs)
- [InworldTTS.cs](../02_Стримербот_и_CSharp/02_Экшены_и_ядро/InworldTTS.cs)
- [OverlayCommand.cs](../02_Стримербот_и_CSharp/02_Экшены_и_ядро/OverlayCommand.cs)
- [AiPersonaSwitcher.cs](../02_Стримербот_и_CSharp/01_Персоны_и_донаты/AiPersonaSwitcher.cs)

## Полезные daily actions

- [SafeModeToggle.cs](../02_Стримербот_и_CSharp/02_Экшены_и_ядро/SafeModeToggle.cs)
- [ViewerModesToggle.cs](../02_Стримербот_и_CSharp/02_Экшены_и_ядро/ViewerModesToggle.cs)
- [StartupHealthCheck.cs](../02_Стримербот_и_CSharp/02_Экшены_и_ядро/StartupHealthCheck.cs)
- [RuntimeNowPlayingPush.cs](../02_Стримербот_и_CSharp/02_Экшены_и_ядро/RuntimeNowPlayingPush.cs)

## Весёлые actions

- [FortuneOracle.cs](../02_Стримербот_и_CSharp/02_Экшены_и_ядро/FortuneOracle.cs)
- [LootDrop.cs](../02_Стримербот_и_CSharp/02_Экшены_и_ядро/LootDrop.cs)
- [InventoryShow.cs](../02_Стримербот_и_CSharp/02_Экшены_и_ядро/InventoryShow.cs)
- [DuelMiniGame.cs](../02_Стримербот_и_CSharp/02_Экшены_и_ядро/DuelMiniGame.cs)
