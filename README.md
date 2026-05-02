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

https://github.com/ArtyomKlimenko/mudickland-updater/releases/tag/v0.1.0

Файлы:

- `MuDickLand.Updater-win-x64.zip` — updater для Windows x64.
- `MuDickLand.Updater-win-x64.zip.sha256` — контрольная сумма архива.

## Как проверить на Windows

Сейчас опубликованный тестовый manifest использует `http://127.0.0.1:8088/...`.
Это специально локальный smoke-test URL. С удаленного Windows-ПК он заработает
через SSH-туннель, пока не настроен нормальный HTTPS-домен.

1. Скачай `MuDickLand.Updater-win-x64.zip` из GitHub Releases.
2. Распакуй архив, например в `C:\MuDickLandUpdater`.
3. В архиве есть `updater.localhost.json`. Скопируй его рядом с
   `MuDickLand.Updater.exe` и переименуй в `updater.json`.

Итоговый `updater.json` для проверки через SSH-туннель должен выглядеть так:

```json
{
  "latestUrl": "http://127.0.0.1:8088/downloads/experimental/latest.json",
  "siteUrl": "http://127.0.0.1:8088/",
  "telegramUrl": "https://t.me/pz_family_chat_bot",
  "supportUrl": "https://github.com/ArtyomKlimenko/mudickland-updater/issues",
  "telemetryUrl": "http://127.0.0.1:8088/api/updater-event",
  "launcherPath": "",
  "allowInsecureHttp": false
}
```

4. В PowerShell на Windows подними SSH-туннель до сервера:

```powershell
ssh -L 8088:127.0.0.1:8088 o1o4@YOUR_SERVER_HOST
```

Если у тебя в `~/.ssh/config` уже есть алиас на сервер, можно так:

```powershell
ssh -L 8088:127.0.0.1:8088 YOUR_SSH_ALIAS
```

5. В другом PowerShell проверь, что Windows видит manifest:

```powershell
Invoke-WebRequest http://127.0.0.1:8088/downloads/experimental/latest.json
```

6. Запусти `MuDickLand.Updater.exe`.
7. В поле install directory выбери отдельную папку, например:

```text
%APPDATA%\.minecraft-pz-exp
```

8. Нажми `Check`. Должно показать версию `experimental-2026.05.02`, количество
   файлов к скачиванию и размер.
9. Нажми `Update`. На первом запуске будет скачано около `555 MB` клиентских
   файлов сборки.
10. После обновления открой свой Minecraft-лаунчер и укажи game directory на
    выбранную папку.

Важно: updater не устанавливает Forge и не настраивает аккаунт. Для игры нужен
уже установленный Minecraft/Forge `1.20.1 / 47.4.20` в твоем лаунчере.

## Как это должно работать для друзей

Публичный адрес сейчас: `http://82.26.151.254/`.

Но на момент написания `http://82.26.151.254/downloads/experimental/latest.json`
еще отвечает старым `404`, где `/downloads/*` закрыт. До раздачи друзьям нужно
сначала направить этот публичный адрес на актуальный updater-сайт, который сейчас
локально отвечает на `127.0.0.1:8088`.

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
  --version experimental-2026.05.02 \
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

Если временно решено раздавать прямо по `http://82.26.151.254/`, в архиве есть
`updater.82-http.example.json`. Его можно переименовать в `updater.json`, но
только после того, как
`http://82.26.151.254/downloads/experimental/latest.json` начнет отвечать `200`.
В этом файле включен явный флаг:

```json
{
  "allowInsecureHttp": true
}
```

Это временный режим. Подпись manifest все еще защищает файлы сборки от подмены,
но сам HTTP-трафик не шифруется.

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

GitHub Actions собирает Windows release artifact на тегах вида `v0.1.0`.
