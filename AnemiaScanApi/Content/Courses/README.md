# Курсы

Каждый `*.json` в этой папке — один курс. При старте сервис загружает их в базу.

- Имя файла = `slug` курса: `iron-basics-7.json`.
- `"contentStatus": "Draft"` — курс в базе, но в каталоге не виден; `"Published"` — виден.
- Удаление файла курс из базы не убирает. Чтобы снять с витрины — вернуть `Draft`.
- Проверка перед pull request: `dotnet test` в корне репозитория.

Формат и правила для автора: [`docs/course-content-schema.md`](../../docs/course-content-schema.md).
