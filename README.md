# MuDickLand Updater

Открытый Windows-updater для клиентской сборки **MuDickLand Experimental**.

Updater занимается только файлами модпака: скачивает моды, конфиги и данные сборки,
проверяет их хэши и приводит выбранную папку к нужной версии. Он **не логинит
Minecraft**, **не обходит лицензирование**, **не содержит Minecraft** и **не
собирает приватные данные с компьютера**.

## Что он делает

- Загружает `latest.json`, подписанный `manifest.json` и `manifest.json.sig`.
- Проверяет подпись манифеста до любых изменений на диске.
- Скачивает только отсутствующие или изменившиеся файлы.
- Проверяет SHA-256 каждого скачанного файла.
- Удаляет лишние файлы только внутри управляемых папок сборки.
- Не трогает пользовательские данные вне управляемых папок: `saves`,
  `screenshots`, `options.txt`, `servers.dat`, аккаунты лаунчера и т.п.

## Скачать

Актуальный релиз:

https://github.com/ArtyomKlimenko/mudickland-updater/releases/latest

Временная сборка с публичного updater-сайта:

http://82.26.151.254/downloads/updater/MuDickLand.Updater-win-x64.zip

Файлы:

- `MuDickLand.Updater-win-x64.zip` — updater для Windows x64.
- `MuDickLand.Updater-win-x64.zip.sha256` — контрольная сумма архива.

## Как проверить на Windows

Сейчас публичный updater-index доступен по временному HTTP-адресу:

```text
http://82.26.151.254/downloads/experimental/latest.json
```

Подпись manifest защищает файлы сборки от подмены, но сам HTTP-трафик не
шифруется. Для постоянной раздачи нужен HTTPS.

1. Скачай `MuDickLand.Updater-win-x64.zip` из GitHub Releases.
2. Распакуй архив, например в `C:\MuDickLandUpdater`.
3. Рядом с `MuDickLand.Updater.exe` должен лежать `updater.json` с публичным
   HTTP-адресом. В релизном архиве он уже подготовлен.

Итоговый `updater.json` для публичной проверки должен выглядеть так:

```json
{
  "latestUrl": "http://82.26.151.254/downloads/experimental/latest.json",
  "siteUrl": "http://82.26.151.254/",
  "telegramUrl": "https://t.me/pz_family_chat_bot",
  "supportUrl": "https://github.com/ArtyomKlimenko/mudickland-updater/issues",
  "telemetryUrl": "http://82.26.151.254/api/updater-event",
  "launcherPath": "",
  "allowInsecureHttp": true
}
```

4. В PowerShell проверь, что Windows видит manifest:

```powershell
Invoke-WebRequest http://82.26.151.254/downloads/experimental/latest.json
```

5. Запусти `MuDickLand.Updater.exe`.
6. В поле "Папка установки" выбери отдельную папку, например:

```text
%APPDATA%\.minecraft-pz-exp
```

7. Нажми `Проверить`. Должно показать текущую версию сборки, количество файлов к
   скачиванию и размер.
8. Нажми `Обновить`. На первом запуске будет скачан набор клиентских файлов
   сборки.
9. После обновления открой свой Minecraft-лаунчер и укажи game directory на
    выбранную папку.

Важно: updater не устанавливает Forge и не настраивает аккаунт. Для игры нужен
уже установленный Minecraft/Forge `1.20.1 / 47.4.20` в твоем лаунчере.

## Как это должно работать для друзей

Публичный адрес сейчас: `http://82.26.151.254/`.

Публичный updater-index сейчас должен отвечать `200`:

```text
http://82.26.151.254/downloads/experimental/latest.json
```

Этот HTTP-режим временный. Подпись manifest защищает файлы сборки от подмены,
но сам HTTP-трафик не шифруется.

Правильный production-вариант — HTTPS:

```text
https://82.26.151.254/downloads/experimental/latest.json
```

После настройки HTTPS нужно пересобрать manifest на сервере с HTTPS base URL:

```bash
python3 tools/manifest-builder/build_manifest.py \
  --source /opt/minecraft-zomboid/experimental/pz-exp \
  --output /opt/minecraft-zomboid/site/public/downloads/experimental \
  --base-url https://82.26.151.254/downloads/experimental \
  --version experimental-2026.05.04 \
  --private-key /home/o1o4/mudickland-updater-signing/manifest_private.pem
```

Затем рядом с `.exe` в релизе/архиве нужно положить готовый `updater.json`:

```json
{
  "latestUrl": "https://82.26.151.254/downloads/experimental/latest.json",
  "siteUrl": "https://82.26.151.254/",
  "telegramUrl": "https://t.me/pz_family_chat_bot",
  "supportUrl": "https://github.com/ArtyomKlimenko/mudickland-updater/issues",
  "telemetryUrl": "https://82.26.151.254/api/updater-event",
  "launcherPath": "",
  "allowInsecureHttp": false
}
```

Если временно решено раздавать прямо по `http://82.26.151.254/`, в релизном
архиве уже лежит готовый `updater.json` с этим адресом. В этом файле включен
явный флаг:

```json
{
  "allowInsecureHttp": true
}
```

Публичная раздача живет на VPS `82.26.151.254`, сервис `mudickland-site`.
После пересборки manifest локально нужно синхронизировать
`/opt/minecraft-zomboid/site/server.py` и `/opt/minecraft-zomboid/site/public/`
на VPS и перезапустить сервис.

## Что попадает в сборку

Управляемые папки V1:

- `mods`
- `config`
- `defaultconfigs`
- `kubejs`
- `tacz`
- `mod_data`
- `data`
- `patchouli_books`
- `fancymenu_data`

Manifest builder по умолчанию не публикует:

- `world*`
- `saves`
- `logs`
- `crash-reports`
- `server.properties`
- `ops.json`
- `whitelist.json`
- `banned-*.json`
- `usercache.json`
- `usernamecache.json`
- `bridge*`
- `.env`
- backups
- архивы с `private` или `do-not-share` в имени
- скрытые/cache-папки вроде `mods/.connector`

## Логи и приватность

Updater может отправлять только минимальные события:

- `check`
- `update_success`
- `update_failed`
- `open_launcher`

В событии есть random `installId`, версия updater, версия сборки и статус.
Updater не отправляет список процессов, установленные программы, токены,
Minecraft-аккаунты, ники, hardware id или содержимое папок.

Сервер может хранить стандартные HTTP access logs: IP, путь запроса, статус,
байты, user agent. Сырые логи режутся до последних 30 дней, долгосрочно остается
агрегат без IP/installId в `daily-summary.json`.

Подробнее:

- [Privacy](docs/PRIVACY.md)
- [Security](docs/SECURITY.md)

## Разработка

Сборка updater:

```bash
dotnet publish src/MuDickLand.Updater/MuDickLand.Updater.csproj \
  -c Release \
  -r win-x64 \
  --self-contained true \
  -p:PublishSingleFile=true \
  -p:EnableCompressionInSingleFile=true \
  -p:IncludeNativeLibrariesForSelfExtract=true
```

Тесты:

```bash
python3 -m unittest discover -s tests -p 'test_*.py'
dotnet build src/MuDickLand.Updater/MuDickLand.Updater.csproj -c Release -p:EnableWindowsTargeting=true
dotnet build tests/MuDickLand.Updater.Tests/MuDickLand.Updater.Tests.csproj -c Release -p:EnableWindowsTargeting=true
```

GitHub Actions собирает Windows release artifact на тегах вида `v0.1.3`.
