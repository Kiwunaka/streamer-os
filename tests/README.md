# Локальные проверки

Ни одна проверка ниже не вызывает платные API, Twitch или реальные действия Streamer.bot.

## Провайдеры, настройки, модерация

Нужны Python 3, Roslyn C# 11+ и Newtonsoft.Json 13 (можно взять из Streamer.bot):

```powershell
./tests/run-providers.ps1 -Compiler 'C:/Tools/Roslyn/csc.exe' -NewtonsoftJson 'D:/Streamer.bot/Newtonsoft.Json.dll'
```

Тестовые копии исходников используют localhost:18409. Проверяются отправка fal один раз, ошибка задания, скачивание без ключа, Fish → ElevenLabs, выбор голоса персоны, формат Inworld, атомарная валидация настроек и отсутствие двойных страйков. Настоящие URL в исходниках не меняются.

## Native runtime

Нужны CMake, C++17, Qt6 Core/Network, websocketpp и Asio из OBS dependencies. Из корня:

```powershell
cmake -S tests -B tests/build -DCMAKE_PREFIX_PATH='D:/Qt/6/msvc2022_64' -DOBS_DEPS_ROOT='D:/obs-deps'
cmake --build tests/build --config Release
# Добавь Qt bin в PATH, затем:
./tests/build/Release/runtime-tests.exe './03_OBS_и_аудио/01_Плагин_OBS/audio-visualizer-plugin'
```

Порты: 17408 HTTP и 17407 WebSocket. Проверяются приоритет, минимальный показ, замена, hide, FIFO, истечение ожидающих событий, HTTP-маршруты и повторный запуск WebSocket с реальным handshake и отправкой кадра.

## Панель в браузере

Для локального стенда запусти native test executable с дополнительным `--serve` и `python tests/browser_fixture.py` (нужен `websockets==13.1`). Открой `http://127.0.0.1:17408/admin`, WebSocket `ws://127.0.0.1:18080/`, пароль `test-password`. Используются только образцы ключей. Путь `/noauth` проверяет отказ чтения секретов без аутентификации.

В Chromium проверены: вход/ошибка пароля, сохранение и перечитывание, отсутствие localStorage, очистка ключей после отключения, тест/очистка native очереди, ширина 390 px и подписи полей. Runtime отдельно проверен на изображение, hide и восстановление того же события после HTTP 503. Звук и реальные API требуют проверки в своём OBS/Streamer.bot.
