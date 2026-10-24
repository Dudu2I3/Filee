# Filee Lite — план форка

Сделать из [KnifeLemon/Filee](https://github.com/KnifeLemon/Filee) облегчённую сборку под macOS (Apple Silicon,
`osx-arm64`). Бублик, CLI и watch folders сохраняются, остаются только часто используемые форматы.
Итог — `.app`, который ставится из `.dmg` в Releases.

Фазы:

0. Подготовка (remote, PLAN.md, Actions).
1. Мягкая обрезка: отфильтровать рёбра в `ConverterCatalog`, оставить нужные движки в `EngineRegistry`, форматы в
   `FormatRegistry`, пресеты/профили в `BuiltInData`, пакеты в `engines.json`; удалить тесты удалённых модулей.
2. UI-хвосты: языки en/ru, шрифты, Engines-страница (опционально).
3. Жёсткая обрезка кода по модулям (опционально, kill criterion ~40 ошибок компиляции).
4. Платформы (опционально).
5. Выключить автообновление от upstream, версия `2.0.0-lite.1`, README форка.
6. GitHub Actions `mac-lite.yml` на `macos-15`: тесты, `build-portable.ps1`, `.dmg`, релиз `v2.0.0-lite.N`.
7. Приёмка (чек-лист, ручная GUI-проверка на Mac).
8. Редизайн бублика: Liquid Glass, тёмная тема Apple; релиз `v2.0.0-lite.2`.

## Оставляем

| Группа | Форматы |
|---|---|
| Изображения | png, jpg, webp, heic, avif, gif, tiff, bmp, ico, svg |
| PDF | pdf (в картинки, в DOCX, merge, split, compress, crop, редактор страниц) |
| Документы | docx, doc, odt, rtf |
| Таблицы | xlsx, xls, csv |
| Презентации | pptx, ppt |
| Текст | txt, md, html |
| Видео | mp4, mov, mkv, webm, avi, m4v (FFmpeg) |
| Аудио | mp3, m4a, wav, flac, ogg, opus, aac (FFmpeg) |
| Архивы | zip, 7z, rar, tar, tgz, gz, `folder` |

OCR — Apple Vision, остаётся. Tesseract удаляется.

## Удаляем

HWP/HWPX, e-book, CAD, 3D, Geo, шрифты, субтитры, Data, Email, OFD, экзотические изображения и векторные форматы,
экзотические видео/аудио/архивы. Движки: rhwp, Unhwp, Pandoc, calibre, Ghostscript, Tesseract, JRE, H2Orestart, WIC,
ICNS. Профили бублика: `hwp`, `ebooks`, `vector`, `data`, `models`, `geo`, `subtitles`.

## Архитектурные заметки

- `src/Filee.Engines/Hwp/Hwpx/` содержит общую модель документов (DocxReader/Writer, MarkdownReader/Writer,
  HtmlReader, PptxReader, ...). Папку целиком удалять нельзя.
- Папку `Vector` оставить (SVG, водяные знаки PDF).
- `FormatRegistry.Get(id)` бросает на неизвестном id: сначала фильтр рёбер, потом чистка реестра.
- `LICENSE` и `THIRD-PARTY-NOTICES.md` не удалять.
- Автообновление указывает на upstream: обязательно выключить.
- Платформенный код Windows/Linux не трогать. PR и push в `KnifeLemon/Filee` не делать никогда.

## Правила

- Каждая фаза — отдельный коммит; после каждой проходят `dotnet build` и `dotnet test`.
- Фазы 2–4 опциональны, 1/5/6 обязательны. Модуль, удаление которого даёт >~40 ошибок, остаётся мёртвым кодом.
- Критерий готовности: опубликован `v2.0.0-lite.2` с новым бубликом (фаза 8, Liquid Glass).

## Фаза 8 — спецификация Liquid Glass (кратко)

- Кольцо: `HoleRatio` 0.42, заливка `rgba(44,44,50,0.28)`, блик 150°, края и разделители белым с малой
  непрозрачностью, акцент `#0A84FF` с 0.78 для активного слайса, подписи SF Pro 14 px semibold.
- Хаб: диск r≈62, `rgba(60,60,67,0.30)`, блик сверху, тень.
- Нативное размытие через `Filee.Platform.MacOS` (`NSGlassEffectView` на macOS 26+, иначе `NSVisualEffectView`),
  маска-кольцо `CAShapeLayer`. Kill criterion: 3 неудачные попытки → псевдо-стекло `rgba(36,36,40,0.82)`.
- Геометрию и hit-testing (`DonutGeometry`) не менять. Новый стиль `LiquidGlass` по умолчанию, старый переключаемый.
- Тёмная тема по умолчанию, палитра Apple dark; уважать Reduce animations / Reduce transparency.
- Headless-рендер PNG профилей Изображения/PDF/Видео в `docs/media/lite/`; контраст подписей ≥ 4.5:1.
